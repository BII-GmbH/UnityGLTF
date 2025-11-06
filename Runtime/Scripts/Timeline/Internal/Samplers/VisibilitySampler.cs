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

        internal VisibilityTrack startNewAnimationTrackAt(AnimationData data, ulong time) {
            // pass null as time here to force manual initial sample recording
            var track = new VisibilityTrack(data, this, initialSampleIndex: null);
            
            
            if(time == 0)
                track.SampleIfChanged(0);
            else {
                // we are not at the start of time when starting this track, so the thing is invisible until the current time.
                // We need to add the samples for that
                track.recordVisibilityAt(0, false);
                // if time == 1 the first frame it would be invisible and interpolate to visible at time 1. We dont need an additional sample in this case
                if(time > 1)
                    track.recordVisibilityAt(time - 1, false);
                track.SampleIfChanged(time);
            }

            return track;
        }

        internal override GameObject getTarget(Transform transform) {
            if (!transform) 
                return null;
            return transform.gameObject;
        }

        public override bool GetValue(Transform transform, GameObject target, AnimationData data) =>
            target.activeSelf;
    }

    internal sealed class VisibilityTrack : BaseAnimationTrack<GameObject, bool>
    {
        public VisibilityTrack(AnimationData tr, VisibilitySampler plan, ulong? initialSampleIndex) :
            base(tr, plan, initialSampleIndex, plan.DataComparer) { }

        internal void recordVisibilityAt(ulong time, bool visible) => recordSampleIfChanged(time, visible);
    }
}