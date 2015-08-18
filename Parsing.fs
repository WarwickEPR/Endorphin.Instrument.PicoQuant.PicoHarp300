namespace Endorphin.Instrument.PicoHarp300

open Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols
open System.Text
open Endorphin.Core
open Endorphin.Core.NationalInstruments
open Endorphin.Core.String
open ExtCore.Control

[<AutoOpen>]
module internal Parsing =
 
    /// Converts type Mode into integers. 
    let modeEnum = function
        | Histogramming -> ModeEnum.Histogramming
        | T2 -> ModeEnum.T2

    /// Converts mode integer back into type Mode.
    let parseMode = function
        | ModeEnum.Histogramming -> Histogramming
        | ModeEnum.T2 -> T2
        | mode               -> failwithf "Unexpected mode enum value: %A." mode

    /// Converts type InputChannel into corresponding integers.
    let channelEnum = function
        | Channel0 -> ChannelEnum.Channel0
        | Channel1 -> ChannelEnum.Channel1

    /// Converts channel number back into type InputChannel. 
    let parseChannel = function
        | ChannelEnum.Channel0 -> Channel0
        | ChannelEnum.Channel1 -> Channel1
        | channel              -> failwithf "Unexpected channel enum value: %A." channel

    
    /// Converts the bin width into corresponding power of 2, e.g 8 -> 3.
    let resolutionEnum = function        
        | Resolution_4ps   -> ResolutionEnum.Resolution_4ps
        | Resolution_8ps   -> ResolutionEnum.Resolution_8ps
        | Resolution_16ps  -> ResolutionEnum.Resolution_16ps
        | Resolution_32ps  -> ResolutionEnum.Resolution_32ps
        | Resolution_64ps  -> ResolutionEnum.Resolution_64ps
        | Resolution_128ps -> ResolutionEnum.Resolution_128ps
        | Resolution_256ps -> ResolutionEnum.Resolution_256ps
        | Resolution_512ps -> ResolutionEnum.Resolution_512ps

    /// Converts bin width base number into type of Width.
    let parseResolution = function
        | ResolutionEnum.Resolution_4ps   -> Resolution_4ps
        | ResolutionEnum.Resolution_8ps   -> Resolution_8ps
        | ResolutionEnum.Resolution_16ps  -> Resolution_16ps
        | ResolutionEnum.Resolution_32ps  -> Resolution_32ps
        | ResolutionEnum.Resolution_64ps  -> Resolution_64ps
        | ResolutionEnum.Resolution_128ps -> Resolution_128ps
        | ResolutionEnum.Resolution_256ps -> Resolution_256ps
        | ResolutionEnum.Resolution_512ps -> Resolution_512ps
        | resolution                      -> failwithf "Unexpected channel enum value: %A." resolution

    /// Converts type Rate into corresponding number.
    let rateDividerEnum rate = 
        match rate.RateDivider with
        | RateDivider_1 -> RateDividerEnum.RateDividerEnum_1 
        | RateDivider_2 -> RateDividerEnum.RateDividerEnum_2
        | RateDivider_4 -> RateDividerEnum.RateDividerEnum_4
        | RateDivider_8 -> RateDividerEnum.RateDividerEnum_8

    /// Converts number into type Rate.
    let parseRateDivider = function
        | RateDividerEnum.RateDividerEnum_1 -> RateDivider_1
        | RateDividerEnum.RateDividerEnum_2 -> RateDivider_2
        | RateDividerEnum.RateDividerEnum_4 -> RateDivider_4
        | RateDividerEnum.RateDividerEnum_8 -> RateDivider_8
        | ratedivider                       -> failwithf "Unexpected rate division: %A." ratedivider
        


