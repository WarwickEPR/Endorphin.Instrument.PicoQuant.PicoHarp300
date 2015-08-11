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


//log4net.Config.BasicConfigurator.Configure()
//
//let form = new Form(Visible = true, TopMost = true, Width = 800, Height = 600)
//let uiContext = SynchronizationContext.Current
//
//let countEvent = new Event<int*int>()
//
//let showChart data = async {
//    do! Async.SwitchToContext uiContext // add the chart to the form using the UI thread context
//    
//    let chart = data   
//                |> Observable.observeOn uiContext
//                |> LiveChart.FastLineIncremental
//                |> Chart.WithXAxis(Title = "Time")
//                |> Chart.WithYAxis(Title = "Counts")
//
//
//    new ChartTypes.ChartControl(chart, Dock = DockStyle.Fill)
//    |> form.Controls.Add
//    
//    do! Async.SwitchToThreadPool() } |> AsyncChoice.liftAsync
//
///// Contains histogram parameters. 
//let histogram = {
//    Resolution      = Resolution_512ps;
//    AcquisitionTime = Duration_s 1.0<s>;
//    Overflow        = None;
//    }
//
///// Runs the experiment and returns total histogram counts. 
//let experiment number = 
//    let array = Array.create 65535 number
//    let total = Array.sum array
//    total 
// 
///// Generates chart data. 
//let rec liveCounts (duration:int) (time:int) = asyncChoice {
//     if duration > 0 then
//        let count = experiment duration 
//        countEvent.Trigger (time, count)
//        do! countEvent.Publish |> showChart
//        do! liveCounts  (duration - 1) (time + 1)}  
//
//liveCounts 10 0 |> Async.RunSynchronously


let array = Array.init 65536 (fun i -> i) 
let shortArray = Array.create 1024 0 
//let marker = 63
//
//let multipleof number factor = if int(number/factor)*factor = number then true
//                                   else false   
//let buffer = Array.mapi (fun i elem -> if (multipleof (i - marker) 64 || i = marker) then elem else -1) array
//                   |> Array.filter (fun elem -> elem <> -1)
//Array.length buffer
//Array.map2 (fun x y -> x + y) buffer shortArray

// Finds if number is a multiple of factor. 
let multipleof number factor = if int(number/factor)*factor = number then true
                               else false    

// Sums array elements in blocks of 64 to compress a 65536 element array into a 1024 elemnt array. 
let rec private compressArray (array:int[]) (storedArray:int[]) (marker) =
    // Marker only needs to go up to 64. 
    if marker < 64 then
        // Leaves element marker and every 16th afterwards unchanged, sets all others to zero. 
        // Array.filter removes all zero elements. 
        let buffer = Array.mapi (fun i elem -> if (multipleof (i - marker) 64 || i = marker) then elem else -1) array
                     |> Array.filter (fun elem -> elem <> -1)
        // sums buffer elements into stored data. 
        let newArray = Array.map2 (fun x y -> x + y) buffer storedArray
        compressArray array newArray (marker + 1)
    else 
        storedArray

compressArray array shortArray 0