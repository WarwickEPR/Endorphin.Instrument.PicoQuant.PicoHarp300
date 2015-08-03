namespace Endorphin.Instrument.PicoHarp300

open Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols
open System.Text
open Endorphin.Core
open Endorphin.Core.NationalInstruments
open Endorphin.Core.StringUtils
open ExtCore.Control

module Quantities = 
    
    let durationSeconds = function
        | Duration_ps duration -> Picoseconds.toSeconds duration 
        | Duration_ns duration -> Nanoseconds.toSeconds duration
        | Duration_us duration -> Microseconds.toSeconds duration
        | Duration_ms duration -> Milliseconds.toSeconds duration
        | Duration_s  duration -> duration

    let durationNanoSeconds = function
        | Duration_ps duration -> Picoseconds.toNanoseconds duration 
        | Duration_ns duration -> duration
        | Duration_us duration -> Microseconds.toNanoseconds duration
        | Duration_ms duration -> Milliseconds.toNanoseconds duration
        | Duration_s  duration -> Seconds.toNanoseconds duration

    let voltageMillivolts = function
        | Voltage_mV voltage -> voltage
        | Voltage_V voltage  -> Volts.toMillivolts (voltage)

