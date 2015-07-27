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

module PicoHarpInfo =
    
    /// Returns the PicoHarp's serial number.
    let GetSerialNumber deviceIndex = asyncChoice{
        let serial = stringBuilder(String_8)
        let success = PH_GetSerialNumber (deviceIndex , serial)
        return serial}

    /// Returens the PicoHarp's model number, part number ans version.
    let hardwareInformation deviceIndex = asyncChoice{
        let model = stringBuilder(String_16)
        let partnum = stringBuilder(String_8)
        let vers = stringBuilder(String_8)
        let hardware = PH_GetHardwareInfo (deviceIndex, model, partnum, vers)
        return (model, partnum, vers)} 

    /// Returns the histogram base resolution (the PicoHarp needs to be in histogram mode for function to return a success)
    let getBaseResolution deviceIndex = asyncChoice{
        let mutable resolution : double = Unchecked.defaultof<_>
        let success = PH_GetResolution(deviceIndex , &resolution) 
        return sprintf "Set resolution: %i" success}

    /// Returns the features of the PicoHarp as a bit pattern.
    let getFeatures deviceIndex = asyncChoice{
        let mutable features : int = Unchecked.defaultof<_>
        let featureBit = PH_GetFeatures (deviceIndex , &features)
        return featureBit}
        
