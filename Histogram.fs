﻿namespace Endorphin.Instrument.PicoHarp300

open Microsoft.FSharp.NativeInterop
open System.Runtime.InteropServices
open Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols
open System.Text
open Endorphin.Core
open Endorphin.Core.NationalInstruments
open Endorphin.Core.StringUtils
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
                            |> PicoHarp.logDeviceQueryResult picoHarp300
                                (sprintf "Successfully set limit for number of counts per histogram channel: %A.")
                                (sprintf "Failed to set limit for counts per histogram channel: %A")
                            |> AsyncChoice.liftChoice
                        else failwithf "Invalid count limit, must be between 1 and 65535"
        /// If overflow is switched off then set limit to the maximum and turn off.
        | None ->           PicoHarp.logDevice picoHarp300 "Setting histogram channel count limit to maximum: 65535"
                            NativeApi.SetStopOverflow (PicoHarp.index picoHarp300, 1, 65535)
                            |> PicoHarp.checkStatus
                            |> PicoHarp.logDeviceQueryResult picoHarp300
                                (sprintf "Successfully set limit for number of counts per histogram channel to maximum: %A.")
                                (sprintf "Failed to set limit for counts per histogram channel to maximum: %A")
                            |> AsyncChoice.liftChoice
    
    /// Sets the bin resolution for the histogram.
    let setBinning picoHarp300 (histogram : HistogramParameters) = 
        let binning = resolutionEnum (histogram.Resolution)
        PicoHarp.logDevice picoHarp300 "Setting histogram bin resolution."
        NativeApi.SetBinning (PicoHarp.index picoHarp300, binning)
        |> PicoHarp.checkStatus 
        |> PicoHarp.logQueryResult 
            (sprintf "Successfully set histogram binning resolution to %A: %A" binning) 
            (sprintf "Failed to set histogram binning resolution to %A: %A" binning)
        |> AsyncChoice.liftChoice

    let checkMeasurementFinished picoHarp300 =
        let mutable result = 0
        PicoHarp.logDevice picoHarp300 "Checking whether acquisition has finished..."
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
        |> PicoHarp.logQueryResult
            (sprintf "Successfully set histogram acquisition time %A and started Measurements: %A" (int (acquisitionTime))) 
            (sprintf "Failed to start %A: %A" acquisitionTime)
        |> AsyncChoice.liftChoice
    
    /// Stops histogram mode Measurements. 
    let endMeasurements picoHarp300 = 
        PicoHarp.logDevice picoHarp300 "Ending Measurements."
        NativeApi.StopMeasurement (PicoHarp.index picoHarp300)
        |> PicoHarp.checkStatus
        |> PicoHarp.logDeviceQueryResult picoHarp300
            (sprintf "Successfully closed device (%A): %A" picoHarp300)
            (sprintf "Failed to close device (%A): %A" picoHarp300)
        |> AsyncChoice.liftChoice
        
    /// Writes histogram data into the array histogramData.
    /// The argument block will always be zero unless routing is used. 
    let getHistogram picoHarp300 (histogramData:int[]) (block:int)  = 
        PicoHarp.logDevice picoHarp300 "Writing histogram data from device to an external array."
        GetHistogram (PicoHarp.index picoHarp300, histogramData, block)
        |> PicoHarp.checkStatus
        |> PicoHarp.logDeviceQueryResult picoHarp300
            (sprintf "Successfully retrieved histogram data from device (%A): %A" picoHarp300)
            (sprintf "Failed to retrieve histogram data from device (%A): (%A)" picoHarp300) 
        |> AsyncChoice.liftChoice 
    
    ///Clears the histogram from picoHarps memory
    /// The argument block will always be zero unless routing is used. 
    let clearmemory picoHarp300 block =    
        PicoHarp.logDevice picoHarp300 "Clearing histogram data from device memory."
        NativeApi.ClearHistMem (PicoHarp.index picoHarp300, block)
        |> PicoHarp.checkStatus
        |> PicoHarp.logDeviceQueryResult picoHarp300
            (sprintf "Cleared histogram data in block %A from device (%A) memory: %A" block picoHarp300)
            (sprintf "Failed to clear histogram data in block %A from device (%A) memory: %A" block picoHarp300)
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
                (sprintf "Successfully retrieved measurement time, %A: %A" elasped)
                (sprintf "Failed to retrieve measurement time: %A") 
            |> AsyncChoice.liftChoice

        /// If 0 is returned acquisition time is still running, >0 then acquisition time has finished. 
        let getCTCStatus picoHarp300 = 
            let mutable ctcStatus : int = Unchecked.defaultof<_>
            PicoHarp.logDevice picoHarp300 "Checking CTC status" 
            NativeApi.CTCStatus (PicoHarp.index picoHarp300, &ctcStatus)   
            |> PicoHarp.checkStatus
            |> PicoHarp.logQueryResult 
                (sprintf "Successfully retrieved CTC status, %A: %A" ctcStatus)
                (sprintf "Failed to retrieve CTC status: %A") 
            |> AsyncChoice.liftChoice


  