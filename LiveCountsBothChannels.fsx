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

let form = new Form(Visible = true, TopMost = true, Width = 800, Height = 600)
let uiContext = SynchronizationContext.Current


let showChart channel_0 channel_1 = async {
    
    do! Async.SwitchToContext uiContext // add the chart to the form using the UI thread context
    
    let chart =    
        Chart.Combine [
            channel_0
            |> Observable.observeOn uiContext
            |> LiveChart.FastLineIncremental
            
            channel_1
            |> Observable.observeOn uiContext
            |> LiveChart.FastLineIncremental ]
        |> Chart.WithXAxis(Title = "Time")
        |> Chart.WithYAxis(Title = "Counts")
    
    new ChartTypes.ChartControl(chart, Dock = DockStyle.Fill)
    |> form.Controls.Add
    
    do! Async.SwitchToThreadPool()} 

/// Initialise the PicoHarp to histogramming mode, sets bin resolution and overflow limit.
let initialise handle = asyncChoice{  
    let! opendev  = PicoHarp.initialise.openDevice handle   
    do! PicoHarp.initialise.initialiseMode handle Histogramming
    do! Histogram.setBinning handle histogram
    do! Histogram.stopOverflow handle histogram
    return opendev}

/// Events for charting.
let channelZeroEvent = new Event<int * int>()
let channelOneEvent = new Event<int * int>()

/// Generates chart data. 
let rec liveCounts (duration:int) (time:int) handle = asyncChoice {
     let! channel_0 = Histogram.getCountRate handle 0
     let! channel_1 = Histogram.getCountRate handle 1
     channelZeroEvent.Trigger (time, channel_0)
     channelOneEvent.Trigger (time, channel_1)
     let publish_0 = channelZeroEvent.Publish
     let publish_1 = channelOneEvent.Publish
     do! showChart publish_0 publish_1 |> AsyncChoice.liftAsync
     if duration > 0 then
        do! Async.Sleep 100 |> AsyncChoice.liftAsync
        do! liveCounts  (duration - 1) (time + 1) handle
     else 
        do! PicoHarp.initialise.closeDevice handle }

initialise handle |> Async.RunSynchronously
Async.StartWithContinuations(liveCounts 30 0 handle , printfn "%A", ignore, ignore)



