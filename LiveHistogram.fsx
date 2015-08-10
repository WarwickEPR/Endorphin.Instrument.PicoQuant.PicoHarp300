#r @"..\packages\log4net.2.0.3\lib\net40-full\log4net.dll"
#r @"..\packages\ExtCore.0.8.45\lib\net45\ExtCore.dll"
#r @"..\Endorphin.Core\bin\Debug\Endorphin.Core.dll"
#r "NationalInstruments.Common.dll"
#r "NationalInstruments.VisaNS.dll"
#r "FSharp.PowerPack.dll"
#r "bin/Debug/PicoHarp300.dll"
#r "../packages/FSharp.Charting.0.90.12/lib/net40/FSharp.Charting.dll"
#r "System.Windows.Forms.DataVisualization.dll"

open Microsoft.FSharp.NativeInterop
open System.Runtime.InteropServices
open Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols
open System.Text
open Endorphin.Core
open Endorphin.Core.NationalInstruments
open Endorphin.Core.StringUtils
open ExtCore.Control
open Endorphin.Instrument.PicoHarp300
open FSharp.Charting

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

/// Runs the experiment and returns total histogram counts. 
let experiment handle = asyncChoice{ 
    let array = Array.create 65535 0
    do! Histogram.clearmemory handle 0   
    do! Histogram.startMeasurements handle histogram
    /// Sleep for acquisition and then stop measurements.  
    do! Histogram.waitToFinishMeasurement handle 1000
    do! Histogram.endMeasurements handle     
    do! Histogram.getHistogram handle array 0
    return array}

/// Converts type Resolution into type int.
let width = Quantities.resolutiontoWidth (histogram.Resolution) 

/// Array containing values for bar chart x axis.  
let xAxis = Array.init 65535 (fun i -> i*width) 

/// Bar chart event. 
let barData = new Event<int * int>()

/// Takes data and adds to the array total
let rec liveCounts duration (previousData:int[]) handle = asyncChoice{
    if duration > 0 then 
        /// The array returned by the PicoHarp containing count data.
        let! picoHarpData = experiment handle 
        /// Sums picoHarpData counts with previously obtained data.
        let yAxis = Array.map2 (fun x y -> x + y) previousData picoHarpData
        /// Combines xAxis and yAxis into an array of tuples.
        let barChart = Array.map2 (fun x y -> (x,y)) xAxis yAxis
        /// ????????????????????????????????????????????????????????????????????????????????????????????????????????????????????
        

        do! liveCounts (duration - 1) yAxis handle 
    else 
        do! PicoHarp.initialise.closeDevice handle }

initialise handle |> Async.RunSynchronously



         


