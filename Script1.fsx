#r @"..\packages\log4net.2.0.3\lib\net40-full\log4net.dll"
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

let form = new Form(Visible = true, TopMost = true, Width = 800, Height = 600)
let uiContext = SynchronizationContext.Current

let countEvent = new Event<int*int>()

let showChart data = async {
    do! Async.SwitchToContext uiContext // add the chart to the form using the UI thread context
    
    let chart = data   
                |> Observable.observeOn uiContext
                |> LiveChart.FastLineIncremental
                |> Chart.WithXAxis(Title = "Time")
                |> Chart.WithYAxis(Title = "Counts")


    new ChartTypes.ChartControl(chart, Dock = DockStyle.Fill)
    |> form.Controls.Add
    
    do! Async.SwitchToThreadPool() } |> AsyncChoice.liftAsync

/// Contains histogram parameters. 
let histogram = {
    Resolution      = Resolution_512ps;
    AcquisitionTime = Duration_s 1.0<s>;
    Overflow        = None;
    }

/// Runs the experiment and returns total histogram counts. 
let experiment number = 
    let array = Array.create 65535 number
    let total = Array.sum array
    total 
 
/// Generates chart data. 
let rec liveCounts (duration:int) (time:int) = asyncChoice {
     if duration > 0 then
        let count = experiment duration 
        countEvent.Trigger (time, count)
        do! countEvent.Publish |> showChart
        do! liveCounts  (duration - 1) (time + 1)}  

liveCounts 10 0 |> Async.RunSynchronously



