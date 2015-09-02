namespace Endorphin.Instrument.PicoHarp300

open Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols
open System.Text
open Endorphin.Core
open Endorphin.Core.NationalInstruments
open Endorphin.Core.String
open ExtCore.Control

[<AutoOpen>]
module internal Status = 

    /// Checks return value of the NativeApi function and converts to a success or gives an error message.
    let internal checkStatus = function
        | Error.Ok -> succeed ()
        | Error.Error string -> fail string 

    /// Check the value of set flags
    let internal checkFlags = function
        | Flag.Ok -> succeed ()
        | Flag.Flag string -> fail string

    let internal checkStatusAndReturn value status = choice {
        do! checkStatus status
        return value }

    [<AutoOpen>]
    module internal Logging = 

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
   
    [<AutoOpen>]
    module internal CheckErrros = 
       
        /// Returns Ok or Error with the error string. 
        let stringtoStatus = function
            | "OK. No Error." -> Ok
            | str -> Error str
    
        /// Converts string from status.
        let statustoString = function
            | Ok        -> "Ok. No Error."
            | Error str -> str

        /// Checks return value of the NativeApi function and converts to a success or gives an error message.
        let checkStatus = function
            | Ok            -> succeed ()
            | Error message -> fail message

        /// Handles errors, if no errors returns function values else fails. 
        let check piezojena (workflow:Async<'T>) = asyncChoice {
            let stage = id piezojena 
            let! workflowResult = workflow |> AsyncChoice.liftAsync
            let mutable error : string = Unchecked.defaultof<_>
            stage.GetCommandError (&error)
            logDevice piezojena (error) 
            let statusError = stringtoStatus error
            do! if statusError = Ok then
                    Ok |> checkStatus
                else 
                    Error error |> checkStatus
            return workflowResult }
    
        /// Checks the errors for multiple async workflows.      
        let checkMulti piezojena (workflowArray : Async<'T>[]) =   
            let stage = id piezojena 
            // Runs workflow and returns a status.
            let statusCheck (workflow:Async<'T>) = asyncChoice {
                let! result = workflow |> AsyncChoice.liftAsync 
                let mutable error : string = Unchecked.defaultof<_> 
                stage.GetCommandError (&error)
                printfn "%s" error
                logDevice piezojena (error) 
                return stringtoStatus error 
                }
        
            // Workflow takes an async workflow array and returns an asyncChoice. 
            let rec results index (workflowArray : Async<'T> []) = asyncChoice{
                  let length = Array.length workflowArray - 1 
                  if index < length || index =  length then 
                      let workflow = Array.get workflowArray index
                      let! status  = statusCheck workflow  
                      if status = Ok then
                        return! results (index + 1) workflowArray    
                      else 
                        return! (fail "Failed to execute all workflows.")
                  else 
                    return () 
            }
            results 0 workflowArray 


