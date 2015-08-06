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

/// Runs the experiment.
let experiment handle = asyncChoice{ 
    let array = Array.create 65535 0
    do! Histogram.clearmemory handle 0 
    do! Histogram.startMeasurements handle histogram
    /// Recursive, checks every second.1
    do! Histogram.waitToFinishMeasurement handle 1000
    /// Sleep for acquisition and then stop measurements.  
    let sleep = Async.Sleep(10000) 
    do! Histogram.endMeasurements handle
    do! Histogram.getHistogram handle array 0      
    return array}

/// Takes data and adds to the array total
let rec liveCounts duration (array:int []) handle = asyncChoice{
    if duration > 0 then 
        let countData = Array.create 65535 0
        let! array = experiment handle 
        let total = Array.map2 (fun x y -> x + y) array countData
        do! liveCounts (duration - 1) total handle 
    else 
        do! PicoHarp.initialise.closeDevice handle }
       
/// Event for chart.
let countLine = new Event<int * int>()

/// Live charts a line chart.
let chart = countLine.Publish |> LiveChart.FastLineIncremental

initialise handle |> Async.RunSynchronously

chart |> Chart.Show

         


