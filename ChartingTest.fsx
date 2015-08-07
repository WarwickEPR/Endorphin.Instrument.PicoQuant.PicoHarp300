#r "../packages/FSharp.Charting.0.90.12/lib/net40/FSharp.Charting.dll"

open System
open FSharp.Charting

let random = new Random()
let next () = random.NextDouble() > 0.5
let next2 () = random.NextDouble() > 0.6
let next3 () = random.NextDouble() > 0.2

let randomWalk = new Event<int * int>()
let randomWalk2 = new Event<int * int>()
let randomWalk3 = new Event<int * int>()

let chart = randomWalk.Publish |> LiveChart.FastLineIncremental
let chart2 =  randomWalk2.Publish |> LiveChart.FastLineIncremental
let chart3 =  randomWalk3.Publish |> LiveChart.FastLineIncremental

let rec generateWalk3 (x, y) count = async {
     randomWalk2.Trigger (x, y)
     if count > 0 then
        do! Async.Sleep 50
        do! generateWalk3 (x + 1, if next3 () then y + 1 else y - 1) (count - 1) }

let rec generateWalk2 (x, y) count = async {
     randomWalk2.Trigger (x, y)
     if count > 0 then
        do! Async.Sleep 50
        do! generateWalk2 (x + 1, if next2 () then y + 1 else y - 1) (count - 1) }

let rec generateWalk (x, y) count = async {
     randomWalk.Trigger (x, y)
     if count > 0 then
        do! Async.Sleep 50
        do! generateWalk (x + 1, if next () then y + 1 else y - 1) (count - 1) }

generateWalk (0, 0) 1000 |> Async.StartImmediate
generateWalk2 (0, 0) 1000 |> Async.StartImmediate 
generateWalk3 (0, 0) 1000 |> Async.StartImmediate 

let chartTotal = Chart.Combine [
        (chart)
        (chart2)
        (chart3)]

chartTotal |> Chart.Show