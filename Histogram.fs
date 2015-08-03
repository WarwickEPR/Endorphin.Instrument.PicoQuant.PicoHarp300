namespace Endorphin.Instrument.PicoHarp300

open Microsoft.FSharp.NativeInterop
open System.Runtime.InteropServices
open Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols
open System.Text
open Endorphin.Core
open Endorphin.Core.NationalInstruments
open Endorphin.Core.StringUtils
open ExtCore.Control

module Histogram = 
    
    let private index (PicoHarp300 h) = h

    module initalise  = 
     
        /// Sets the overflow limit on or off for the histogram bins.
        let private stopOverflow deviceIndex (histogram : HistogramParameters) = 
            /// Sets cap on number of counts per bin, minimum cap is 1 and maximum is 65535.      
            match histogram.Overflow with
            /// If overflow is turned on then check if limit is inside allowed range and then write to PicoHarp.
            | On limit -> if (limit < 65535 && limit > 0) then 
                              SetStopOverflow (deviceIndex, 0, limit)
                          else failwithf "Invalid count limit, must be between 1 and 65535"
            /// If overflow is switched off then set limit to the maximum and turn off.
            | Off -> SetStopOverflow (deviceIndex, 1, 65535)

        /// Sets the bin resolution for the histogram.
        let private setBinning picoHarp300 (histogram : HistogramParameters) = 
            let binning = resolutionEnum (histogram.Resolution)
            NativeApi.SetBinning (index picoHarp300, binning)
            |> PicoHarp.checkStatus 
            |> PicoHarp.logQueryResult 
                (sprintf "Successfully set histogram binning resolution to %A: %A" binning) 
                (sprintf "Failed to set histogram binning resolution to %A: %A" binning)
            
        /// Starts histogram mode measurments, requires an acquisition time aka the period of time to take measurments over.
        let private startMeasurments picoHarp300 (histogram : HistogramParameters) = 
            let acquisitionTime = Quantities.durationSeconds (histogram.AcquisitionTime)
            NativeApi.StartMeasurment (index picoHarp300 , int (acquisitionTime)) 
            |> PicoHarp.checkStatus
            |> PicoHarp.logQueryResult
                (sprintf "Successfully set histogram acquisition time to %A: %A" acquisitionTime) 
                (sprintf "Failed to set histogram binning resolution %A: %A" acquisitionTime)

        /// Stops histogram mode measurments. 
        let private endMeasurments picoHarp300 = 
            NativeApi.StopMeasurment (index picoHarp300)
            |> PicoHarp.checkStatus
            |> PicoHarp.logDeviceQueryResult picoHarp300
                (sprintf "Successfully closed device (%A): %A" picoHarp300)
                (sprintf "Failed to close device (%A): %A" picoHarp300)
            
        /// Writes histogram data into the array histogramData.
        /// The argument block will always be zero unless routing is used. 
        let private getHistogram picoHarp300 (histogramData:int[]) (block:int)  = 
            NativeApi.GetHistogram (index picoHarp300, histogramData, block)
            |> PicoHarp.checkStatus
            |> PicoHarp.logDeviceQueryResult picoHarp300
                (sprintf "Successfully retrieved histogram data from device (%A): %A" picoHarp300)
                (sprintf "Failed to retrieve histogram data from device (%A): (%A)" picoHarp300) 

        ///Clears the histogram from picoHarps memory
        /// The argument block will always be zero unless routing is used. 
        let private clearmemory picoHarp300 block = 
            NativeApi.ClearHistMem (index picoHarp300, block)
            |> PicoHarp.checkStatus
            |> PicoHarp.logDeviceQueryResult picoHarp300
                (sprintf "Cleared histogram data in block %A from device (%A) memory: %A" block picoHarp300)
                (sprintf "Failed to clear histogram data in block %A from device (%A) memory: %A" block picoHarp300)
        
        
        /// Ties together functions needed to take a single measurment 
        let measurmentSingle deviceIndex (histogram : HistogramParameters) (array : int[]) = asyncChoice{ 
            let! clear = clearmemory deviceIndex 0 
            let! bin = setBinning 0 histogram
            let! overflow = stopOverflow 0 histogram
            let! start = startMeasurments deviceIndex histogram  
            let! endMeasurment = endMeasurments deviceIndex
            let! histogram = getHistogram deviceIndex array 0
            return histogram}

        /// Calculates the total counts measured by summing the histogram channels.
        let countTotal (pinnedArray: int []) =  Array.sum pinnedArray






        

      


    module query =
       
        /// Returns time period over which experiment was running. 
        let getMeasurmentTime deviceIndex = asyncChoice{
            let mutable elasped : double = Unchecked.defaultof<_>
            let time =  GetElapsedMeasTime (deviceIndex, &elasped) 
            return time}
           
        /// If 0 is returned acquisition time is still running, >0 then acquisition time has finished. 
        let getCTCStatus deviceIndex = asyncChoice{
            let mutable ctcStatus : int = Unchecked.defaultof<_>
            let success = CTCStatus (deviceIndex, &ctcStatus)
            return success}



  
  