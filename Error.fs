namespace Endorphin.Instrument.PicoHarp300

[<AutoOpen>]
module internal Error = 
    
    /// Error code for the PicoHarp 300.
    type ErrorCode = 
        | NoError               =  0
        | DeviceOpenFail        = -1
        | DeviceBusy            = -2
        | DeviceHeventFail      = -3
        | DeviceCallbsetFail    = -4
        | DeviceBarmapFail      = -5
        | DeviceCloseFail       = -6
        | DeviceResetFail       = -7
        | DeviceGetversionFail  = -8
        | DeviceVersionMismatch = -9
        | DeviceNotOpen         = -10
        | DeviceLocked          = -11
        | InstanceRunning       = -16
        | InvalidArgument       = -17
        | InvalidMode           = -18
        | InvalidOption         = -19
        | InvalidMemory         = -20
        | InvalidData           = -21
        | NotInitialized        = -22
        | NotCalibrated         = -23
        | DmaFail               = -24
        | XtdeviceFail          = -25
        | FpgaconfFail          = -26
        | IfconfFail            = -27
        | FiforesetFail         = -28
        | StatusFail            = -29
        | UsbGetdriververFail   = -32
        | UsbDriververMismatch  = -33
        | UsbGetifinfoFail      = -34
        | UsbHispeedFail        = -35
        | UsbVcmdFail           = -36
        | UsbBulkrdFail         = -37
        | HardwareF01           = -64
        | HardwareF02           = -65
        | HardwareF03           = -66
        | HardwareF04           = -67
        | HardwareF05           = -68
        | HardwareF06           = -69
        | HardwareF07           = -70
        | HardwareF08           = -71
        | HardwareF09           = -72
        | HardwareF10           = -73
        | HardwareF11           = -74
        | HardwareF12           = -75
        | HardwareF13           = -76
        | HardwareF14           = -77
        | HardwareF15           = -78

    let errorMessage = function
        | ErrorCode.NoError               -> "No Error"
        | ErrorCode.DeviceOpenFail        -> "PicoHarp has failed to open."
        | ErrorCode.DeviceBusy            -> "The PicoHarp is busy."
        | ErrorCode.DeviceHeventFail      -> "Device Event failed."
        | ErrorCode.DeviceCallbsetFail    -> "Call fail."
        | ErrorCode.DeviceBarmapFail      -> "Barmap fail."
        | ErrorCode.DeviceCloseFail       -> "Unable to close the PicoHarp."
        | ErrorCode.DeviceResetFail       -> "Unable to reset the PicoHarp."
        | ErrorCode.DeviceGetversionFail  -> "Unable to retrieve the PicoHarp's version"
        | ErrorCode.DeviceVersionMismatch -> "Version mismatch."
        | ErrorCode.DeviceNotOpen         -> "The PicoHarp has not been opened."
        | ErrorCode.DeviceLocked          -> "The PicoHarp is locked."
        | ErrorCode.InstanceRunning       -> "Instance already running."
        | ErrorCode.InvalidArgument       -> "Invaild argument."
        | ErrorCode.InvalidMode           -> "Invalid mode, possible modes: Histogram, T2, T3."
        | ErrorCode.InvalidOption         -> "Invalid option."
        | ErrorCode.InvalidMemory         -> "Invalid memory."
        | ErrorCode.InvalidData           -> "Invalid data."
        | ErrorCode.NotInitialized        -> "The PicoHarp has not been initilised."
        | ErrorCode.NotCalibrated         -> "The PcioHarp has not been calibrated."
        | ErrorCode.DmaFail               -> "Dma fail."
        | ErrorCode.XtdeviceFail          -> "Xtd device fail"
        | ErrorCode.FpgaconfFail          -> "FpgaconfFail"
        | ErrorCode.IfconfFail            -> "IfconfFail"
        | ErrorCode.FiforesetFail         -> "FiforesetFail"
        | ErrorCode.StatusFail            -> "Status fail"
        | ErrorCode.UsbGetdriververFail   -> "Unable to retrieve the USB's driver version."
        | ErrorCode.UsbDriververMismatch  -> "Usb driver mismatch."
        | ErrorCode.UsbGetifinfoFail      -> "Unable to retrieve USB imformation."
        | ErrorCode.UsbHispeedFail        -> "Usb Hispeed fail" 
        | ErrorCode.UsbVcmdFail           -> "Usb Vcmd fail"
        | ErrorCode.UsbBulkrdFail         -> "Usb bulkrd fail."
        | ErrorCode.HardwareF01           -> "HardwareF01"
        | ErrorCode.HardwareF02           -> "HardwareF02"
        | ErrorCode.HardwareF03           -> "HardwareF03"
        | ErrorCode.HardwareF04           -> "HardwareF04"
        | ErrorCode.HardwareF05           -> "HardwareF05"
        | ErrorCode.HardwareF06           -> "HardwareF06"
        | ErrorCode.HardwareF07           -> "HardwareF07"
        | ErrorCode.HardwareF08           -> "HardwareF08"
        | ErrorCode.HardwareF09           -> "HardwareF09"
        | ErrorCode.HardwareF10           -> "HardwareF10"
        | ErrorCode.HardwareF11           -> "HardwareF11"
        | ErrorCode.HardwareF12           -> "HardwareF12"
        | ErrorCode.HardwareF13           -> "HardwareF13"
        | ErrorCode.HardwareF14           -> "HardwareF14"
        | ErrorCode.HardwareF15           -> "HardwareF15"
        | errorCode                       -> failwithf "Unexpected error code enum value: %A." errorCode
        
    let (|Ok|Error|) = function
        | ErrorCode.NoError -> Ok  
        | errorCode         -> Error (errorMessage errorCode)