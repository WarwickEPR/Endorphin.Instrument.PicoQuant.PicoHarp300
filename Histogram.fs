namespace Endorphin.Instrument.PicoHarp300

open Microsoft.FSharp.NativeInterop
open System.Runtime.InteropServices
open Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols
open System.Text
open Endorphin.Core
open Endorphin.Core.NationalInstruments
open Endorphin.Core.StringUtils
open ExtCore.Control
open Endorphin.Instrument.PicoHarp300.Native


module Histogram = 
    
    /// Sets the overflow limit on or off for the histogram bins.
    let stopOverflow deviceIndex switch = asyncChoice{ 
        /// Sets cap on number of counts per bin, minimum cap is 1 and maximum is 65535. 
        let overflow switch =     
            match switch.Overflow with
            /// If overflow is turned on then check if limit is inside allowed range and then write to PicoHarp.
            | On limit -> if (limit < 65535 && limit > 0) then 
                              let stopcount = limit
                              PH_SetStopOverflow (deviceIndex, 0, limit)
                          else failwithf "Invalid count limit, must be between 1 and 65535"
            /// If overflow is switched off then set limit to the maximum and turn off.
            | Off -> PH_SetStopOverflow (deviceIndex, 1, 65535)
        return overflow
        }

    /// Sets the bin resolution for the histogram.
    let setBinning deviceIndex (resolution:Histogram) = asyncChoice{
        let success = PH_SetBinning (deviceIndex, (binning resolution))
        return sprintf "Set binning: %i" success}

    /// Starts histogram mode measurments, requires an acquisition time aka the period of time to take measurments over.
    let startMeasurments deviceIndex (time:Histogram) = asyncChoice{
        let success = PH_StartMeas (deviceIndex , int (acquisitionTime time) ) 
        return sprintf "Start measurments: %i" success}
    
    /// Stops histogram mode measurments. 
    let endMeasurments deviceIndex = asyncChoice{
        let success = PH_StopMeas (deviceIndex)
        return sprintf "Stop measurments: %i" success}

    /// Creates an initilised array for storing histogram data.
    let histogramData : int array = Array.zeroCreate 65536 
    /// Creates a pinned array for PicoHarp to write into.
    let pinnedHistogram =  PinnedArray.of_array histogramData

    /// Writes histogram data in the pinned array.
    /// The argument block will always be zero unless routing is used. 
    let getHistogram deviceIndex block = asyncChoice{
        let success = PH_GetHistogram (deviceIndex, pinnedHistogram.Ptr, block)
        return pinnedHistogram}

    ///Clears the histogram from picoHarps memrory
    /// The argument block will always be zero unless routing is used. 
    let clearHistogramMemrory deviceIndex block = asyncChoice{
        let success = PH_ClearHistMem (deviceIndex, block)
        return success}

    let countRate (pinnedArray: int []) =  Array.sum pinnedArray
        