#r @"..\packages\log4net.2.0.3\lib\net40-full\log4net.dll"
#r @"..\packages\ExtCore.0.8.45\lib\net45\ExtCore.dll"
#r @"..\Endorphin.Core\bin\Debug\Endorphin.Core.dll"
#r "NationalInstruments.Common.dll"
#r "NationalInstruments.VisaNS.dll"
#r "FSharp.PowerPack.dll"

open Microsoft.FSharp.NativeInterop
open System.Runtime.InteropServices
open Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols
open System.Text
open Endorphin.Core
open Endorphin.Core.NationalInstruments
open Endorphin.Core.StringUtils
open ExtCore.Control

module Native = 

    [<DllImport("PHLib.Dll", EntryPoint = "PH_OpenDevice")>]
    /// Opens the PicoHarp and writes its serial number to the space created by StringBuilder.
    extern int PH_OpenDevice (int devidx, StringBuilder serial);
    
    [<DllImport("PHLib.Dll", EntryPoint = "PH_CloseDevice")>]
    /// Closes the PicoHarp.
    extern int PH_CloseDevice (int devidx); 

    [<DllImport("PHLib.Dll", EntryPoint = "PH_SetBinning")>]
    /// Sets the bin resolution for the histogram.
    extern int PH_SetBinning (int devidx, int binning); 
    
    [<DllImport("PHLib.Dll", EntryPoint = "PH_Initialize")>]
    /// Sets the mode of the PicoHarp, modes 0 , 2 or 3.
    extern int PH_Initialize (int devidx, int mode);
   
    [<DllImport("PHLib.Dll", EntryPoint = "PH_StartMeas")>]
    /// Starts taking measurments. 
    extern int PH_StartMeas (int devidx, int tacq);

    [<DllImport("PHLib.Dll", EntryPoint = "PH_StopMeas")>]
    /// Stops measurments. 
    extern int PH_StopMeas (int devidx);

    [<DllImport("PHLib.Dll", EntryPoint = "PH_GetHistogram")>]
    /// Writes histogram data to an empty array chcount. 
    extern int PH_GetHistogram (int devidx, int [] chcount, int clear);

    [<DllImport("PHLib.Dll", EntryPoint = "PH_ClearHistMem")>]
    /// Clear all stored histograms from memrory.
    extern int PH_ClearHistMem (int devidx, int block);

        
module Initial = 
  
    /// Builds a strings to write the serial number into. 
    let serialNumber = StringBuilder (8)
    /// Creates an initilised array for storing histogram data.
    let histogramData = Array.create 65536 14
    /// Creates a pinned array for PicoHarp to write into.
    let histogramDataWriteTo = Array.create 65536 14

module CallFunctions = 

    /// Sets the bin resolution. 
    let setResolution bins = Native.PH_SetBinning (0, bins)
     
    /// Starts histogram mode measurments, requires an acquisition time aka the period of time to take measurments over.
    let startMeasurments deviceIndex time = asyncChoice{
        let success =  Native.PH_StartMeas (deviceIndex , int (time)) 
        return success}
    /// Stops histogram mode measurments. 
    let endMeasurments deviceIndex = asyncChoice{
        let success = Native.PH_StopMeas (deviceIndex)
        return success}
    
    /// Writes histogram data in the pinned array.
    /// The argument block will always be zero unless routing is used. 
    let getHistogram deviceIndex block = asyncChoice{
        let success = Native.PH_GetHistogram (deviceIndex, Initial.histogramDataWriteTo , block)
        return success}

    /// Clears histograms from the PicoHarps memrory.
    let memClear deviceIndex block = asyncChoice{
        let success = Native.PH_ClearHistMem (deviceIndex , block)
        return success}
    
    /// Calculates the total counts measured by summing the histogram channels.
    let countTotal (array : int []) =  Array.sum array

    /// Closes the PicoHarp. 
    let closeDevice deviceIndex = Native.PH_CloseDevice (deviceIndex)

Native.PH_OpenDevice (0, Initial.serialNumber)
Native.PH_Initialize (0, 0)
CallFunctions.setResolution 1
CallFunctions.memClear 0 0 |> Async.RunSynchronously
CallFunctions.startMeasurments 0 10.0
CallFunctions.endMeasurments 0
CallFunctions.getHistogram 0 0 |> Async.RunSynchronously
Native.PH_CloseDevice(0)


    

