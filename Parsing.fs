namespace Endorphin.Instrument.PicoHarp300

open Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols
open System.Text
open Endorphin.Core
open Endorphin.Core.NationalInstruments
open Endorphin.Core.StringUtils
open ExtCore.Control

[<AutoOpen>]
module Parsing =
 
    [<AutoOpen>]
    module General = 
       
        /// Builds a string for PicoHarp to write into.
        let stringBuilder = function
            | String_8 -> StringBuilder (8)
            | String_16 -> StringBuilder (16)
            | String_40 -> StringBuilder (40)
            | String_16384 -> StringBuilder (16384)
        
        /// Converts all times into seconds. 
        let timeConvert = function 
            | Time_pico time -> (1E-12)*time
            | Time_nano time ->  (1E-9)*time
            | Time_micro time -> (1E-6)*time
            | Time_milli time -> (1E-3)*time
            | Time_second time -> time

        /// Converts all voltages into millivolts.
        let voltsConvert = function
            | Volts_mV voltage -> voltage
            | Volts_V voltage -> voltage*1000.0                                                                                                                                 
    
    [<AutoOpen>]
    module Initialise = 
       
        /// Converts type Mode into integers. 
        let modeNumber = function
            | Histogram -> 0
            | T2 -> 2
            | T3 -> 3

        /// Converts mode integer back into type Mode.
        let parseMode = function
            | 0 -> Histogram
            | 2 -> T2
            | 3 -> T3
            | _ -> failwithf "Invalid mode command"

        /// Converts type InputChannel into corresponding integers.
        let channelNumber = function
            | Channel_0 -> 0
            | Channel_1 -> 1

        /// Converts channel number back into type InputChannel. 
        let parseChannel = function
            | 0 -> Channel_0
            | 1 -> Channel_1
            | _ -> failwithf "Invaild channel"

    [<AutoOpen>]
    module CFDConvert =
        
        /// Takes InputChannel from record type CFD and converts into an integer.
        let inputChannel channel = 
            if channel.InputChannel = Channel_0 then 0
            else 1
        
        /// Takes DiscriminatorLevel from type CFD and converts into a voltage in units volts.
        let discriminatorLevel level = General.voltsConvert (level.DiscriminatorLevel) 
        
        /// Takes ZeroCross from type CFD and converts into a voltage in units volts.
        let zeroCross zero = General.voltsConvert (zero.ZeroCross)

    [<AutoOpen>]
    module Histogram =
        
        /// Converts the bin width into corresponding power of 2, e.g 8 -> 3.
        let binningNumber resolution = 
            match resolution.BinWidth with
            | Width_4ps ->   0
            | Width_8ps  ->  1
            | Width_16ps ->  2
            | Width_32ps ->  3
            | Width_64ps ->  4
            | Width_128ps -> 5
            | Width_256ps -> 6 
            | Width_512ps -> 7

        /// Converts bin width base number into type of Width.
        let parseBinning number =
            match number with 
            | 0 -> Width_4ps
            | 1 -> Width_8ps
            | 2 -> Width_16ps
            | 3 -> Width_32ps
            | 4 -> Width_64ps
            | 5 -> Width_128ps
            | 6 -> Width_256ps
            | 7 -> Width_512ps
            | _ -> failwithf "Not a valid resolution."

        /// Takes aquisition time from type histogram and convert into milliseconds. 
        let acquisitionTime time = General.timeConvert (time.AcquisitionTime)*(1E-3) 
           
    [<AutoOpen>]
    /// Channel 1 settings.
    module ChannelSync = 
        
        /// Converts type Rate into corresponding number.
        let rateDividerNumber rate = 
            match rate.RateDivider with
            | RateDivider_1 -> 1 
            | RateDivider_2 -> 2
            | RateDivider_4 -> 4
            | RateDivider_8 -> 8

        /// Converts number into type Rate.
        let parseRateDivider number =
            match number with 
            | 1 -> RateDivider_1
            | 2 -> RateDivider_2
            | 3 -> RateDivider_4
            | 4 -> RateDivider_8
            | _ -> failwithf "Not a valid rate division."

       
       /// Converts time into picoseconds 
        let offset time = (General.timeConvert(time))*(1E-12)
        
