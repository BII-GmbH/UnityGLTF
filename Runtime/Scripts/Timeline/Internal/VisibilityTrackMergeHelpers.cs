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
        private readonly float[] inputVisibilityTimes;
        private readonly float[] inputScaleTimes;

        private readonly bool[] inputVisibilities;
        private readonly Vector3[] inputScales;
        private float? lastVisibleTime => visIndex > 0 ? inputVisibilityTimes[visIndex - 1] : null;
        private bool? lastVisible => visIndex > 0 ? inputVisibilities[visIndex - 1] : null;

        private float? lastScaleTime => scaleIndex > 0 ? inputScaleTimes[scaleIndex - 1] : null;
        private Vector3? lastScale => scaleIndex > 0 ? inputScales[scaleIndex - 1] : null;

        
        private float currentVisibilityTime => inputVisibilityTimes[visIndex];
        private bool currentVisibility => inputVisibilities[visIndex];

        private float currentScaleTime => inputScaleTimes[scaleIndex];
        private Vector3 currentScale => inputScales[scaleIndex];

        private int visIndex { get; set; } = 0;

        private int scaleIndex { get; set; } = 0;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void incrementVisIndex() => visIndex++;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void incrementScaleIndex() => scaleIndex++;

        public IEnumerable<(float Time, Vector3 mergedScale)> Merge() {
            var lastRecordedTime = 0.0f;
            while (visIndex < inputVisibilityTimes.Length && scaleIndex < inputScaleTimes.Length) {
                var visTime = currentVisibilityTime;
                var scaleTime = currentScaleTime;
                var visible = currentVisibility;
                var scale = currentScale;

                if (visTime.nearlyEqual(scaleTime)) {
                    // safety check to ensure that a sample a time 0 is set correctly
                    if (visTime <= 0) {
                        yield return (visTime, visible ? (lastScale ?? scale) : Vector3.zero);
                    }
                    else {
                        foreach (var sample in handleBothSampledAtSameTime(
                            visTime,
                            visible,
                            scale,
                            lastVisible ?? visible,
                            lastRecordedTime
                        ))
                            yield return sample;
                    }

                    lastRecordedTime = visTime;
                    incrementVisIndex();
                    incrementScaleIndex();
                }
                else if (visTime < scaleTime) {
                    // safety check to ensure that a sample a time 0 is set correctly
                    if(visTime <= 0) {
                        yield return (visTime, visible ? (lastScale ?? scale) : Vector3.zero);
                    }
                    else {
                        foreach (var (time, value) in mergedSamplesForNextVisibilityChange(
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
                            lastScale ?? scale
                        )) { yield return (time, value); }
                    }

                    lastRecordedTime = visTime;
                    incrementVisIndex();
                } else if (scaleTime < visTime) {
                    // the next scale change occurs sooner than the next visibility change
                    // However, if the model is currently invisible, we simply dont care
                    if (lastVisible ?? visible) 
                        yield return (scaleTime, scale);

                    lastRecordedTime = scaleTime;
                    incrementScaleIndex();
                }
            }

            // process remaining visibility changes - this will only enter if scaleTimes end was reached first
            while (visIndex < inputVisibilityTimes.Length) {
                var visTime = inputVisibilityTimes[visIndex];
                var visible = inputVisibilities[visIndex];
                if (lastVisible != visible) {
                    // if the value flipped, this needs two samples - one for the previous value and
                    // then another one at the new value
                    yield return (visTime.nextSmaller(lastVisibleTime ?? lastRecordedTime), (lastVisible ?? visible) ? (lastScale ?? Vector3.one) : Vector3.zero);
                }
                // always record one of them, otherwise the first or last values may be lost
                yield return (visTime, visible ? (lastScale ?? Vector3.one) : Vector3.zero);
                incrementVisIndex();
            }

            // process remaining scale changes - this will only enter if vis end was reached first -
            // if last visibility was invisible then there is no point in adding these
            while ((lastVisible ?? currentVisibility) && scaleIndex < inputScaleTimes.Length) {
                var scaleTime = inputScaleTimes[scaleIndex];
                var scale = inputScales[scaleIndex];
                yield return (scaleTime, scale);
                incrementScaleIndex();
            }
        }
        
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static IEnumerable<(float Time, Vector3 Scale)> handleBothSampledAtSameTime(
            float time,
            bool visibility,
            Vector3 scale,
            bool lastVisible,
            float lastTime
        ) {
            // both samples have the same timestamp
            // choose output value depending on visibility, but use scale value if visible
            switch (lastVisible, visibility) {
                case (true, false):
                    // visibility changed from visible to invisible
                    // use last scale value
                    if (time > 0) 
                        yield return (time.nextSmaller(lastTime), scale);
                    yield return (time, Vector3.zero);
                    break;
                case (true, true):
                    // both are visible, use scale value
                    yield return (time, scale);
                    break;
                case (false, false):
                    break;
                case (false, true):
                    // visibility changed from invisible to visible
                    // use scale value
                    if (time > 0) yield return (time.nextSmaller(lastTime), Vector3.zero);
                    yield return(time, scale);
                    break;
            }
        }

        /// Finds the appropriate samples for when a visibility change occurs before the next scale change
        /// <param name="visTime">time at which the visibility changes. Must be > 0</param>
        /// <param name="visible">new visibility</param>
        /// <param name="scaleTime">time at which the next scale change occurs. Expects scaleTime > visTime! This is not validated though. if this precondition is violated, the results will not be correct</param>
        /// <param name="scale">the scale that is set at scaleTime</param>
        /// <param name="lastVisibleTime">the time of the last visibility change</param>
        /// <param name="lastVisible">the last visibility state</param>
        /// <param name="lastScaleTime">the time of the last scale change</param>
        /// <param name="lastScale">the last scale</param>
        /// <returns>an enumerable of merged samples that correctly represent this relation of samples for visibility and scale</returns>
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static IEnumerable<(float Time, Vector3 Scale)> mergedSamplesForNextVisibilityChange(
            float visTime,
            bool visible,
            float scaleTime,
            Vector3 scale,
            float lastVisibleTime,
            bool lastVisible,
            float lastScaleTime,
            Vector3 lastScale
        ) {
            // the next visibility change occurs sooner than the next scale change
            // record two samples:
            // 1) sample of the last visibility state _right_ before the change occurs to prevent linear interpolation from breaking the animation
            // 2) Sample of the scale using visibility state and (if visible) the scale
            // value interpolated between the last and next sample

            // both samples have the same timestamp
            // choose output value depending on visibility, but use scale value if visible
            switch (lastVisible, visible) {
                case (true, false):
                    // visibility changed from visible to invisible
                    // use last scale value
                    
                    yield return (visTime.nextSmaller(Math.Max(lastVisibleTime, lastScaleTime)),
                        Vector3.LerpUnclamped(
                            lastScale,
                            scale,
                            (visTime - lastScaleTime) / (scaleTime - lastScaleTime)
                        ));
                    
                    yield return (visTime, Vector3.zero);
                    break;
                case (true, true):
                    // both are visible, we don't need a sample
                    break;
                case (_, false):
                    // both are visible, we don't need a sample
                    break;
                case (_, true):
                    // visibility changed from invisible to visible
                    // use scale value
                    yield return (visTime.nextSmaller(Math.Max(lastVisibleTime, lastScaleTime)), Vector3.zero);
                    yield return (visTime,
                        Vector3.LerpUnclamped(
                            lastScale,
                            scale,
                            (visTime - lastScaleTime) / (scaleTime - lastScaleTime)
                        ));
                    break;
            }
        }
        
        public MergeVisibilityAndScaleTrackMerger(
            float[] inputVisibilityTimes,
            bool[] inputVisibilities,
            float[] inputScaleTimes,
            Vector3[] inputScales
        ) {
            this.inputVisibilityTimes = inputVisibilityTimes;
            this.inputVisibilities = inputVisibilities;
            this.inputScaleTimes = inputScaleTimes;
            this.inputScales = inputScales;
        }
    }
}