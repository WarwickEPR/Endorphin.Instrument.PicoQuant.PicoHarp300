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
    let openDevice deviceIndex  = asyncChoice{ 
        let serialNumber = StringBuilder (8)
        let serial = OpenDevice(deviceIndex , serialNumber)
        return serialNumber} 
    
    /// Calibrates the PicoHarp. 
    let calibrate deviceIndex = asyncChoice{
        let success = Calibrate (deviceIndex)
        return sprintf "Calibration: %i" success}

    /// Sets the PicoHarps mode.
    let initialiseMode deviceIndex initalMode = asyncChoice{
        let success = InitialiseMode (deviceIndex, modeNumber(initalMode))
        return sprintf "Initialisation: %i" success}
    
    /// Closes the PicoHarp.
    let closeDevice deviceIndex = asyncChoice{
        let success = CloseDevice (deviceIndex)
        return success}
