#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.Profiling;
using Object = UnityEngine.Object;

namespace UnityGLTF.Timeline
{
    public sealed class AnimationTrackId { }
    
    public interface AnimationTrack
    {
        Object? AnimatedObject { get; }
        string PropertyName { get; }
        double[] Times { get; }
        object[] Values { get; }
        
        double? LastTime { get; }
        object? LastValue { get; }
        
        void SampleIfChanged(double time);
    }

    internal abstract class BaseAnimationTrack<TObject, TData> : AnimationTrack where TObject : Object
    {
        private readonly struct LastSampleData
        {
            public readonly double Time;
            [System.Diagnostics.CodeAnalysis.NotNull] [DisallowNull]
            public readonly TData Sample;

            public LastSampleData(double time, TData sample) {
                Time = time;
                Sample = sample;
            }
        }
        
        private readonly Dictionary<double, TData> samples;
        
        private readonly AnimationData animationData;
        private readonly AnimationSampler<TObject, TData> sampler;

        private readonly IEqualityComparer<TData> equalityComparer;

        private double lastSampleTime = Double.NegativeInfinity, secondToLastSampleTime = Double.NegativeInfinity;
        private TData? lastSampleValue, secondToLastSampleValue;
        
        public Object? AnimatedObject => sampler.GetTarget(animationData.transform);
        public string PropertyName => sampler.PropertyName;
        public double[] Times => samples.Keys.ToArray();
        
        public object[] Values => samples.Values.Cast<object>().ToArray();
        internal TData[] values => samples.Values.ToArray();

        public double? LastTime => lastSampleTime;
        public object? LastValue => lastValue;
        internal TData? lastValue => lastSampleValue;
        
        protected BaseAnimationTrack(AnimationData tr, AnimationSampler<TObject, TData> plan, double time, IEqualityComparer<TData> equalityComparer, Func<TData?, TData?>? overrideInitialValueFunc = null) {
            this.animationData = tr;
            this.sampler = plan;
            this.equalityComparer = equalityComparer;
            this.lastSampleValue = default;
            this.secondToLastSampleValue = default;
            samples = new Dictionary<double, TData>();
            if(overrideInitialValueFunc != null)
                recordSampleIfChanged(time, overrideInitialValueFunc(sampler.sample(animationData)));
            else 
                SampleIfChanged(time);
            
            recordSampleIfChangedMarker = new ProfilerMarker($"BaseAnimationTrack<{typeof(TObject).Name}, {typeof(TData).Name}> - recordSampleIfChanged"); 
        }
        
        public void SampleIfChanged(double time) => recordSampleIfChanged(time, sampler.sample(animationData));

        private readonly ProfilerMarker recordSampleIfChangedMarker;
        private readonly ProfilerMarker unityObjectCheck = new ProfilerMarker("unityObjectCheck");
        private readonly ProfilerMarker lastSampleCheck = new ProfilerMarker("lastSampleCheck");
        private readonly ProfilerMarker lastSampleCheckEquality = new ProfilerMarker("lastSampleCheckEquality");
        private readonly ProfilerMarker removeLastSample = new ProfilerMarker("removeLastSample");
        private readonly ProfilerMarker insertData = new ProfilerMarker("insert Data");
        
        protected void recordSampleIfChanged(double time, TData? value) {
            using var _ = recordSampleIfChangedMarker.Auto();
            {
                using var __ = unityObjectCheck.Auto();
                if (value == null || (value is Object o && !o)) return;
            }
            // As a memory optimization we want to be able to skip identical samples.
            // But, we cannot always skip samples when they are identical to the previous one - otherwise cases like this break:
            // - First assume an object is positioned at coordinates (1,2,3)
            // - At some point in time, it is "instantaneously" teleported to (4,5,6)
            // If we simply skip identical samples on insert, instead of an almost instantaneous
            // teleport we get a linearly interpolated scale change because only two samples will be recorded:
            // - one at (1,2,3) at the start of time
            // - (4,5,6) at the time of the visibility change
            // What we want to get is
            // - one sample with (1,2,3) at the start,
            // - one with the same value right before the instantaneous teleportation,
            // - and then at the time of the change, we need a sample at (4,5,6)
            // With this setup, now the linear interpolation only has an effect in the
            // very short duration between the last two samples and we get the animation we want.

            // How do we achieve both?
            // Always sample & record and then on adding the next sample(s) we check
            // if the *last two* samples were identical to the current sample.
            // If that is the case we can remove/overwrite the middle sample with the new value.
            lastSampleCheck.Begin();
            if (!double.IsNegativeInfinity(lastSampleTime) && 
                !double.IsNegativeInfinity(secondToLastSampleTime)) {
                var lastSampled = lastSampleValue;
                var secondLastSampled = secondToLastSampleValue;
                using var __ = lastSampleCheckEquality.Auto(); 
                if(lastSampled != null && secondLastSampled != null &&
                    equalityComparer.Equals(lastSampled, secondLastSampled) &&
                    equalityComparer.Equals(lastSampled, value)) {
                    using var ___ = removeLastSample.Auto();
                    samples.Remove(lastSampleTime);
                }
            }

            lastSampleCheck.End();
            
            insertData.Begin();
            samples[time] = value;
            secondToLastSampleValue = lastSampleValue;
            secondToLastSampleTime = lastSampleTime;
            lastSampleTime = time;
            lastSampleValue = value;
            insertData.End();
        }
    }

    internal sealed class AnimationTrack<TObject, TData> : BaseAnimationTrack<TObject, TData> where TObject : Object
    {
        public AnimationTrack(AnimationData tr, AnimationSampler<TObject, TData> plan, double time, IEqualityComparer<TData> equalityComparer) : base(tr, plan, time, equalityComparer) { }
    }
}