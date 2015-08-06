#r "../packages/FSharp.Charting.0.90.12/lib/net40/FSharp.Charting.dll"

open System
open FSharp.Charting

let random = new Random()
let next () = random.NextDouble() > 0.5

let randomWalk = new Event<int * int>()

let chart = randomWalk.Publish |> LiveChart.FastLineIncremental

let rec generateWalk (x, y) count = async {
     randomWalk.Trigger (x, y)
     if count > 0 then
        do! Async.Sleep 50
        do! generateWalk (x + 1, if next () then y + 1 else y - 1) (count - 1) }

generateWalk (0, 0) 1000 |> Async.StartImmediate

chart |> Chart.Show
