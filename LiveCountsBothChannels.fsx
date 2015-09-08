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

let mutable mutableString : string = "Both"
let baseline = 0
let baselineString = string(baseline)

let form = new Form(Text = baselineString, Visible = true, Width = 600, Height = 600)
let uiContext = SynchronizationContext.Current

let click0 = new Event<_>()
let click1 = new Event<_>()
let clickBoth = new Event<_>()

let toolBar = new ToolBar ()
let button1 = new ToolBarButton(Text = "Channel 0" )
let button2 = new ToolBarButton(Text = "Channel 1" )
let button3 = new ToolBarButton(Text = "Both")
let button4 = new ToolBarButton(Text = "Close")

toolBar.Buttons.AddRange [|button1; button2; button3; button4|]

toolBar.ButtonClick.Add (fun button -> if toolBar.Buttons.IndexOf (button.Button) = 0 then click0.Trigger()
                                       elif toolBar.Buttons.IndexOf (button.Button) = 1 then click1.Trigger() 
                                       elif toolBar.Buttons.IndexOf (button.Button) = 2 then clickBoth.Trigger() 
                                       elif toolBar.Buttons.IndexOf (button.Button) = 3 then let close picoHarp300 = asyncChoice{
                                                                                                do! PicoHarp.Initialise.closeDevice picoHarp300
                                                                                                }
                                                                                             close handle |> Async.RunSynchronously |> ignore 
                                                                                             form.Close ()
                                       )  
click0.Publish.Add(fun _ -> mutableString <- "Channel0")
click1.Publish.Add(fun _ -> mutableString <- "Channel1")
clickBoth.Publish.Add(fun _ -> mutableString <- "Both")

form.Controls.Add toolBar  

let showChart channel_0 channel_1 (channel) (initialcount: int*int) = async {
    // Charts both channel one and channel two count rates.  
    if channel = Both then
            do! Async.SwitchToContext uiContext // add the chart to the form using the UI thread context
            let chart3 =    
                Chart.Combine [
                    channel_0
                    |> Observable.bufferMapiCountOverlapped windowSize (fun i x -> (i, x - (baseline)))
                    |> Observable.sample (TimeSpan.FromMilliseconds refreshRate)
                    |> Observable.observeOnContext uiContext
                    |> LiveChart.FastLine

                    channel_1
                    |> Observable.bufferMapiCountOverlapped windowSize (fun i x -> (i, x - (baseline)))
                    |> Observable.sample (TimeSpan.FromMilliseconds refreshRate)
                    |> Observable.observeOnContext uiContext
                    |> LiveChart.FastLine]
                |> Chart.WithXAxis(Title = "Time")
                |> Chart.WithYAxis(Title = "Count Rate", LabelStyle = ChartTypes.LabelStyle.Create(Format="0.###E+0"))
            
            new ChartTypes.ChartControl(chart3, Dock = DockStyle.Fill)
            |> form.Controls.Add
        // Charts channel one's count rate. 
        elif channel = Channel1 then
            let chart1 = channel_1
                         |> Observable.bufferMapiCountOverlapped windowSize (fun i x -> (i, (x - baseline)))
                         |> Observable.sample (TimeSpan.FromMilliseconds refreshRate)
                         |> Observable.observeOnContext uiContext
                         |> LiveChart.FastLine
                         |> Chart.WithXAxis(Title = "Time")
                         |> Chart.WithYAxis(Title = "Count Rate", LabelStyle = ChartTypes.LabelStyle.Create(Format="0.###E+0"))
            
            new ChartTypes.ChartControl(chart1, Dock = DockStyle.Fill)
            |> form.Controls.Add
        // Charts channel two's count rate.
        elif channel = Channel0 then
            let chart2 = channel_0
                         |> Observable.bufferMapiCountOverlapped windowSize (fun i x -> (i, (x - baseline)))
                         |> Observable.sample (TimeSpan.FromMilliseconds refreshRate)
                         |> Observable.observeOnContext uiContext
                         |> LiveChart.FastLine
                         |> Chart.WithXAxis(Title = "Time")
                         |> Chart.WithYAxis(Title = "Count Rate", LabelStyle = ChartTypes.LabelStyle.Create(Format="0.###E+0"))
            
            new ChartTypes.ChartControl(chart2, Dock = DockStyle.Fill)
            |> form.Controls.Add
    else
        failwithf "Invalid channel: %A" channel
    do! Async.SwitchToThreadPool()} 
        

