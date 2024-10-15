#nullable enable
using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityGLTF.Timeline.Samplers;

namespace UnityGLTF.Timeline
{
    internal sealed class AnimationData
    {
        private static readonly ProfilerMarker updateMarker = new ProfilerMarker("AnimationData - Update");
        private static readonly ProfilerMarker visibilityUpdate = new ProfilerMarker("AnimationData - Visibility Update");
        private static readonly ProfilerMarker otherTracks = new ProfilerMarker("AnimationData - Other Tracks");
        
        internal readonly Transform transform;
        
        /// GLTF natively does not support animated visibility - as a result it has to be merged with the scale track later on
        /// in the export process.
        /// At the same time visibility has a higher priority than the other tracks, since
        /// there is no point in animating properties of an invisible object.
        /// These requirements / constraints are easier to fulfill when we store the visibility track explicitly
        /// instead of putting it in the <see cref="tracks"/> field alongside the other tracks. 
        internal readonly VisibilityTrack? visibilityTrack;
        
        internal readonly List<AnimationTrack> tracks = new List<AnimationTrack>();
        
        public AnimationData(
            AnimationSamplers animationSamplers,
            Transform transform,
            double time
        ) {
            this.transform = transform;
            
            // the visibility track always starts at time = 0, inserting additional invisible samples at the start of the time if required
            visibilityTrack = animationSamplers.VisibilitySampler?.startNewAnimationTrackAtStartOfTime(this, time);
            if (visibilityTrack != null && time > 0) {
                // make sure to insert another sample right before the change so that the linear interpolation is very short, not from the start of time
                visibilityTrack.recordVisibilityAt(time-Double.Epsilon, visibilityTrack.LastValue is { HasValue: true, Value: true });
                // if we are not at the start of time, add another visibility sample to the current time, where the object started to exist
                visibilityTrack.SampleIfChanged(time);
            }

            foreach (var plan in animationSamplers.GetAdditionalAnimationSamplers()) {
                if (plan.GetTarget(transform)) {
                    var track = plan.StartNewAnimationTrackAt(this, time);
                    tracks.Add(track);
                }
            }
        }

        public void Update(double time) {
            using var _ = updateMarker.Auto();
            visibilityUpdate.Begin();
            visibilityTrack?.SampleIfChanged(time);
            visibilityUpdate.End();
            // if visibility is not being sampled, or the object is currently visible, sample the other tracks
            if (visibilityTrack == null || visibilityTrack.LastValue.HasValue) {
                otherTracks.Begin();
                foreach (var track in tracks) {
                    track.SampleIfChanged(time);
                }
                otherTracks.End();
            }
        }
    }
}