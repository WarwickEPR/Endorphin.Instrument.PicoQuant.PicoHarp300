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

module CFD = 
        
        /// Takes type of CFD and converts elements into integers then passes to the function PH_SetInputCFD.
        /// This sets the discriminator level and zero cross for the CFD in channels 1 or 2. 
        let initialiseCFD deviceIndex (cfd:CFD) = asyncChoice{
            let channel = inputChannel cfd
            let level = discrminatorLevel cfd
            let cross = zeroCross cfd
            let success = PH_SetInputCFD (deviceIndex, channel, int(level), int(cross))
            return success}
