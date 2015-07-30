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

    [<DllImport("PHLib.Dll", EntryPoint = "PH_GetLibraryVersion")>]
    /// Returns the version of PHLinb.DLL, writes it to space created by StringBuilder.
    extern int GetLibraryVersion (StringBuilder vers);

    [<DllImport("PHLib.Dll", EntryPoint = "PH_OpenDevice")>]
    /// Opens the PicoHarp and writes its serial number to the space created by StringBuilder.
    extern int OpenDevice (int devidx, StringBuilder serial);

    [<DllImport("PHLib.Dll", EntryPoint = "PH_GetResolution")>]
    /// Returns the bin width of the histogram.
    extern int GetResolution (int devidx, [<Out>] double& resolution);  
    
    [<DllImport("PHLib.Dll", EntryPoint = "PH_CloseDevice")>]
    /// Closes the PicoHarp.
    extern int CloseDevice (int devidx); 

    [<DllImport("PHLib.Dll", EntryPoint = "PH_SetBinning")>]
    /// Sets the bin resolution for the histogram.
    extern void SetBinning (int devidx, int binning); 
    
    [<DllImport("PHLib.Dll", EntryPoint = "PH_Initialize")>]
    /// Sets the mode of the PicoHarp, modes 0 , 2 or 3.
    extern void Initialize (int devidx, int mode);

    [<DllImport("PHLib.Dll", EntryPoint = "PH_GetHistogram")>]
    /// Writes histogram data to an empty array chcount. 
    extern int GetHistogram (int devidx, int* chcount, int clear);

    [<DllImport("PHLib.Dll", EntryPoint = "PH_ClearHistMem")>]
    /// Clear all stored histograms from memrory.
    extern int ClearHistMem (int devidx, int block);

module CallFunctions = 

    /// Builds two strings to write the serial number and library version into. 
    let serialNumber = StringBuilder (8)
    let libraryVersion = StringBuilder (8)
    
    /// Opens the deice and returns its serial number. 
    let openDevice deviceIndex = asyncChoice{ 
        Native.OpenDevice( deviceIndex , serialNumber)
        return serialNumber} 
    
    let setResolution bins = Native.SetBinning (0, bins)

    let getBaseResolution deviceIndex = asyncChoice{
        let mutable resolution : double = Unchecked.defaultof<_>
        let error = Native.GetResolution(deviceIndex , &resolution)
        printfn "Error?: %d" error
        return printfn "%f" (resolution)}
   
   /// Returns the library version of PHLib.Dll.
    let library = Native.GetLibraryVersion (libraryVersion)
       
    /// Creates an initilised array for storing histogram data.
    let histogramData : int array = Array.create 65536 1 
    /// Creates a pinned array for PicoHarp to write into.
    let pinnedHistogram =  PinnedArray.of_array histogramData

    /// Writes histogram data in the pinned array.
    /// The argument block will always be zero unless routing is used. 
    let getHistogram deviceIndex block = asyncChoice{
        let success = Native.GetHistogram (deviceIndex, pinnedHistogram.Ptr, block)
        return pinnedHistogram}

    let closeDevice deviceIndex = Native.CloseDevice (deviceIndex)

    let memClear deviceIndex block = asyncChoice{
        let success = Native.ClearHistMem (deviceIndex , block)
        return success}

///let deviceIndex = 0, always true if the PicoHarp is used in isolation. 
let libraryVersion = StringBuilder(8)
Native.GetLibraryVersion libraryVersion
printfn "%s" (libraryVersion.ToString())

let opendevice = StringBuilder(8)
Native.OpenDevice (0, opendevice)
printfn "%s" (opendevice.ToString())
Native.Initialize(0, 0)
CallFunctions.setResolution 1
CallFunctions.getBaseResolution 0 |> Async.RunSynchronously
CallFunctions.memClear 0 0 |> Async.RunSynchronously
CallFunctions.getHistogram 0 0 |> Async.RunSynchronously
Native.CloseDevice(0)



//do CallFunctions.openDevice deviceIndex
 //   |> Async.RunSynchronously
  //  |> ignore 

//do CallFunctions.closeDevice deviceIndex
    

