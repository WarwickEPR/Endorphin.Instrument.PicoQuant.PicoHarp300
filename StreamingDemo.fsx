#r "../packages/ExtCore.0.8.45/lib/net45/ExtCore.dll"
#r "../Endorphin.Core/bin/Debug/Endorphin.Core.dll"
#r "../packages/FSharp.Charting.0.90.12/lib/net40/FSharp.Charting.dll"
#r "bin/Debug/Endorphin.Instrument.PicoHarp300.dll"
#r "System.Windows.Forms.DataVisualization.dll"
#r "../packages/Rx-Core.2.2.5/lib/net45/System.Reactive.Core.dll"
#r "../packages/Rx-Interfaces.2.2.5/lib/net45/System.Reactive.Interfaces.dll"
#r "../packages/Rx-Linq.2.2.5/lib/net45/System.Reactive.Linq.dll"
#r "../packages/FSharp.Control.Reactive.3.2.0/lib/net40/FSharp.Control.Reactive.dll"
#r "../packages/log4net.2.0.3/lib/net40-full/log4net.dll"

open Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols
open System
open System.Threading
open System.Windows.Forms

open ExtCore.Control
open FSharp.Charting
open FSharp.Control.Reactive

open Endorphin.Core
open Endorphin.Instrument.PicoHarp300

// log4net.Config.BasicConfigurator.Configure()

let form = new Form(Visible = true, TopMost = true, Width = 800, Height = 600)
let form2 = new Form (Visible=true, Width=600, Height=800)
let uiContext = SynchronizationContext.Current
let cts = new CancellationTokenSource()

let expTime = 5.0<s>
let binTime = 0.01<ms>
let startFreq = 2.85
let endFreq = 2.89
let spanFreq = endFreq - startFreq
let numPoints = (Seconds.toMilliseconds expTime) / binTime

form.Closed |> Observable.add (fun _ -> cts.Cancel())

let group_fold key value fold acc seq =
    seq |> List.groupBy key 
        |> List.map (fun (key, seq) -> (key, seq |> List.map value |> List.fold fold acc))

let collateCounts histogramList histogram =
    match histogramList with
    | [] -> group_fold fst snd (+) 0 histogram
    | histogramList -> group_fold fst snd (+) 0 <| List.append histogramList histogram

let showHistogramChart acquisition = async {
    do! Async.SwitchToContext uiContext

    let chart = 
        Streaming.Acquisition.Histogram.HistogramsAvailable acquisition
        |> Observable.observeOnContext uiContext
        |> Observable.map (fun (histogram) -> List.map (fun (bin, counts) -> (startFreq + (spanFreq / numPoints) * float (bin + 1), counts)) <| histogram.Histogram)
        |> Observable.scan collateCounts
        |> LiveChart.Line
        |> Chart.WithXAxis(Title = "Frequency (GHz)")
        |> Chart.WithYAxis(Title = "Counts")

    new ChartTypes.ChartControl(chart, Dock = DockStyle.Fill)
    |> form.Controls.Add

    do! Async.SwitchToThreadPool() }  

let showLiveChart acquisition = async {
    do! Async.SwitchToContext uiContext
    
    let chart = Streaming.Acquisition.LiveCounts.LiveCountAvailable acquisition
                |> Observable.observeOnContext uiContext
                |> Observable.mapi (fun i x -> (i, x))
                |> LiveChart.FastLineIncremental
                |> Chart.WithXAxis (Title = "Time")
                |> Chart.WithYAxis (Title = "Counts")

    new ChartTypes.ChartControl(chart, Dock = DockStyle.Fill)
    |> form2.Controls.Add

    do! Async.SwitchToThreadPool() }

let printStatusUpdates acquisition =
    Streaming.Acquisition.status acquisition
    |> Observable.add (printfn "%A")

let experiment = async {
    //let picoHarp = PicoHarp.Initialise.picoHarp "1020854"
    // do! Histogram.clearmemory picoHarp
    //do! PicoHarp.Initialise.initialiseMode picoHarp T2
    let! picoHarp = PicoHarp.Initialise.initialise "1020854" Model.SetupInputChannel.T2

    let streamingAcquisition = Streaming.Acquisition.create picoHarp
    let streamingAcquisitionHandle = Streaming.Acquisition.startWithCancellationToken streamingAcquisition cts.Token

    try
        let histogramParameters = Streaming.Acquisition.Histogram.Parameters.create (Duration_ms 100.<ms>) (Duration_ms 10000.<ms>) Model.SetupTTTRMeasurements.Marker1
        let histogramAcquisition = Streaming.Acquisition.Histogram.create streamingAcquisition histogramParameters// Streaming.Acquisition.create picoHarp (Streaming.Parameters.create (Duration_ms binTime) (Duration_s expTime))
        do! showHistogramChart histogramAcquisition
        printStatusUpdates streamingAcquisition

        do! Async.Sleep 200000
        do Streaming.Acquisition.stop streamingAcquisitionHandle

    finally 
        async { do! PicoHarp.Initialise.closeDevice picoHarp } |> Async.RunSynchronously }

Async.StartWithContinuations(experiment,
    (fun () -> printfn "%s" "All good here"),
    (fun exn -> printfn "%s %s" "Something went wrong boss:" exn.Message),
    (fun _ -> printfn "%s" "Cancelled"), cts.Token)