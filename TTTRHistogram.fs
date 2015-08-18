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
        
    /// Starts histogram mode Measurements, requires an acquisition time aka the period of time to take Measurements over.
    let startMeasurements picoHarp300 (histogram : HistogramParameters) = 
        let acquisitionTime = Quantities.durationMilliSeconds (histogram.AcquisitionTime)
        PicoHarp.logDevice picoHarp300 "Setting acquisition time and starting Measurements."
        NativeApi.StartMeasurement (PicoHarp.index picoHarp300 , int (acquisitionTime)) 
        |> PicoHarp.checkStatus
        |> PicoHarp.logDeviceOpResult picoHarp300
            ("Successfully set histogram acquisition time and started Measurements") 
            (sprintf "Failed to start: %A.")
        |> AsyncChoice.liftChoice
    
    /// Stops histogram mode Measurements. 
    let endMeasurements picoHarp300 = 
        PicoHarp.logDevice picoHarp300 "Ending Measurements."
        NativeApi.StopMeasurement (PicoHarp.index picoHarp300)
        |> PicoHarp.checkStatus
        |> PicoHarp.logDeviceOpResult picoHarp300
            ("Successfully ended measurements.")
            (sprintf "Failed to end measurements: %A.")
        |> AsyncChoice.liftChoice
        
    /// Writes histogram data into the array histogramData.
    /// The argument block will always be zero unless routing is used. 
    let getHistogram picoHarp300 (histogramData:int[]) (block:int)  = 
        PicoHarp.logDevice picoHarp300 "Writing histogram data from device to an external array."
        GetHistogram (PicoHarp.index picoHarp300, histogramData, block)
        |> PicoHarp.checkStatus
        |> PicoHarp.logDeviceOpResult picoHarp300
            ("Successfully retrieved histogram data from device.")
            (sprintf "Failed to retrieve histogram data from device: %A.") 
        |> AsyncChoice.liftChoice 

    /// Writes channel count rate to a mutable int.  
    let getCountRate picoHarp300 (channel:int) =
        let mutable rate : int = Unchecked.defaultof<_>
        PicoHarp.logDevice picoHarp300 "Retrieving channel count rate."
        GetCountRate (PicoHarp.index picoHarp300, channel, &rate)
        |> PicoHarp.checkStatusAndReturn (rate)
        |> PicoHarp.logDeviceOpResult picoHarp300
            ("Successfully retrieved channel count rate.")
            (sprintf "Failed to retrieve channel count rate: %A")
        |> AsyncChoice.liftChoice
    
    /// Clears the histogram from picoHarps memory
    /// The argument block will always be zero unless routing is used. 
    let clearmemory picoHarp300 block =    
        PicoHarp.logDevice picoHarp300 "Clearing histogram data from device memory."
        NativeApi.ClearHistMem (PicoHarp.index picoHarp300, block)
        |> PicoHarp.checkStatus
        |> PicoHarp.logDeviceOpResult picoHarp300
            ("Cleared histogram data from device memory.")
            (sprintf "Failed to clear histogram data from device memory: %A.")
        |> AsyncChoice.liftChoice
    
    /// Ties together functions needed to take a single measurement 
    let private measurement picoHarp300 (histogram : HistogramParameters) (array : int[]) = asyncChoice{ 
        let! clear     = clearmemory picoHarp300 0 
        let! bin       = setBinning picoHarp300 histogram
        let! overflow  = stopOverflow picoHarp300 histogram
        let! startMeas = startMeasurements picoHarp300 histogram  
        let! endMeas   = endMeasurements picoHarp300
        let! histogram = getHistogram picoHarp300 array 0
        return histogram}
    
    let counts picoHarp300 (histogram: int[]) = Array.sum histogram

    module query =
       
        /// Returns time period over which experiment was running. 
        let getMeasurementTime picoHarp300 =
            let mutable elasped : double = Unchecked.defaultof<_>
            PicoHarp.logDevice picoHarp300 "Retrieving time passed since the start of histogram measurements."
            NativeApi.GetElapsedMeasTime (PicoHarp.index picoHarp300, &elasped) 
            |> PicoHarp.checkStatus
            |> PicoHarp.logQueryResult 
                (sprintf "Successfully retrieved measurement time: %A")
                (sprintf "Failed to retrieve measurement time: %A") 
            |> AsyncChoice.liftChoice

        /// If 0 is returned acquisition time is still running, >0 then acquisition time has finished. 
        let getCTCStatus picoHarp300 = 
            let mutable ctcStatus : int = Unchecked.defaultof<_>
            PicoHarp.logDevice picoHarp300 "Checking CTC status" 
            NativeApi.CTCStatus (PicoHarp.index picoHarp300, &ctcStatus)   
            |> PicoHarp.checkStatus
            |> PicoHarp.logQueryResult 
                (sprintf "Successfully retrieved CTC status: %A")
                (sprintf "Failed to retrieve CTC status: %A") 
            |> AsyncChoice.liftChoice