﻿#r @"..\packages\log4net.2.0.3\lib\net40-full\log4net.dll"
#r @"..\packages\ExtCore.0.8.45\lib\net45\ExtCore.dll"
#r @"..\Endorphin.Core\bin\Debug\Endorphin.Core.dll"
#r "NationalInstruments.Common.dll"
#r "NationalInstruments.VisaNS.dll"
#r "FSharp.PowerPack.dll"
#r "bin/Debug/PicoHarp300.dll"
#r "../packages/FSharp.Charting.0.90.12/lib/net40/FSharp.Charting.dll"
#r "System.Windows.Forms.DataVisualization.dll"

open System
open System.Drawing
open System.Threading
open System.Windows.Forms
open System.Text
open Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols
open Endorphin.Core
open ExtCore.Control
open Endorphin.Instrument.PicoHarp300
open FSharp.Charting

log4net.Config.BasicConfigurator.Configure()

/// Contains histogram parameters. 
let histogram = {
    Resolution      = Resolution_512ps;
    AcquisitionTime = Duration_s 1.0<s>;
    Overflow        = None;
    }

/// Finds the device index of the PicoHarp. 
let handle = PicoHarp.initialise.picoHarp "1020854"

/// Initialise the PicoHarp to histogramming mode, sets bin resolution and overflow limit.
let initialise handle = asyncChoice{  
    let! opendev  = PicoHarp.initialise.openDevice handle   
    do! PicoHarp.initialise.initialiseMode handle Histogramming
    do! Histogram.setBinning handle histogram
    do! Histogram.stopOverflow handle histogram
    return opendev}

/// Runs the experiment and returns total histogram counts. 
let experiment handle = asyncChoice {
    let array = Array.create 65535 0
    do! Histogram.clearmemory handle 0 
    do! Histogram.startMeasurements handle histogram
    /// Sleep for acquisition and then stop measurements.  
    do! Histogram.waitToFinishMeasurement handle 100
    do! Histogram.endMeasurements handle     
    do! Histogram.getHistogram handle array 0
    let total = Array.sum array
    return total }

/// Event for chart.
let countLine = new Event<int * int>()

/// Live charts a line chart.
let chart = countLine.Publish |> LiveChart.FastLineIncremental

/// Generates chart data. 
let rec liveCounts (duration:int) (time:int) handle = asyncChoice {
     let! count = experiment handle 
     countLine.Trigger (time, count)
     if duration > 0 then
        do! liveCounts  (duration - 1) (time + 1) handle
     else 
        do! PicoHarp.initialise.closeDevice handle }


initialise handle |> Async.RunSynchronously
liveCounts 10 0 handle |> Async.RunSynchronously
chart |> Chart.Show
