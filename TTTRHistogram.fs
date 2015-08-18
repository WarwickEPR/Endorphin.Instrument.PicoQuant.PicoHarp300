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

    type EventStreamObserved = 
        internal { EventStream      : double[] }

    type HistogramAcquisition = 
        private { Parameters            : TTTRHistogramParameters
                  PicoHarp              : PicoHarp300
                  StopCapability        : CancellationCapability<StreamStopOptions>
                  StatusChanged         : Event<StreamStatus>
                  EventsAvailable       : Event<EventStreamObserved> }

    type HistogramAcquisitionHandle = 
        private { Acquisition   : HistogramAcquisition
                  WaitToFinish  : AsyncChoice<unit, string> }

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


    module Acquisition = 
        let create picoHarp histogramParameters = 
            { Parameters = histogramParameters
              PicoHarp = picoHarp
              StopCapability = new CancellationCapability<StreamStopOptions>()
              StatusChanged = new Event<StreamStatus>()
              EventsAvailable = new Event<EventStreamObserved>() }
        
        let status acquisition =
            acquisition.StatusChanged.Publish
            |> Observable.completeOn (function
                | FinishedStream _
                | CancelledStream _ -> true
                | _                 -> false)
            |> Observable.errorOn (function
                | FailedStream message -> Some (Exception message)
                | _                    -> None)
              
        let private startMeasurement acquisition = 
            let acquisitionTime = Quantities.durationMilliSeconds acquisition.Parameters.AcquisitionTime
            NativeApi.StartMeasurement (PicoHarp.index acquisition.PicoHarp, int acquisitionTime)
            |> PicoHarp.checkStatus
            |> AsyncChoice.liftChoice

        let start acquisition : AsyncChoice<HistogramAcquisitionHandle, string> = 
            if acquisition.StopCapability.IsCancellationRequested then
                failwith "Cannot start an acquisition which has already been stopped."

            let finishAcquisition cont acquisitionResult = Async.StartImmediate <| async {
                let! stopResult = endMeasurement acquisition.PicoHarp 
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
                    cont (fail f)
                                }


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
        
    /// Starts TTTR mode measurement
    let startMeasurement picoHarp300 (histogram : HistogramParameters) = 
        let acquisitionTime = Quantities.durationMilliSeconds (histogram.AcquisitionTime)
        PicoHarp.logDevice picoHarp300 "Setting acquisition time and starting measurement."
        NativeApi.StartMeasurement (PicoHarp.index picoHarp300 , int (acquisitionTime)) 
        |> PicoHarp.checkStatus
        |> PicoHarp.logDeviceOpResult picoHarp300
            ("Successfully set acquisition time and started TTTR measurement") 
            (sprintf "Failed to start: %A.")
        |> AsyncChoice.liftChoice
    
    /// Stops TTTR mode measurement 
    let endMeasurement picoHarp300 = 
        PicoHarp.logDevice picoHarp300 "Ceasing measurement."
        NativeApi.StopMeasurement (PicoHarp.index picoHarp300)
        |> PicoHarp.checkStatus
        |> PicoHarp.logDeviceOpResult picoHarp300
            ("Successfully ended measurement.")
            (sprintf "Failed to end measurement: %A.")
        |> AsyncChoice.liftChoice

    /// Ties together functions needed to take a single measurement 
    let private measurement picoHarp300 (histogram : HistogramParameters) (array : int[]) = asyncChoice{ 
        let! startMeas = startMeasurement picoHarp300 histogram  
        let! endMeas   = endMeasurement picoHarp300
        return histogram}
    
    let counts picoHarp300 (histogram: int[]) = Array.sum histogram