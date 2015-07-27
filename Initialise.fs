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
open Endorphin.Instrument.PicoHarp300.Native


module Initialise = 
    
    /// Returns the PicoHarp's serial number.
    let openDevice deviceIndex = asyncChoice{ 
        let serialNumber = stringBuilder(String_8)
        let serial = PH_OpenDevice(deviceIndex , serialNumber)
        return serialNumber} 
    
    /// Calibrates the PicoHarp. 
    let calibrate deviceIndex = asyncChoice{
        let success = PH_Calibrate (deviceIndex)
        return sprintf "Calibration: %i" success}

    /// Sets the PicoHarps mode.
    let initialiseMode deviceIndex initalMode = asyncChoice{
        let success = PH_Initialise (deviceIndex, modeNumber(initalMode))
        return sprintf "Initialisation: %i" success}
    
    /// Closes the PicoHarp.
    let closeDevice deviceIndex = asyncChoice{
        let success = PH_CloseDevice (deviceIndex)
        return sprintf "Close device: %i"success}
