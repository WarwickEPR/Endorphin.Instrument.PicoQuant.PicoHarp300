// Copyright (c) University of Warwick. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE.txt in the project root for license information.

namespace Endorphin.Instrument.PicoQuant.PicoHarp300

open Microsoft.FSharp.NativeInterop
open System.Runtime.InteropServices
open System.Text

/// For functions in the module Native the argument int devidx represents the PicoHarps device index.
/// The device index is always zero when the PicoHarp is being used in isolation.
[<AutoOpen>]
module internal NativeApi =

    [<Literal>]
    let dllName = "PHlib.dll"

    [<DllImport(dllName, EntryPoint = "PH_GetLibraryVersion")>]
    /// Returns the version of the PHLib library.
    extern ErrorCode GetLibraryVersion (StringBuilder vers);

    [<DllImport(dllName, EntryPoint = "PH_GetErrorString")>]
    /// Returns the error code corresponding to the error number.
    extern ErrorCode GetErrorString (StringBuilder errstring, int errcode);

    [<DllImport(dllName, EntryPoint = "PH_OpenDevice")>]
    /// Opens device and returns the serial number.
    extern ErrorCode OpenDevice (int devidx, StringBuilder serial);

    [<DllImport(dllName, EntryPoint = "PH_GetSerialNumber")>]
    /// Returns the PicoHarp's serial number.
    extern ErrorCode GetSerialNumber (int devidx, StringBuilder serial);

    [<DllImport(dllName, EntryPoint = "PH_GetFeatures")>]
    /// Returns features of the device in a bit pattern.
    extern ErrorCode GetFeatures (int devidx, [<Out>] int& features);

    [<DllImport(dllName, EntryPoint = "PH_Initialize")>]
    /// Sets the mode of the PicoHarp, modes 0 , 2 or 3.
    extern ErrorCode InitialiseMode (int devidx, ModeEnum mode);

    [<DllImport(dllName, EntryPoint = "PH_GetHardwareInfo")>]
    /// Returns model number, part number and hardware version.
    extern ErrorCode GetHardwareInfo (int devidx, StringBuilder model, StringBuilder partno, StringBuilder version);

    [<DllImport(dllName, EntryPoint = "PH_Calibrate")>]
    /// Calibrates the PicoHarp.
    extern ErrorCode Calibrate(int devidx);

    [<DllImport(dllName, EntryPoint = "PH_SetSyncDiv")>]
    extern ErrorCode SetSyncDiv (int devidx, RateDividerEnum div);

    [<DllImport(dllName, EntryPoint = "PH_SetInputCFD")>]
    /// Sets the discriminator level and the zerocross offset for either the channel 0 CFD or the channel 1 CFD.
    extern ErrorCode SetInputCFD (int devidx, ChannelEnum channel, int level, int zerocross);

    [<DllImport(dllName, EntryPoint = "PH_SetBinning")>]
    /// Sets the bin resolution for the histogram.
    extern ErrorCode SetBinning (int devidx, ResolutionEnum binning);

    [<DllImport(dllName, EntryPoint = "PH_SetOffset")>]

    extern ErrorCode SetOffset (int devidx, int offset);

    [<DllImport(dllName, EntryPoint = "PH_SetSyncOffset")>]
    /// Sets offest of channel 0.
    extern ErrorCode SetSyncDelay (int devidx, int delay);

    [<DllImport(dllName, EntryPoint = "PH_GetResolution")>]
    /// Returns the bin width of the histogram.
    extern ErrorCode GetResolution (int devidx, [<Out>] double& resolution);

    [<DllImport(dllName, EntryPoint = "PH_SetStopOverflow")>]
    /// Sets the histogram channels saturation point, limits the counts per channel.
    extern ErrorCode SetStopOverflow (int devidx, int stop_ovfl, int stopcount);

    [<DllImport(dllName, EntryPoint = "PH_ClearHistMem")>]
    /// Clear all stored histograms from memrory.
    extern ErrorCode ClearHistMem (int devidx, int block);

    [<DllImport(dllName, EntryPoint = "PH_StartMeas")>]
    /// Starts taking Measurements.
    extern ErrorCode StartMeasurement (int devidx, int tacq);

    [<DllImport(dllName, EntryPoint = "PH_StopMeas")>]
    /// Stops Measurements.
    extern ErrorCode StopMeasurement (int devidx);

    [<DllImport(dllName, EntryPoint = "PH_GetElapsedMeasTime")>]
    /// Closes the PicoHarp.
    extern ErrorCode GetElapsedMeasTime (int devidx, [<Out>] double& elasped);

    [<DllImport(dllName, EntryPoint = "PH_CTCStatus")>]
    /// Checks if Measurements have been taken or are still being taken.
    extern ErrorCode CTCStatus (int devidx, [<Out>] int& ctcstatus);

    [<DllImport(dllName, EntryPoint = "PH_GetHistogram")>]
    /// Writes histogram data to an empty array chcount.
    extern ErrorCode GetHistogram (int devidx, int [] chcount, int clear);

    [<DllImport(dllName, EntryPoint = "PH_GetCountRate")>]
    /// Averages counts in channel over 100ms and returns a count rate.
    extern ErrorCode GetCountRate (int devidx, int channel, int& rate);

    [<DllImport(dllName, EntryPoint = "PH_GetFlags")>]
    /// Retrieve hardware status flags
    extern ErrorCode GetFlags (int devidx, int* flags);

    [<DllImport(dllName, EntryPoint = "PH_CloseDevice")>]
    /// Closes the PicoHarp.
    extern ErrorCode CloseDevice (int devidx);

    [<DllImport(dllName, EntryPoint = "PH_ReadFiFo")>]
    /// Read the TTTR mode buffer in a FIFO fashion
    extern ErrorCode ReadFiFo(int devidx, uint32[] buffer, int count, [<Out>] int& nactual);

    [<DllImport(dllName, EntryPoint = "PH_SetMarkerHoldoffTime")>]
    extern ErrorCode SetHoldOffTime(int devidx, int holdofftime);