/// Events for charting.
let channelZeroEvent = new Event<int >()
let channelOneEvent = new Event<int>()

let stringtoChannel = function
    | "Channel0" -> Channel0
    | "Channel1" -> Channel1
    | "Both"     -> Both
    | _ -> failwithf "Not a channel."


let channeltoString = function
    | Channel0 -> "Channe0"
    | Channel1 -> "Channel"
    | Both     -> "Both"
    | _ -> failwithf "Not a channel."

/// Generates chart data. 
let rec liveCounts (duration:int) (time:int) handle (channel: string) one two= asyncChoice {
     if mutableString = channel then  
        // let! statments retrievethe count rates for channel one and channel two. 
        let! channel_0 = PicoHarp.Query.getCountRate handle 0
        let! channel_1 = PicoHarp.Query.getCountRate handle 1
        // Trigger both channels events. 
        do     
            if channel = "Channel1" then
                channelZeroEvent.Trigger channel_1
                channelOneEvent.Trigger channel_1
            elif channel = "Channel0" then 
                channelZeroEvent.Trigger channel_0
                channelOneEvent.Trigger channel_0
            else 
                channelZeroEvent.Trigger channel_0
                channelOneEvent.Trigger channel_1
        if duration > 0 then
           do! Async.Sleep 100 |> AsyncChoice.liftAsync
           do! liveCounts  (duration - 1) (time + 1) handle channel one two
        else 
           do! PicoHarp.Initialise.closeDevice handle 
    else 
        // let! statments retrievethe count rates for channel one and channel two. 
        let! channel_0 = PicoHarp.Query.getCountRate handle 0
        let! channel_1 = PicoHarp.Query.getCountRate handle 1
        // Trigger both channels events.
        let newChannel = stringtoChannel mutableString
        do     
            if channel = "Channel1" then
                channelZeroEvent.Trigger channel_1
                channelOneEvent.Trigger channel_1
            elif channel = "Channel0" then 
                channelZeroEvent.Trigger channel_0
                channelOneEvent.Trigger channel_0
            else 
                channelZeroEvent.Trigger channel_0
                channelOneEvent.Trigger channel_1
        if duration > 0 then
           do! Async.Sleep 100 |> AsyncChoice.liftAsync
           do! liveCounts  (duration - 1) (time + 1) handle mutableString one two
        else 
           do! PicoHarp.Initialise.closeDevice handle 
    }

        
/// Initialise the PicoHarp to histogramming mode, sets bin resolution and overflow limit.
let initialise = asyncChoice{  
    let! picoHarp = PicoHarp.Initialise.initialise "1020854" Histogramming 
    do! Histogram.setBinning picoHarp histogram
    do! Histogram.stopOverflow picoHarp histogram
    return picoHarp}

let rec initial channel = asyncChoice{ 
   Async.Sleep 2000 |> Async.RunSynchronously 
   let! initialZeroCount  = PicoHarp.Query.getCountRate handle 0 
   let! initialFirstCount = PicoHarp.Query.getCountRate handle 0 
   if initialZeroCount = 0 && channel = Channel0 then
      Async.Sleep 500 |> Async.RunSynchronously
      return! initial channel
   elif initialFirstCount = 0 && channel = Channel1 then
      Async.Sleep 500 |> Async.RunSynchronously
      return! initial channel
   else return (initialZeroCount, initialFirstCount)
   }     

let experiment duration channel = asyncChoice {
     let! handle = initialise 
     let publish_0 = channelZeroEvent.Publish
     let publish_1 = channelOneEvent.Publish
     let! initialCount = initial channel
     do! showChart publish_0 publish_1 channel initialCount |> AsyncChoice.liftAsync
     let stringChannel = channeltoString channel
     do! liveCounts duration 0 handle stringChannel publish_0 publish_1}

Async.StartWithContinuations(experiment 10000000 Both , printfn "%A", ignore, ignore)



