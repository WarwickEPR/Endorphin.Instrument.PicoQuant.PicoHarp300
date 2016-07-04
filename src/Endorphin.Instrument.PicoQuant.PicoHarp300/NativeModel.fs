// Copyright (c) University of Warwick. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE.txt in the project root for license information.

namespace Endorphin.Instrument.PicoQuant.PicoHarp300

[<AutoOpen>]
module internal NativeModel =

    type ModeEnum =
        | Histogramming = 0
        | T2 = 2

    type ChannelEnum =
        | Channel0 = 0
        | Channel1 = 1

    type ResolutionEnum =
        | Resolution_4ps   = 0
        | Resolution_8ps   = 1
        | Resolution_16ps  = 2
        | Resolution_32ps  = 3
        | Resolution_64ps  = 4
        | Resolution_128ps = 5
        | Resolution_256ps = 6
        | Resolution_512ps = 7

    type RateDividerEnum =
        | RateDividerEnum_1 = 1
        | RateDividerEnum_2 = 2
        | RateDividerEnum_4 = 4
        | RateDividerEnum_8 = 8