// Copyright (c) University of Warwick. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE.txt in the project root for license information.

namespace Endorphin.Instrument.PicoQuant.PicoHarp300

open Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols
open Endorphin.Core
open System
open FSharp.Control.Reactive
open Endorphin.Core.CancellationCapability

module Streaming =
    type private Tag = uint32

    type StreamStopOptions = { AcquisitionDidAutoStop : bool }

    type StreamStatus =
        | PreparingStream
        | Streaming
        | FinishedStream of didAutoStop : bool
        | FailedStream of error : string
        | CancelledStream

    type internal TagStream = { Tags                 : seq<Tag> }

    /// List of bin number and number of counts in bin
    type Histogram = { Histogram : (int * int) list }

    type StreamResult =
        | StreamCompleted
        | StreamError of exn
        | StreamCancelled

    type StreamingAcquisition =
        private { StreamingBuffer     : StreamingBuffer
                  PicoHarp            : PicoHarp300
                  StopCapability      : CancellationCapability<StreamStopOptions>
                  StatusChanged       : NotificationEvent<StreamStatus>
                  TagsAvailable       : Event<TagStream> }

    type StreamingAcquisitionHandle =
        private { Acquisition   : StreamingAcquisition
                  WaitToFinish  : Async<StreamResult> }

    type internal HistogramResidual =
                 {  WorkingHistogram    : int list
                    OverflowMarkers     : int
                    MarkerTimestamp     : uint32 }

    /// module for manipulating the tags from the PicoHarp
    module internal TagHelper =
        let inline isPhoton (tag : Tag) =
            ((tag >>> 28 ) &&& 0xFu) < 2u

        let inline timestamp (tag : Tag) =
            tag &&& 0x0FFFFFFFu

        let inline isTimeOverflow (tag : Tag) =
            ((tag >>> 28) &&& 0xFu) = 15u && (tag &&& 0xFu) = 0u

        let inline isMarker (tag : Tag) =
            ((tag >>> 28) &&& 0xFu) = 15u && (tag &&& 0xFu) <> 0u

        let inline markerChannel (tag : Tag) : uint32 =
            (tag &&& 0xFu)

        let inline photonChannel (tag : Tag) : uint32 =
            ((tag >>> 28 ) &&& 0xFu)

        let inline isPhotonChannel0 (tag : Tag) : bool =
            (((tag >>> 28) &&& 0x1u) <> 1u)

        let inline timeBetweenTags (startTag : Tag) (stopTag : Tag) overflowMarkerCount =
            4.0<ps> * nanosecondsPerPicosecond * float ((uint64 overflowMarkerCount) * TTTROverflowTime + (uint64 <| timestamp stopTag) - (uint64 <| timestamp startTag))

        // units of measure not supported on unsigned integer types
        let inline totalTimeInPicoseconds (tag : Tag) (overflowMarkerCount : uint64) =
            4UL * (overflowMarkerCount * TTTROverflowTime + (uint64 <| timestamp tag))

    module internal TagStreamReader =
        let incrementTimeOverflowMarkers histogramResidual =
            { histogramResidual with OverflowMarkers = histogramResidual.OverflowMarkers + 1 }

        let histogramResidualCreate timestamp =
            { WorkingHistogram = List.empty; OverflowMarkers = 0; MarkerTimestamp = timestamp }

        let inline addPhoton (histogramResidual : HistogramResidual) binNumber =
            { histogramResidual with WorkingHistogram = binNumber :: histogramResidual.WorkingHistogram }

        let inline timeSinceMarker histogramResidual tag =
            4.0<ps> * nanosecondsPerPicosecond * float ((uint64 histogramResidual.OverflowMarkers) * TTTROverflowTime + (uint64 <| TagHelper.timestamp tag) - (uint64 histogramResidual.MarkerTimestamp))

        let inline histogramBin (histogramResidual : HistogramResidual) (parameters : HistogramParameters) tag : int option =
            match (timeSinceMarker histogramResidual tag) with
                | tagTime when tagTime < parameters.TotalLength   -> Some (tagTime / parameters.Resolution |> floor |> int)
                | _                                               -> None

        /// Extract all histograms from the incoming tag stream, and build them into a sequence of histograms
        let extractAllHistograms (parameters : HistogramParameters) (histogramResidual : HistogramResidual option) (tagStream : TagStream) =
            let result =
                tagStream.Tags
                |> Seq.fold (fun (histograms, residual) tag ->
                    /// options for incoming tag:
                    ///     if not currently building a histogram:
                    ///         if a marker, start new histogram
                    ///         if not, ignore
                    ///     if in a histogram:
                    ///         if it's a photon:
                    ///             if within the total histogram time, add the photon
                    ///             else, this is the first record since the end of the previous histogram --- write the histogram to sequence
                    ///         if it's an overflow:
                    ///             increment the overflow counter
                    ///         if it's a marker:
                    ///             if the marker is the first record since the end of the previous histogram, start a new one
                    ///             else, fail - the user has asked for histogram timings which do not fit within their acquisition triggers
                    match residual, tag with
                        | None, tag when not <| TagHelper.isMarker tag || (TagHelper.markerChannel tag <> (uint32 (parseMarkerChannel parameters.MarkerChannel))) -> (histograms, residual)
                        | None, tag                                      -> (histograms, Some (histogramResidualCreate <| TagHelper.timestamp tag))
                        | Some residual, tag when TagHelper.isPhoton tag ->
                            match (histogramBin residual parameters tag) with
                                | Some bin                      -> (histograms, Some <| (addPhoton residual bin))
                                | None                          ->
                                    let h = ((Seq.countBy id residual.WorkingHistogram) |> Seq.toList)
                                    ({ Histogram = h } :: histograms, None)
                        | Some residual, tag when TagHelper.isTimeOverflow tag ->
                            (histograms, Some <| incrementTimeOverflowMarkers residual)
                        | Some residual, tag when TagHelper.isMarker tag && (histogramBin residual parameters tag) = None && (TagHelper.markerChannel tag = (uint32 (parseMarkerChannel parameters.MarkerChannel))) ->
                            ({ Histogram = ((Seq.countBy id residual.WorkingHistogram) |> Seq.toList) } :: histograms, Some <| (histogramResidualCreate <| TagHelper.timestamp tag))
                        /// should only get here if there is residual, and the tag is a marker - this should cause an error!
                        | _ ->  printfn "STREAMFAIL";failwith "Encountered an unexpected marker tag. Do the histogram settings match the experimental settings?") (List.empty, histogramResidual)
            result

        let extractPhotonsByChannelAndTime (tagStream : TagStream) (numberOfOverflowMarkers : uint64) =
            let result =
                tagStream.Tags
                |> Seq.fold (fun ((photons : uint64 list * uint64 list), numberOfOverflows) tag ->
                    match tag with
                        | tag when TagHelper.isTimeOverflow tag     -> (photons, numberOfOverflows + 1UL)
                        | tag when TagHelper.isPhoton tag && TagHelper.isPhotonChannel0 tag
                                                                    -> (((TagHelper.totalTimeInPicoseconds tag numberOfOverflows)::(fst photons), snd photons), numberOfOverflows)
                        | tag when TagHelper.isPhoton tag           -> ((fst photons, (TagHelper.totalTimeInPicoseconds tag numberOfOverflows)::(snd photons)), numberOfOverflows)
                        // if not any of the above, must be a marker channel and can be ignored in correlation measurement
                        | _                                         -> (photons, numberOfOverflows)) ((List.empty, List.empty), numberOfOverflowMarkers)
            result

        let photonCountRate (tagStream : TagStream) =
            let tagArray = tagStream.Tags
                           |> Seq.toArray

            let photons =
                tagArray
                |> Array.filter TagHelper.isPhoton

            let overflowCount =
                tagArray
                |> Array.filter TagHelper.isTimeOverflow
                |> Array.length

            let channel0photons =
                photons
                |> Array.filter TagHelper.isPhotonChannel0
                |> Array.length

            let normalizationFactor =
                if (Array.length tagArray) <> 0 then
                    ((float <| TagHelper.timeBetweenTags (tagArray.[0]) (tagArray.[Array.length tagArray - 1]) overflowCount) / 1E8) * 1.3
                else
                    1.

            (int ((float channel0photons) / normalizationFactor), int ((float ((Array.length photons) - channel0photons) / normalizationFactor)))

    module Acquisition =
        let private buffer =  Array.zeroCreate TTTRMaxEvents

        let create picoHarp =
            let tagsAvailable = new Event<TagStream>()

            { StreamingBuffer = { Buffer = buffer }
              PicoHarp = picoHarp
              StopCapability = new CancellationCapability<StreamStopOptions>()
              StatusChanged = new NotificationEvent<StreamStatus>()
              TagsAvailable = tagsAvailable }

        let status acquisition =
            acquisition.StatusChanged.Publish
            |> Observable.fromNotificationEvent

        let private startStreaming acquisition = async {
            do! PicoHarp.Acquisition.start acquisition.PicoHarp (Duration_s (1.0<s> * 100.0 * 3600.0))
            acquisition.StatusChanged.Trigger (Next <| Streaming) }

        let private copyBufferAndFireEvent acquisition counts =
            let buffer = Array.zeroCreate counts
            Array.blit acquisition.StreamingBuffer.Buffer 0 buffer 0 counts
            let tagSequence = Seq.ofArray buffer
            acquisition.TagsAvailable.Trigger { Tags = tagSequence }//; HistogramParameters = acquisition.Parameters }

        let rec private pollUntilFinished acquisition = async {
            if not acquisition.StopCapability.IsCancellationRequested then
                let! counts =
                    PicoHarp.Acquisition.readFifoBuffer
                    <| acquisition.PicoHarp
                    <| acquisition.StreamingBuffer

                copyBufferAndFireEvent acquisition counts

                let! finished = PicoHarp.Acquisition.checkMeasurementFinished acquisition.PicoHarp
                if finished then
                    acquisition.StopCapability.Cancel { AcquisitionDidAutoStop = true }

                do! pollUntilFinished acquisition }

        let startWithCancellationToken acquisition cancellationToken =
            if acquisition.StopCapability.IsCancellationRequested then
                failwith "Cannot start an acquisition which has already been stopped."

            let resultChannel = new ResultChannel<_>()

            let finishAcquisition () =
                Async.StartWithContinuations(
                    PicoHarp.Acquisition.stop acquisition.PicoHarp,
                    (fun () ->
                        acquisition.StatusChanged.Trigger
                            (Next
                            <| FinishedStream acquisition.StopCapability.Options.AcquisitionDidAutoStop)
                        acquisition.StatusChanged.Trigger Completed),
                    (fun stopExn ->
                        let error = sprintf "Acquisition failed to stop: %s" stopExn.Message
                        acquisition.StatusChanged.Trigger
                            (Next
                            <| FailedStream error)
                        acquisition.StatusChanged.Trigger
                            (Error (Exception error))),
                    ignore )

            let stopAcquisitionAfterError exn =
                Async.StartWithContinuations (
                    PicoHarp.Acquisition.stop acquisition.PicoHarp,
                    (fun () ->
                        acquisition.StatusChanged.Trigger (Error exn)
                        resultChannel.RegisterResult (StreamError exn)),
                    (fun stopExn ->
                        acquisition.StatusChanged.Trigger (Error exn)
                        resultChannel.RegisterResult (StreamError stopExn)),
                    ignore )

            let stopAcquisitionAfterCancellation _ =
                Async.StartWithContinuations(
                    PicoHarp.Acquisition.stop acquisition.PicoHarp,
                    (fun () ->
                        acquisition.StatusChanged.Trigger (Next <| CancelledStream)
                        acquisition.StatusChanged.Trigger Completed
                        resultChannel.RegisterResult StreamCancelled),
                    (fun stopExn ->
                        acquisition.StatusChanged.Trigger (Error stopExn)
                        resultChannel.RegisterResult (StreamError stopExn)),
                    ignore)

            let acquisitionWorkflow = async {
                do! startStreaming acquisition
                do! pollUntilFinished acquisition }

            Async.StartWithContinuations (
                acquisitionWorkflow,
                finishAcquisition,
                stopAcquisitionAfterError,
                stopAcquisitionAfterCancellation,
                cancellationToken)

            { Acquisition = acquisition; WaitToFinish = resultChannel.AwaitResult () }

        let start acquisition = startWithCancellationToken acquisition Async.DefaultCancellationToken

        let startAsChild acquisition = async {
            let! ct = Async.CancellationToken
            return startWithCancellationToken acquisition ct }

        let waitToFinish acquisitionHandle = acquisitionHandle.WaitToFinish

        let stop acquisitionHandle =
            if not acquisitionHandle.Acquisition.StopCapability.IsCancellationRequested then
                acquisitionHandle.Acquisition.StopCapability.Cancel { AcquisitionDidAutoStop = false }

        let stopAndFinish acquisitionHandle =
            stop acquisitionHandle
            waitToFinish acquisitionHandle

        // signal projections

        /// Bin incoming photons into a 2D histogram
        module Histogram =
           module Parameters =
               let internal computeNumberOfBins resolution length =
                   int <| ((Quantities.durationNanoSeconds length) / (Quantities.durationNanoSeconds resolution))

               let create resolution totalLength markerChannel : HistogramParameters =
                   { Resolution        = Quantities.durationNanoSeconds resolution
                     TotalLength       = Quantities.durationNanoSeconds totalLength
                     NumberOfBins      = computeNumberOfBins resolution totalLength
                     MarkerChannel     = markerChannel }

               let withResolution resolution         (parameters: HistogramParameters) = { parameters with Resolution = Quantities.durationNanoSeconds resolution; NumberOfBins = int <| ((parameters.TotalLength) / (Quantities.durationNanoSeconds resolution)) }
               let withTotalLength totalLength       (parameters: HistogramParameters) = { parameters with TotalLength = Quantities.durationNanoSeconds totalLength;  NumberOfBins = int <| ((Quantities.durationNanoSeconds totalLength) / (parameters.Resolution)) }


           type HistogramEvent =
               internal {  Output   : IObservable<Histogram> }

           let create streamingAcquisition parameters =
               let eventScheduler = new System.Reactive.Concurrency.EventLoopScheduler()

               let output =
                   streamingAcquisition.TagsAvailable.Publish
                   |> Observable.observeOn eventScheduler
                   |> Observable.scanInit (List.empty, None) (fun (_,residual) tagStream ->
                        TagStreamReader.extractAllHistograms parameters residual tagStream
                       )  // pass each new tag stream and previous residual into the processing function
                   |> Observable.map (fst >> List.rev)
                   |> Observable.flatmapSeq Seq.ofList // fire event for each histogram

               { Output = output }

           let HistogramsAvailable histogramAcquisition = histogramAcquisition.Output

        /// Live counts per channel, normalised to counts per second
        module LiveCounts =
            type LiveCount =
                internal { Output : IObservable<int * int> }

            let create streamingAcquisition  =
                let eventScheduler = new System.Reactive.Concurrency.EventLoopScheduler()

                let output =
                    streamingAcquisition.TagsAvailable.Publish
                    |> Observable.observeOn eventScheduler
                    |> Observable.map TagStreamReader.photonCountRate

                { Output = output }

            let LiveCountAvailable liveCountAcquisition = liveCountAcquisition.Output

        /// Separates incoming photons by channel and assigns them an absolute time since the start of the experiment
        module PhotonTimeStream =
            type PhotonTimeStream =
                internal { Output: IObservable<uint64 list * uint64 list>}

            let create (streamingAcquisition : StreamingAcquisition) =
                let eventScheduler = new System.Reactive.Concurrency.EventLoopScheduler()

                let output =
                    streamingAcquisition.TagsAvailable.Publish
                    |> Observable.observeOn eventScheduler
                    |> Observable.scanInit ((List.empty, List.empty), 0UL) (fun (_, numberOfOverflowMarkers) tagStream ->
                        TagStreamReader.extractPhotonsByChannelAndTime tagStream numberOfOverflowMarkers)
                    |> Observable.map (fun (photonList, _) -> (List.rev (fst photonList), List.rev (snd photonList)))

                { Output = output }

            let PhotonTimesAvailable photonTimeAcquisition = photonTimeAcquisition.Output