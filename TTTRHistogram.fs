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

module TTTRHistogram = 
    type StreamStopOptions = { AcquisitionDidAutoStop : bool }

    type StreamStatus = 
        | PreparingStream
        | Streaming
        | FinishedStream of options : StreamStopOptions
        | FailedStream of error : string
        | CancelledStream of exn : OperationCanceledException

    type EventStream = 
        private { Events : int[] }

    type HistogramAcquisition = 
        private { Parameters            : TTTRHistogramParameters
                  StreamingBuffer       : StreamingBuffer
                  PicoHarp              : PicoHarp300
                  StopCapability        : CancellationCapability<StreamStopOptions>
                  StatusChanged         : Event<StreamStatus>
                  EventsAvailable       : Event<EventStream> }

    type HistogramAcquisitionHandle = 
        private { Acquisition   : HistogramAcquisition
                  WaitToFinish  : AsyncChoice<unit, string> }

    type Histogram = unit

    module Parameters =
        let create resolution numberOfBins acquisitionTime markerChannel = 
            { Resolution        = resolution
              NumberOfBins      = numberOfBins
              AcquisitionTime   = acquisitionTime
              MarkerChannel     = markerChannel }

        let withResolution resolution           (parameters: TTTRHistogramParameters) = { parameters with Resolution = resolution }
        let withNumberOfBins numberOfBins       (parameters: TTTRHistogramParameters) = { parameters with NumberOfBins = numberOfBins }
        let withAcquisitionTime acquisitionTime (parameters: TTTRHistogramParameters) = { parameters with AcquisitionTime = acquisitionTime }
        let withMarkerChannel markerChannel     (parameters: TTTRHistogramParameters) = { parameters with MarkerChannel = markerChannel }

     module internal HistogramEvent = 
        type HistogramEvent = 
            private { Trigger   : EventStream -> unit
                      Publish   : IObservable<Histogram> }
        
        let trigger event = event.Trigger

        let available event = 
            event.Publish

        let create = 
            let input = new Event<_>()
            let output = input .Publish|> Event.map ignore // TODO : implement processing here
            
            { Trigger = input.Trigger ; Publish = output }
            
    module Acquisition = 
        let private buffer =  Array.zeroCreate TTTRMaxEvents
        
        let create picoHarp histogramParameters = 
            { Parameters = histogramParameters
              StreamingBuffer = { Buffer = buffer }
              PicoHarp = picoHarp
              StopCapability = new CancellationCapability<StreamStopOptions>()
              StatusChanged = new Event<StreamStatus>()
              EventsAvailable = new Event<EventStream>() }
        
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

            acquisition.EventsAvailable.Trigger { Events = buffer }

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