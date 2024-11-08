using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Profiling;

namespace UnityGLTF
{
    internal static class AnimationFilteringUtils
    {
	    
	    private static ProfilerMarker removeAnimationUnneededKeyframesMarker = new ProfilerMarker("Simplify Keyframes");
	    private static ProfilerMarker removeAnimationUnneededKeyframesInitMarker = new ProfilerMarker("Init");
	    private static ProfilerMarker removeAnimationUnneededKeyframesCheckIdenticalMarker = new ProfilerMarker("Check Identical");
	    private static ProfilerMarker removeAnimationUnneededKeyframesCheckIdenticalKeepMarker = new ProfilerMarker("Keep Keyframe");
	    private static ProfilerMarker removeAnimationUnneededKeyframesFinalizeMarker = new ProfilerMarker("Finalize");

	    public static (float[], object[]) RemoveUnneededKeyframes(float[] times, object[] values) {
		    if (times.Length <= 1) return (times, values);

		    removeAnimationUnneededKeyframesMarker.Begin();

		    var t2 = new List<float>(times.Length);
		    var v2 = new List<object>(values.Length);

		    var arraySize = values.Length / times.Length;

		    if (arraySize == 1) {
			    t2.Add(times[0]);
			    v2.Add(values[0]);

			    int lastExportedIndex = 0;
			    for (int i = 1; i < times.Length - 1; i++) {
				    removeAnimationUnneededKeyframesCheckIdenticalMarker.Begin();
				    var isIdentical = (lastExportedIndex >= i - 1 || values[lastExportedIndex].Equals(values[i]))
					    && values[i - 1].Equals(values[i])
					    && values[i].Equals(values[i + 1]);
				    if (!isIdentical) {
					    lastExportedIndex = i;
					    t2.Add(times[i]);
					    v2.Add(values[i]);
				    }

				    removeAnimationUnneededKeyframesCheckIdenticalMarker.End();
			    }

			    var max = times.Length - 1;
			    t2.Add(times[max]);
			    v2.Add(values[max]);
		    }
		    else {
			    var singleFrameWeights = new object[arraySize];
			    Array.Copy(values, 0, singleFrameWeights, 0, arraySize);
			    t2.Add(times[0]);
			    v2.AddRange(singleFrameWeights);

			    int lastExportedIndex = 0;
			    for (int i = 1; i < times.Length - 1; i++) {
				    removeAnimationUnneededKeyframesCheckIdenticalMarker.Begin();
				    var isIdentical = arrayRangeEquals(
					    values,
					    arraySize,
					    lastExportedIndex * arraySize,
					    (i - 1) * arraySize,
					    i * arraySize,
					    (i + 1) * arraySize
				    );
				    if (!isIdentical) {
					    Array.Copy(values, (i - 1) * arraySize, singleFrameWeights, 0, arraySize);
					    v2.AddRange(singleFrameWeights);
					    t2.Add(times[i]);
				    }

				    removeAnimationUnneededKeyframesCheckIdenticalMarker.End();
			    }

			    var max = times.Length - 1;
			    t2.Add(times[max]);
			    var skipped = values.Skip((max - 1) * arraySize).ToArray();
			    v2.AddRange(skipped.Take(arraySize));
		    }

		    times = t2.ToArray();
		    values = v2.ToArray();

		    removeAnimationUnneededKeyframesMarker.End();
		    return (times, values);
	    }

	    private static bool arrayRangeEquals(
		    object[] array,
		    int sectionLength,
		    int lastExportedSectionStart,
		    int prevSectionStart,
		    int sectionStart,
		    int nextSectionStart
	    ) {
		    var equals = true;
		    for (int i = 0; i < sectionLength; i++) {
			    equals &=
				    (lastExportedSectionStart >= prevSectionStart
					    || array[lastExportedSectionStart + i].Equals(array[sectionStart + i]))
				    && array[prevSectionStart + i].Equals(array[sectionStart + i])
				    && array[sectionStart + i].Equals(array[nextSectionStart + i]);
			    if (!equals) return false;
		    }

		    return true;
	    }
    }
}