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
open ExtCore.Control
open Endorphin.Instrument.PicoHarp300

/// Contains histogram parameters.
let histogram = {
    Resolution      = Resolution_512ps;
    AcquisitionTime = Duration_s 1.0<s>;
    Overflow        = None;
    }

/// Obtains the PicoHarp's device index. 
let handle = PicoHarp.initialise.picoHarp "1020854"

/// Initoalises the PicoHarp to histogramming mode, sets bin width and overflow limits.
let initialise handle = asyncChoice{  
    let! opendev  = PicoHarp.initialise.openDevice handle   
    do! PicoHarp.initialise.initialiseMode handle Histogramming
    do! Histogram.setBinning handle histogram
    do! Histogram.stopOverflow handle histogram
    return opendev}

/// Runs the experiment.
let experiment handle = asyncChoice{ 
    let array = Array.create 65535 0
    do! Histogram.clearmemory handle 0 
    do! Histogram.startMeasurements handle histogram
    /// Recursive, checks every second.1
    do! Histogram.waitToFinishMeasurement handle 1000
    /// Sleep for acquisition and then stop measurements.  
    let sleep = Async.Sleep(10000) 
    let! endMeasurement = Histogram.endMeasurements handle     
    return endMeasurement}

let rec liveCounts duration (array:int []) handle = 
    if duration = 0 then 
         array
    else 
        let countData = Array.create 65535 0
        do experiment handle 
        do Histogram.getHistogram handle countData 0 
        let total = Array.map2 (fun x y -> x + y) array countData
        liveCounts (duration - 1) total handle 
    

/// Collects many histograms and adds all their counts to get total count rate over the acquisition time.
let rec intergrationMeasurement iterations (array:int[]) handle = 
      if iterations = 0 then
           array
      else 
          /// Runs experiment.
          do experiment handle |> ignore  
          /// Creates an array
          let arrayData = Array.create 65535 0
          /// Writes data to the array.
          do Histogram.getHistogram handle (arrayData) (0) |> ignore
          /// Sums the data array with the master array to get a running count.   
          let total = Array.map2 (fun x y -> x + y) arrayData array
          intergrationMeasurement (iterations - 1) array handle  

/// Determines number of histograms to be collected and runs the experiment.
let integrationMode (time:float<s>) = 
    /// Creates an array to store total count data in.
    let totalArray = Array.create 65535 0
    /// Calculates number of histograms that can be acquired within the acquisition time. 
    let iterationsFloat = (Quantities.durationSeconds(histogram.AcquisitionTime))/time
    /// Converts iterationsFloat to an int.
    let iterationsInt = int(iterationsFloat)
    /// Takes measurements.
    intergrationMeasurement iterationsInt totalArray

         


