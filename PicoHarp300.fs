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
            let workflow = asyncChoice{        
                let serial = StringBuilder (8)
                logDevice picoHarp300 "Retrieving device serial number."
                do! NativeApi.GetSerialNumber (index picoHarp300 , serial) |> Status.Logging.logError picoHarp300
                return serial |> string}
            workflow 
           
        /// Returens the PicoHarp's model number, part number ans version.
        let gethardwareInformation picoHarp300 = 
            let workflow = asyncChoice{ 
                let model   = StringBuilder (16)
                let partnum = StringBuilder (8)
                let vers    = StringBuilder (8)
                logDevice picoHarp300 "Retrieving device hardware information: model number, part number, version."
                do! NativeApi.GetHardwareInfo (index picoHarp300, model, partnum, vers) |> Status.Logging.logError picoHarp300
                return (string(model), string(partnum), string(vers)}
            workflow 
        
        /// Returns the histogram base resolution (the PicoHarp needs to be in histogram mode for function to return a success)
        let getBaseResolution picoHarp300 = 
            let workflow = asyncChoice{        
                let mutable resolution : double = Unchecked.defaultof<_>
                logDevice picoHarp300 "Retrieving device base resolution."
                do! NativeApi.GetResolution(index picoHarp300 , &resolution) |> Status.Logging.logError picoHarp300
                return resolution} 
            workflow 
                     
    module Initialise =     

        /// Opens the PicoHarp.
        let private openDevice picoHarp300 = 
            let workflow = asyncChoice{
                let serial = StringBuilder (8)
                logDevice picoHarp300 "Opening device."
                do! NativeApi.OpenDevice (index picoHarp300, serial) |> Status.Logging.logError picoHarp300
                } 
            workflow 

        /// Calibrates the PicoHarp. 
        let private calibrate picoHarp300 = 
            let workflow = asyncChoice{
                logDevice picoHarp300 "Calibrating device."
                do! NativeApi.Calibrate (index picoHarp300) |> Status.Logging.logError picoHarp300
                }
            workflow 

        /// Sets the PicoHarps mode.
        let initialiseMode picoHarp300 (mode:Mode) = 
            let workflow = asyncChoice{        
                let modeCode = modeEnum mode
                logDevice picoHarp300 "Setting device mode."
                do! NativeApi.InitialiseMode (index picoHarp300 , modeCode) |> Status.Logging.logError picoHarp300
                }
            workflow 

        /// Open connection to device and perform calibration
        let initialise serial = asyncChoice {
            let device = picoHarp serial
            do! openDevice device 
            do! calibrate device
            return device
            }

        /// Closes the PicoHarp.
        let closeDevice picoHarp300 =
            let workflow = asyncChoice{
                logDevice picoHarp300 "Closing device."
                do! NativeApi.CloseDevice (index picoHarp300) |> Status.Logging.logError picoHarp300
                }
            workflow 
        
    /// measurement functions which are useful in both histogramming and TTTR mode
    module Query =
        /// Returns time period over which experiment was running. 
        let getMeasurementTime picoHarp300 =
            let workflow = asyncChoice{        
                let mutable elasped : double = Unchecked.defaultof<_>
                logDevice picoHarp300 "Retrieving time passed since the start of histogram measurements."
                do! NativeApi.GetElapsedMeasTime (index picoHarp300, &elasped) |> Status.Logging.logError picoHarp300 
                }
            workflow     

        /// If 0 is returned acquisition time is still running, >0 then acquisition time has finished. 
        let getCTCStatus picoHarp300 = 
            let workflow = asyncChoice{        
                let mutable ctcStatus : int = Unchecked.defaultof<_>
                logDevice picoHarp300 "Checking CTC status" 
                do! NativeApi.CTCStatus (index picoHarp300, &ctcStatus) |> Status.Logging.logError picoHarp300
                }
            workflow   
        
        /// Writes channel count rate to a mutable int.  
        let getCountRate picoHarp300 (channel:int) =
            let workflow = asyncChoice{        
                let mutable rate : int = Unchecked.defaultof<_>
                logDevice picoHarp300 "Retrieving channel count rate."
                do! NativeApi.GetCountRate (index picoHarp300, channel, &rate) |> Status.Logging.logError picoHarp300
                return rate}
            workflow 
            
    module internal Acquisition = 
        /// Starts a measurement
        let start picoHarp300 duration = 
            let workflow = asyncChoice{        
                let acquisitionTime = Quantities.durationMilliSeconds duration
                logDevice picoHarp300 "Setting acquisition time and starting measurement."
                do! NativeApi.StartMeasurement (index picoHarp300 , int (acquisitionTime)) |> Status.Logging.logError picoHarp300
                }
            workflow 
          

        /// Read TTTR FIFO buffer
        let readFifoBuffer picoHarp300 (streamingBuffer : StreamingBuffer) = 
            let mutable counts = Unchecked.defaultof<_>
            NativeApi.ReadFiFo(index picoHarp300, streamingBuffer.Buffer, TTTRMaxEvents, &counts)
            |> Status.checkStatusAndReturn (counts)
            |> AsyncChoice.liftChoice
    
        let checkMeasurementFinished picoHarp300 =
            let workflow = asyncChoice{        
                let mutable result = 0
                logDevice picoHarp300 "Checking whether acquisition has finished."
                do! NativeApi.CTCStatus (index picoHarp300, &result) |> Status.Logging.logError picoHarp300
                return result}
            workflow

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
