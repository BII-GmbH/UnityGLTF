using System;
using GLTF.Schema;

namespace UnityGLTF
{
    /// <summary>
    /// The supported interpolation types for animation tracks.
    /// This is a copy of the GLTF.Schema.InterpolationType enum.
    /// It is intentionally a copy for two reasons:
    /// 1. We dont need to depend on the GLTF.Schema namespace
    /// 2. We have the ability to add additional values that are converted
    ///    to the GLTF.Schema.InterpolationType enum on export.
    /// </summary>
    public enum AnimationInterpolationType
    {
        LINEAR,
        STEP,
        CATMULLROMSPLINE,
        CUBICSPLINE
    }

    public static class AnimationInterpolationTypeExtensions
    {
        public static InterpolationType ToSchemaEnum(this AnimationInterpolationType interpolation) {
            return interpolation switch {
                AnimationInterpolationType.LINEAR => InterpolationType.LINEAR,
                AnimationInterpolationType.STEP => InterpolationType.STEP,
                AnimationInterpolationType.CATMULLROMSPLINE => InterpolationType.CATMULLROMSPLINE,
                AnimationInterpolationType.CUBICSPLINE => InterpolationType.CUBICSPLINE,
                _ => throw new ArgumentOutOfRangeException(nameof(interpolation), interpolation, null)
            };
        }
    }
}