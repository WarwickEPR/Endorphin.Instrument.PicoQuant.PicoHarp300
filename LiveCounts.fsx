#r @"..\packages\log4net.2.0.3\lib\net40-full\log4net.dll"
#r @"..\packages\ExtCore.0.8.45\lib\net45\ExtCore.dll"
#r @"..\Endorphin.Core\bin\Debug\Endorphin.Core.dll"
#r "NationalInstruments.Common.dll"
#r "NationalInstruments.VisaNS.dll"
#r "../packages/FSPowerPack.Core.Community.2.0.0.0/Lib/Net40/FSharp.PowerPack.dll"
#r "bin/Debug/Endorphin.Instrument.PicoHarp300.dll"
#r "../packages/FSharp.Charting.0.90.12/lib/net40/FSharp.Charting.dll"
#r "System.Windows.Forms.DataVisualization.dll"
#r "../packages/Rx-Core.2.2.5/lib/net45/System.Reactive.Core.dll"
#r "../packages/Rx-Interfaces.2.2.5/lib/net45/System.Reactive.Interfaces.dll"
#r "../packages/Rx-Linq.2.2.5/lib/net45/System.Reactive.Linq.dll"
#r "../packages/FSharp.Control.Reactive.3.2.0/lib/net40/FSharp.Control.Reactive.dll"

open System
open System.Drawing
open System.Threading
open System.Windows.Forms
open System.Text
open System.Reactive.Linq 
open Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols
open Endorphin.Core
open ExtCore.Control
open Endorphin.Instrument.PicoHarp300
open FSharp.Charting
//open FSharp.Control.Reactive

log4net.Config.BasicConfigurator.Configure()

/// Contains histogram parameters. 
let histogram = {
    Resolution      = Resolution_512ps;
    AcquisitionTime = Duration_s 0.25<s>;
    Overflow        = None;
    }

/// Finds the device index of the PicoHarp. 
let handle = PicoHarp.Initialise.picoHarp "1020854"

let form = new Form(Visible = true, TopMost = true, Width = 800, Height = 600)
let uiContext = SynchronizationContext.Current

let showChart data = async {
    do! Async.SwitchToContext uiContext // add the chart to the form using the UI thread context
    
    let chart =   
                Observable.ObserveOn (data, uiContext)
                |> LiveChart.FastLineIncremental
                |> Chart.WithXAxis(Title = "Time")
                |> Chart.WithYAxis(Title = "Counts")

    new ChartTypes.ChartControl(chart, Dock = DockStyle.Fill)
    |> form.Controls.Add
    
    do! Async.SwitchToThreadPool() } |> AsyncChoice.liftAsync

/// Initialise the PicoHarp to histogramming mode, sets bin resolution and overflow limit.
let initialise = asyncChoice{  
    let! picoHarp = PicoHarp.Initialise.initialise "1020854" Histogramming 
    do! Histogram.setBinning picoHarp histogram
    do! Histogram.stopOverflow picoHarp histogram
    return picoHarp}

/// Runs the experiment and returns total histogram counts. 
let experiment handle = asyncChoice {
    let array = Array.create 65535 0
    do! Histogram.clearmemory handle
    do! Histogram.startMeasurement handle histogram
    /// Sleep for acquisition and then stop measurements.  
    do! Histogram.waitToFinishMeasurement handle 500
    do! Histogram.endMeasurement handle     
    do! Histogram.getHistogram handle array 0
    let total = Array.sum array
    return total }

/// Event for chart.
let countEvent = new Event<int * int>()

/// Generates chart data. 
let rec liveCounts (duration:int) (time:int) handle = asyncChoice {
     let! count = experiment handle 
     countEvent.Trigger (time, count)
     do! countEvent.Publish |> showChart
     if duration > 0 then
        do! Async.Sleep 100 |> AsyncChoice.liftAsync
        do! liveCounts  (duration - 1) (time + 1) handle
     else 
        do! PicoHarp.Initialise.closeDevice handle }

initialise |> Async.RunSynchronously
Async.StartWithContinuations(liveCounts 10000 0 handle , printfn "%A", ignore, ignore)



