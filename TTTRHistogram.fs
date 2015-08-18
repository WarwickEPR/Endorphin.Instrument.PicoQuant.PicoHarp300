namespace Endorphin.Instrument.PicoHarp300

open Microsoft.FSharp.NativeInterop
open System.Runtime.InteropServices
open Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols
open System.Text
open Endorphin.Core
open Endorphin.Core.NationalInstruments
open Endorphin.Core.String
open ExtCore.Control

module TTTRHistogram = 
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