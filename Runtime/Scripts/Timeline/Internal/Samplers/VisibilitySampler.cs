using System.Collections.Generic;
using UnityEngine;

namespace UnityGLTF.Timeline.Samplers
{
    internal sealed class VisibilitySampler : AnimationSampler<GameObject, bool>
    {
        public override string PropertyName => "visibility";

        // Because visibility cannot be animated properly in the gltf and has to use the scale track, we never want the
        // visibility track to be interpolated linearly between samples, but instaed to be stepped.
        // Note that this track is never exported to the gltf, since it is merged with scale, which likely uses LINEAR,
        // so this value may not show up in the file.
        public override AnimationInterpolationType InterpolationType => AnimationInterpolationType.STEP;

        public override IEqualityComparer<bool> DataComparer => EqualityComparer<bool>.Default;

        internal VisibilityTrack startNewAnimationTrackAtStartOfTime(AnimationData data, float time) =>
            new VisibilityTrack(data, this, time);

        internal override GameObject getTarget(Transform transform) => transform.gameObject;

        public override bool GetValue(Transform transform, GameObject target, AnimationData data) =>
            target.activeSelf;
    }

    internal sealed class VisibilityTrack : BaseAnimationTrack<GameObject, bool>
    {
        public VisibilityTrack(AnimationData tr, VisibilitySampler plan, float time) :
            base(tr, plan, time, plan.DataComparer, objectVisibility => {
                var overridenVisibility = time <= 0 && objectVisibility;
                return overridenVisibility;
            }) { }

        internal void recordVisibilityAt(float time, bool visible) => recordSampleIfChanged(time, visible);
    }
}