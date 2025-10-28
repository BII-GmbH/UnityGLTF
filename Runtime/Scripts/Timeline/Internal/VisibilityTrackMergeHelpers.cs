using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace UnityGLTF.Timeline
{
    /// Small helper class to perform the merge of visibility and scale tracks when both are present.
    /// Main reason for abstracting this into a separate class is to make the merge logic testable,
    /// which is desperately necessary, since it is quite complex.
    internal sealed class MergeVisibilityAndScaleTrackMerger
    {
        private readonly ulong[] inputVisibilityTimes;
        private readonly ulong[] inputScaleTimes;

        private readonly bool[] inputVisibilities;
        private readonly Vector3[] inputScales;
        private ulong? lastVisibleTime => visIndex > 0 ? inputVisibilityTimes[visIndex - 1] : null;
        private bool? lastVisible => visIndex > 0 ? inputVisibilities[visIndex - 1] : null;

        private ulong? lastScaleTime => scaleIndex > 0 ? inputScaleTimes[scaleIndex - 1] : null;
        private Vector3? lastScale => scaleIndex > 0 ? inputScales[scaleIndex - 1] : null;

        
        private ulong currentVisibilityTime => inputVisibilityTimes[visIndex];
        private bool currentVisibility => inputVisibilities[visIndex];

        private ulong currentScaleTime => inputScaleTimes[scaleIndex];
        private Vector3 currentScale => inputScales[scaleIndex];

        private int visIndex { get; set; } = 0;

        private int scaleIndex { get; set; } = 0;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void incrementVisIndex() => visIndex++;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void incrementScaleIndex() => scaleIndex++;

        private static (ulong InsertedSample, ulong NormalSample) chooseSampleIndicesForInserted(ulong insertBeforeThisSample, ulong lastSampled) {
            return (insertBeforeThisSample, insertBeforeThisSample + 1);
            // var preferredTime = insertBeforeThisSample - 1;
            //
            // // inserting the additional sample at the previous sample index might not work (if there is a sample at that time already)
            // return lastSampled < preferredTime
            //     ? (insertBeforeThisSample - 1, insertBeforeThisSample)
            //     : (insertBeforeThisSample, insertBeforeThisSample + 1);
        }

        public IEnumerable<(ulong Time, Vector3 mergedScale)> Merge() {
            var lastRecordedTime = 0ul;
            
            var result = new List<(ulong Time, Vector3 mergedScale)>();
            
            while (visIndex < inputVisibilityTimes.Length && scaleIndex < inputScaleTimes.Length) {
                var visTime = currentVisibilityTime;
                var scaleTime = currentScaleTime;
                var visible = currentVisibility;
                var scale = currentScale;

                if (visTime == scaleTime) {
                    var emittedExtraSample = false;
                    // safety check to ensure that a sample a time 0 is set correctly
                    if (visTime == 0) {
                        // this is also a scale sample so we should always use the current scale value if visible
                        result.Add((0, visible ? scale : Vector3.zero));
                    } else {
                        handleBothSampledAtSameTime(
                            result,
                            visTime,
                            visible,
                            scale,
                            lastVisible ?? visible,
                            lastRecordedTime,
                            out emittedExtraSample
                        );
                    }

                    lastRecordedTime = emittedExtraSample ? visTime + 1 : visTime;
                    incrementVisIndex();
                    incrementScaleIndex();
                }
                else if (visTime < scaleTime) {
                    // safety check to ensure that a sample a time 0 is set correctly
                    var emittedExtraSample = false;

                    if(visTime == 0) {
                        result.Add((0, visible ? (lastScale ?? scale) : Vector3.zero));
                    }
                    else {
                        mergedSamplesForNextVisibilityChange(
                            result,
                            visTime,
                            currentVisibility,
                            scaleTime,
                            currentScale,
                            lastVisibleTime ?? lastRecordedTime,
                            lastVisible ?? visible,
                            lastScaleTime ?? lastRecordedTime,
                            // intentionally using (current) scale as fallback here because any
                            // other default feels even more unexpected. Note that this
                            // case should never actually happen in reality since creating
                            // a new animation data object samples all tracks at the time
                            // it is created!
                            lastScale ?? scale,
                            lastRecordedTime,
                            out emittedExtraSample
                        );
                    }

                    lastRecordedTime = emittedExtraSample ? visTime + 1 : visTime;
                    incrementVisIndex();
                } else if (scaleTime < visTime) {
                    // the next scale change occurs sooner than the next visibility change
                    // However, if the model is currently invisible, we simply don't care
                    if (lastVisible ?? visible) {
                        // special edge case: the last sample was a visibility change that emitted an
                        // additional sample that now conflicts with our own sample
                        // since it cannot know the correct scale value ahead of time,
                        // so update the last emitted value instead of emitting a new one
                        if (lastRecordedTime == scaleTime && scaleTime > 0) {
                            result[^1] = (scaleTime, scale);
                        }
                        else {
                            result.Add((scaleTime, scale));
                        }
                    }
                    else {
                        //else if(scaleTime == 0)
                        // we are invisible and at 0, emit a sample for this to ensure the
                        // viewer doesn't handle this case differently than we expect

                        // both are invisible, use 0. 
                        // We could also skip this sample, but for consistency we keep it
                        // as there is already loads of logic that skips samples that are not necessary.
                        // This would just be another source of potential errors
                        result.Add((scaleTime, Vector3.zero));
                    }

                    lastRecordedTime = scaleTime;
                    incrementScaleIndex();
                }
            }

            // process remaining visibility changes - this will only enter if scaleTimes end was reached first
            while (visIndex < inputVisibilityTimes.Length) {
                var visTime = inputVisibilityTimes[visIndex];
                var visible = inputVisibilities[visIndex];
                if (lastVisible != visible) {
                    // if the value flipped, this needs two samples - one for
                    // the previous value and then another one at the new value
                    var (insertedSample, visSample) = chooseSampleIndicesForInserted(visTime, lastVisibleTime ?? lastRecordedTime);
                    result.Add((insertedSample, (lastVisible ?? visible) ? (lastScale ?? Vector3.one) : Vector3.zero));
                    result.Add((visSample, visible ? (lastScale ?? Vector3.one) : Vector3.zero));
                } else {
                    // always record one of them, otherwise the first or last values may be lost
                    result.Add((visTime, visible ? (lastScale ?? Vector3.one) : Vector3.zero));
                }
                incrementVisIndex();
            }

            // process remaining scale changes - this will only enter if vis end was reached first -
            // if last visibility was invisible then there is no point in adding these.
            // However, as the other branches of this class do not skip unnecessary samples, we also do not here
            // (lastVisible ?? currentVisibility) &&
            while (scaleIndex < inputScaleTimes.Length) {
                var scaleTime = inputScaleTimes[scaleIndex];
                var scale = inputScales[scaleIndex];
                result.Add((scaleTime, scale));
                incrementScaleIndex();
            }
            
            return result;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void handleBothSampledAtSameTime(
            List<(ulong Time, Vector3 Scale)> resultList,
            ulong time,
            bool visibility,
            Vector3 scale,
            bool lastVisible,
            ulong lastTime,
            out bool emittedExtraSample 
        ) {
            emittedExtraSample = false;
            // both samples have the same timestamp
            // choose output value depending on visibility; use scale value if visible
            switch (lastVisible, visibility) {
                case (true, false):
                    // visibility changed from visible to invisible
                    // use last scale value
                    if (time > 0) {
                        emittedExtraSample = true;
                        var (insertedSample, timeSample) = chooseSampleIndicesForInserted(time, lastTime);
                        resultList.Add( (insertedSample, scale));
                        resultList.Add( (timeSample, Vector3.zero));
                    } else {
                        resultList.Add( (0, Vector3.zero));
                    }
                        
                    break;
                case (true, true):
                    // both are visible, use scale value
                    // special edge case: the last sample was a visibility change that emitted an
                    // additional sample that now conflicts with our own sample
                    // since it cannot know the correct scale value ahead of time,
                    // so update the last emitted value instead of emitting a new one
                    if (lastTime == time && time > 0) {
                        resultList[^1] = (time, scale);
                    } else {
                        resultList.Add((time, scale));
                    }
                    break;
                case (false, false):
                    // both are invisible, use 0. 
                    // We could also skip this sample, but for consistency we keep it
                    // as there is already loads of logic that skips samples that are not necessary.
                    // This would just be another source of potential errors
                    resultList.Add( (time, Vector3.zero));
                    break;
                case (false, true):
                    // visibility changed from invisible to visible
                    // use scale value
                    if (time > 0) {
                        emittedExtraSample = true;
                        var (insertedInvisibleSample, visibleSample) = chooseSampleIndicesForInserted(time, lastTime);
                        
                        resultList.Add( (insertedInvisibleSample, Vector3.zero));
                        resultList.Add( (visibleSample, scale));
                    } else {
                        resultList.Add( (0, scale));
                    }
                    break;
            }
        }

        /// Finds the appropriate samples for when a visibility change occurs before the next scale change
        /// <param name="visTime">time at which the visibility changes. Must be > 0</param>
        /// <param name="visible">new visibility</param>
        /// <param name="nextScaleTime">time at which the next scale change occurs. Expects scaleTime > visTime! This is not validated though. if this precondition is violated, the results will not be correct</param>
        /// <param name="nextScale">the scale that is set at scaleTime</param>
        /// <param name="lastVisibleTime">the time of the last visibility change</param>
        /// <param name="lastVisible">the last visibility state</param>
        /// <param name="lastScaleTime">the time of the last scale change</param>
        /// <param name="lastScale">the last scale</param>
        /// <returns>an enumerable of merged samples that correctly represent this relation of samples for visibility and scale</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void mergedSamplesForNextVisibilityChange(
            List<(ulong Time, Vector3 Scale)> resultList,
            ulong visTime,
            bool visible,
            ulong nextScaleTime,
            Vector3 nextScale,
            ulong lastVisibleTime,
            bool lastVisible,
            ulong lastScaleTime,
            Vector3 lastScale,
            ulong lastRecordedTime,
            out bool emittedExtraSample 
        ) {
            // the next visibility change occurs sooner than the next scale change
            // record two samples:
            // 1) sample of the last visibility state _right_ before the change occurs to prevent linear interpolation from breaking the animation
            // 2) Sample of the scale using visibility state and (if visible) the scale
            // value interpolated between the last and next sample
            
            // both samples have the same timestamp
            // choose output value depending on visibility, but use scale value if visible
            switch (lastVisible, visible) {
                case (true, false): {
                    // visibility changed from visible to invisible
                    // use last scale value
                    emittedExtraSample = true;
                    var (insertedTime, sampleTime) = chooseSampleIndicesForInserted(
                        visTime,
                        lastSampled: Math.Max(lastVisibleTime, lastScaleTime)
                    );
                    resultList.Add( (insertedTime, Vector3.LerpUnclamped(
                        lastScale,
                        nextScale,
                        // do the math as double for higher precision and reduce to float when necessary
                        (float)((visTime - lastScaleTime) / (double)(nextScaleTime - lastScaleTime))
                    )));
                    resultList.Add( (sampleTime, Vector3.zero));
                    break;
                }
                case (true, true):
                    emittedExtraSample = false;
                    // This code does not skip unnecessary samples, other logic does that, so still emit the sample
                    
                    // both are visible, use scale value
                    // special edge case: the last sample was a visibility change that emitted an
                    // additional sample that now conflicts with our own sample
                    // since it cannot know the correct scale value ahead of time,
                    // so update the last emitted value instead of emitting a new one
                    if (lastRecordedTime == visTime && visTime > 0) {
                        resultList[^1] = (visTime, lastScale);
                    } else {
                        resultList.Add((visTime, lastScale));
                    }
                    // both are visible, we don't need a sample
                    break;
                case (_, false):
                    emittedExtraSample = false;
                    // This code does not skip unnecessary samples, other logic does that, so still emit the sample
                    resultList.Add( (visTime, Vector3.zero));
                    // both are visible, we don't need a sample
                    break;
                case (_, true): {
                    emittedExtraSample = true;
                    // visibility changed from invisible to visible
                    // use scale value
                    var (insertedTime, sampleTime) = chooseSampleIndicesForInserted(
                        visTime,
                        lastSampled: Math.Max(lastVisibleTime, lastScaleTime)
                    );

                    resultList.Add( (insertedTime, Vector3.zero));
                    resultList.Add( (sampleTime, Vector3.LerpUnclamped(
                        lastScale,
                        nextScale,
                        // do the math as double for higher precision and reduce to float when necessary
                        (float)((visTime - lastScaleTime) / (double)(nextScaleTime - lastScaleTime))
                    )));
                    break;
                }
                default:
                    emittedExtraSample = false;
                    throw new ArgumentOutOfRangeException($"Unexpected visibility state combination: {lastVisible} {visible}");
            }
        }
        
        public MergeVisibilityAndScaleTrackMerger(
            ulong[] inputVisibilityTimes,
            bool[] inputVisibilities,
            ulong[] inputScaleTimes,
            Vector3[] inputScales
        ) {
            this.inputVisibilityTimes = inputVisibilityTimes;
            this.inputVisibilities = inputVisibilities;
            
            if(inputVisibilityTimes.Length != inputVisibilities.Length)
                throw new ArgumentException("Visibility times and values must have the same length");
            
            this.inputScaleTimes = inputScaleTimes;
            this.inputScales = inputScales;
            
            if(inputScaleTimes.Length != inputScales.Length)
                throw new ArgumentException("Scale times and values must have the same length");
        }
    }
}