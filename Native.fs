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
    extern int GetLibraryVersion (StringBuilder vers);

    [<DllImport("PHLib.Dll", EntryPoint = "PH_GetErrorString")>]
    /// Returns the error code corresponding to the error number.
    extern int GetErrorString (StringBuilder errstring, int errcode);

    [<DllImport("PHLib.Dll", EntryPoint = "PH_OpenDevice")>]
    /// Opens device and returns the serial number.
    extern int OpenDevice (int devidx, StringBuilder serial); 

    [<DllImport("PHLib.Dll", EntryPoint = "PH_GetSerialNumber")>]
    /// Returns the PicoHarp's serial number.
    extern int GetSerialNumber (int devidx, StringBuilder serial);

    [<DllImport("PHLib.Dll", EntryPoint = "PH_GetFeatures")>]
    /// Returns features of the device in a bit pattern.
    extern int GetFeatures (int devidx, [<Out>] int& features); 

    [<DllImport("PHLib.Dll", EntryPoint = "PH_Initialize")>]
    /// Sets the mode of the PicoHarp, modes 0 , 2 or 3.
    extern int InitialiseMode (int devidx, int mode);

    [<DllImport("PHLib.Dll", EntryPoint = "PH_GetHardwareInfo")>]
    /// Returns model number, part number and hardware version. 
    extern int GetHardwareInfo (int devidx, StringBuilder model, StringBuilder partno, StringBuilder version);  

    [<DllImport("PHLib.Dll", EntryPoint = "PH_Calibrate")>]
    /// Calibrates the PicoHarp.
    extern int Calibrate(int devidx);

    [<DllImport("PHLib.Dll", EntryPoint = "PH_SetSyncDiv")>]
    extern int SetSyncDiv (int devidx, int div);

    [<DllImport("PHLib.Dll", EntryPoint = "PH_SetInputCFD")>]
    /// Sets the discriminator level and the zerocross offset for either the channel 0 CFD or the channel 1 CFD.
    extern int SetInputCFD (int devidx, int channel, int level, int zerocross);

    [<DllImport("PHLib.Dll", EntryPoint = "PH_SetBinning")>]
    /// Sets the bin resolution for the histogram.
    extern int SetBinning (int devidx, int binning);

    [<DllImport("PHLib.Dll", EntryPoint = "PH_SetOffset")>]
    
    extern int SetOffset (int devidx, int offset);

    [<DllImport("PHLib.Dll", EntryPoint = "PH_SetSyncOffset")>]
    /// Sets offest of channel 0.
    extern int SetSyncOffset (int devidx, int offset);

    [<DllImport("PHLib.Dll", EntryPoint = "PH_GetResolution")>]
    /// Returns the bin width of the histogram.
    extern int GetResolution (int devidx, [<Out>] double& resolution);  

    [<DllImport("PHLib.Dll", EntryPoint = "PH_GetCountRate")>]
    /// Returns the count rate at this channel.
    extern int GetCountRate (int devidx, int channel, int* countrate);

    [<DllImport("PHLib.Dll", EntryPoint = "PH_SetStopOverflow")>]
    /// Sets the histogram channels saturation point, limits the counts per channel.
    extern int SetStopOverflow (int devidx, int stop_ovfl, int stopcount);

    [<DllImport("PHLib.Dll", EntryPoint = "PH_ClearHistMem")>]
    /// Clear all stored histograms from memrory.
    extern int ClearHistMem (int devidx, int block);

    [<DllImport("PHLib.Dll", EntryPoint = "PH_StartMeas")>]
    /// Starts taking measurments. 
    extern int StartMeasurment (int devidx, int tacq);

    [<DllImport("PHLib.Dll", EntryPoint = "PH_StopMeas")>]
    /// Stops measurments. 
    extern int StopMeasurment (int devidx);
    
    [<DllImport("PHLib.Dll", EntryPoint = "PH_GetElapsedMeasTime")>]
    /// Closes the PicoHarp. 
    extern int GetElapsedMeasTime (int devidx, [<Out>] double& elasped);
    
    [<DllImport("PHLib.Dll", EntryPoint = "PH_CTCStatus")>]
    /// Checks if measurments have been taken or are still being taken.
    extern int CTCStatus (int devidx, [<Out>] int& ctcstatus);
    
    [<DllImport("PHLib.Dll", EntryPoint = "PH_GetHistogram")>]
    /// Writes histogram data to an empty array chcount. 
    extern int GetHistogram (int devidx, int [] chcount, int clear);
    
    [<DllImport("PHLib.Dll", EntryPoint = "PH_GetFlags")>]

    extern int GetFlags (int devidx, int* flags); 
    
    [<DllImport("PHLib.Dll", EntryPoint = "PH_CloseDevice")>]
    /// Closes the PicoHarp. 
    extern int CloseDevice (int devidx);

    




