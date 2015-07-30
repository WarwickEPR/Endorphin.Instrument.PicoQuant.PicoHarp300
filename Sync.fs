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

module Sync = 
    
    /// Sets the rate divider of the sync/channel 0.
    let setSyncDiv deviceIndex (sync:Sync) = asyncChoice{
        let div = rateDivider sync 
        let success = SetSyncDiv (deviceIndex, div)
        return success}

    /// Sets the offset of the sync/channel 0.
    let SetSyncOffset deviceIndex (sync:Sync) = asyncChoice{
        let off = offset sync.Offset
        let success = SetSyncOffset (deviceIndex, int(off))
        return success}