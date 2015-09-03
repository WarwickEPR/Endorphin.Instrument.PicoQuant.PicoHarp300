#r "System.Windows.Forms.DataVisualization.dll"
#r "../packages/ExtCore.0.8.45/lib/net45/ExtCore.dll"
#r "../packages/log4net.2.0.3/lib/net40-full/log4net.dll"
#r @"..\packages\Rx-Core.2.2.5\lib\net45\System.Reactive.Core.dll"
#r @"..\packages\Rx-Interfaces.2.2.5\lib\net45\System.Reactive.Interfaces.dll"
#r @"..\packages\Rx-PlatformServices.2.2.5\lib\net45\System.Reactive.PlatformServices.dll"
#r @"..\packages\Rx-Linq.2.2.5\lib\net45\System.Reactive.Linq.dll"
#r "../packages/FSharp.Control.Reactive.3.2.0/lib/net40/FSharp.Control.Reactive.dll"
#r "../packages/FSharp.Charting.0.90.12/lib/net40/FSharp.Charting.dll"
#r "../Endorphin.Core/bin/Debug/Endorphin.Core.dll"
#r "bin/Debug/Endorphin.Instrument.PicoHarp300.dll"

open Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols
open System.Text
open System.Drawing
open System.Threading
open System.Runtime
open System.Windows.Forms
open Endorphin.Core
open ExtCore.Control
open Endorphin.Instrument.PicoHarp300
open FSharp.Charting
open FSharp.Control.Reactive
open System.Reactive.Linq
open System.Reactive
open System

//log4net.Config.BasicConfigurator.Configure()

/// Contains histogram parameters. 
let histogram = {
    Resolution      = Resolution_512ps;
    AcquisitionTime = Duration_s 1.0<s>;
    Overflow        = None;
    }

/// Finds the device index of the PicoHarp. 
let handle = PicoHarp.Initialise.picoHarp "1020854"

let refreshRate = 20.0
let windowSize = 200

let form = new Form(Visible = true, TopMost = true, Width = 20, Height = 600)
let uiContext = SynchronizationContext.Current

let showChart channel_0 channel_1 (channel:InputChannel) = async {
    // Charts both channel one and channel two count rates.  
    if channel = Both then
        do! Async.SwitchToContext uiContext // add the chart to the form using the UI thread context
        
        let chart3 =    
            Chart.Combine [
                channel_0
                |> Observable.bufferMapiCountOverlapped windowSize (fun i x -> (i, x))
                |> Observable.sample (TimeSpan.FromMilliseconds refreshRate)
                |> Observable.observeOnContext uiContext
                |> LiveChart.FastLine

                channel_1
                |> Observable.bufferMapiCountOverlapped windowSize (fun i x -> (i, x))
                |> Observable.sample (TimeSpan.FromMilliseconds refreshRate)
                |> Observable.observeOnContext uiContext
                |> LiveChart.FastLine ]
            |> Chart.WithXAxis(Title = "Time")
            |> Chart.WithYAxis(Title = "Counts")
        
        new ChartTypes.ChartControl(chart3, Dock = DockStyle.Fill)
        |> form.Controls.Add
    // Charts channel one's count rate. 
    elif channel = Channel1 then
        let chart1 = channel_1
                     |> Observable.bufferMapiCountOverlapped windowSize (fun i x -> (i, x))
                     |> Observable.sample (TimeSpan.FromMilliseconds refreshRate)
                     |> Observable.observeOnContext uiContext
                     |> LiveChart.FastLine
                     |> Chart.WithXAxis(Title = "Time")
                     |> Chart.WithYAxis(Title = "Count Rate")
        
        new ChartTypes.ChartControl(chart1, Dock = DockStyle.Fill)
        |> form.Controls.Add
    // Charts channel two's count rate.
    elif channel = Channel0 then
        let chart2 = channel_0
                     |> Observable.bufferMapiCountOverlapped windowSize (fun i x -> (i, x))
                     |> Observable.sample (TimeSpan.FromMilliseconds refreshRate)
                     |> Observable.observeOnContext uiContext
                     |> LiveChart.FastLine
                     |> Chart.WithXAxis(Title = "Time")
                     |> Chart.WithYAxis(Title = "Counts")
        
        new ChartTypes.ChartControl(chart2, Dock = DockStyle.Fill)
        |> form.Controls.Add
    else
        failwithf "Invalid channel: %A" channel
    do! Async.SwitchToThreadPool()} 
        

/// Initialise the PicoHarp to histogramming mode, sets bin resolution and overflow limit.
let initialise = asyncChoice{  
    let! picoHarp = PicoHarp.Initialise.initialise "1020854" Histogramming 
    do! Histogram.setBinning picoHarp histogram
    do! Histogram.stopOverflow picoHarp histogram
    return picoHarp}

/// Events for charting.
let channelZeroEvent = new Event<int >()
let channelOneEvent = new Event<int>()

/// Generates chart data. 
let rec liveCounts (duration:int) (time:int) handle = asyncChoice {
     // let! statments retrievethe count rates for channel one and channel two. 
     let! channel_0 = PicoHarp.Query.getCountRate handle 0
     let! channel_1 = PicoHarp.Query.getCountRate handle 1
     // Trigger both channels events. 
     channelZeroEvent.Trigger channel_0
     channelOneEvent.Trigger  channel_1
     if duration > 0 then
        do! Async.Sleep 100 |> AsyncChoice.liftAsync
        do! liveCounts  (duration - 1) (time + 1) handle
     else 
        do! PicoHarp.Initialise.closeDevice handle }

let experiment duration handle = asyncChoice {
     // let statments publish both channels events. 
     let publish_0 = channelZeroEvent.Publish
     let publish_1 = channelOneEvent.Publish
     do! showChart publish_0 publish_1 Both |> AsyncChoice.liftAsync
     do! liveCounts duration 0 handle }

initialise |> Async.RunSynchronously
Async.StartWithContinuations(experiment 10000000 handle , printfn "%A", ignore, ignore)



