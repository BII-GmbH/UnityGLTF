using System.Collections.Generic;
using UnityEngine;

namespace UnityGLTF.Timeline.Samplers
{
    internal sealed class BaseColorSampler : AnimationSampler<Material, Color?>
    {
        private readonly int materialIndex;
        
        public BaseColorSampler(int materialIndex) => this.materialIndex = materialIndex;

        public override string PropertyName => "baseColorFactor";

        public override AnimationInterpolationType InterpolationType => AnimationInterpolationType.LINEAR;
        
        public override IEqualityComparer<Color?> DataComparer => EqualityComparer<Color?>.Default;

        internal override Material getTarget(Transform transform) {
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