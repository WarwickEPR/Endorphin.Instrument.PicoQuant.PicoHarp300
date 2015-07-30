namespace Endorphin.Instrument.PicoHarp300

open Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols
open System.Text
open Endorphin.Core
open Endorphin.Core.NationalInstruments
open Endorphin.Core.StringUtils
open ExtCore.Control

[<AutoOpen>]
module Model = 

    /// Type containing error message and string.
    type errorString = {Number : int; Meassage : string}

    /// Switch type, if On then an int value can be specified.
    type OnorOff = On | Off
    type Switch = On of int |Off

    /// Type containing all PicoHarp's propeties.
    type DevicePropeties = {
        DeviceIndex : int
        SerialNumber : StringBuilder
        Model : string
        PartNumber : string
        Version : string
        BaseResolution : string    
        Features : int}

    ///Possible units of time. 
    type Time = 
        | Time_pico of float
        | Time_nano of float
        | Time_micro of float
        | Time_milli of float
        | Time_second of float

    /// Type volts with two available units. 
    type Volts = 
        | Volts_mV of float
        | Volts_V of float

    /// Type containing required stringBuilder sizes.
    type StringPointers =
        | String_8 
        | String_16   
        | String_40 
        | String_16384 

    [<AutoOpen>]
    module SetupInputChannel = 
        
        ///Two possible input channels on PicoHarp.       
        type InputChannel = 
            | Channel_0
            | Channel_1  

        ///Both input channels contain a CFD, discriminator level and zero cross should be in millivolts, will have a unit converotr function.
        type CFD = {
            InputChannel : InputChannel;
            DiscriminatorLevel : Volts; 
            ZeroCross: Volts}

        ///Three possible modes, likley this programme will only deal with Histogramming.
        type Modes = 
            | Histogramming
            | T2
            | T3
    
    ///Contains types relating to propeties of the histogram.    
    [<AutoOpen>]
    module Histogram =
        
        ///Possible histogram bin widths.
        type Width =  
            | Width_4ps 
            | Width_8ps 
            | Width_16ps 
            | Width_32ps 
            | Width_64ps 
            | Width_128ps 
            | Width_256ps 
            | Width_512ps                                                                               
        
        ///Histogram requires 3 parameters, number of bins, bin width and aquisition time. Always 65536 bins. 
        type Histogram = {
            BinWidth : Width
            AcquisitionTime : Time
            Overflow : Switch}
        
        /// Channel 1 is used as a sync input for time resolved fluorescence with a pulsed exitation source.
        /// For correlation experiments ignore the sync settings. 
    [<AutoOpen>]
    module ChannelSync = 
        
        /// Type containing possible rate divisions.
        type Rate = 
            | RateDivider_1 
            | RateDivider_2
            | RateDivider_4
            | RateDivider_8
        
        /// Type containing possible sync channel settings (channel 1). 
        type Sync = {
            RateDivider : Rate
            Offset : Time}
        
            








































































































































































