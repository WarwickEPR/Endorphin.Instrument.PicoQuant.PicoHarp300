namespace Endorphin.Instrument.PicoHarp300

open Microsoft.FSharp.NativeInterop
open System.Runtime.InteropServices
open Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols
open System.Text
open Endorphin.Core
open Endorphin.Core.NationalInstruments
open Endorphin.Core.StringUtils
open ExtCore.Control

module internal Initialise = 
   
       
    /// Checks return value of the NativeApi function and converts to a success or gives an error message.
    let checkStatus = 
        function
        | Error.Ok -> succeed ()
        | Error.Error string -> fail string 
   
    /// Creates log for PicoHarp 300.
    let private log = log4net.LogManager.GetLogger "PicoHarp 300"
    
    let log = log.Info
    
    let logDevice (picoHarp : PicoHarp300) message =
        sprintf "[%A] %s" picoHarp message |> log

    let logQueryResult successMessageFunc failureMessageFunc input =
        match input with
        | Success value -> successMessageFunc value |> log.Debug
        | Failure error -> failureMessageFunc error |> log.Error
        

    let logDeviceQueryResult (picoScope : PicoHarp300) successMessageFunc failureMessageFunc =
        logQueryResult 
            (fun value -> sprintf "[%A] %s" picoScope (successMessageFunc value))
            (fun error -> sprintf "[%A] %s" picoScope (failureMessageFunc error))

    let logDeviceOpResult picoScope successMessage = logDeviceQueryResult picoScope (fun _ -> successMessage)
    
    /// The device index. 
    let private index (PicoHarp300 h) = h
    
    module initialise =     

        /// Opens the PicoHarp.
        let openDevice picoHarp300 = 
            let serial = StringBuilder (8)
            NativeApi.OpenDevice (index picoHarp300, serial) 
            |> checkStatus 
            |> logQueryResult 
                (sprintf "Successfully opened the PicoHarp: %A.")
                (sprintf "Failed to open the PicoHarp: %A.")  
        
        /// Calibrates the PicoHarp. 
        let calibrate picoHarp300 = 
            NativeApi.Calibrate (index picoHarp300)
            |> checkStatus
        
        /// Sets the PicoHarps mode.
        let initialiseMode picoHarp300 mode = 
            NativeApi.InitialiseMode (index picoHarp300 , mode)
            |> checkStatus

        /// Closes the PicoHarp.
        let closeDevice picoHarp300 =
            NativeApi.CloseDevice (index picoHarp300)
            |> checkStatus

    module Information = 
            
        /// Returns the PicoHarp's serial number.
        let GetSerialNumber picoHarp300 = 
            let serial = StringBuilder (8)
            NativeApi.GetSerialNumber (index picoHarp300 , serial)
            |> checkStatus
       
        /// Returens the PicoHarp's model number, part number ans version.
        let private hardwareInformation picoHarp300 = 
            let model   = StringBuilder (16)
            let partnum = StringBuilder (8)
            let vers    = StringBuilder (8)
            NativeApi.GetHardwareInfo (index picoHarp300, model, partnum, vers)
            |> checkStatus 
        
        /// Logs PicoHarp's model number. 
        let model picoHarp300 = 
            hardwareInformation picoHarp300 
        
        /// Logs PicoHarp's part number.
        let partNumber picoHarp300 = 
            hardwareInformation picoHarp300

        /// Logs PicoHarp's version.
        let version picoHarp300 = 
            hardwareInformation picoHarp300 
        
        /// Returns the histogram base resolution (the PicoHarp needs to be in histogram mode for function to return a success)
        let getBaseResolution picoHarp300 = 
            let mutable resolution : double = Unchecked.defaultof<_>
            NativeApi.GetResolution(index picoHarp300 , &resolution) 
            |> checkStatus

        /// Returns the features of the PicoHarp as a bit pattern.
        let getFeatures picoHarp300 = 
            let mutable features : int = Unchecked.defaultof<_>
            NativeApi.GetFeatures (index picoHarp300 , &features)
            |> checkStatus
        
    module SyncChannel = 
            
        /// Sets the rate divider of the sync channel.
        let setSyncDiv picoHarp300 (sync:SyncParameters) = 
            let divider = rateDividerEnum sync            
            NativeApi.SetSyncDiv (index picoHarp300, divider)
            
        /// Sets the offset of the sync/channel 0.
        let SetSyncOffset picoHarp300 (sync:SyncParameters) = 
            let delay = Quantities.durationNanoSeconds (sync.Delay)
            NativeApi.SetSyncDelay (index picoHarp300, int(delay))
            |> checkStatus
           
    module CFD = 
        
        /// Checks if the values for discriminator level and the zerocross are inside their respective ranges. 
        let private rangeCFD (cfd:CFD) = 
            let level = Quantities.voltageMilliVolts (cfd.DiscriminatorLevel) 
            let cross = Quantities.voltageMilliVolts (cfd.ZeroCross)
            if (level < 0.0<mV> || level < -800.0<mV>) then
                failwithf "Discriminator level outside of range: 0 - 800 mV."
            elif (cross < 0.0<mV> || cross > 20.0<mV>) then 
                failwithf "Zerocorss setting outside of range: 0 - 20 mV."
            else 0

        /// Takes type of CFD and converts elements into integers then passes to the function PH_SetInputCFD.
        /// This sets the discriminator level and zero cross for the CFD in channels 1 or 2. 
        let initialiseCFD picoHarp300 (cfd:CFD) = asyncChoice{
            let channel =   cfd.InputChannel
            let level   =   cfd.DiscriminatorLevel 
            let cross   =   cfd.ZeroCross
            if rangeCFD cfd <> 0 then 
                return 0
            else 
                let success = SetInputCFD (deviceIndex, channel, int(level), int(cross))
                return success}

               
        

