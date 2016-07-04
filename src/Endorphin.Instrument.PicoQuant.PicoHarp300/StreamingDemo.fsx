// Copyright (c) University of Warwick. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE.txt in the project root for license information.

#I "../../packages/"

#r "Endorphin.Core/lib/net452/Endorphin.Core.dll"
#r "FSharp.Charting/lib/net40/FSharp.Charting.dll"
#r "bin/Release/Endorphin.Instrument.PicoQuant.PicoHarp300.dll"
#r "System.Windows.Forms.DataVisualization.dll"
#r "Rx-Core/lib/net45/System.Reactive.Core.dll"
#r "Rx-Interfaces/lib/net45/System.Reactive.Interfaces.dll"
#r "Rx-Linq/lib/net45/System.Reactive.Linq.dll"
#r "FSharp.Control.Reactive/lib/net40/FSharp.Control.Reactive.dll"
#r "log4net/lib/net40-full/log4net.dll"

open Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols
open System
open System.Threading
open System.Windows.Forms

open FSharp.Charting
open FSharp.Control.Reactive

open Endorphin.Core
open Endorphin.Instrument.PicoQuant.PicoHarp300


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
        |> Observable.mapi (fun i histogram -> List.map (fun (bin, counts) -> (startFreq + (spanFreq / numPoints) * float (bin + 1), counts)) <| histogram.Histogram)
        //|> Observable.scan collateCounts
        |> LiveChart.Line
        |> Chart.WithXAxis(Title = "Time")
        |> Chart.WithYAxis(Title = "Histocounts")

    new ChartTypes.ChartControl(chart, Dock = DockStyle.Fill)
    |> form.Controls.Add

    do! Async.SwitchToThreadPool() }

let showLiveChart acquisition = async {
    do! Async.SwitchToContext uiContext

    let chart = Streaming.Acquisition.LiveCounts.LiveCountAvailable acquisition
                |> Observable.observeOnContext uiContext
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
    let! picoHarp = PicoHarp.Initialise.initialise "1020854" Model.SetupInputChannel.T2

    let streamingAcquisition = Streaming.Acquisition.create picoHarp
    let streamingAcquisitionHandle = Streaming.Acquisition.startWithCancellationToken streamingAcquisition cts.Token

    try
        let histogramParameters = Streaming.Acquisition.Histogram.Parameters.create (Duration_ms 1.<ms>) (Duration_ms 80.<ms>) Marker2
        let histogramAcquisition = Streaming.Acquisition.Histogram.create streamingAcquisition histogramParameters
        do! showHistogramChart histogramAcquisition

        let liveCountAcquisition = Streaming.Acquisition.LiveCounts.create streamingAcquisition
        do! showLiveChart liveCountAcquisition

        printStatusUpdates streamingAcquisition

        do! Async.Sleep 200000
        do Streaming.Acquisition.stop streamingAcquisitionHandle

    finally
        async { do! PicoHarp.Initialise.closeDevice picoHarp } |> Async.RunSynchronously }

Async.StartWithContinuations(experiment,
    (fun () -> printfn "%s" "All good here"),
    (fun exn -> printfn "%s %s" "Something went wrong boss:" exn.Message),
    (fun _ -> printfn "%s" "Cancelled"), cts.Token)