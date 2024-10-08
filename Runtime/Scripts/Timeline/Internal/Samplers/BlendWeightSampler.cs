using System.Collections.Generic;
using UnityEngine;

namespace UnityGLTF.Timeline.Samplers
{
    internal sealed class BlendWeightSampler : AnimationSampler<SkinnedMeshRenderer, float[]>
    {
        public override string PropertyName => "weights";
        public override IEqualityComparer<float[]> DataComparer => EqualityComparer<float[]>.Default;
        
        protected override SkinnedMeshRenderer getTarget(Transform transform) => transform.TryGetComponent<SkinnedMeshRenderer>(out var smr) ? smr : null;

        public override float[] GetValue(Transform transform, SkinnedMeshRenderer target, AnimationData data) {
            if (target.sharedMesh) {
                var mesh = target.sharedMesh;
                var blendShapeCount = mesh.blendShapeCount;
                if (blendShapeCount == 0) return null;
                var weights = new float[blendShapeCount];
                for (var i = 0; i < blendShapeCount; i++)
                    weights[i] = target.GetBlendShapeWeight(i);
                return weights;
            }
            return null;
        }
    }
}