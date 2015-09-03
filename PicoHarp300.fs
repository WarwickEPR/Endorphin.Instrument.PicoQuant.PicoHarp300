namespace Endorphin.Instrument.PicoHarp300

open Microsoft.FSharp.NativeInterop
open System.Runtime.InteropServices
open Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols
open System.Text
open Endorphin.Core
open ExtCore.Control

module PicoHarp300 = 
    
    /// Returns the device index of a PicoHarp using serial number.
    /// Stops recursion after deviceIndex > 7 as allowed values are 0..7. 
    let rec private getDeviceIndex (serial:string) (deviceIndex) = 
         if deviceIndex > 7 then failwithf "No PicoHarp found."
         else     
             let picoHarp300 = PicoHarp300 deviceIndex 
             let serialNumber = StringBuilder (8)
             let check = NativeApi.OpenDevice (index picoHarp300, serialNumber) 
                         |> Status.checkStatus
             if (serial = string(serialNumber)) then
                 deviceIndex
             else 
                 getDeviceIndex serial (deviceIndex + 1)   
        
    let private indexPico (serial:string) = getDeviceIndex serial 0
    ///  Gets device index, starts with device index 0.
    let picoHarp (serial:string) = PicoHarp300 (indexPico serial)
        
    module internal Information = 

        /// Returns the PicoHarp's serial number.
        let getSerialNumber picoHarp300 = 
            let serial = StringBuilder (8)
            logDevice picoHarp300 "Retrieving device serial number."
            NativeApi.GetSerialNumber (index picoHarp300 , serial)
            |> Status.checkStatus
            |> logDeviceOpResult picoHarp300
                ("Successfully retrieved the device (%A) serial number.")
                (sprintf "Failed to retrieve the device serial number: %A.")
            |> AsyncChoice.liftChoice

        /// Returens the PicoHarp's model number, part number ans version.
        let gethardwareInformation picoHarp300 = 
            let model   = StringBuilder (16)
            let partnum = StringBuilder (8)
            let vers    = StringBuilder (8)
            logDevice picoHarp300 "Retrieving device hardware information: model number, part number, version."
            NativeApi.GetHardwareInfo (index picoHarp300, model, partnum, vers)
            |> Status.checkStatus 
            |> logDeviceOpResult picoHarp300
                ("Successfully retrieved the device (%A) hardware information." )
                (sprintf "Failed to retrieve the device hardware information: %A")
            |> AsyncChoice.liftChoice

        /// Logs PicoHarp's model number. 
        let model picoHarp300 = 
            gethardwareInformation picoHarp300 
        
        /// Logs PicoHarp's part number.
        let partNumber picoHarp300 = 
            gethardwareInformation picoHarp300

        /// Logs PicoHarp's version.
        let version picoHarp300 = 
            gethardwareInformation picoHarp300 
        
        /// Returns the histogram base resolution (the PicoHarp needs to be in histogram mode for function to return a success)
        let getBaseResolution picoHarp300 = 
            let mutable resolution : double = Unchecked.defaultof<_>
            logDevice picoHarp300 "Retrieving device base resolution."
            NativeApi.GetResolution(index picoHarp300 , &resolution) 
            |> Status.checkStatus
            |> logDeviceOpResult picoHarp300
                ("Successfully retrieved the device base resolution.")
                (sprintf "Failed to retrieve the device base resolution: %A")
            |> AsyncChoice.liftChoice
    
    module Initialise =     

        /// Opens the PicoHarp.
        let private openDevice picoHarp300 = 
            let serial = StringBuilder (8)
            logDevice picoHarp300 "Opening device."
            NativeApi.OpenDevice (index picoHarp300, serial) 
            |> Status.checkStatus 
            |> logDeviceOpResult picoHarp300 
                ("Successfully opened the PicoHarp.")
                (sprintf "Failed to open the PicoHarp: %A.")
            |> AsyncChoice.liftChoice

        /// Calibrates the PicoHarp. 
        let private calibrate picoHarp300 = 
            logDevice picoHarp300 "Calibrating device."
            NativeApi.Calibrate (index picoHarp300)
            |> Status.checkStatus
            |> logDeviceOpResult picoHarp300  
                ("Successfully calibrated the device.")
                (sprintf "Failed to calibrate the device: %A.")
            |> AsyncChoice.liftChoice

        /// Sets the PicoHarps mode.
        let initialiseMode picoHarp300 (mode:Mode) = 
            let modeCode = modeEnum mode
            logDevice picoHarp300 "Setting device mode."
            NativeApi.InitialiseMode (index picoHarp300 , modeCode)
            |> Status.checkStatus
            |> logDeviceOpResult picoHarp300
                ("Successfully set the device mode.")
                (sprintf "Failed to set the device mode: %A.")
            |> AsyncChoice.liftChoice

        /// Open connection to device and perform calibration
        let initialise serial = asyncChoice {
            let device = picoHarp serial
            do! openDevice device 
            do! calibrate device
            return device
            }

        /// Closes the PicoHarp.
        let closeDevice picoHarp300 =
            logDevice picoHarp300 "Closing device."
            NativeApi.CloseDevice (index picoHarp300)
            |> Status.checkStatus
            |> logDeviceOpResult picoHarp300
                ("Successfully closed the PicoHarp.")
                (sprintf "Failed to close the PicoHarp: %A.")  
            |> AsyncChoice.liftChoice 
    
    /// measurement functions which are useful in both histogramming and TTTR mode
    module Query =
        /// Returns time period over which experiment was running. 
        let getMeasurementTime picoHarp300 =
            let mutable elasped : double = Unchecked.defaultof<_>
            logDevice picoHarp300 "Retrieving time passed since the start of histogram measurements."
            NativeApi.GetElapsedMeasTime (index picoHarp300, &elasped) 
            |> Status.checkStatus
            |> logQueryResult 
                (sprintf "Successfully retrieved measurement time: %A")
                (sprintf "Failed to retrieve measurement time: %A") 
            |> AsyncChoice.liftChoice

        /// If 0 is returned acquisition time is still running, >0 then acquisition time has finished. 
        let getCTCStatus picoHarp300 = 
            let mutable ctcStatus : int = Unchecked.defaultof<_>
            logDevice picoHarp300 "Checking CTC status" 
            NativeApi.CTCStatus (index picoHarp300, &ctcStatus)   
            |> Status.checkStatus
            |> logQueryResult 
                (sprintf "Successfully retrieved CTC status: %A")
                (sprintf "Failed to retrieve CTC status: %A") 
            |> AsyncChoice.liftChoice
        
        /// Writes channel count rate to a mutable int.  
        let getCountRate picoHarp300 (channel:int) =
            let mutable rate : int = Unchecked.defaultof<_>
            logDevice picoHarp300 "Retrieving channel count rate."
            GetCountRate (index picoHarp300, channel, &rate)
            |> Status.checkStatusAndReturn (rate)
            |> logDeviceOpResult picoHarp300
                ("Successfully retrieved channel count rate.")
                (sprintf "Failed to retrieve channel count rate: %A")
            |> AsyncChoice.liftChoice
    
    module internal Acquisition = 
        /// Starts a measurement
        let start picoHarp300 duration = 
            let acquisitionTime = Quantities.durationMilliSeconds duration
            logDevice picoHarp300 "Setting acquisition time and starting measurement."
            NativeApi.StartMeasurement (index picoHarp300 , int (acquisitionTime)) 
            |> Status.checkStatus
            |> logDeviceOpResult picoHarp300
                ("Successfully set acquisition time and started TTTR measurement") 
                (sprintf "Failed to start: %A.")
            |> AsyncChoice.liftChoice

        /// Read TTTR FIFO buffer
        let readFifoBuffer picoHarp300 (streamingBuffer : StreamingBuffer) = 
            let mutable counts = Unchecked.defaultof<_>
            NativeApi.ReadFiFo(index picoHarp300, streamingBuffer.Buffer, TTTRMaxEvents, &counts)
            |> Status.checkStatusAndReturn (counts)
            |> AsyncChoice.liftChoice
    
        let checkMeasurementFinished picoHarp300 =
            let mutable result = 0
            logDevice picoHarp300 "Checking whether acquisition has finished."
            NativeApi.CTCStatus (index picoHarp300, &result)
            |> Status.checkStatusAndReturn (result <> 0)
            |> logQueryResult
                (sprintf "Successfully checked acquisition finished: %A.")
                (sprintf "Failed to check acquisition status due to error: %A.")
            |> AsyncChoice.liftChoice

        /// Stops a measurement 
        let stop picoHarp300 = 
            logDevice picoHarp300 "Ceasing measurement."
            NativeApi.StopMeasurement (index picoHarp300)
            |> Status.checkStatus
            |> logDeviceOpResult picoHarp300
                ("Successfully ended measurement.")
                (sprintf "Failed to end measurement: %A.")
            |> AsyncChoice.liftChoice
                    
    module SyncChannel = 
            
        /// Sets the rate divider of the sync channel.
        let setSyncDiv picoHarp300 (sync:SyncParameters) = 
            let divider = rateDividerEnum sync       
            logDevice picoHarp300 "Setting the sync channel divider."     
            NativeApi.SetSyncDiv (index picoHarp300, divider)
            |> Status.checkStatus 
            |> logDeviceOpResult picoHarp300
                ("Successfully set the device sync channel divider.")
                (sprintf "Failed to set the device sync channel divider: %A")
            |> AsyncChoice.liftChoice

        /// Sets the offset of the sync/channel 0.
        let SetSyncOffset picoHarp300 (sync:SyncParameters) = 
            let delay = Quantities.durationNanoSeconds (sync.Delay)
            logDevice picoHarp300 "Setting the sync channel offset."
            NativeApi.SetSyncDelay (index picoHarp300, int(delay))
            |> Status.checkStatus
            |> logDeviceOpResult picoHarp300
                (sprintf "Successfully set the device sync offset.")
                (sprintf "Failed to set the device sync offset: %A")
            |> AsyncChoice.liftChoice

    module CFD = 
        
        /// Checks if the values for discriminator level and the zerocross are inside their respective ranges. 
        let private rangeCFD (cfd:CFD) = 
            let level = Quantities.voltageMillivolts (cfd.DiscriminatorLevel) 
            let cross = Quantities.voltageMillivolts (cfd.ZeroCross)
            if (level < 0.0<mV> || level < -800.0<mV>) then
                1
            elif (cross < 0.0<mV> || cross > 20.0<mV>) then 
                2
            else 
                0

        /// Logs CFD errors with log level warn.
        let private rangeErrorCodes picoHarp300 code = 
            if code = 1 then
                log.Warn ("Failed to initialise the CFD, discriminator level is outside of the range -800mV to 0mV.")
            else
                log.Warn ("Failed to initialise the CFD, zerocross is outside to the range 20mV to 0mV")

        /// Takes type of CFD and converts elements into integers then passes to the function PH_SetInputCFD.
        /// This sets the discriminator level and zero cross for the CFD in channels 1 or 2. 
        let initialiseCFD picoHarp300 (cfd:CFD) = 
            let channel =   channelEnum (cfd.InputChannel)
            let level   =   Quantities.voltageMillivolts (cfd.DiscriminatorLevel) 
            let cross   =   Quantities.voltageMillivolts (cfd.ZeroCross)  
            logDevice picoHarp300 "Initialising the channel's CFD."
            NativeApi.SetInputCFD (index picoHarp300, channel, int(level), int(cross))
            |> Status.checkStatus 
            |> logDeviceOpResult picoHarp300
                ("Successfully initialised channel's CFD.")
                (sprintf "Failed to initialis channel's CFD: %A")
            |> AsyncChoice.liftChoice
