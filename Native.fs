namespace Endorphin.Instrument.PicoHarp300

open Microsoft.FSharp.NativeInterop
open System.Runtime.InteropServices
open Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols
open System.Text
open Endorphin.Core
open Endorphin.Core.NationalInstruments
open Endorphin.Core.StringUtils
open ExtCore.Control

/// For functions in the module Native the argument int devidx represents the PicoHarps device index. 
/// The device index is always zero when the PicoHarp is being used in isolation. 
[<AutoOpen>]
module Native = 

    [<DllImport("PHLib.Dll", EntryPoint = "PH_GetLibraryVersion")>]
    /// Returns the version of the PHLib library.
    extern int PH_GetLibraryVersion (StringBuilder vers);

    [<DllImport("PHLib.Dll", EntryPoint = "PH_GetErrorString")>]
    /// Returns the error code corresponding to the error number.
    extern int PH_GetErrorString (StringBuilder errstring, int errcode);

    [<DllImport("PHLib.Dll", EntryPoint = "PH_OpenDevice")>]
    /// Opens device and returns the serial number.
    extern int PH_OpenDevice (int devidx, StringBuilder serial); 

    [<DllImport("PHLib.Dll", EntryPoint = "PH_GetSerialNumber")>]
    /// Returns the PicoHarp's serial number.
    extern int PH_GetSerialNumber (int devidx, StringBuilder serial);

    [<DllImport("PHLib.Dll", EntryPoint = "PH_GetFeatures")>]
    /// Returns features of the device in a bit pattern.
    extern int PH_GetFeatures (int devidx, [<Out>] int& features); 

    [<DllImport("PHLib.Dll", EntryPoint = "PH_Initialize")>]
    /// Sets the mode of the PicoHarp, modes 0 , 2 or 3.
    extern int PH_Initialise (int devidx, int mode);

    [<DllImport("PHLib.Dll", EntryPoint = "PH_GetHardwareInfo")>]
    /// Returns model number, part number and hardware version. 
    extern int PH_GetHardwareInfo (int devidx, StringBuilder model, StringBuilder partno, StringBuilder version);  

    [<DllImport("PHLib.Dll", EntryPoint = "PH_Calibrate")>]
    /// Calibrates the PicoHarp.
    extern int PH_Calibrate(int devidx);

    [<DllImport("PHLib.Dll", EntryPoint = "PH_SetSyncDiv")>]
    extern int PH_SetSyncDiv (int devidx, int div);

    [<DllImport("PHLib.Dll", EntryPoint = "PH_SetInputCFD")>]
    /// Sets the discriminator level and the zerocross offset for either the channel 0 CFD or the channel 1 CFD.
    extern int PH_SetInputCFD (int devidx, int channel, int level, int zerocross);

    [<DllImport("PHLib.Dll", EntryPoint = "PH_SetBinning")>]
    /// Sets the bin resolution for the histogram.
    extern int PH_SetBinning (int devidx, int binning);

    [<DllImport("PHLib.Dll", EntryPoint = "PH_SetOffset")>]
    
    extern int PH_SetOffset (int devidx, int offset);

    [<DllImport("PHLib.Dll", EntryPoint = "PH_SetSyncOffset")>]
    /// Sets offest of channel 0.
    extern int PH_SetSyncOffset (int devidx, int offset);

    [<DllImport("PHLib.Dll", EntryPoint = "PH_GetResolution")>]
    /// Returns the bin width of the histogram.
    extern int PH_GetResolution (int devidx, [<Out>] double& resolution);  

    [<DllImport("PHLib.Dll", EntryPoint = "PH_GetCountRate")>]
    /// Returns the count rate at this channel.
    extern int PH_GetCountRate (int devidx, int channel, int* countrate);

    [<DllImport("PHLib.Dll", EntryPoint = "PH_SetStopOverflow")>]
    /// Sets the histogram channels saturation point, limits the counts per channel.
    extern int PH_SetStopOverflow (int devidx, int stop_ovfl, int stopcount);

    [<DllImport("PHLib.Dll", EntryPoint = "PH_ClearHistMem")>]
    /// Clear all stored histograms from memrory.
    extern int PH_ClearHistMem (int devidx, int block);

    [<DllImport("PHLib.Dll", EntryPoint = "PH_StartMeas")>]
    /// Starts taking measurments. 
    extern int PH_StartMeas (int devidx, int tacq);

    [<DllImport("PHLib.Dll", EntryPoint = "PH_StopMeas")>]
    /// Stops measurments. 
    extern int PH_StopMeas (int devidx);
    
    [<DllImport("PHLib.Dll", EntryPoint = "PH_GetElapsedMeasTime")>]
    /// Closes the PicoHarp. 
    extern int PH_GetElapsedMeasTime (int devidx, [<Out>] double& elasped);
    
    [<DllImport("PHLib.Dll", EntryPoint = "PH_CTCStatus")>]
    /// Checks if measurments have been taken or are still being taken.
    extern int PH_CTCStatus (int devidx, [<Out>] int& ctcstatus);
    
    [<DllImport("PHLib.Dll", EntryPoint = "PH_GetHistogram")>]
    /// Writes histogram data to an empty array chcount. 
    extern int PH_GetHistogram (int devidx, int [] chcount, int clear);
    
    [<DllImport("PHLib.Dll", EntryPoint = "PH_GetFlags")>]
    extern int PH_GetFlags (int devidx, int* flags); 
    
    [<DllImport("PHLib.Dll", EntryPoint = "PH_CloseDevice")>]
    /// Closes the PicoHarp. 
    extern int PH_CloseDevice (int devidx);

    




