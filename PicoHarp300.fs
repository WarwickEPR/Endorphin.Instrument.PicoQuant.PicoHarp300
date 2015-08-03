namespace Endorphin.Instrument.PicoHarp300

open Microsoft.FSharp.NativeInterop
open System.Runtime.InteropServices
open Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols
open System.Text
open Endorphin.Core
open ExtCore.Control

module internal PicoHarp = 
   
    /// Checks return value of the NativeApi function and converts to a success or gives an error message.
    let checkStatus = 
        function
        | Error.Ok -> succeed ()
        | Error.Error string -> fail string 
   
    /// Creates log for PicoHarp 300.
    let private log = log4net.LogManager.GetLogger "PicoHarp 300"
    
    /// Logs the PicoHarp.
    let logDevice (picoHarp : PicoHarp300) message =
        sprintf "[%A] %s" picoHarp message |> log.Info

    /// Logs a success or failure message based on result of function. 
    let logQueryResult successMessageFunc failureMessageFunc input =
        match input with
        | Success value -> successMessageFunc value |> log.Debug
        | Failure error -> failureMessageFunc error |> log.Error
        
    /// Logs a success or failure message based on result of function using the PicoHarp's index.
    let logDeviceQueryResult (picoHarp : PicoHarp300) successMessageFunc failureMessageFunc =
        logQueryResult 
            (fun value -> sprintf "[%A] %s" picoHarp (successMessageFunc value))
            (fun error -> sprintf "[%A] %s" picoHarp (failureMessageFunc error))

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
            |> logDeviceQueryResult picoHarp300 
                (sprintf "Successfully calibrated the device, %A: %A." picoHarp300)
                (sprintf "Failed to calibrate the device, %A: %A." picoHarp300)

        /// Sets the PicoHarps mode.
        let initialiseMode picoHarp300 mode = 
            NativeApi.InitialiseMode (index picoHarp300 , mode)
            |> checkStatus
            |> logDeviceQueryResult picoHarp300
                (sprintf "Successfully set the device (%A) mode to %A: %A." picoHarp300 mode)
                (sprintf "Successfully set the device (%A) mode to %A: %A." picoHarp300 mode)
             

        /// Closes the PicoHarp.
        let closeDevice picoHarp300 =
            NativeApi.CloseDevice (index picoHarp300)
            |> checkStatus
            |> logQueryResult 
                (sprintf "Successfully closed the PicoHarp: %A.")
                (sprintf "Failed to close the PicoHarp: %A.")  
         

    module Information = 
            
        /// Returns the PicoHarp's serial number.
        let GetSerialNumber picoHarp300 = 
            let serial = StringBuilder (8)
            NativeApi.GetSerialNumber (index picoHarp300 , serial)
            |> checkStatus
            |> logDeviceQueryResult picoHarp300
                (sprintf "Successfully retrieved the device (%A) serial number, %A: %A." picoHarp300 serial)
                (sprintf "Failed to retrieve the device (%A) serial number, %A: %A." picoHarp300 serial)
       
        /// Returens the PicoHarp's model number, part number ans version.
        let private hardwareInformation picoHarp300 = 
            let model   = StringBuilder (16)
            let partnum = StringBuilder (8)
            let vers    = StringBuilder (8)
            NativeApi.GetHardwareInfo (index picoHarp300, model, partnum, vers)
            |> checkStatus 
            |> logDeviceQueryResult picoHarp300
                (sprintf "Successfully retrieved the device (%A) hardware information: %A" picoHarp300)
                (sprintf "Failed to retrieve the device (%A) hardware information: %A" picoHarp300)

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
            |> logDeviceQueryResult picoHarp300
                (sprintf "Successfully retrieved the device (%A) base resolution, %A: %A" picoHarp300 resolution)
                (sprintf "Failed to retrieve the device (%A) base resolution, %A: %A" picoHarp300 resolution)

        /// Returns the features of the PicoHarp as a bit pattern.
        /// let getFeatures picoHarp300 = 
        ///     let mutable features : int = Unchecked.defaultof<_>
        ///     NativeApi.GetFeatures (index picoHarp300 , &features)
        ///     |> checkStatus
        
    module SyncChannel = 
            
        /// Sets the rate divider of the sync channel.
        let setSyncDiv picoHarp300 (sync:SyncParameters) = 
            let divider = rateDividerEnum sync            
            NativeApi.SetSyncDiv (index picoHarp300, divider)
            |> checkStatus 
            |> logDeviceQueryResult picoHarp300
                (sprintf "Successfully set the device (%A) sync channel divider, %A: %A" picoHarp300 divider)
                (sprintf "Failed to set the device (%A) sync channel divider %A: %A" picoHarp300 divider)
            
        /// Sets the offset of the sync/channel 0.
        let SetSyncOffset picoHarp300 (sync:SyncParameters) = 
            let delay = Quantities.durationNanoSeconds (sync.Delay)
            NativeApi.SetSyncDelay (index picoHarp300, int(delay))
            |> checkStatus
            |> logDeviceQueryResult picoHarp300
                (sprintf "Successfully set the device (%A) sync offset, %A: %A" picoHarp300 (int(delay)) )
                (sprintf "Failed to set the device (%A) sync offset, %A: %A" picoHarp300 (int(delay)) )

    module CFD = 
        
        /// Checks if the values for discriminator level and the zerocross are inside their respective ranges. 
        let private rangeCFD (cfd:CFD) = 
            let level = Quantities.voltageMilliVolts (cfd.DiscriminatorLevel) 
            let cross = Quantities.voltageMilliVolts (cfd.ZeroCross)
            if (level < 0.0<mV> || level < -800.0<mV>) then
                1
            elif (cross < 0.0<mV> || cross > 20.0<mV>) then 
                2
            else 
                0

        /// Logs CFD errors with log level warn.
        let private rangeErrorCodes picoHarp300 code = 
            if code = 1 then
                log.Warn (sprintf "Failed to initialise the CFD, discriminator level is outside of the range -800mV to 0mV.")
            else
                log.Warn (sprintf "Failed to initialise the CFD, zerocross is outside to the range 20mV to 0mV")

        /// Takes type of CFD and converts elements into integers then passes to the function PH_SetInputCFD.
        /// This sets the discriminator level and zero cross for the CFD in channels 1 or 2. 
        let initialiseCFD picoHarp300 (cfd:CFD) = 
            let channel =   channelEnum (cfd.InputChannel)
            let level   =   Quantities.voltageMilliVolts (cfd.DiscriminatorLevel) 
            let cross   =   Quantities.voltageMilliVolts (cfd.ZeroCross)
            if rangeCFD cfd <> 0 then 
                rangeCFD cfd
                |> rangeErrorCodes
            else 
                NativeApi.SetInputCFD (index picoHarp300, channel, int(level), int(cross))
                |> checkStatus 
                |> logDeviceQueryResult picoHarp300 
                    (sprintf "Successfully initialised channel %A's CFD for device %A : %A" channel picoHarp300 )
                    (sprintf "Failed to initialis channel %A's CFD for device %A: %A" channel picoHarp300 )

               
        

