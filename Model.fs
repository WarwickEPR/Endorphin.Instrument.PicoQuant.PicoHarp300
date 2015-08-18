namespace Endorphin.Instrument.PicoHarp300

open Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols
open System.Text
open Endorphin.Core
open Endorphin.Core.NationalInstruments
open Endorphin.Core.String
open ExtCore.Control

[<AutoOpen>]
module Model = 

    type PicoHarp300 = internal PicoHarp300 of index : int

    /// Type containing all PicoHarp's propeties.
    type DeviceParameters =
        { SerialNumber   : string
          Model          : string
          PartNumber     : string
          Version        : string
          BaseResolution : float<ps> }

    ///Possible units of time. 
    type Duration = 
        | Duration_ps of float<ps>
        | Duration_ns of float<ns>
        | Duration_us of float<us>
        | Duration_ms of float<ms>
        | Duration_s of float<s>

    /// Type volts with two available units. 
    type Voltage = 
        | Voltage_mV of float<mV>
        | Voltage_V of float<V>

    [<AutoOpen>]
    module SetupInputChannel = 
        
        ///Two possible input channels on PicoHarp.       
        type InputChannel = 
            | Channel0
            | Channel1 
            | Both 

        ///Both input channels contain a CFD, discriminator level and zero cross should be in millivolts, will have a unit converotr function.
        type CFD =
            { InputChannel       : InputChannel
              DiscriminatorLevel : Voltage 
              ZeroCross          : Voltage }
            
        /// Only two of the three modes are implemented here
        type Mode = 
            | Histogramming
            | T2
        
        /// Sets offset, only possible in modes Histogram and T3.
        type Offset = Offset of int option 
   
    /// Settings which apply only to free-running TTTR mode measurements
    [<AutoOpen>]
    module SetupT2Measurements = 
            
        /// Possible marker channels which can be recorded in the TTTR stream
        type MarkerChannel = 
            | MarkerChannel_0
            | MarkerChannel_1
            | MarkerChannel_2
            | MarkerChannel_3

        [<Literal>]
        let TTTRMaxEvents = 131072
    ///Contains types relating to propeties of the histogram.    
        
    ///Possible histogram bin widths.
    type Resolution =  
        | Resolution_4ps 
        | Resolution_8ps 
        | Resolution_16ps 
        | Resolution_32ps 
        | Resolution_64ps 
        | Resolution_128ps 
        | Resolution_256ps 
        | Resolution_512ps                                                                               
    
    ///Histogram requires 3 parameters, number of bins, bin width and aquisition time. Always 65536 bins. 
    type HistogramParameters = 
        { Resolution      : Resolution
          AcquisitionTime : Duration
          Overflow        : int option 
          MarkerChannel   : MarkerChannel option}
    
    /// Channel 1 is used as a sync input for time resolved fluorescence with a pulsed exitation source.
    /// For correlation experiments ignore the sync settings. 
    
    [<AutoOpen>]
    module ChannelSync = 
        
        /// Type containing possible rate divisions.
        type RateDivider = 
            | RateDivider_1 
            | RateDivider_2
            | RateDivider_4
            | RateDivider_8
        
        /// Type containing possible sync channel settings (channel 1). 
        type SyncParameters =
            { RateDivider : RateDivider
              Delay       : Duration }