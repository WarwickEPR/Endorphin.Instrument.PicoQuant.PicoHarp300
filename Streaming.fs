namespace Endorphin.Instrument.PicoHarp300

open Microsoft.FSharp.NativeInterop
open System.Runtime.InteropServices
open Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols
open System.Text
open Endorphin.Core
//open ExtCore.Control
open System
open FSharp.Control.Reactive
open Endorphin.Core.ObservableHelpers

module Streaming = 
    type private Tag = uint32

    type StreamStopOptions = { AcquisitionDidAutoStop : bool }

    type StreamStatus = 
        | PreparingStream
        | Streaming
        | FinishedStream of didAutoStop : bool
        | FailedStream of error : string
        | CancelledStream

    type internal TagStream = { Tags                 : seq<Tag>
                                HistogramParameters  : StreamingParameters }

    /// List of bin number and number of counts in bin
    type Histogram = { Histogram : (int * int) list }

    type StreamResult =
        | StreamCompleted
        | StreamError of exn
        | StreamCancelled

    type StreamingAcquisition = 
        private { Parameters          : StreamingParameters
                  StreamingBuffer     : StreamingBuffer
                  PicoHarp            : PicoHarp300
                  StopCapability      : CancellationCapability<StreamStopOptions>
                  StatusChanged       : NotificationEvent<StreamStatus>
                  TagsAvailable       : Event<TagStream>
                  HistogramAvailable  : IObservable<Histogram> }

    type HistogramAcquisitionHandle = 
        private { Acquisition   : StreamingAcquisition
                  WaitToFinish  : Async<StreamResult> }

    type internal HistogramResidual =
                 {  WorkingHistogram    : int list
                    OverflowMarkers     : int
                    MarkerTimestamp     : uint32 }

    module Parameters =
        let internal computeNumberOfBins resolution length = 
            int <| ((Quantities.durationNanoSeconds length) / (Quantities.durationNanoSeconds resolution))

        let create resolution totalLength : StreamingParameters = 
            { Resolution        = Quantities.durationNanoSeconds resolution
              TotalLength       = Quantities.durationNanoSeconds totalLength
              NumberOfBins      = computeNumberOfBins resolution totalLength }

        let withResolution resolution         (parameters: StreamingParameters) = { parameters with Resolution = Quantities.durationNanoSeconds resolution; NumberOfBins = int <| ((parameters.TotalLength) / (Quantities.durationNanoSeconds resolution)) }
        let withTotalLength totalLength       (parameters: StreamingParameters) = { parameters with TotalLength = Quantities.durationNanoSeconds totalLength;  NumberOfBins = int <| ((Quantities.durationNanoSeconds totalLength) / (parameters.Resolution)) }

    /// module for manipulating the tags from the PicoHarp
    module internal TagHelper =
        let inline isPhoton (tag : Tag) =
            ((tag >>> 28 ) &&& 0xFu) < 2u

        let inline timestamp (tag : Tag) =
            tag &&& 0x0FFFFFFFu

        let inline isTimeOverflow (tag : Tag) = 
            ((tag >>> 28) &&& 0xFu) = 15u && (tag &&& 0xFu) = 0u

        let inline isMarker (tag : Tag) = 
            ((tag >>> 28) &&& 0xFu) = 15u && (tag &&& 0xFu) <> 0u

    module internal TagStreamReader =
        let incrementTimeOverflowMarkers histogramResidual =
            { histogramResidual with OverflowMarkers = histogramResidual.OverflowMarkers + 1 }

        let histogramResidualCreate timestamp = 
            { WorkingHistogram = List.empty; OverflowMarkers = 0; MarkerTimestamp = timestamp }

        let inline addPhoton (histogramResidual : HistogramResidual) binNumber =
            { histogramResidual with WorkingHistogram = binNumber :: histogramResidual.WorkingHistogram }

        let inline timeSinceMarker histogramResidual tag = 
            4.0<ps> * nanosecondsPerPicosecond * float ((uint64 histogramResidual.OverflowMarkers) * TTTROverflowTime + (uint64 <| TagHelper.timestamp tag) - (uint64 histogramResidual.MarkerTimestamp))

        let inline histogramBin (histogramResidual : HistogramResidual) (parameters : StreamingParameters) tag : int option = 
            match (timeSinceMarker histogramResidual tag) with
                | tagTime when tagTime < parameters.TotalLength   -> Some (tagTime / parameters.Resolution |> floor |> int)
                | _                                               -> None
                 
        /// Extract all histograms from the incoming tag stream, and build them into a sequence of histograms           
        let extractAllHistograms (parameters : StreamingParameters) (histogramResidual : HistogramResidual option) (tagStream : TagStream) =
            let result = 
                tagStream.Tags
                |> Seq.fold (fun histogramState tag -> 
                    let (histSequence, histResidual) = histogramState
                    /// options for incoming tag:
                    ///     if not currently building a histogram: 
                    ///         if a marker, start new histogram
                    ///         if not, ignore
                    ///     if in a histogram:
                    ///         if it's a photon:
                    ///             if within the total histogram time, add the photon
                    ///             else, this is the first record since the end of the previous histogram --- write the histogram to sequence
                    ///         if it's an overflow:
                    ///             increment the overflow counter
                    ///         if it's a marker:
                    ///             if the marker is the first record since the end of the previous histogram, start a new one
                    ///             else, fail - the user has asked for histogram timings which do not fit within their acquisition triggers
                    match histResidual, tag with
                        | None, tag when not <| TagHelper.isMarker tag   -> histogramState
                        | None, tag                                      -> (histSequence, Some (histogramResidualCreate <| TagHelper.timestamp tag))
                        | Some residual, tag when TagHelper.isPhoton tag -> 
                            match (histogramBin residual parameters tag) with
                                | Some bin                      -> (histSequence, Some <| (addPhoton residual bin))
                                | None                          -> 
                                    let h = ((Seq.countBy id residual.WorkingHistogram) |> Seq.toList)
                                    (Seq.appendSingleton { Histogram = h } histSequence, None)
                        | Some residual, tag when TagHelper.isTimeOverflow tag -> 
                            (histSequence, Some <| incrementTimeOverflowMarkers residual)
                        | Some residual, tag when TagHelper.isMarker tag && (histogramBin residual parameters tag) = None -> 
                            (Seq.appendSingleton { Histogram = ((Seq.countBy id residual.WorkingHistogram) |> Seq.toList) } histSequence, Some <| (histogramResidualCreate <| TagHelper.timestamp tag))
                        /// should only get here if there is residual, and the tag is a marker - this should cause an error!
                        | _ ->  printfn "STREAMFAIL";failwith "Encountered an unexpected marker tag. Do the histogram settings match the experimental settings?") (Seq.empty, histogramResidual)
            result

     module internal HistogramEvent = 
        type HistogramEvent = 
            internal {  Output   : IObservable<Histogram> }

        let create tagsAvailable parameters = 
            let eventScheduler = new System.Reactive.Concurrency.EventLoopScheduler()

            let output = 
                tagsAvailable
                |> Observable.observeOn eventScheduler 
                |> Observable.scanInit (Seq.empty, None) (fun (_,residual) tagStream ->
                     TagStreamReader.extractAllHistograms parameters residual tagStream
                    )  // pass each new tag stream and previous residual into the processing function
                |> Observable.map (fun f -> fst f)
                |> Observable.collectSeq id // fire event for each histogram
              
            { Output = output }
           
    module Acquisition = 
        let private buffer =  Array.zeroCreate TTTRMaxEvents
        
        
        let create picoHarp histogramParameters = 
            let tagsAvailable = new Event<TagStream>()

            { Parameters = histogramParameters
              StreamingBuffer = { Buffer = buffer }
              PicoHarp = picoHarp
              StopCapability = new CancellationCapability<StreamStopOptions>()
              StatusChanged = new NotificationEvent<StreamStatus>()
              TagsAvailable = tagsAvailable
              HistogramAvailable = (HistogramEvent.create tagsAvailable.Publish histogramParameters).Output }
        
        let status acquisition =
            acquisition.StatusChanged.Publish
            |> Observable.fromNotificationEvent
              
        let private startStreaming acquisition = async { 
            let! __ = PicoHarp.Acquisition.start acquisition.PicoHarp (Duration_s (1.0<s> * 100.0 * 3600.0))
            acquisition.StatusChanged.Trigger (Next <| Streaming) }
    
        let private copyBufferAndFireEvent acquisition counts =
            let buffer = Array.zeroCreate counts
            Array.blit acquisition.StreamingBuffer.Buffer 0 buffer 0 counts
            let tagSequence = Seq.ofArray buffer
            acquisition.TagsAvailable.Trigger { Tags = tagSequence; HistogramParameters = acquisition.Parameters }

        let rec private pollUntilFinished acquisition = async {
            if not acquisition.StopCapability.IsCancellationRequested then
                let! counts = 
                    PicoHarp.Acquisition.readFifoBuffer 
                    <| acquisition.PicoHarp
                    <| acquisition.StreamingBuffer

                copyBufferAndFireEvent acquisition counts

                let! finished = PicoHarp.Acquisition.checkMeasurementFinished acquisition.PicoHarp
                if finished then
                    acquisition.StopCapability.Cancel { AcquisitionDidAutoStop = true } 

                do! pollUntilFinished acquisition }

        let startWithCancellationToken acquisition cancellationToken =           
            if acquisition.StopCapability.IsCancellationRequested then
                failwith "Cannot start an acquisition which has already been stopped."

            let resultChannel = new ResultChannel<_>()

            let finishAcquisition () = 
                Async.StartWithContinuations(
                    PicoHarp.Acquisition.stop acquisition.PicoHarp,
                    (fun () ->
                        acquisition.StatusChanged.Trigger 
                            (Next 
                            <| FinishedStream acquisition.StopCapability.Options.AcquisitionDidAutoStop)
                        acquisition.StatusChanged.Trigger Completed),
                    (fun stopExn ->
                        let error = sprintf "Acquisition failed to stop: %s" stopExn.Message
                        acquisition.StatusChanged.Trigger 
                            (Next
                            <| FailedStream error)
                        acquisition.StatusChanged.Trigger
                            (Error (Exception error))),
                    ignore )

            let stopAcquisitionAfterError exn = 
                Async.StartWithContinuations (
                    PicoHarp.Acquisition.stop acquisition.PicoHarp, 
                    (fun () ->
                        acquisition.StatusChanged.Trigger (Error exn)
                        resultChannel.RegisterResult (StreamError exn)),
                    (fun stopExn ->
                        acquisition.StatusChanged.Trigger (Error exn)
                        resultChannel.RegisterResult (StreamError stopExn)),
                    ignore )

            let stopAcquisitionAfterCancellation _ = 
                Async.StartWithContinuations(
                    PicoHarp.Acquisition.stop acquisition.PicoHarp,
                    (fun () ->
                        acquisition.StatusChanged.Trigger (Next <| CancelledStream)
                        acquisition.StatusChanged.Trigger Completed
                        resultChannel.RegisterResult StreamCancelled),
                    (fun stopExn ->
                        acquisition.StatusChanged.Trigger (Error stopExn)
                        resultChannel.RegisterResult (StreamError stopExn)),
                    ignore)

            let acquisitionWorkflow = 
                async {
                    do! startStreaming acquisition
                    do! pollUntilFinished acquisition }

            Async.StartWithContinuations (
                acquisitionWorkflow,
                finishAcquisition,
                stopAcquisitionAfterError,
                stopAcquisitionAfterCancellation,
                cancellationToken)
                
            { Acquisition = acquisition; WaitToFinish = resultChannel.AwaitResult () }
        
        let start acquisition = startWithCancellationToken acquisition Async.DefaultCancellationToken

        let startAsChild acquisition = async {
            let! ct = Async.CancellationToken
            return startWithCancellationToken acquisition ct }

        let waitToFinish acquisitionHandle = acquisitionHandle.WaitToFinish   
              
        let stop acquisitionHandle =
            if not acquisitionHandle.Acquisition.StopCapability.IsCancellationRequested then
                acquisitionHandle.Acquisition.StopCapability.Cancel { AcquisitionDidAutoStop = false }

        let stopAndFinish acquisitionHandle =
            stop acquisitionHandle
            waitToFinish acquisitionHandle

        /// Alert when new histograms are available
        let HistogramsAvailable acquisition = acquisition.HistogramAvailable