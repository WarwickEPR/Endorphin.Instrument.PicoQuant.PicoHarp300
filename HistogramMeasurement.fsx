#r @"..\packages\log4net.2.0.3\lib\net40-full\log4net.dll"
#r @"..\packages\ExtCore.0.8.45\lib\net45\ExtCore.dll"
#r @"..\Endorphin.Core\bin\Debug\Endorphin.Core.dll"
#r "NationalInstruments.Common.dll"
#r "NationalInstruments.VisaNS.dll"
#r "FSharp.PowerPack.dll"
#r "bin/Debug/PicoHarp300.dll"
#r "../packages/FSharp.Charting.0.90.9/lib/net40/FSharp.Charting.dll"
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
    AcquisitionTime = Duration_s 5.0<s>;
    Overflow        = None;
    }

/// List to store counts per histogram. 
/// let counts = []
/// Finds the device index of the PicoHarp. 
let handle = PicoHarp.initialise.picoHarp "1020854"

/// Initialise the PicoHarp to histogramming mode, sets bin resolution and overflow limit.
let initialise handle = asyncChoice{  
    let! opendev  = PicoHarp.initialise.openDevice handle   
    do! PicoHarp.initialise.initialiseMode handle Histogramming
    do! Histogram.setBinning handle histogram
    do! Histogram.stopOverflow handle histogram
    return opendev}

/// Runs the experiment and returns total histogram counts. 
let experiment handle = asyncChoice{ 
    let array = Array.create 65535 0
    do! Histogram.clearmemory handle 0   
    do! Histogram.startMeasurements handle histogram
    /// Sleep for acquisition and then stop measurements.  
    do! Histogram.waitToFinishMeasurement handle 1000
    do! Histogram.endMeasurements handle     
    do! Histogram.getHistogram handle array 0
    let sum = Array.sum array
    return sum}

initialise handle |> Async.RunSynchronously
experiment handle |> Async.RunSynchronously
                  |> sprintf "%A"
                 


