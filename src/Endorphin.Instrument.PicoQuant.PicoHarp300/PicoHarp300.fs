// Copyright (c) University of Warwick. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE.txt in the project root for license information.

namespace Endorphin.Instrument.PicoQuant.PicoHarp300

open Microsoft.FSharp.NativeInterop
open System.Runtime.InteropServices
open Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols
open System
open System.Text
open Endorphin.Core

module internal Async =
    let ReturnFromThreadPool x = async {
        do! Async.SwitchToThreadPool ()
        return x }

module PicoHarp =
    /// Checks return value of the NativeApi function and converts to a success or gives an error message.
    let internal checkStatus =
        function
        | Error.Ok -> Choice.succeed ()
        | Error.Error str -> Choice.fail (Exception str)

    /// Check the value of set flags
    let internal checkFlags =
        function
        | Flag.Ok -> Choice.succeed ()
        | Flag.Flag string -> Choice.fail string

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


    module Initialise =

       /// Returns the device index of a PicoHarp using serial number.
       /// Stops recursion after deviceIndex > 7 as allowed values are 0..7.
        let rec private getDeviceIndex (serial:string) (deviceIndex) =
            if deviceIndex > 7 then failwithf "No PicoHarp found."
            else
                let picoHarp300 = PicoHarp300 deviceIndex
                let serialNumber = StringBuilder (8)
                let check = NativeApi.OpenDevice (index picoHarp300, serialNumber)
                            |> checkStatus
                if (serial = string(serialNumber)) then
                    deviceIndex
                else
                    getDeviceIndex serial (deviceIndex + 1)

        let private indexPico (serial:string) = getDeviceIndex serial 0
        ///  Gets device index, starts with device index 0.
        let picoHarp (serial:string) = PicoHarp300 (indexPico serial)

        /// Opens the PicoHarp.
        let private openDevice picoHarp300 =
            let serial = StringBuilder (8)
            logDevice picoHarp300 "Opening device."
            NativeApi.OpenDevice (index picoHarp300, serial)
            |> checkStatus
            |> logDeviceOpResult picoHarp300
                ("Successfully opened the PicoHarp.")
                (sprintf "Failed to open the PicoHarp: %A.")
            |> Choice.bindOrRaise
            |> Async.ReturnFromThreadPool


        /// Calibrates the PicoHarp.
        let private calibrate picoHarp300 =
            logDevice picoHarp300 "Calibrating device."
            NativeApi.Calibrate (index picoHarp300)
            |> checkStatus
            |> logDeviceOpResult picoHarp300
                ("Successfully calibrated the device.")
                (sprintf "Failed to calibrate the device: %A.")
            |> Choice.bindOrRaise
            |> Async.ReturnFromThreadPool

        /// Sets the PicoHarps mode.
        let setMode picoHarp300 (mode:Mode) =
            let modeCode = modeEnum mode
            logDevice picoHarp300 "Setting device mode."
            NativeApi.InitialiseMode (index picoHarp300 , modeCode)
            |> checkStatus
            |> logDeviceOpResult picoHarp300
                ("Successfully set the device mode.")
                (sprintf "Failed to set the device mode: %A.")
            |> Choice.bindOrRaise
            |> Async.ReturnFromThreadPool

        /// Open connection to device and perform calibration
        let initialise serial mode  = async {
            let picoharp = picoHarp serial
            do! openDevice picoharp
            do! setMode picoharp mode
            do! calibrate picoharp
            return picoharp }

        /// Re-open connection to device and perform calibration
        let reOpen picoharp mode  = async {
            do! openDevice picoharp
            do! setMode picoharp mode
            do! calibrate picoharp }

        /// Closes the PicoHarp.
        let closeDevice picoHarp300 =
            logDevice picoHarp300 "Closing device."
            NativeApi.CloseDevice (index picoHarp300)
            |> checkStatus
            |> logDeviceOpResult picoHarp300
                ("Successfully closed the PicoHarp.")
                (sprintf "Failed to close the PicoHarp: %A.")
            |> Choice.bindOrRaise
            |> Async.ReturnFromThreadPool

    module Information =
        /// Returns the PicoHarp's serial number.
        let GetSerialNumber picoHarp300 =
            let serial = StringBuilder (8)
            logDevice picoHarp300 "Retrieving device serial number."
            NativeApi.GetSerialNumber (index picoHarp300 , serial)
            |> checkStatusAndReturn ( serial.ToString() )
            |> logDeviceOpResult picoHarp300
                ("Successfully retrieved the device (%A) serial number.")
                (sprintf "Failed to retrieve the device serial number: %A.")
            |> Choice.bindOrRaise
            |> Async.ReturnFromThreadPool

        /// Returens the PicoHarp's model number, part number ans version.
        let private hardwareInformation picoHarp300 =
            let model   = StringBuilder (16)
            let partnum = StringBuilder (8)
            let vers    = StringBuilder (8)
            logDevice picoHarp300 "Retrieving device hardware information: model number, part number, version."
            NativeApi.GetHardwareInfo (index picoHarp300, model, partnum, vers)
            |> checkStatusAndReturn ((model.ToString(), partnum.ToString(), vers.ToString()))
            |> logDeviceOpResult picoHarp300
                ("Successfully retrieved the device (%A) hardware information." )
                (sprintf "Failed to retrieve the device hardware information: %A")
            |> Choice.bindOrRaise
            |> Async.ReturnFromThreadPool

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
            logDevice picoHarp300 "Retrieving device base resolution."
            NativeApi.GetResolution(index picoHarp300 , &resolution)
            |> checkStatusAndReturn resolution
            |> logDeviceOpResult picoHarp300
                ("Successfully retrieved the device base resolution.")
                (sprintf "Failed to retrieve the device base resolution: %A")
            |> Choice.bindOrRaise
            |> Async.ReturnFromThreadPool

    module SyncChannel =

        /// Sets the rate divider of the sync channel.
        let setSyncDiv picoHarp300 (sync:SyncParameters) =
            let divider = rateDividerEnum sync
            logDevice picoHarp300 "Setting the sync channel divider."
            NativeApi.SetSyncDiv (index picoHarp300, divider)
            |> checkStatus
            |> logDeviceOpResult picoHarp300
                ("Successfully set the device sync channel divider.")
                (sprintf "Failed to set the device sync channel divider: %A")
            |> Choice.bindOrRaise
            |> Async.ReturnFromThreadPool

        /// Sets the offset of the sync/channel 0.
        let SetSyncOffset picoHarp300 (sync:SyncParameters) =
            let delay = Quantities.durationNanoSeconds (sync.Delay)
            logDevice picoHarp300 "Setting the sync channel offset."
            NativeApi.SetSyncDelay (index picoHarp300, int(delay))
            |> checkStatus
            |> logDeviceOpResult picoHarp300
                (sprintf "Successfully set the device sync offset.")
                (sprintf "Failed to set the device sync offset: %A")
            |> Choice.bindOrRaise
            |> Async.ReturnFromThreadPool

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
            |> checkStatus
            |> logDeviceOpResult picoHarp300
                ("Successfully initialised channel's CFD.")
                (sprintf "Failed to initialis channel's CFD: %A")
            |> Choice.bindOrRaise
            |> Async.ReturnFromThreadPool

    /// measurement functions which are useful in both histogramming and TTTR mode
    module Query =
        /// Returns time period over which experiment was running.
        let getMeasurementTime picoHarp300 =
            let mutable elapsed : double = Unchecked.defaultof<_>
            logDevice picoHarp300 "Retrieving time passed since the start of histogram measurements."
            NativeApi.GetElapsedMeasTime (index picoHarp300, &elapsed)
            |> checkStatusAndReturn elapsed
            |> logQueryResult
                (sprintf "Successfully retrieved measurement time: %A")
                (sprintf "Failed to retrieve measurement time: %A")
            |> Choice.bindOrRaise
            |> Async.ReturnFromThreadPool

        /// If 0 is returned acquisition time is still running, >0 then acquisition time has finished.
        let getCTCStatus picoHarp300 =
            let mutable ctcStatus : int = Unchecked.defaultof<_>
            logDevice picoHarp300 "Checking CTC status"
            NativeApi.CTCStatus (index picoHarp300, &ctcStatus)
            |> checkStatusAndReturn ctcStatus
            |> logQueryResult
                (sprintf "Successfully retrieved CTC status: %A")
                (sprintf "Failed to retrieve CTC status: %A")
            |> Choice.bindOrRaise
            |> Async.ReturnFromThreadPool

        /// Writes channel count rate to a mutable int.
        let getCountRate picoHarp300 (channel:int) =
            let mutable rate : int = Unchecked.defaultof<_>
            logDevice picoHarp300 "Retrieving channel count rate."
            GetCountRate (index picoHarp300, channel, &rate)
            |> checkStatusAndReturn rate
            |> logDeviceOpResult picoHarp300
                ("Successfully retrieved channel count rate.")
                (sprintf "Failed to retrieve channel count rate: %A")
            |> Choice.bindOrRaise
            |> Async.ReturnFromThreadPool

        let triggerHoldOff picoHarp300 deadTime =
            logDevice picoHarp300 "Setting trigger hold-off"
            NativeApi.SetHoldOffTime (index picoHarp300, deadTime)
            |> checkStatus
            |> logDeviceOpResult picoHarp300
                ("Successfully set hold-off.")
                (sprintf "Failed to set hold-off: %A.")
            |> Choice.bindOrRaise
            |> Async.ReturnFromThreadPool

    module internal Acquisition =
        /// Starts a measurement
        let start picoHarp300 duration =
            let acquisitionTime = Quantities.durationMilliSeconds duration
            logDevice picoHarp300 "Setting acquisition time and starting measurement."
            NativeApi.StartMeasurement (index picoHarp300 , int (acquisitionTime))
            |> checkStatus
            |> logDeviceOpResult picoHarp300
                ("Successfully set acquisition time and started TTTR measurement")
                (sprintf "Failed to start: %A.")
            |> Choice.bindOrRaise
            |> Async.ReturnFromThreadPool

        /// Read TTTR FIFO buffer
        let readFifoBuffer picoHarp300 (streamingBuffer : StreamingBuffer) =
            let mutable counts = Unchecked.defaultof<_>
            NativeApi.ReadFiFo(index picoHarp300, streamingBuffer.Buffer, TTTRMaxEvents, &counts)
            |> checkStatusAndReturn counts
            |> Choice.bindOrRaise
            |> Async.ReturnFromThreadPool

        let checkMeasurementFinished picoHarp300 =
            let mutable result = 0
            logDevice picoHarp300 "Checking whether acquisition has finished."
            NativeApi.CTCStatus (index picoHarp300, &result)
            |> checkStatusAndReturn (result <> 0)
            |> logQueryResult
                (sprintf "Successfully checked acquisition finished: %A.")
                (sprintf "Failed to check acquisition status due to error: %A.")
            |> Choice.bindOrRaise
            |> Async.ReturnFromThreadPool

        /// Stops a measurement
        let stop picoHarp300 =
            logDevice picoHarp300 "Ceasing measurement."
            NativeApi.StopMeasurement (index picoHarp300)
            |> checkStatus
            |> logDeviceOpResult picoHarp300
                ("Successfully ended measurement.")
                (sprintf "Failed to end measurement: %A.")
            |> Choice.bindOrRaise
            |> Async.ReturnFromThreadPool