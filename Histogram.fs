namespace Endorphin.Instrument.PicoHarp300

open Microsoft.FSharp.NativeInterop
open System.Runtime.InteropServices
open Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols
open System.Text
open Endorphin.Core
open ExtCore.Control

module Histogram = 
    /// Sets the overflow limit on or off for the histogram bins.
    let stopOverflow picoHarp300 (histogram : HistogramParameters) = 
        /// Sets cap on number of counts per bin, minimum cap is 1 and maximum is 65535.      
        match histogram.Overflow with
        /// If overflow is turned on then check if limit is inside allowed range and then write to PicoHarp.
        | Some limit -> if (limit < 65535 && limit > 0) then 
                            PicoHarp.logDevice picoHarp300 "Setting histogram channel count limit"  
                            NativeApi.SetStopOverflow (PicoHarp.index picoHarp300, 0, limit)
                            |> PicoHarp.checkStatus
                            |> PicoHarp.logDeviceOpResult picoHarp300
                                ("Successfully set limit for number of counts per histogram channel.")
                                (sprintf "Failed to set limit for counts per histogram channel: %A")
                            |> AsyncChoice.liftChoice
                        else failwithf "Invalid count limit, must be between 1 and 65535"
        /// If overflow is switched off then set limit to the maximum and turn off.
        | None ->           PicoHarp.logDevice picoHarp300 "Setting histogram channel count limit to maximum: 65535"
                            NativeApi.SetStopOverflow (PicoHarp.index picoHarp300, 1, 65535)
                            |> PicoHarp.checkStatus
                            |> PicoHarp.logDeviceOpResult picoHarp300
                                (sprintf "Successfully set limit for number of counts per histogram channel to maximum.")
                                (sprintf "Failed to set limit for counts per histogram channel to maximum: %A")
                            |> AsyncChoice.liftChoice
    
    /// Sets the bin resolution for the histogram.
    let setBinning picoHarp300 (histogram : HistogramParameters) = 
        let binning = resolutionEnum (histogram.Resolution)
        PicoHarp.logDevice picoHarp300 "Setting histogram bin resolution."
        NativeApi.SetBinning (PicoHarp.index picoHarp300, binning)
        |> PicoHarp.checkStatus 
        |> PicoHarp.logDeviceOpResult picoHarp300 
            ("Successfully set histogram binning resolution.") 
            (sprintf "Failed to set histogram binning resolution: %A")
        |> AsyncChoice.liftChoice

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
    let startMeasurement picoHarp300 (histogram : HistogramParameters) = 
        let acquisitionTime = Quantities.durationMilliSeconds (histogram.AcquisitionTime)
        PicoHarp.logDevice picoHarp300 "Setting acquisition time and starting Measurements."
        NativeApi.StartMeasurement (PicoHarp.index picoHarp300 , int (acquisitionTime)) 
        |> PicoHarp.checkStatus
        |> PicoHarp.logDeviceOpResult picoHarp300
            ("Successfully set histogram acquisition time and started Measurements") 
            (sprintf "Failed to start: %A.")
        |> AsyncChoice.liftChoice
    
    /// Stops histogram mode Measurements. 
    let endMeasurement picoHarp300 = 
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

    /// Clears the histogram from picoHarps memory
    /// The argument block will always be zero unless routing is used. 
    let clearmemory picoHarp300 =    
        PicoHarp.logDevice picoHarp300 "Clearing histogram data from device memory."
        NativeApi.ClearHistMem (PicoHarp.index picoHarp300, 0)
        |> PicoHarp.checkStatus
        |> PicoHarp.logDeviceOpResult picoHarp300
            ("Cleared histogram data from device memory.")
            (sprintf "Failed to clear histogram data from device memory: %A.")
        |> AsyncChoice.liftChoice
    
    /// Ties together functions needed to take a single measurement 
    let private measurement picoHarp300 (histogram : HistogramParameters) (array : int[]) = asyncChoice{ 
        let! clear     = clearmemory picoHarp300
        let! bin       = setBinning picoHarp300 histogram
        let! overflow  = stopOverflow picoHarp300 histogram
        let! startMeas = startMeasurement picoHarp300 histogram  
        let! endMeas   = endMeasurement picoHarp300
        let! histogram = getHistogram picoHarp300 array 0
        return histogram}
    
    let counts picoHarp300 (histogram: int[]) = Array.sum histogram  