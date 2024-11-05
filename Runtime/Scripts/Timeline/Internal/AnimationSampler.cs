#nullable enable
using System;
using System.Collections.Generic;
using GLTF.Schema;
using JetBrains.Annotations;
using UnityEngine;
using UnityGLTF.Timeline.Samplers;
using Object = UnityEngine.Object;

namespace UnityGLTF.Timeline
{
    
    internal sealed class AnimationSamplers
    {
        /// GLTF internally does not seem to support animated visibility state, but recording this explicitly makes a lot of things easier.
        /// The resulting animation track will be merged with the "scale" track of any animation while exporting, forcing the scale
        /// to (0,0,0) when the object is invisible.
        public VisibilitySampler? VisibilitySampler { get; private set; }

        /// all animation samplers that do not require special treatment - so currently all others, except visibility 
        private readonly Dictionary<Type, AnimationSampler> registeredAnimationSamplers =
            new Dictionary<Type, AnimationSampler>();

        public static AnimationSamplers From(
            Func<Transform, bool> useWorldSpaceForTransform,
            bool sampleVisibility,
            bool recordBlendShapes,
            bool recordAnimationPointer,
            IEnumerable<AnimationSampler>? additionalSamplers = null
        ) {
            var otherSamplers = new List<AnimationSampler>{
                new TranslationSampler(useWorldSpaceForTransform),
                new RotationSampler(useWorldSpaceForTransform),
                new ScaleSampler(useWorldSpaceForTransform)
            };
            if (recordBlendShapes) {
                otherSamplers.Add(new BlendWeightSampler());
            }
            if (recordAnimationPointer) {
                // TODO add other animation pointer export plans
                otherSamplers.Add(new BaseColorSampler());
            }
            if (additionalSamplers != null) {
                otherSamplers.AddRange(additionalSamplers);
            }
            return new AnimationSamplers(sampleVisibility, otherSamplers);
        }
        
        public AnimationSamplers(bool sampleVisibility, IEnumerable<AnimationSampler> otherSamplers) {
            VisibilitySampler = sampleVisibility ? new VisibilitySampler() : null;

            foreach (var sampler in otherSamplers) {
                registeredAnimationSamplers.TryAdd(sampler.GetType(), sampler);
            }
        }

        public IEnumerable<AnimationSampler> GetAdditionalAnimationSamplers() => registeredAnimationSamplers.Values;
    }
    
    internal interface AnimationSampler
    {
        public string PropertyName { get; }
        
        public InterpolationType InterpolationType { get; }
        
        object? Sample(AnimationData data);

        public Object? GetTarget(Transform transform);
        
        public AnimationTrack StartNewAnimationTrackAt(AnimationData data, float time);
    }
    
    internal abstract class AnimationSampler<TObject, TData> : AnimationSampler
        where TObject : UnityEngine.Object
    {
        public abstract string PropertyName { get; }
        public abstract InterpolationType InterpolationType { get; }
        public Type dataType => typeof(TData);

        public abstract IEqualityComparer<TData> DataComparer { get; }
        
        public object? Sample(AnimationData data) => sample(data);

        public Object? GetTarget(Transform transform) => getTarget(transform);
        internal abstract TObject? getTarget(Transform transform);
        public abstract TData? GetValue(Transform transform, TObject target, AnimationData data);

        internal TData? sample(AnimationData data) {
            var target = getTarget(data.transform);
            return target != null ? GetValue(data.transform, target, data) : default;
        }
        
        public AnimationTrack StartNewAnimationTrackAt(AnimationData data, float time) =>
            new AnimationTrackImpl<TObject, TData>(data, this, time, DataComparer);
    }

    internal sealed class CustomAnimationSamplerWrapper<TComponent, TData> : AnimationSampler<TComponent, TData> where TComponent : Component
    {

        public override string PropertyName => customSampler.PropertyName;

        public override InterpolationType InterpolationType => customSampler.InterpolationType;

        public override IEqualityComparer<TData> DataComparer => customSampler.EqualityComparer;

        internal override TComponent? getTarget(Transform transform) => customSampler.getTarget(transform);

        public override TData? GetValue(Transform transform, TComponent target, AnimationData data) =>
            customSampler.GetValue(transform, target);

        private readonly CustomComponentAnimationSampler<TComponent, TData> customSampler;
        public CustomAnimationSamplerWrapper(CustomComponentAnimationSampler<TComponent, TData> customSampler) => this.customSampler = customSampler;

    }
}