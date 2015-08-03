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

    let errorMessage = 
        function
        | ErrorCode.NoError               -> "No Error"
        | ErrorCode.DeviceOpenFail        -> "PicoHarp has failed to open."
        | ErrorCode.DeviceBusy            -> "The PicoHarp is busy."
        | ErrorCode.DeviceHeventFail      -> ""
        | ErrorCode.DeviceCallbsetFail    -> ""
        | ErrorCode.DeviceBarmapFail      -> ""
        | ErrorCode.DeviceCloseFail       -> "Unable to close the PicoHarp."
        | ErrorCode.DeviceResetFail       -> "Unable to reset the PicoHarp."
        | ErrorCode.DeviceGetversionFail  -> "Unable to retrieve the PicoHarp's version"
        | ErrorCode.DeviceVersionMismatch ->  ""
        | ErrorCode.DeviceNotOpen         -> "The PicoHarp has not been opened."
        | ErrorCode.DeviceLocked          -> "The PicoHarp is locked."
        | ErrorCode.InstanceRunning       -> ""
        | ErrorCode.InvalidArgument       -> "Invaild argument."
        | ErrorCode.InvalidMode           -> "Invalid mode, possible modes: Histogram, T2, T3."
        | ErrorCode.InvalidOption         -> "Invalid option."
        | ErrorCode.InvalidMemory         -> ""
        | ErrorCode.InvalidData           -> ""
        | ErrorCode.NotInitialized        -> "The PicoHarp has not been initilised."
        | ErrorCode.NotCalibrated         -> "The PcioHarp has not been calibrated."
        | ErrorCode.DmaFail               -> ""
        | ErrorCode.XtdeviceFail          -> ""
        | ErrorCode.FpgaconfFail          -> ""
        | ErrorCode.IfconfFail            -> ""
        | ErrorCode.FiforesetFail         -> ""
        | ErrorCode.StatusFail            -> ""
        | ErrorCode.UsbGetdriververFail   -> "Unable to retrieve the USB's driver version."
        | ErrorCode.UsbDriververMismatch  -> ""
        | ErrorCode.UsbGetifinfoFail      -> "Unable to retrieve USB imformation."
        | ErrorCode.UsbHispeedFail        -> "" 
        | ErrorCode.UsbVcmdFail           -> ""
        | ErrorCode.UsbBulkrdFail         -> ""
        | ErrorCode.HardwareF01           -> ""
        | ErrorCode.HardwareF02           -> ""
        | ErrorCode.HardwareF03           -> ""
        | ErrorCode.HardwareF04           -> ""
        | ErrorCode.HardwareF05           -> ""
        | ErrorCode.HardwareF06           -> ""
        | ErrorCode.HardwareF07           -> ""
        | ErrorCode.HardwareF08           -> ""
        | ErrorCode.HardwareF09           -> ""
        | ErrorCode.HardwareF10           -> ""
        | ErrorCode.HardwareF11           -> ""
        | ErrorCode.HardwareF12           -> ""
        | ErrorCode.HardwareF13           -> ""
        | ErrorCode.HardwareF14           -> ""
        | ErrorCode.HardwareF15           -> ""
        
    let (|Ok|Error|) = function
        | ErrorCode.NoError -> Ok  
        | errorCode         -> Error (errorMessage errorCode)