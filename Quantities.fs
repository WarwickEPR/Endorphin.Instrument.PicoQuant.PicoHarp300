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

    let durationMilliSeconds = function
        | Duration_ps duration -> Picoseconds.toMilliseconds duration 
        | Duration_ns duration -> Nanoseconds.toMilliseconds duration
        | Duration_us duration -> Microseconds.toMilliseconds duration
        | Duration_ms duration -> duration
        | Duration_s  duration -> Seconds.toMilliseconds duration

    let durationNanoSeconds = function
        | Duration_ps duration -> Picoseconds.toNanoseconds duration 
        | Duration_ns duration -> duration
        | Duration_us duration -> Microseconds.toNanoseconds duration
        | Duration_ms duration -> Milliseconds.toNanoseconds duration
        | Duration_s  duration -> Seconds.toNanoseconds duration

    let voltageMillivolts = function
        | Voltage_mV voltage -> voltage
        | Voltage_V voltage  -> Volts.toMillivolts (voltage)

    let resolutiontoWidth = function        
        | Resolution_4ps   -> 4
        | Resolution_8ps   -> 8
        | Resolution_16ps  -> 16
        | Resolution_32ps  -> 32
        | Resolution_64ps  -> 64
        | Resolution_128ps -> 128
        | Resolution_256ps -> 256
        | Resolution_512ps -> 512