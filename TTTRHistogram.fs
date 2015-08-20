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
        private { Parameters            : TTTRHistogramParameters
                  StreamingBuffer       : StreamingBuffer
                  PicoHarp              : PicoHarp300
                  StopCapability        : CancellationCapability<StreamStopOptions>
                  StatusChanged         : Event<StreamStatus>
                  EventsAvailable       : Event<TagStream> }

    type HistogramAcquisitionHandle = 
        private { Acquisition   : HistogramAcquisition
                  WaitToFinish  : AsyncChoice<unit, string> }

    /// List of bin number and number of counts in bin
    type Histogram = { Histogram : (int * int) list }

    type HistogramResidual = 
        internal {  Histogram           : (int * int) list
                    OverflowMarkers     : int
                    MarkerTimestamp     : int }

    module Parameters =
        let create binWidth numberOfBins : TTTRHistogramParameters = 
            { BinWidth          = binWidth
              NumberOfBins      = numberOfBins }

        let withResolution binWidth         (parameters: TTTRHistogramParameters) = { parameters with BinWidth = binWidth }
        let withNumberOfBins numberOfBins   (parameters: TTTRHistogramParameters) = { parameters with NumberOfBins = numberOfBins }

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
        /// Identify the incoming record as a photon, overflow
        /// or marker record.
        let (| Photon | TimeOverflow | Marker |) tag =
            /// The channel number (0 - 3 for photons; 15 for markers including overflow)
            /// is held in the 4 highest significance bits.
            /// Currently, there is no distinction between the different marker records
            ///  - all marker channels are treated identically as simply a "marker".
            match ((tag >>> 28) &&& 0xF) with
                | channel when channel < 2                              -> Photon (channel, tag &&& 0x0FFFFFFFF)
                | marker when (marker = 15) && (marker &&& 0xF) = 0     -> TimeOverflow
                | marker when marker = 15                               -> Marker
                | other                                                 -> failwith "Unexpected time-tagged record identified: %d." other
(*
        let extractNextHistogram (tagStream : TagStream) = 
            /// find the first marker position in the stream; don't process any further if there isn't one
            let streamFromMarkerPosition = Seq.skipWhile (fun tag -> not <| TagHelper.isMarker tag) tagStream.Tags
            
            if not <| Seq.isEmpty streamFromMarkerPosition then
                let startTimestamp = TagHelper.getTimestamp <| Seq.head streamFromMarkerPosition
                Seq.*)

        let incrementTimeOverflowMarkers histogramResidual =
            { histogramResidual with OverflowMarkers = histogramResidual.OverflowMarkers + 1 }

        let histogramResidualCreate timestamp = 
            { Histogram = List.empty; OverflowMarkers = 0; MarkerTimestamp = timestamp }

        let addPhoton 

        let histogramEndTime (histogramResidual : HistogramResidual) (parameters : TTTRHistogramParameters) tag : int option = 
            TagHelper.timestamp tag histogram.MarkerTimestamp

        let extractAllHistograms (histogramResidual : HistogramResidual option) (tagStream : TagStream) : Histogram seq * (HistogramResidual option)  =
            Seq.fold (fun histogramState tag -> 
                let (histSequence, histResidual) = histogramState
                match histResidual, tag with
                    | None, tag when not <| TagHelper.isMarker tag -> histogramState
                    | None, tag                                    -> (histSequence, Some <| histogramResidualCreate TagHelper.timestamp tag)
                    | Some residual, tag when TagHelper.timestamp * res
                    | Some residual, tag when TagHelper.isPhoton -> 
                        
                        
                    | Some residual, tag when TagHelper.isTimeOverflow tag -> 
                        (histSequence, Some <| incrementTimeOverflowMarkers histResidual)
                    
                    | Some residual, tag when TagHelper.isMarker tag -> 
                        
                        failwith "Encountered an unexpected marker tag. Do the histogram settings match the experimental settings?"


                if histResidual.IsNone && not <| TagHelper.isMarker then
                    histogramState
                else
                    
                
                ) (Seq.empty, histogramResidual) (tagStream.Tags)
                        

     module internal HistogramEvent = 
        type HistogramEvent = 
            private { Trigger   : TagStream -> unit
                      Publish   : IObservable<Histogram> }
        
        let trigger event = event.Trigger

        let available event = 
            event.Publish

        let create = 
            let eventScheduler = new System.Reactive.Concurrency.EventLoopScheduler()
            let input = new Event<_>()

            let (markerStream, photonStream) = 
                input.Publish
                |> Observable.observeOn eventScheduler //need to extract elements from sequence!
                |> Observable.foldMap //folding stream (see paper); map ==> ignores hist. res
                |> Observable.collectSeq // fire event for each histogram
                                     
            { Trigger = input.Trigger ; Publish = output }
           
    module Acquisition = 
        let private buffer =  Array.zeroCreate TTTRMaxEvents
        
        let create picoHarp histogramParameters = 
            { Parameters = histogramParameters
              StreamingBuffer = { Buffer = buffer }
              PicoHarp = picoHarp
              StopCapability = new CancellationCapability<StreamStopOptions>()
              StatusChanged = new Event<StreamStatus>()
              EventsAvailable = new Event<TagStream>() }
        
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
            let acquisitionTime = Quantities.durationMilliSeconds acquisition.Parameters.AcquisitionTime
            let! __ = PicoHarp.Acquisition.start acquisition.PicoHarp acquisition.Parameters.AcquisitionTime
            acquisition.StatusChanged.Trigger (Streaming) }
    
        let private copyBufferAndFireEvent acquisition counts =
            let buffer = Array.zeroCreate counts
            acquisition.StreamingBuffer.Buffer.CopyTo (buffer, 0)
            
            let tagSequence = Seq.ofArray buffer
            acquisition.EventsAvailable.Trigger { Tags = tagSequence; BinWidth = acquisition.Parameters.BinWidth; NumberOfBins = acquisition.Parameters.NumberOfBins }

        let rec private pollUntilFinished acquisition = asyncChoice {
            if not acquisition.StopCapability.IsCancellationRequested then
                let! counts = 
                    PicoHarp.Acquisition.readFifoBuffer 
                    <| acquisition.PicoHarp
                    <| acquisition.StreamingBuffer

                copyBufferAndFireEvent acquisition counts

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

    let checkMeasurementFinished picoHarp300 =
        let mutable result = 0
        PicoHarp.logDevice picoHarp300 "Checking whether acquisition has finished."
        NativeApi.CTCStatus (PicoHarp.index picoHarp300, &result)
        |> PicoHarp.checkStatusAndReturn (result <> 0)
        |> PicoHarp.logQueryResult
            (sprintf "Successfully checked acquisition finished: %A.")
            (sprintf "Failed to check acquisition status due to error: %A.")
        |> AsyncChoice.liftChoice

    let rec waitToFinishMeasurement picoHarp300 pollDelay = asyncChoice {
        let! finished = checkMeasurementFinished picoHarp300
        if not finished then
            do! Async.Sleep pollDelay |> AsyncChoice.liftAsync
            do! waitToFinishMeasurement picoHarp300 pollDelay }