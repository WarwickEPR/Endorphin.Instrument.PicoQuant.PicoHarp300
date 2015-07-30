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

[<AutoOpen>]
module PicoHarpInfo =
        
    /// Returns the PicoHarp's serial number.
    let GetSerialNumber deviceIndex = asyncChoice{
        let serial = stringBuilder(String_8)
        let success = GetSerialNumber (deviceIndex , serial)
        return serial}
    
    [<AutoOpen>]
    module HardwareInfo = 
       
        /// Returens the PicoHarp's model number, part number ans version.
        let hardwareInformation deviceIndex = asyncChoice{
            let model = stringBuilder(String_16)
            let partnum = stringBuilder(String_8)
            let vers = stringBuilder(String_8)
            let hardware = GetHardwareInfo (deviceIndex, model, partnum, vers)
            return (model, partnum, vers)} 
        
        /// Returns the model number from the hardwareImformation tuple. 
        let tupleModel = function
            | (x, y, z) -> x
            |_ -> failwithf "No tuple"
        
        /// Returns the part number number from the hardwareImformation tuple. 
        let tuplePartnum = function
            | (x, y, z) -> y
            |_ -> failwithf "No tuple"

        /// Returns the version number from the hardwareImformation tuple.     
        let tupleVers = function
            | (x, y, z) -> z
            |_ -> failwithf "No tuple"

    /// Returns the histogram base resolution (the PicoHarp needs to be in histogram mode for function to return a success)
    let getBaseResolution deviceIndex = asyncChoice{
        let mutable resolution : double = Unchecked.defaultof<_>
        let success = GetResolution(deviceIndex , &resolution) 
        return sprintf "Set resolution: %i" success}

    /// Returns the features of the PicoHarp as a bit pattern.
    let getFeatures deviceIndex = asyncChoice{
        let mutable features : int = Unchecked.defaultof<_>
        let featureBit = GetFeatures (deviceIndex , &features)
        return featureBit}
        
