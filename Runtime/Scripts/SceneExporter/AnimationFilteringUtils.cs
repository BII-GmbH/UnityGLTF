using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Unity.Profiling;

namespace UnityGLTF
{
	// Contains some util functions to remove duplicate entries from the keyframe arrays
    internal static class AnimationFilteringUtils
    {
	    private static readonly ProfilerMarker removeAnimationUnneededKeyframesMarker = new ProfilerMarker("Simplify Keyframes");
	    private static readonly ProfilerMarker removeAnimationUnneededKeyframesFindDuplicatesMarker = new ProfilerMarker("Search&Find Duplicate Keyframes");
	    private static readonly ProfilerMarker removeAnimationUnneededKeyframesCopyWithoutDuplicatesMarker = new ProfilerMarker("Copy Without Duplicates Keyframes");
	    private static readonly ProfilerMarker removeAnimationUnneededKeyframesCheckIdenticalMarker = new ProfilerMarker("Check Identical");

	    
	    /// <summary>
	    /// Remove Unnecessary keyframes from the animation arrays passed in.
	    /// Keyframes are deemed unnecessary if and only if their value is identical to the keyframes before and after them.
	    /// 
	    /// Expects the arrays to be of the same length. If they are not,
	    /// the function will attempt to return a best-effort result, but it may not be what you expect.
	    /// </summary>
	    /// <param name="times">The timestamps of the animations</param>
	    /// <param name="values">The values of the animation at the timestamp at the corresponding index</param>
	    /// <returns></returns>
	    [Pure]
	    public static (float[], object[]) RemoveUnneededKeyframes(float[] times, object[] values) {
		    if (times.Length <= 1) return (times, values);

		    using var _ = removeAnimationUnneededKeyframesMarker.Auto();

		    // NOTE: This check previously allowed for slight differences in the length due to integer division.
		    // Although it worked correctly, this _felt_ very error-prone & hard to reason about so we limit
		    // this to the exact same length now, which is the only case that makes sense anyway.
		    if (values.Length == times.Length) {
			    // This follows a very simple approach:
			    // 1.   In  first iteration, search for any occurrences of three identical values in a row and put them in a queue.
			    // 1.1. If we did not find any, we avoid any further work and just return the input.
			    // 2.   If we found such values, we create a new list and copy all values except
			    //      the duplicates over, of course leaving out both the time and the value for duplicates
			    
			    removeAnimationUnneededKeyframesFindDuplicatesMarker.Begin();
			    var foundDuplicates = new Queue<int>();
			    var lastEquals = false;
			    // cache-friendly optimized loop that only does one comparison+one AND per iteration,
			    // re-using the last result.
			    for (var i = 0; i < values.Length - 1; i++) {
				    var nextEquals = values[i].Equals(values[i + 1]);
				    if (lastEquals && nextEquals) {
					    foundDuplicates.Enqueue(i);
				    }
				    lastEquals = nextEquals;
			    }
				removeAnimationUnneededKeyframesFindDuplicatesMarker.End();
			    if (foundDuplicates.Count <= 0) return (times, values);
			    removeAnimationUnneededKeyframesCopyWithoutDuplicatesMarker.Begin();
			    var t2 = new List<float>(times.Length);
			    var v2 = new List<object>(values.Length);
			    
			    var nextDuplicate = foundDuplicates.Dequeue();
			    for (var i = 0; i < values.Length; i++) {
				    if (i == nextDuplicate) {
					    // deque may fail due to there not being any more duplicates - we dont want
					    // to do anything different in that case since we still need to copy over
					    // the remaining values to the new lists.
					    // But we need to handle the case that the deque fails - so use TryDequeue
					    var __ = foundDuplicates.TryDequeue(out nextDuplicate);
					    continue;
				    }
				    t2.Add(times[i]);
				    v2.Add(values[i]);
				    
			    }
			    removeAnimationUnneededKeyframesCopyWithoutDuplicatesMarker.End();
			    return (t2.ToArray(), v2.ToArray());
		    } else {
			    // Note: This branch is chaos & not covered by unit tests.
			    // I do not understand why it exists in the first place, but I do not want to outright remove it.
			    // - When is this ever going to be hit, were it is not an actual error case?
			    // - When is this ever going to produce the expected result?
			    var arraySize = values.Length / times.Length;
			    var singleFrameWeights = new object[arraySize];
			    Array.Copy(values, 0, singleFrameWeights, 0, arraySize);
			    
			    var t2 = new List<float>(times.Length);
			    var v2 = new List<object>(values.Length);
			    
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
			    
			    return (t2.ToArray(), v2.ToArray());
		    }
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