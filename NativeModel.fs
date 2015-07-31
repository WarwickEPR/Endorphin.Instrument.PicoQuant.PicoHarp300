namespace Endorphin.Instrument.PicoHarp300

[<AutoOpen>]
module internal NativeModel =

    type ModeEnum =
        | Histogram = 0

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