#r @"..\packages\log4net.2.0.3\lib\net40-full\log4net.dll"
#r @"..\packages\ExtCore.0.8.45\lib\net45\ExtCore.dll"
#r @"..\Endorphin.Core\bin\Debug\Endorphin.Core.dll"
#r "NationalInstruments.Common.dll"
#r "NationalInstruments.VisaNS.dll"
#r "FSharp.PowerPack.dll"
#r "bin/Debug/Endorphin.Instrument.PicoHarp300.dll"


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
/// Creates an initilised array for storing histogram data.
let histogramData = Array.create 65536 14
/// Creates a pinned array for PicoHarp to write into.
let histogramDataWriteTo = Array.create 65536 14

/// Type containing histogram propeties.
let histogramPropeties = {
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

/// Returns the model number from the hardwareImformation tuple. 
let private model = function
    | (x:StringBuilder, y:StringBuilder, z:StringBuilder) -> x
    |_ -> failwithf "No tuple"

/// Returns the part number number from the hardwareImformation tuple. 
let private partnum = function
    | (x:StringBuilder, y:StringBuilder, z:StringBuilder) -> y
    |_ -> failwithf "No tuple"

/// Returns the version number from the hardwareImformation tuple.     
let private vers = function
    | (x:StringBuilder, y:StringBuilder, z:StringBuilder) -> z
    |_-> failwithf "No tuple"

module Info = 
   
    /// Returns all PicoHarp imformation in the form of the DevicePropeties type. 
    let info deviceIndex = asyncChoice{
       
        /// Returns hardware info.
        let! hardware = PicoHarpInfo.hardwareInformation deviceIndex   
       
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
            Model = model hardware;
            PartNumber = partnum hardware;
            Version = vers hardware;
            BaseResolution = baseRes;
            Features = features;}
        
        return propeties
             }

       
Initialise.openDevice 0 
Info.info 0 |> Async.RunSynchronously
Initialise.closeDevice 0

   /// let initial = asyncChoice{
        /// Sets the PicoHarp's mode.
      ///  let initialise = Initialise.initialiseMode deviceIndex Histogramming 
        /// Sets the bin width. 
   //     let binWidth = Histogram.setBinning deviceIndex histogramPropeties 
        /// Clears Picoharps memrory. Block argument will always be zero.
    //    let clear = Histogram.clearHistogramMemrory deviceIndex 0
        /// Start measurment.
    //    let startMeas = Histogram.startMeasurments deviceIndex histogramPropeties 
        /// Ends measurments. 
    //    let endMeas = Histogram.endMeasurments deviceIndex
        /// Writes histogram data to an array.
   //     let getData = Histogram.getHistogram (deviceIndex) (histogramDataWriteTo) (0)

        

    

     

