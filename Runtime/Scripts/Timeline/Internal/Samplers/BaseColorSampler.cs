using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace UnityGLTF.Timeline.Samplers
{
    /// Samples the color of a material to sample it changing over time.
    /// Since a renderer can have multiple materials, the materialIndex parameter
    /// determines which material to sample. If you want to sample multiple materials,
    /// create multiple instances of this sampler with different materialIndex values.
    /// <remarks>
    /// The reason for this way of handling multiple materials - this is the most compatible and straightforward way:
    /// 
    /// The first generic parameter has to be the target - which is the material itself under GLTF's documentation.
    /// https://github.com/KhronosGroup/glTF/blob/main/extensions/2.0/Khronos/KHR_animation_pointer/README.md
    /// The second generic parameter is the data type - which must be Color.
    ///
    /// Alternatives, such as having a list of materials as targets would be more complex to implement and use.
    /// </remarks>
    internal sealed class BaseColorSampler : AnimationSampler<Material, Color?>
    {
        private static readonly ProfilerMarker getTargetMarker = new ProfilerMarker("BaseColorSampler - GetTarget");
        
        private readonly int materialIndex;
        
        public BaseColorSampler(int materialIndex) => this.materialIndex = materialIndex;

        public override string PropertyName => "baseColorFactor";

        public override AnimationInterpolationType InterpolationType => AnimationInterpolationType.LINEAR;
        
        public override IEqualityComparer<Color?> DataComparer => EqualityComparer<Color?>.Default;

        internal override Material getTarget(Transform transform) {
            // Note DG: Since this sampler may be instantiated multiple times for
            // different material indices, we need to keep an eye on performance here.
            // This method is called once per sample per transform per unique sampler.
            // Thus, the number of supported materials per renderer should be kept low.
            // If they are very high, this profile marker is an indicator that optimizations may be needed.
            using var _ = getTargetMarker.Auto();
            if (!transform) 
                return null;
            if (transform.TryGetComponent<MeshRenderer>(out var mr)) 
                return mr.sharedMaterials.Length > materialIndex ? mr.sharedMaterials[materialIndex] : null;
            if (transform.TryGetComponent<SkinnedMeshRenderer>(out var smr))
                return smr.sharedMaterials.Length > materialIndex ? smr.sharedMaterials[materialIndex] : null;
            return null;
        }

        public override Color? GetValue(Transform transform, Material target, AnimationData data) {
            
            if (target) {
                if (target.HasProperty("_BaseColor")) return target.GetColor("_BaseColor");
                if (target.HasProperty("_Color")) return target.GetColor("_Color");
                if (target.HasProperty("baseColorFactor")) return target.GetColor("baseColorFactor");
            }
            return null;
        }
    }
}