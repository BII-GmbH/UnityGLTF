#nullable enable
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
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
            ulong initialSampleIndex
        ) {
            this.transform = transform;
            
            // the visibility track always starts at time = 0, inserting additional invisible samples at the start of the time if required
            visibilityTrack = animationSamplers.VisibilitySampler?.startNewAnimationTrackAt(this, initialSampleIndex);

            foreach (var plan in animationSamplers.GetAdditionalAnimationSamplers()) {
                if (plan.GetTarget(transform)) {
                    var track = plan.StartNewAnimationTrackAt(this, initialSampleIndex);
                    tracks.Add(track);
                }
            }
        }

        public void Update(ulong sampleIndex) {
            using var _ = updateMarker.Auto();
            visibilityUpdate.Begin();
            visibilityTrack?.SampleIfChanged(sampleIndex);
            visibilityUpdate.End();
            otherTracks.Begin();
            foreach (var track in tracks) {
                track.SampleIfChanged(sampleIndex);
            }
            otherTracks.End();
        }
    }
}