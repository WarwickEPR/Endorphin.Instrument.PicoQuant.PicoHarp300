// Copyright (c) University of Warwick. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE.txt in the project root for license information.

namespace Endorphin.Instrument.PicoQuant.PicoHarp300
open System

[<AutoOpen>]
module internal Flag =
    /// Hardware flags
    [<Flags>]
    type FlagCode =
        | NoFlag                        = 0x0000
        | FifoFull                      = 0x0003
        | Overflow                      = 0x0040
        | SystemError                   = 0x0100

    let flagMessage = function
        | FlagCode.NoFlag               -> "No flags set"
        | FlagCode.FifoFull             -> "FIFO buffer full"
        | FlagCode.Overflow             -> "Histogram overflow flag set"
        | FlagCode.SystemError          -> "Hardware problem"
        | flagCode                      -> failwithf "Multiple flags set. Flag value: %A." flagCode

    let (|Ok|Flag|) = function
        | FlagCode.NoFlag               -> Ok
        | flagCode                      -> Flag (flagMessage flagCode)