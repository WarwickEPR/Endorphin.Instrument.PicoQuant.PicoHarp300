#r @"..\packages\log4net.2.0.3\lib\net40-full\log4net.dll"
#r @"..\packages\ExtCore.0.8.45\lib\net45\ExtCore.dll"
#r @"..\Endorphin.Core\bin\Debug\Endorphin.Core.dll"
#r "NationalInstruments.Common.dll"
#r "NationalInstruments.VisaNS.dll"
#r "FSharp.PowerPack.dll"
#r "bin/Debug/PicoHarp300.dll"
#r "../packages/FSharp.Charting.0.90.12/lib/net40/FSharp.Charting.dll"
#r "System.Windows.Forms.DataVisualization.dll"

open Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols
open System.Text
open System.Drawing
open System.Threading
open System.Windows.Forms
open Endorphin.Core
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

let form = new Form(Visible = true, TopMost = true, Width = 800, Height = 600)
let uiContext = SynchronizationContext.Current

let showChart data = async {
    do! Async.SwitchToContext uiContext // add the chart to the form using the UI thread context
    
    let chart = data   
                |> Observable.observeOn uiContext
                |> LiveChart.Bar
                |> Chart.WithXAxis(Title = "Time")
                |> Chart.WithYAxis(Title = "Counts")

    new ChartTypes.ChartControl(chart, Dock = DockStyle.Fill)
    |> form.Controls.Add
    
    do! Async.SwitchToThreadPool() } |> AsyncChoice.liftAsync

/// Initialises the PicoHarp to histogramming mode, sets bin width and overflow limits.
let initialise handle = asyncChoice{  
    let! opendev  = PicoHarp.initialise.openDevice handle   
    do! PicoHarp.initialise.initialiseMode handle Histogramming
    do! Histogram.setBinning handle histogram
    do! Histogram.stopOverflow handle histogram
    return opendev}

/// Runs the experiment and returns total histogram counts. 
let experiment handle = asyncChoice{ 
    let array = Array.create 65536 0
    do! Histogram.clearmemory handle 0   
    do! Histogram.startMeasurements handle histogram
    /// Sleep for acquisition and then stop measurements.  
    do! Histogram.waitToFinishMeasurement handle 1000
    do! Histogram.endMeasurements handle     
    do! Histogram.getHistogram handle array 0
    return array}

/// Converts type Resolution into type int.
let width = Quantities.resolutiontoWidth (histogram.Resolution) 

/// Bar chart event. 
let barEvent = new Event<(int * int)[]>()

/// Inital data, counts all zero.
let initalData = Array.create 1024 0

let storedArray = Array.create 1024 0 



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



/// Takes data and adds to the array total
let rec liveCounts duration (previousData:int[]) handle = asyncChoice{
    /// The array returned by the PicoHarp containing count data.
    let! picoHarpData = experiment handle 
    let buffer = compressArray (picoHarpData) storedArray 0 
    /// Array containing values for bar chart x axis.  
    let xAxis = Array.init 1024 (fun i -> i*width*64) 
    /// Sums picoHarpData counts with previously obtained data.
    let yAxis = Array.map2 (fun x y -> x + y) previousData buffer
    /// Combines xAxis and yAxis into an array of tuples.
    let barChart = Array.map2 (fun x y -> (x,y)) xAxis yAxis
    barEvent.Trigger barChart
    if duration > 0 then     
        do! Async.Sleep 100 |> AsyncChoice.liftAsync
        do! barEvent.Publish |> showChart  
        do! liveCounts (duration - 1) yAxis handle
    else 
        do! PicoHarp.initialise.closeDevice handle }

initialise handle |> Async.RunSynchronously
Async.StartWithContinuations(liveCounts 10 initalData handle , printfn "%A", ignore, ignore)



         


