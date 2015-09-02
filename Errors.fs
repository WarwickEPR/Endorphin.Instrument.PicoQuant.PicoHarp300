namespace Endorphin.Instrument.PicoHarp300

open Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols
open System.Text
open Endorphin.Core
open Endorphin.Core.NationalInstruments
open Endorphin.Core.String
open ExtCore.Control

[<AutoOpen>]
module Errors = 
    
    [<AutoOpen>]
    module Logging = 

        /// Checks return value of the NativeApi function and converts to a success or gives an error message.
        let internal checkStatus = 
            function
            | Error.Ok -> succeed ()
            | Error.Error string -> fail string 

        /// Check the value of set flags
        let internal checkFlags = 
            function
            | Flag.Ok -> succeed ()
            | Flag.Flag string -> fail string

        let internal checkStatusAndReturn value status = choice {
            do! checkStatus status
            return value }

        /// Creates log for PicoHarp 300.
        let log = log4net.LogManager.GetLogger "PicoHarp 300"

        /// Logs the PicoHarp.
        let internal logDevice (picoHarp : PicoHarp300) message =
            sprintf "[%A] %s" picoHarp message |> log.Info

        /// Logs a success or failure message based on result of function. 
        let internal logQueryResult successMessageFunc failureMessageFunc input =
            match input with
            | Success value -> successMessageFunc value |> log.Debug
            | Failure error -> failureMessageFunc error |> log.Error
            input 
            
        /// Logs a success or failure message based on result of function using the PicoHarp's index.
        let internal logDeviceQueryResult (picoHarp : PicoHarp300) successMessageFunc failureMessageFunc =
            logQueryResult 
                (fun value -> sprintf "[%A] %s" picoHarp (successMessageFunc value))
                (fun error -> sprintf "[%A] %s" picoHarp (failureMessageFunc error))

        let internal logDeviceOpResult picoHarp300 successMessage = logDeviceQueryResult picoHarp300 (fun _ -> successMessage)
        
        /// Extract the device index 
        let internal index (PicoHarp300 h) = h
    
    module CheckErrors = 


