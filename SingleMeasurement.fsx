#r @"..\packages\log4net.2.0.3\lib\net40-full\log4net.dll"
#r @"..\packages\ExtCore.0.8.45\lib\net45\ExtCore.dll"
#r @"..\Endorphin.Core\bin\Debug\Endorphin.Core.dll"
#r "NationalInstruments.Common.dll"
#r "NationalInstruments.VisaNS.dll"
#r "FSharp.PowerPack.dll"
#r "bin/Debug/PicoHarp300.dll"

open Microsoft.FSharp.NativeInterop
open System.Runtime.InteropServices
open Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols
open System.Text
open Endorphin.Core
open Endorphin.Core.NationalInstruments
open Endorphin.Core.StringUtils
open Endorphin.Instrument.PicoHarp300
open ExtCore.Control

/// Builds a strings to write the serial number into. 
let serialNumber = StringBuilder (8)

/// Array to stroe data in.
let dataArray = Array.create 65535 1

/// Type containing histogram propeties.
let histogram = {
    BinWidth = Width_4ps;
    AcquisitionTime = Time_nano 10.0;
    Overflow = Off;}

/// Type containing PicoHarp propeties.
type DevicePropeties = {
    DeviceIndex : int
    SerialNumber : StringBuilder
    Model : StringBuilder
    PartNumber : StringBuilder
    Version : StringBuilder
    BaseResolution : string    
    Features : int}

module Info = 
   
    /// Returns all PicoHarp imformation in the form of the DevicePropeties type. 
    let info deviceIndex = asyncChoice{

        /// Returns hardware info.
        let! hardware = PicoHarpInfo.HardwareInfo.hardwareInformation deviceIndex   
       
        /// Returns serial number.
        let! serial = PicoHarpInfo.GetSerialNumber deviceIndex
       
        /// Returns base resolution.
        let! baseRes = PicoHarpInfo.getBaseResolution deviceIndex
       
        /// Retruns PicoHarp's features 
        let! features = PicoHarpInfo.getFeatures deviceIndex  

        /// Stores all info in type DevicePropeties.    
        let propeties  = {
            DeviceIndex = 0;
            SerialNumber = serial;
            Model = PicoHarpInfo.HardwareInfo.tupleModel hardware;
            PartNumber = PicoHarpInfo.HardwareInfo.tuplePartnum hardware;
            Version =  PicoHarpInfo.HardwareInfo.tupleVers hardware
            BaseResolution = baseRes;
            Features = features;}
        
        printfn "%A" propeties
        
        return propeties
             }
                    

Native.OpenDevice (0, serialNumber)
Initialise.initialiseMode 0 Histogram  |> Async.RunSynchronously
Histogram.setBinning 0 histogram  |> Async.RunSynchronously
Histogram.measurmentSingle 0 histogram dataArray |> Async.RunSynchronously
Info.info 0 |> Async.RunSynchronously
Native.CloseDevice 0


