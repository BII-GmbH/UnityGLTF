using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace UnityGLTF.Timeline
{
    internal sealed class MergeVisibilityAndScaleTracksCurrentState
    {
        private readonly double[] inputVisibilityTimes;
        private readonly double[] inputScaleTimes;

        private readonly bool[] inputVisibilities;
        private readonly Vector3[] inputScales;

        private readonly List<double> mergedTimes;
        private readonly List<Vector3> mergedScales;

        public bool? lastVisible => VisIndex > 0 ? inputVisibilities[VisIndex - 1] : null;

        public double? lastScaleTime => ScaleIndex > 0 ? inputScaleTimes[ScaleIndex - 1] : null;
        public Vector3? lastScale => ScaleIndex > 0 ? inputScales[ScaleIndex - 1] : null;

        public int VisIndex { get; private set; }
        public int ScaleIndex { get; private set; }

        public double CurrentVisibilityTime => inputVisibilityTimes[VisIndex];
        public bool CurrentVisibility => inputVisibilities[VisIndex];

        public double CurrentScaleTime => inputScaleTimes[ScaleIndex];
        public Vector3 CurrentScale => inputScales[ScaleIndex];

        public void IncrementVisIndex() => VisIndex++;
        public void IncrementScaleIndex() => ScaleIndex++;

        public (double[] times, Vector3[] mergedScales) Merge() {
            while (VisIndex < inputVisibilityTimes.Length && ScaleIndex < inputScaleTimes.Length) {
                var visTime = CurrentVisibilityTime;
                var scaleTime = CurrentScaleTime;
                var visible = CurrentVisibility;
                var scale = CurrentScale;

                if (visTime.nearlyEqual(scaleTime)) {
                    
                    handleBothSampledAtSameTime(visTime);

                    // visIndex++;
                    // scaleIndex++;
                    //
                    // lastScaleTime = visTime;
                    // lastScale = scale;
                    // lastVisible = visible;
                }
                else if (visTime < scaleTime) {
                    foreach (var (time, value) in mergedSamplesForNextVisibilityChange(
                        visTime,
                        CurrentVisibility,
                        scaleTime,
                        CurrentScale,
                        lastVisible,
                        lastScaleTime,
                        lastScale
                    )) {
                        record(time, value);
                    }

                    IncrementVisIndex();
                    // visIndex++;
                    //
                    // lastVisible = visible;
                }
                else if (scaleTime < visTime) {
                    // the next scale change occurs sooner than the next visibility change
                    // However, if the model is currently invisible, we simply dont care
                    if (lastVisible ?? true) record(scaleTime, scale);

                    IncrementScaleIndex();

                    // scaleIndex++;
                    // lastScaleTime = scaleTime;
                    // lastScale = scale;
                }
            }

            // process remaining visibility changes - this will only enter if scale end was reached first
            while (VisIndex < inputVisibilityTimes.Length) {
                var visTime = inputVisibilityTimes[VisIndex];
                var visible = inputVisibilities[VisIndex];

                // next vis change is sooner than next scale change
                // time: -> visTime
                // res: visible -> lastScale : 0
                record(visTime, visible ? (lastScale ?? Vector3.one) : Vector3.zero);
                IncrementVisIndex();

                // visIndex++;
                //
                // lastVisible = visible;
            }

            // process remaining scale changes - this will only enter if vis end was reached first -
            // if last visibility was invisible then there is no point in adding these
            while ((lastVisible ?? true) && ScaleIndex < inputScaleTimes.Length) {
                var scaleTime = inputScaleTimes[ScaleIndex];
                var scale = inputScales[ScaleIndex];
                record(scaleTime, scale);
                IncrementScaleIndex();
            }

            return (mergedTimes.ToArray(), mergedScales.ToArray());
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void handleBothSampledAtSameTime(double time) {
            var visibility = CurrentVisibility;
            var scale = CurrentScale;
            
            // both samples have the same timestamp
            // choose output value depending on visibility, but use scale value if visible
            switch (lastVisible, visibility) {
                case (true, false):
                    // visibility changed from visible to invisible
                    // use last scale value
                    if (time > 0) record(time.nextSmaller(), scale);
                    record(time, Vector3.zero);
                    break;
                case (true, true):
                    // both are visible, use scale value
                    record(time, scale);
                    break;
                // _ to catch null and false as values
                case (_, false):
                    // both are invisible, use scale value
                    record(time, Vector3.zero);
                    break;
                case (_, true):
                    // visibility changed from invisible to visible
                    // use scale value
                    if (time > 0) record(time.nextSmaller(), Vector3.zero);
                    record(time, scale);
                    break;
            }

            IncrementVisIndex();
            IncrementScaleIndex();
        }

        
        
        /// <summary>
        /// Finds the appropriate samples for when a visibility change occurs before the next scale change
        /// </summary>
        /// <param name="visTime">time at which the visibility changes</param>
        /// <param name="visible">new visibility</param>
        /// <param name="scaleTime">time at which the next scale change occurs. Expects scaleTime > visTime! This is not validated though. if this precondition is violated, the results will not be correct</param>
        /// <param name="scale">the scale that is set at scaleTime</param>
        /// <param name="lastVisible">the last visibility state</param>
        /// <param name="lastScaleTime">the time of the last scale change, or null if no such time exists</param>
        /// <param name="lastScale">the last scale, or null if no such value exists</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static IEnumerable<(double Time, Vector3 Scale)> mergedSamplesForNextVisibilityChange(
            double visTime,
            bool visible,
            double scaleTime,
            Vector3 scale,
            bool? lastVisible,
            double? lastScaleTime,
            Vector3? lastScale
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
                    if (visTime <= 0)
                        // this should not be able to happen, but just in case
                        return new []{ (visTime, Vector3.zero) };
                    var lastTime = lastScaleTime ?? visTime;
                    return new[] {
                        (visTime.nextSmaller(),
                            Vector3.LerpUnclamped(
                                // intentionally using scale as fallback here because any
                                // other default feels even more unexpected. Note that this
                                // case should never actually happen in reality since creating
                                // a new animation data object samples all tracks at the time
                                // it is created!
                                lastScale ?? scale,
                                scale,
                                (float)((visTime - lastTime) / (scaleTime - lastTime))
                            )),
                        (visTime, Vector3.zero)
                    };
                    break;
                case (true, true):
                    // both are visible, use scale value
                    break;
                case (_, false):
                    // both are invisible, use scale value
                    break;
                case (_, true):
                    // visibility changed from invisible to visible
                    // use scale value
                    if (visTime <= 0)
                        return new[] { (visTime, Vector3.one) };
                    
                    var last = lastScaleTime ?? visTime;
                    return new[] {
                        (visTime.nextSmaller(), Vector3.zero),
                        (visTime,
                            Vector3.LerpUnclamped(
                                lastScale ?? Vector3.zero,
                                scale,
                                (float)((visTime - last) / (scaleTime - last))
                            ))
                    };
            }
            return Enumerable.Empty<(double, Vector3)>();
        }
        
        void record(double time, Vector3 scale) {
            mergedTimes.Add(time);
            mergedScales.Add(scale);
        }

        public MergeVisibilityAndScaleTracksCurrentState(
            double[] inputVisibilityTimes,
            bool[] inputVisibilities,
            double[] inputScaleTimes,
            Vector3[] inputScales
        ) {
            this.inputVisibilityTimes = inputVisibilityTimes;
            this.inputVisibilities = inputVisibilities;
            this.inputScaleTimes = inputScaleTimes;
            this.inputScales = inputScales;

            mergedTimes = new(inputVisibilityTimes.Length + inputScaleTimes.Length);
            mergedScales = new List<Vector3>(inputVisibilities.Length + inputScales.Length);

            VisIndex = 0;
            ScaleIndex = 0;
        }
    }
}