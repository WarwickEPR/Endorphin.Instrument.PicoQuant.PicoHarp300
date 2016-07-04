// Copyright (c) University of Warwick. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE.txt in the project root for license information.

namespace Endorphin.Instrument.PicoQuant.PicoHarp300

open Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols
open System.Text
open Endorphin.Core

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
    type T1HistogramParameters =
        { Resolution      : Resolution
          AcquisitionTime : Duration
          Overflow        : int option }


    /// Settings which apply only to free-running TTTR mode measurements
    [<AutoOpen>]
    module TTTRConfiguration =

        /// Maximum number of events that the PicoHarp can return during any one USB transfer
        [<Literal>]
        let internal TTTRMaxEvents = 131072

        [<Literal>]
        let internal TTTROverflowTime = 210698240UL

        type internal MarkerChannelEnum =
            | Marker0  = 1u
            | Marker1  = 2u
            | Marker2  = 4u
            | Marker3  = 8u

        type MarkerChannel =
            | Marker0
            | Marker1
            | Marker2
            | Marker3

        /// Parameters for the computed TTTR histogram. The marker channel indicates which marker channel will be used to separate experimental shots from one another.
        type HistogramParameters =
            internal { Resolution        : float<ns>
                       TotalLength       : float<ns>
                       NumberOfBins      : int
                       MarkerChannel     : MarkerChannel }

        /// Buffer to hold the results of each streaming event
        type StreamingBuffer =
            internal { Buffer    : uint32[] }

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