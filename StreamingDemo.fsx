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
let uiContext = SynchronizationContext.Current
let cts = new CancellationTokenSource()

form.Closed |> Observable.add (fun _ -> cts.Cancel())

let group_fold key value fold acc seq =
    seq |> List.groupBy key 
        |> List.map (fun (key, seq) -> (key, seq |> List.map value |> List.fold fold acc))

let collateCounts (histogramList : (int * int) list) (histogram : (int * int) list) =
    match histogramList with
    | [] -> group_fold fst snd (+) 0 histogram
    | histogramList -> group_fold fst snd (+) 0 <| List.append histogramList histogram

let showHistogramChart acquisition = AsyncChoice.liftAsync <| async {
    do! Async.SwitchToContext uiContext

    let chart = 
        Streaming.Acquisition.HistogramsAvailable acquisition
        |> Observable.observeOnContext uiContext
        |> Observable.map (fun (histogram) -> List.map (fun (bin, counts) -> (100*bin, counts)) <| histogram.Histogram)
        |> Observable.scan collateCounts
        |> LiveChart.Column
        |> Chart.WithXAxis(Title = "Time after marker (ns)")
        |> Chart.WithYAxis(Title = "Counts")

    new ChartTypes.ChartControl(chart, Dock = DockStyle.Fill)
    |> form.Controls.Add

    do! Async.SwitchToThreadPool() }  

let printStatusUpdates acquisition =
    Streaming.Acquisition.status acquisition
    |> Observable.add (printfn "%A")

let experiment = asyncChoice {
    let picoHarp = PicoHarp.Initialise.picoHarp "1020854"
    // do! Histogram.clearmemory picoHarp
    do! PicoHarp.Initialise.initialiseMode picoHarp T2

    try
        let acquisition = Streaming.Acquisition.create picoHarp (Streaming.Parameters.create (Duration_ns 1000.0<ns>) (Duration_ns 100000.0<ns>))
        do! showHistogramChart acquisition
        printStatusUpdates acquisition

        let! acquisitionHandle = Streaming.Acquisition.start acquisition

        do! Async.Sleep 3000 |> AsyncChoice.liftAsync
        do! Streaming.Acquisition.stopAndFinish acquisitionHandle

    finally Async.StartImmediate <| async {
        let! closeResult = PicoHarp.Initialise.closeDevice picoHarp 
        match closeResult with
        | Success () -> printfn "Successfully closed connection to PicoHarp."
        | Failure f  -> printfn "Failed to close connection to PicoHarp due to error: %s" f } }

Async.StartWithContinuations(experiment,
    (function
    | Success () -> printfn "Successfully completed experiment."
    | Failure f  -> printfn "Failed to complete experiment due to error: %s" f),
    ignore, 
    (fun _ -> printfn "Cancelled experiment."), cts.Token)
