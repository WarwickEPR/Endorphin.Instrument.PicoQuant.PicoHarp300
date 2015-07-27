#r @"..\packages\log4net.2.0.3\lib\net40-full\log4net.dll"
#r @"..\packages\ExtCore.0.8.45\lib\net45\ExtCore.dll"
#r @"..\Endorphin.Core\bin\Debug\Endorphin.Core.dll"
#r "NationalInstruments.Common.dll"
#r "NationalInstruments.VisaNS.dll"

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
    extern int PH_GetLibraryVersion (StringBuilder vers);

    [<DllImport("PHLib.Dll", EntryPoint = "PH_OpenDevice")>]
    /// Opens the PicoHarp and writes its serial number to the space created by StringBuilder.
    extern int PH_OpenDevice (int devidx, StringBuilder serial);

    [<DllImport("PHLib.Dll", EntryPoint = "PH_GetResolution")>]
    /// Returns the bin width of the histogram.
    extern int PH_GetResolution (int devidx, [<Out>] double& resolution);  
    
    [<DllImport("PHLib.Dll", EntryPoint = "PH_CloseDevice")>]
    /// Closes the PicoHarp.
    extern int PH_CloseDevice (int devidx); 

    [<DllImport("PHLib.Dll", EntryPoint = "PH_SetBinning")>]
    /// Sets the bin resolution for the histogram.
    extern void PH_SetBinning (int devidx, int binning); 
    
    [<DllImport("PHLib.Dll", EntryPoint = "PH_Initialize")>]
    /// Sets the mode of the PicoHarp, modes 0 , 2 or 3.
    extern void PH_Initialize (int devidx, int mode);

module CallFunctions = 

    /// Builds two strings to write the serial number and library version into. 
    let serialNumber = StringBuilder (8)
    let libraryVersion = StringBuilder (8)
    
    /// Opens the deice and returns its serial number. 
    let openDevice deviceIndex = asyncChoice{ 
        Native.PH_OpenDevice( deviceIndex , serialNumber)
        return serialNumber} 
    
    let setResolution bins = Native.PH_SetBinning (0, bins)

    let getBaseResolution deviceIndex = asyncChoice{
        let mutable resolution : double = Unchecked.defaultof<_>
        let error = Native.PH_GetResolution(deviceIndex , &resolution)
        printfn "Error?: %d" error
        return printfn "%f" (resolution)}
   
   /// Returns the library version of PHLib.Dll.
    let library = Native.PH_GetLibraryVersion (libraryVersion)

    let closeDevice deviceIndex = Native.PH_CloseDevice (deviceIndex)

///let deviceIndex = 0, always true if the PicoHarp is used in isolation. 
let libraryVersion = StringBuilder(8)
Native.PH_GetLibraryVersion libraryVersion
printfn "%s" (libraryVersion.ToString())

let opendevice = StringBuilder(8)
Native.PH_OpenDevice (0, opendevice)
printfn "%s" (opendevice.ToString())
Native.PH_Initialize(0, 0)
CallFunctions.setResolution 1
CallFunctions.getBaseResolution 0 |> Async.RunSynchronously
Native.PH_CloseDevice(0)



//do CallFunctions.openDevice deviceIndex
 //   |> Async.RunSynchronously
  //  |> ignore 

//do CallFunctions.closeDevice deviceIndex
    

