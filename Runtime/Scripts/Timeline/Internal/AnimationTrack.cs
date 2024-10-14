#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using GLTF.Schema;
using Unity.Profiling;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityGLTF.Timeline
{
    public sealed class AnimationTrackId { }
    
    public interface AnimationTrack
    {
        Object? AnimatedObjectUntyped { get; }
        string PropertyName { get; }
     
        InterpolationType InterpolationType { get; }
        
        double[] Times { get; }
        
        object[] ValuesUntyped { get; }
        
        double? LastTime { get; }
        object? LastValueUntyped { get; }
        
        void SampleIfChanged(double time);
        
        // can be used to filter useless animation tracks after sampling
        // (tracks that only have 1 or two recorded samples that do not differ from the initial value)
        object? InitialValueUntyped => ValuesUntyped.Length > 0 ? ValuesUntyped[0] : default;
    }

    public interface AnimationTrack<out TObject, out TData> : AnimationTrack where TObject : Object
    {
        Object? AnimationTrack.AnimatedObjectUntyped => AnimatedObject;
        object[] AnimationTrack.ValuesUntyped => Values.Cast<object>().ToArray();
        object? AnimationTrack.LastValueUntyped => LastValue;

        TObject? AnimatedObject { get; }
        TData[] Values { get; }
        TData? LastValue { get; }

        TData? InitialValue => Values.Length > 0 ? Values[0] : default;
    }
    
    internal abstract class BaseAnimationTrack<TObject, TData> : AnimationTrack<TObject, TData> where TObject : Object
    {
        private readonly List<(double Time, TData Value)> samples;
        
        private readonly AnimationData animationData;
        private readonly AnimationSampler<TObject, TData> sampler;

        private readonly IEqualityComparer<TData> equalityComparer;
        
        public TObject? AnimatedObject => sampler.getTarget(animationData.transform);
        
        public string PropertyName => sampler.PropertyName;

        public InterpolationType InterpolationType => sampler.InterpolationType;

        public double[] Times => samples.Select(t => t.Time).ToArray();
        public TData[] Values => samples.Select(t => t.Value).ToArray();
        
        private (double Time, TData Value)? lastSample => samples.Count > 0 ? samples.Last() : default; 
        private (double Time, TData Value)? secondToLastSample => samples.Count > 1 ? samples.SkipLast(1).Last() : default; 
        
        public double? LastTime => lastSample?.Time ?? default;
        public TData? LastValue => lastSample != null ? lastSample.Value.Value : default;
        
        private double? secondToLastTime => secondToLastSample?.Time ?? default;
        private TData? secondToLastValue => secondToLastSample != null ? secondToLastSample.Value.Value : default;

        
        protected BaseAnimationTrack(AnimationData tr, AnimationSampler<TObject, TData> plan, double time, IEqualityComparer<TData> equalityComparer, Func<TData?, TData?>? overrideInitialValueFunc = null) {
            this.animationData = tr;
            this.sampler = plan;
            this.equalityComparer = equalityComparer;
            samples = new List<(double, TData)>();
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
            // Additional check to make sure we ignore duplicate timestamps.
            // If you think, "This doesn't seem necessary, lets remove this": Removing this is _a bad idea_, do not do it.
            // The resulting animations may otherwise contain the weirdest and seemingly random bugs you can imagine
            // since duplicate timestamps cause the below "replace consecutive equal samples"-logic to trip over
            // its own feet and die a horrible death falling down a cliff.
            // This will cause the duplicated sample value to be recorded, but then removed again entirely and the resulting animation is all kinds of broken:
            // Consider the following example:
            // Event 1: Recording sample at time t1 with value v1, samples = [(t1, v1)], lastSample = (t1, v1), secondToLastSample = null
            // Event 2: Recording sample at time t2 with value v2, samples = [(t1, v1), (t2,v2)], lastSample = (t2, v2), secondToLastSample = (t1, v1)
            // Event 3: Recording sample at time t2 with value v2, samples = [(t1, v1), (t2,v2)], lastSample = (t2, v2), secondToLastSample = (t2, v2)
            //          the logic is now in an unexpected state, where it thinks the (t2,v2) sample is not necessary
            // Event 4: Recording sample at time t3 with value v2, samples = [(t1, v1), (t3,v2)], lastSample = (t3, v2), secondToLastSample = (t2, v2).
            // If you are wondering, why this can even happen since duplicate timestamps are already checked for in the GLTFRecorder:
            // Yeah i am not sure why either, but it definitely happens!
            // Finding this bug cost me 3 days of my life, please don't remove this check.
            if (LastTime.Equals(time)) {
                if(LastValue != null && !LastValue.Equals(value))
                    Debug.LogWarning($"Duplicate timestamp {time} with different values {LastValue} (last sample) and {value} (current sample) in animation track");
                return;
            }
            // As a memory optimization we want to be able to skip identical samples.
            // But, we cannot always skip samples when they are identical to the previous one - otherwise cases like this break:
            // - First assume an object is positioned at coordinates (1,2,3)
            // - At some point in time, it is "instantaneously" teleported to (4,5,6)
            // If we simply skip identical samples on insert, instead of an almost instantaneous
            // teleport we get a linearly interpolated change because only two samples will be recorded:
            // - one with (1,2,3) at the start of time
            // - (4,5,6) at the time of the instantaneous change
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
            if (LastValue != null && secondToLastValue != null) {
                var lastSampled = LastValue;
                var secondLastSampled = secondToLastValue;
                using var __ = lastSampleCheckEquality.Auto(); 
                if(equalityComparer.Equals(lastSampled, secondLastSampled) &&
                    equalityComparer.Equals(lastSampled, value)) {
                    using var ___ = removeLastSample.Auto();
                    samples.RemoveAt(samples.Count - 1);
                }
            }

            lastSampleCheck.End();
            
            insertData.Begin();
            samples.Add((time, value));
            insertData.End();
        }
    }

    internal sealed class AnimationTrackImpl<TObject, TData> : BaseAnimationTrack<TObject, TData> where TObject : Object
    {
        public AnimationTrackImpl(AnimationData tr, AnimationSampler<TObject, TData> plan, double time, IEqualityComparer<TData> equalityComparer) : base(tr, plan, time, equalityComparer) { }
    }
}