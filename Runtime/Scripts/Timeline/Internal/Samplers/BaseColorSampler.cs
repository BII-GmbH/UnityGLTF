using System.Collections.Generic;
using UnityEngine;

namespace UnityGLTF.Timeline.Samplers
{
    internal sealed class BaseColorSampler : AnimationSampler<Material, Color?>
    {
        public override string PropertyName => "baseColorFactor";

        public override AnimationInterpolationType InterpolationType => AnimationInterpolationType.LINEAR;

        public override IEqualityComparer<Color?> DataComparer => EqualityComparer<Color?>.Default;

        internal override Material getTarget(Transform transform) {
            if (!transform) 
                return null;
            if (transform.TryGetComponent<MeshRenderer>(out var mr)) 
                return mr.sharedMaterial;
            if (transform.TryGetComponent<SkinnedMeshRenderer>(out var smr))
                return smr.sharedMaterial;
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