using System.Collections.Generic;
using GLTF.Schema;
using UnityEngine;

namespace UnityGLTF.Timeline.Samplers
{
    internal readonly struct Visibility
    {
        public readonly bool Visible;

        public Visibility(bool visible) => Visible = visible;

        public override bool Equals(object obj) => obj is Visibility o && Equals(o);

        public bool Equals(Visibility other) => Visible == other.Visible;

        public override int GetHashCode() => Visible.GetHashCode();
    }

    internal sealed class VisibilitySampler : AnimationSampler<GameObject, bool>
    {
        public override string PropertyName => "visibility";

        public override InterpolationType InterpolationType => InterpolationType.STEP;

        public override IEqualityComparer<bool> DataComparer => EqualityComparer<bool>.Default;

        internal VisibilityTrack startNewAnimationTrackAtStartOfTime(AnimationData data, double time) =>
            new VisibilityTrack(data, this, time);

        internal override GameObject getTarget(Transform transform) => transform.gameObject;

        public override bool GetValue(Transform transform, GameObject target, AnimationData data) =>
            target.activeSelf;
    }

    internal sealed class VisibilityTrack : BaseAnimationTrack<GameObject, bool>
    {
        public VisibilityTrack(AnimationData tr, VisibilitySampler plan, double time) :
            base(tr, plan, time, plan.DataComparer, objectVisibility => {
                var overridenVisibility = time <= 0 && objectVisibility;
                return overridenVisibility;
            }) { }

        internal void recordVisibilityAt(double time, bool visible) => recordSampleIfChanged(time, visible);
    }
}