namespace Endorphin.Instrument.PicoHarp300

open Microsoft.FSharp.NativeInterop
open System.Runtime.InteropServices
open Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols
open System.Text
open Endorphin.Core
open Endorphin.Core.NationalInstruments
open Endorphin.Core.String
open ExtCore.Control
open System
open FSharp.Control.Reactive

module TTTRHistogram = 
    type private Tag = int

    type StreamStopOptions = { AcquisitionDidAutoStop : bool }

    type StreamStatus = 
        | PreparingStream
        | Streaming
        | FinishedStream of options : StreamStopOptions
        | FailedStream of error : string
        | CancelledStream of exn : OperationCanceledException

    type internal TagStream = { Tags                 : seq<Tag>
                                HistogramParameters  : TTTRHistogramParameters }

    type HistogramAcquisition = 
        private { Parameters          : TTTRHistogramParameters
                  StreamingBuffer     : StreamingBuffer
                  PicoHarp            : PicoHarp300
                  StopCapability      : CancellationCapability<StreamStopOptions>
                  StatusChanged       : Event<StreamStatus>
                  TagsAvailable       : Event<TagStream> }

    type HistogramAcquisitionHandle = 
        private { Acquisition   : HistogramAcquisition
                  WaitToFinish  : AsyncChoice<unit, string> }

    /// List of bin number and number of counts in bin
    type Histogram = { Histogram : (int * int) list }

    type internal HistogramResidual =
                 {  WorkingHistogram    : int list
                    OverflowMarkers     : int
                    MarkerTimestamp     : int }

    module Parameters =
        let internal computeNumberOfBins resolution length = 
            int <| ((Quantities.durationNanoSeconds length) / (Quantities.durationNanoSeconds resolution))

        let create resolution totalLength : TTTRHistogramParameters = 
            { Resolution        = Quantities.durationNanoSeconds resolution
              TotalLength       = Quantities.durationNanoSeconds totalLength
              NumberOfBins      = computeNumberOfBins resolution totalLength }

        let withResolution resolution         (parameters: TTTRHistogramParameters) = { parameters with Resolution = Quantities.durationNanoSeconds resolution; NumberOfBins = int <| ((parameters.TotalLength) / (Quantities.durationNanoSeconds resolution)) }
        let withTotalLength totalLength       (parameters: TTTRHistogramParameters) = { parameters with TotalLength = Quantities.durationNanoSeconds totalLength;  NumberOfBins = int <| ((Quantities.durationNanoSeconds totalLength) / (parameters.Resolution)) }

    /// module for manipulating the tags from the PicoHarp
    module internal TagHelper =
        let inline tagIdentifier (tag : Tag) : int = 
            ((tag >>> 28 ) &&& 0xF)     

        let inline isPhoton identifier =
            identifier < 2

        let inline timestamp (tag : Tag) : int =
            tag &&& 0x0FFFFFFFF

        let inline isTimeOverflow (tag : Tag) = 
            ((tag >>> 28) &&& 0xF) = 15 && (tag &&& 0xF) = 0

        let inline isMarker (tag : Tag) = 
            ((tag >>> 28) &&& 0xF) = 15 && (tag &&& 0xF) <> 0

    module internal TagStreamReader =
        let incrementTimeOverflowMarkers histogramResidual =
            { histogramResidual with OverflowMarkers = histogramResidual.OverflowMarkers + 1 }

        let histogramResidualCreate timestamp = 
            { WorkingHistogram = List.empty; OverflowMarkers = 0; MarkerTimestamp = timestamp }

        let inline addPhoton (histogramResidual : HistogramResidual) binNumber =
            { histogramResidual with WorkingHistogram = binNumber :: histogramResidual.WorkingHistogram }

        let inline timeSinceMarker histogramResidual tag = 
            1.0<ns> * float ((uint64 <| histogramResidual.OverflowMarkers) * TTTROverflowTime + (uint64 <| TagHelper.timestamp tag) - (uint64 histogramResidual.MarkerTimestamp)) / 4.0

        let inline histogramBin (histogramResidual : HistogramResidual) (parameters : TTTRHistogramParameters) tag : int option = 
            match (timeSinceMarker histogramResidual tag) with
                | tagTime when tagTime < parameters.TotalLength   -> Some (tagTime / parameters.Resolution |> floor |> int)
                | _                                               -> None
                 
        /// Extract all histograms from the incoming tag stream, and build them into a sequence of histograms           
        let extractAllHistograms (parameters : TTTRHistogramParameters) (histogramResidual : HistogramResidual option) (tagStream : TagStream) =
            Seq.fold (fun histogramState tag -> 
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
                            | None                          -> (Seq.appendSingleton { Histogram = ((Seq.countBy id residual.WorkingHistogram) |> Seq.toList) } histSequence, None)
                    | Some residual, tag when TagHelper.isTimeOverflow tag -> 
                        (histSequence, Some <| incrementTimeOverflowMarkers residual)
                    | Some residual, tag when TagHelper.isMarker tag && (histogramBin residual parameters tag) = None -> 
                        (histSequence, Some <| (histogramResidualCreate <| TagHelper.timestamp tag))
                    /// should only get here if there is residual, and the tag is a marker - this should cause an error!
                    | _ ->  failwith "Encountered an unexpected marker tag. Do the histogram settings match the experimental settings?"

            ) (Seq.empty, histogramResidual) (tagStream.Tags)         

     module internal HistogramEvent = 
        type HistogramEvent = 
            internal {  Trigger   : TagStream -> unit
                        Publish   : IObservable<Histogram> }
        
        let trigger event = event.Trigger

        let available event = 
            event.Publish

        let create acquisition = 
            let eventScheduler = new System.Reactive.Concurrency.EventLoopScheduler()
            let input = acquisition.TagsAvailable

            let output = 
                input.Publish
                |> Observable.observeOn eventScheduler 
                |> Observable.foldMap (fun (_,residual) tagStream ->
                     TagStreamReader.extractAllHistograms acquisition.Parameters residual tagStream
                    ) (Seq.empty, None) (fun (histograms, _) -> histograms) // pass each new tag stream and previous residual into the processing function
                |> Observable.collectSeq id // fire event for each histogram
                                    
            { Trigger = input.Trigger ; Publish = output }
           
    module Acquisition = 
        let private buffer =  Array.zeroCreate TTTRMaxEvents
        
        let create picoHarp histogramParameters = 
            { Parameters = histogramParameters
              StreamingBuffer = { Buffer = buffer }
              PicoHarp = picoHarp
              StopCapability = new CancellationCapability<StreamStopOptions>()
              StatusChanged = new Event<StreamStatus>()
              TagsAvailable = new Event<TagStream>() }
        
        let status acquisition =
            acquisition.StatusChanged.Publish
            |> Observable.completeOn (function
                | FinishedStream _
                | CancelledStream _ -> true
                | _                 -> false)
            |> Observable.errorOn (function
                | FailedStream message -> Some (Exception message)
                | _                    -> None)
              
        let private startStreaming acquisition = asyncChoice { 
            let! __ = PicoHarp.Acquisition.start acquisition.PicoHarp (Duration_s (1.0<s> * 100.0 * 3600.0))
            acquisition.StatusChanged.Trigger (Streaming) }
    
        let private copyBufferAndFireEvent acquisition counts =
            let buffer = Array.zeroCreate counts
            acquisition.StreamingBuffer.Buffer.CopyTo (buffer, 0)
            
            let tagSequence = Seq.ofArray buffer
            acquisition.TagsAvailable.Trigger { Tags = tagSequence; HistogramParameters = acquisition.Parameters }

        let rec private pollUntilFinished acquisition = asyncChoice {
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

        let start acquisition : AsyncChoice<HistogramAcquisitionHandle, string> =           
            if acquisition.StopCapability.IsCancellationRequested then
                failwith "Cannot start an acquisition which has already been stopped."

            let finishAcquisition cont acquisitionResult = Async.StartImmediate <| async {
                let! stopResult = PicoHarp.Acquisition.stop acquisition.PicoHarp 
                match acquisitionResult, stopResult with
                | Success (), Success () ->
                    acquisition.StatusChanged.Trigger (FinishedStream acquisition.StopCapability.Options)
                    cont (succeed ())
                | _, Failure f ->
                    let error = sprintf "Acquisition failed to stop: %s" f
                    acquisition.StatusChanged.Trigger (FailedStream error)
                    cont (fail error)
                | Failure f, _ ->
                    acquisition.StatusChanged.Trigger (FailedStream f)
                    cont (fail f) }
            
            let stopAcquisitionAfterCancellation ccont econt exn = Async.StartImmediate <| async { 
                let! stopResult =  PicoHarp.Acquisition.stop acquisition.PicoHarp
                match stopResult with
                | Success () ->
                    acquisition.StatusChanged.Trigger (CancelledStream exn)
                    ccont exn
                | Failure f ->
                    let error = sprintf "Acquisition failed to stop after cancellation: %s" f
                    acquisition.StatusChanged.Trigger (FailedStream error)
                    econt (Exception error) }

            let acquisitionWorkflow cancellationToken =
                Async.FromContinuations (fun (cont, econt, ccont) ->
                    Async.StartWithContinuations(
                        asyncChoice {
                            do! startStreaming acquisition
                            do! pollUntilFinished acquisition }, 
                    
                        finishAcquisition cont, econt, stopAcquisitionAfterCancellation ccont econt,
                        cancellationToken))

            async {
                let! cancellationToken = Async.CancellationToken
                let! waitToFinish = Async.StartChild (acquisitionWorkflow cancellationToken)
                return { Acquisition = acquisition ; WaitToFinish = waitToFinish } }
            |> AsyncChoice.liftAsync
        
        /// Alert when new histograms are available
        let HistogramsAvailable acquisition = 
            let histAvailable = HistogramEvent.create acquisition
            histAvailable.Publish