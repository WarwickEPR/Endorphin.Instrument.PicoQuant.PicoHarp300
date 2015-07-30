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
        
        /// Checks if the values for discriminator level and the zerocross are inside their respective ranges. 
        let private rangeCFD (cfd:CFD) = 
            let level = discriminatorLevel cfd 
            let cross = zeroCross cfd
            if (level < 0.0 || level < -800.0) then
                failwithf "Discriminator level outside of range: 0 - 800 mV."
            elif (cross < 0.0 || cross > 20.0) then 
                failwithf "Zerocorss setting outside of range: 0 - 20 mV."
            else 0

        /// Takes type of CFD and converts elements into integers then passes to the function PH_SetInputCFD.
        /// This sets the discriminator level and zero cross for the CFD in channels 1 or 2. 
        let initialiseCFD deviceIndex (cfd:CFD) = asyncChoice{
            let channel = inputChannel cfd
            let level = discriminatorLevel cfd 
            let cross = zeroCross cfd
            if rangeCFD cfd <> 0 then 
                return 0
            else 
                let success = SetInputCFD (deviceIndex, channel, int(level), int(cross))
                return success}
