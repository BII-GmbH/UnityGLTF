using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityGLTF.Timeline.Samplers
{
    internal sealed class TranslationSampler : AnimationSampler<Transform, Vector3>
    {
        private readonly Func<Transform, bool> sampleInWorldSpace;
        public TranslationSampler(Func<Transform, bool> sampleInWorldSpace) => this.sampleInWorldSpace = sampleInWorldSpace;

        public override string PropertyName => "translation";

        public override AnimationInterpolationType InterpolationType => AnimationInterpolationType.LINEAR;

        public override IEqualityComparer<Vector3> DataComparer => EqualityComparer<Vector3>.Default;
        internal override Transform getTarget(Transform transform) => transform;

        public override Vector3 GetValue(Transform transform, Transform target, AnimationData data) =>
            sampleInWorldSpace(transform) ? transform.position : transform.localPosition;
    }
    
    internal sealed class RotationSampler : AnimationSampler<Transform, Quaternion>
    {
        private readonly Func<Transform, bool> sampleInWorldSpace;
        public RotationSampler(Func<Transform, bool> sampleInWorldSpace) => this.sampleInWorldSpace = sampleInWorldSpace;
        public override string PropertyName => "rotation";

        public override AnimationInterpolationType InterpolationType => AnimationInterpolationType.LINEAR;

        public override IEqualityComparer<Quaternion> DataComparer => EqualityComparer<Quaternion>.Default;
        internal override Transform getTarget(Transform transform) => transform;
        public override Quaternion GetValue(Transform transform, Transform target, AnimationData data) =>
            sampleInWorldSpace(transform) ? transform.rotation : transform.localRotation;
    }
    
    internal sealed class ScaleSampler : AnimationSampler<Transform, Vector3>
    {
        private readonly Func<Transform, bool> sampleInWorldSpace;
        public ScaleSampler(Func<Transform, bool> sampleInWorldSpace) => this.sampleInWorldSpace = sampleInWorldSpace;
        
        public override string PropertyName => "scale";

        public override AnimationInterpolationType InterpolationType => AnimationInterpolationType.LINEAR;

        public override IEqualityComparer<Vector3> DataComparer => EqualityComparer<Vector3>.Default;
        internal override Transform getTarget(Transform transform) => transform;
        public override Vector3 GetValue(Transform transform, Transform target, AnimationData data) =>
            sampleInWorldSpace(transform) ? transform.lossyScale : transform.localScale;
    }
}