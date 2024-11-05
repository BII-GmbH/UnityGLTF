// using System.Linq;
// using NUnit.Framework;
// using UnityEngine;
// using UnityGLTF.Timeline;
//
// namespace Tests.Editor
// {
//     public class VisibilityTrackMergeHelperTests
//     {
//
//         public class MergeTests
//         {
//             private static void assertSequenceEqual((double Time, Vector3 Scale)[] expected, (double Time, Vector3 Scale)[] gotten) {
//                 
//                 Assert.AreEqual(
//                     expected.Length,
//                     gotten.Length,
//                     "Expected and gotten arrays have different lengths"
//                 );
//
//                 for (int i = 0; i < expected.Length; i++) {
//                     Assert.AreEqual(
//                         expected[i].Time,
//                         gotten[i].Time,
//                         $"Time mismatch at index {i}:"
//                     );
//                     Assert.AreEqual(
//                         expected[i].Scale,
//                         gotten[i].Scale,
//                         $"Scale mismatch at index {i}:"
//                     );
//                 }
//             }
//
//             [Test]
//             public void Merging_ReturnsExpectedResult() {
//                 // Test Samples:
//                 // 0 : Start
//                 // 0.1: Same Sample Time
//                 // 0.2: Same Sample Time
//                 // 0.3: Next: Scale
//                 // 0.4: Next: Scale
//                 // 0.5: Next: Scale
//                 // 0.6: Next: Visibility
//                 // 0.7: Next: Scale
//                 // 0.8: Next: Scale
//                 // 0.9: Next: Scale
//                 // 1: End
//                 
//                 var scaleTimes = new double[] { 0, 0.1, 0.3, 0.4, 0.5, 0.7, 0.8, 0.9, 1 };
//
//                 var scaleValues = new Vector3[] {
//                     Vector3.one, Vector3.one, new Vector3(2, 2, 2), Vector3.one, new Vector3(2, 2, 2), Vector3.one,
//                     Vector3.one, Vector3.zero, Vector3.zero
//                 };
//                 var visTimes = new double[] { 0, 0.2, 0.5, 0.8, 1 };
//                 var visValues = new bool[] { false, true, false, true, true };
//
//                 var expectedResult = new (double Time, Vector3 Scale)[] {
//                     (0.0, Vector3.zero),
//                     // 0.1 is not recorded since we are invisible  
//                     (0.2.nextSmaller(), Vector3.zero),
//                     (0.2, new Vector3(1.5f, 1.5f, 1.5f)),
//                     (0.3, new Vector3(2, 2, 2)),
//                     (0.4, Vector3.one), 
//                     (0.5.nextSmaller(), new Vector3(2, 2, 2)),
//                     (0.5, Vector3.zero),
//                     // neither has a sample at 0.6 
//                     // 0.7 is not recorded since we are invisible
//                     (0.8.nextSmaller(), Vector3.zero), 
//                     (0.8, Vector3.one), 
//                     (0.9, Vector3.zero),
//                     (1.0, Vector3.zero)
//                 };
//
//                 var uut = new MergeVisibilityAndScaleTrackMerger(visTimes, visValues, scaleTimes, scaleValues);
//                 assertSequenceEqual(expectedResult, uut.Merge().ToArray());
//             }
//
//             [Test] public void IfVisibilityHasMoreEntriesThanScale_TheyAreCorrectlyAppendedToEnd() {
//                 
//                 var scale = new Vector3(2, 2, 2);
//                 var scaleTimes = new double[] {
//                     0, 0.1
//                 };
//                 
//                 var scaleValues = new Vector3[] {
//                     Vector3.one,
//                     scale
//                 };
//                 var visTimes = new double[] {
//                     0, 0.1, 0.5, 0.8, 1
//                 };
//                 var visValues = new bool[] { false, true, false, true, true };
//                 
//                 var expectedResult = new (double Time, Vector3 Scale)[] {
//                     (0, Vector3.zero),          
//                     (0.1.nextSmaller(), Vector3.zero),         
//                     (0.1, scale),   
//                     (0.5.nextSmaller(), scale),          
//                     (0.5, Vector3.zero),          
//                     (0.8.nextSmaller(), Vector3.zero),   
//                     (0.8, scale),   
//                     (1, scale)
//                 };
//                 
//                 var uut = new MergeVisibilityAndScaleTrackMerger(visTimes, visValues, scaleTimes, scaleValues);
//
//                 var gotten = uut.Merge().ToArray();
//                 assertSequenceEqual(expectedResult, gotten);
//             }
//
//             [Test] public void IfScaleHasMoreEntriesThanVisibility_ButLastInvisible_TheseSamplesAreIgnored() {
//                 
//                 var scale = new Vector3(2, 2, 2);
//                 var scaleTimes = new double[] {
//                     0, 0.1, 0.5, 0.8, 1
//                 };
//                 
//                 var scaleValues = new Vector3[] {
//                     Vector3.one,
//                     scale,
//                     new (3,3,3),
//                     new (4,4,4),
//                     new (5,5,5),
//                 };
//                 var visTimes = new double[] {
//                     0, 0.1
//                 };
//                 var visValues = new bool[] { true, false };
//                 
//                 var expectedResult = new (double Time, Vector3 Scale)[] {
//                     (0, Vector3.one),          
//                     (0.1.nextSmaller(), scale),         
//                     (0.1, Vector3.zero)
//                 };
//                 
//                 var uut = new MergeVisibilityAndScaleTrackMerger(visTimes, visValues, scaleTimes, scaleValues);
//
//                 assertSequenceEqual(expectedResult, uut.Merge().ToArray());
//             }
//             
//             [Test] public void IfScaleHasMoreEntriesThanVisibility_AndLastVisible_ThenSamplesAreAppendedToEnd() {
//                 
//                 var scale = new Vector3(2, 2, 2);
//                 var scaleTimes = new double[] {
//                     0, 0.1, 0.5, 0.8, 1
//                 };
//                 
//                 var scaleValues = new Vector3[] {
//                     Vector3.one,
//                     scale,
//                     new (3,3,3),
//                     new (4,4,4),
//                     new (5,5,5),
//                 };
//                 var visTimes = new double[] {
//                     0, 0.1
//                 };
//                 var visValues = new bool[] { false, true };
//                 
//                 var expectedResult = new (double Time, Vector3 Scale)[] {
//                     (0, Vector3.zero),          
//                     (0.1.nextSmaller(), Vector3.zero),         
//                     (0.1, scale),
//                     (0.5, new Vector3(3,3,3)),
//                     (0.8, new Vector3(4,4,4)),
//                     (1.0, new Vector3(5,5,5)),
//                 };
//                 
//                 var uut = new MergeVisibilityAndScaleTrackMerger(visTimes, visValues, scaleTimes, scaleValues);
//                 
//                 var gotten = uut.Merge().ToArray();
//                 assertSequenceEqual(expectedResult, gotten);
//             }
//             
//             [Test] public void IfScaleStartsAfterVisibility_FirstScaleIsUsed() {
//                 
//                 var scale = new Vector3(2, 2, 2);
//                 var scaleTimes = new double[] {
//                     0.5, 0.8, 1.0
//                 };
//                 
//                 var scaleValues = new Vector3[] {
//                     scale,
//                     new (4,4,4),
//                     new (5,5,5),
//                 };
//                 var visTimes = new double[] {
//                     0, 0.1, 0.3, 0.5, 0.8
//                 };
//                 var visValues = new bool[] { false, true, false, true, true };
//                 
//                 var expectedResult = new (double Time, Vector3 Scale)[] {
//                     (0, Vector3.zero),
//                     (0.1.nextSmaller(), Vector3.zero),          
//                     (0.1, scale),
//                     (0.3.nextSmaller(), scale),         
//                     (0.3, Vector3.zero),
//                     (0.5.nextSmaller(), Vector3.zero),          
//                     (0.5, scale),
//                     (0.8, new (4,4,4)),
//                     (1.0, new (5,5,5)),
//                 };
//                 
//                 var uut = new MergeVisibilityAndScaleTrackMerger(visTimes, visValues, scaleTimes, scaleValues);
//
//                 assertSequenceEqual(expectedResult, uut.Merge().ToArray());
//             }
//             
//             [Test] public void IfVisibilityStartsAfterScale_FirstVisibilityIsUsed() {
//                 
//                 var scale = new Vector3(2, 2, 2);
//                 var scaleTimes = new double[] {
//                     0.0, 0.2, 0.3, 0.5, 0.8
//                 };
//                 
//                 var scaleValues = new Vector3[] {
//                     new (2,2,2),
//                     new (4,4,4),
//                     new (5,5,5),
//                     new (6,6,6),
//                     new (5,5,5),
//                 };
//                 var visTimes = new double[] {
//                     0.3, 0.5
//                 };
//                 // since vis only has two potential value, it is much more likely that the implementation 
//                 // would still pass this test even if the value is hardcoded - avoid that by testing
//                 // both possibilities pass
//                 var visValues1 = new bool[] { false, true };
//                 var visValues2 = new bool[] { true, true };
//                 
//                 var expectedResult1 = new (double Time, Vector3 Scale)[] {
//                     (0.5.nextSmaller(), Vector3.zero),          
//                     (0.5, new (6,6,6)),
//                     (0.8, new (5,5,5)),
//                 };
//                 var expectedResult2 = new (double Time, Vector3 Scale)[] {
//                     (0,  new Vector3(2,2,2)),
//                     (0.2, new (4,4,4)),
//                     (0.3, new (5,5,5)),
//                     (0.5, new (6,6,6)),
//                     (0.8, new (5,5,5)),
//                 };
//                 
//                 
//                 var uut1 = new MergeVisibilityAndScaleTrackMerger(visTimes, visValues1, scaleTimes, scaleValues);
//                 var uut2 = new MergeVisibilityAndScaleTrackMerger(visTimes, visValues2, scaleTimes, scaleValues);
//
//                 assertSequenceEqual(expectedResult1, uut1.Merge().ToArray());
//                 assertSequenceEqual(expectedResult2, uut2.Merge().ToArray());
//             }
//             
//         }
//
//         public class MergeSamplesForBothTests
//         {
//             //               L            C
//             //   V 1.0 -| -  x------------x  -   -   -
//             //     0.0 -|
//             //   S 2.0 -| -  -  -  -  -  -x  -   -   -
//             //     0.0 -|             
//             [Test]
//             public void IfLastVisibleAndCurrentVisible_ScaleSampleIsReturned() {
//                 const bool lastVisible = true;
//                 const bool currentVisible = true;
//
//                 const double time = 1.0;
//
//                 var scale = new Vector3(2, 2, 2);
//
//                 var result = MergeVisibilityAndScaleTrackMerger.handleBothSampledAtSameTime(
//                         time,
//                         currentVisible,
//                         scale,
//                         lastVisible
//                     )
//                     .ToArray();
//
//                 Assert.AreEqual(1, result.Length);
//
//                 Assert.AreEqual(time, result[0].Time);
//                 Assert.AreEqual(new Vector3(2, 2, 2), result[0].Scale);
//             }
//
//             //                   L            C
//             //   V 1.0 -|
//             //     0.0 -| -   -  x------------x  -   -   - 
//             [Test]
//             public void IfLastInvisibleAndCurrentInvisible_NoSamplesAreReturned() {
//                 const bool lastVisible = false;
//                 const bool currentVisible = false;
//
//                 const double time = 1.0;
//
//                 var scale = Vector3.one;
//
//                 var result = MergeVisibilityAndScaleTrackMerger.handleBothSampledAtSameTime(
//                     time,
//                     currentVisible,
//                     scale,
//                     lastVisible
//                 );
//                 Assert.IsEmpty(result);
//             }
//
//             //                   L        C
//             //   V 1.0 -| -   -  x---------
//             //     0.0 -|                 x  -  -  -  -  - 
//             //
//             //   S 3.0 -| -  x------------x  -  -  -  -  -
//             //     0.0 -|                  
//             //
//             // Out 3.0 -| -  x------------x 
//             //     0.0 -|                  x__________   
//             [Test]
//             public void IfLastVisible_ButNextInvisible_TransitionFromScaleToInvisibleIsCreated() {
//                 const bool lastVisible = true;
//                 const bool currentVisible = false;
//
//                 const double visTime = 1.0;
//
//                 var scale = new Vector3(3, 3, 3);
//
//                 var result = MergeVisibilityAndScaleTrackMerger.handleBothSampledAtSameTime(
//                         visTime,
//                         currentVisible,
//                         scale,
//                         lastVisible
//                     )
//                     .ToArray();
//
//                 Assert.AreEqual(2, result.Length);
//
//                 Assert.AreEqual(visTime.nextSmaller(), result[0].Time);
//                 Assert.AreEqual(new Vector3(3, 3, 3), result[0].Scale);
//
//                 Assert.AreEqual(visTime, result[1].Time);
//                 Assert.AreEqual(Vector3.zero, result[1].Scale);
//             }
//
//             //          L C
//             //   V 1.0 -|
//             //     0.0 -|x  -  -  -  -  - 
//             //
//             //   S 3.0 -|x  -  -  -  -  -
//             //     0.0 -|                  
//             //
//             // Out 3.0 -| 
//             //     0.0 -|x_______________   
//             [Test]
//             public void IfLastVisibleAndCurrentTimeIsZero_ButNextInvisible_NoSmallerSampleIsEmitted() {
//                 const bool lastVisible = true;
//                 const bool currentVisible = false;
//
//                 const double timeZero = 0.0;
//
//                 var scale = new Vector3(3, 3, 3);
//
//                 var result = MergeVisibilityAndScaleTrackMerger.handleBothSampledAtSameTime(
//                         timeZero,
//                         currentVisible,
//                         scale,
//                         lastVisible
//                     )
//                     .ToArray();
//
//                 Assert.AreEqual(1, result.Length);
//
//                 Assert.AreEqual(timeZero, result[0].Time);
//                 Assert.AreEqual(Vector3.zero, result[0].Scale);
//             }
//
//             //                   L        C
//             //   V 1.0 -|                 x  -   -   -   -
//             //     0.0 -| -   -  x--------- 
//             //
//             //   S 2.0 -| -   -   -   -   x--------
//             //     0.0 -|
//             //
//             // Out 2.0 -|                 x-----------
//             //     0.0 -|        x_______x
//             [Test]
//             public void IfLastInvisible_AndNextVisible_TransitionFromInvisibleToScaleIsCreated() {
//                 const bool lastVisible = false;
//                 const bool currentVisible = true;
//
//                 const double visTime = 1.0;
//
//                 var scale = new Vector3(2, 2, 2);
//
//                 var result = MergeVisibilityAndScaleTrackMerger.handleBothSampledAtSameTime(
//                         visTime,
//                         currentVisible,
//                         scale,
//                         lastVisible
//                     )
//                     .ToArray();
//
//                 Assert.AreEqual(2, result.Length);
//
//                 Assert.AreEqual(visTime.nextSmaller(), result[0].Time);
//                 Assert.AreEqual(Vector3.zero, result[0].Scale);
//
//                 Assert.AreEqual(visTime, result[1].Time);
//                 Assert.AreEqual(new Vector3(2, 2, 2), result[1].Scale);
//             }
//
//             //          L C
//             //   V 1.0 -|x  -   -
//             //     0.0 -|x 
//             //
//             //   S 2.0 -|x-------
//             //     0.0 -|
//             //
//             // Out 2.0 -|x-------
//             //     0.0 -|
//             [Test]
//             public void IfLastInvisibleAndTimeZero_AndNextVisible_InitialSampleIsScale() {
//                 const bool lastVisible = false;
//                 const bool currentVisible = true;
//
//                 const double visTime = 0.0;
//
//                 var scale = new Vector3(2, 2, 2);
//
//                 var result = MergeVisibilityAndScaleTrackMerger.handleBothSampledAtSameTime(
//                         visTime,
//                         currentVisible,
//                         scale,
//                         lastVisible
//                     )
//                     .ToArray();
//
//                 Assert.AreEqual(1, result.Length);
//
//                 Assert.AreEqual(visTime, result[0].Time);
//                 Assert.AreEqual(new Vector3(2, 2, 2), result[0].Scale);
//             }
//         }
//
//         public class MergeSamplesForNextVisibilityChangeTests
//         {
//             //                  L            C
//             //   V 1.0 -|-   -  x------------x  -   -   -
//             //     0.0 -|
//             [Test]
//             public void IfLastVisibleAndCurrentVisible_NoSamplesAreReturned() {
//                 const bool lastVisible = true;
//                 const bool currentVisible = true;
//
//                 const double visTime = 1.0;
//                 const double scaleTime = 2.0;
//                 const double lastScaleTime = 0.5;
//
//                 var scale = Vector3.one;
//                 var lastScale = Vector3.one;
//
//                 var result = MergeVisibilityAndScaleTrackMerger.mergedSamplesForNextVisibilityChange(
//                     visTime,
//                     currentVisible,
//                     scaleTime,
//                     scale,
//                     lastVisible,
//                     lastScaleTime,
//                     lastScale
//                 );
//
//                 Assert.IsEmpty(result);
//             }
//
//             //                   L            C
//             //   V 1.0 -|
//             //     0.0 -| -   -  x------------x  -   -   - 
//             [Test]
//             public void IfLastInvisibleAndCurrentInvisible_NoSamplesAreReturned() {
//                 const bool lastVisible = false;
//                 const bool currentVisible = false;
//
//                 const double visTime = 1.0;
//                 const double scaleTime = 2.0;
//                 const double lastScaleTime = 0.5;
//
//                 var scale = Vector3.one;
//                 var lastScale = Vector3.one;
//
//                 var result = MergeVisibilityAndScaleTrackMerger.mergedSamplesForNextVisibilityChange(
//                     visTime,
//                     currentVisible,
//                     scaleTime,
//                     scale,
//                     lastVisible,
//                     lastScaleTime,
//                     lastScale
//                 );
//
//                 Assert.IsEmpty(result);
//             }
//
//             //                   L        C
//             //   V 1.0 -| -   -  x---------
//             //     0.0 -|                 x  -   -   -   - 
//             //
//             //   S 4.0 -| -  x--------
//             //     2.0 -|             --------
//             //     0.0 -|                     --------x
//             //
//             // Out 4.0 -| -  x--------
//             //     2.0 -|             ----x
//             //     0.0 -|                  x__________x   
//             [Test]
//             public void IfLastVisible_ButNextInvisible_TransitionFromLastScaleToInvisibleIsCreated() {
//                 const bool lastVisible = true;
//                 const bool currentVisible = false;
//
//                 const double visTime = 1.0;
//                 const double scaleTime = 2.0;
//                 const double lastScaleTime = 0.0;
//
//                 var scale = Vector3.zero;
//                 var lastScale = new Vector3(4, 4, 4);
//
//                 var result = MergeVisibilityAndScaleTrackMerger.mergedSamplesForNextVisibilityChange(
//                         visTime,
//                         currentVisible,
//                         scaleTime,
//                         scale,
//                         lastVisible,
//                         lastScaleTime,
//                         lastScale
//                     )
//                     .ToArray();
//
//                 Assert.AreEqual(2, result.Length);
//
//                 Assert.AreEqual(visTime.nextSmaller(), result[0].Time);
//                 Assert.AreEqual(new Vector3(2, 2, 2), result[0].Scale);
//
//                 Assert.AreEqual(visTime, result[1].Time);
//                 Assert.AreEqual(Vector3.zero, result[1].Scale);
//             }
//
//             //                   L        C
//             //   V 1.0 -| -   -  x---------
//             //     0.0 -|                 x  -   -   -   - 
//             //
//             //   S 4.0 -| -  x--------
//             //     2.0 -|             --------
//             //     0.0 -|                     --------x
//             //
//             // Out 4.0 -| -  x--------
//             //     2.0 -|             ----x
//             //     0.0 -|                  x__________x   
//             [Test]
//             public void IfLastVisibleAndCurrentTimeIsZero_ButNextInvisible_NoSmallerSampleIsEmitted() {
//                 const bool lastVisible = true;
//                 const bool currentVisible = false;
//
//                 const double visTime = 1.0;
//                 const double scaleTime = 2.0;
//                 const double lastScaleTime = 0.0;
//
//                 var scale = Vector3.zero;
//                 var lastScale = new Vector3(4, 4, 4);
//
//                 var result = MergeVisibilityAndScaleTrackMerger.mergedSamplesForNextVisibilityChange(
//                         visTime,
//                         currentVisible,
//                         scaleTime,
//                         scale,
//                         lastVisible,
//                         lastScaleTime,
//                         lastScale
//                     )
//                     .ToArray();
//
//                 Assert.AreEqual(2, result.Length);
//
//                 Assert.AreEqual(visTime.nextSmaller(), result[0].Time);
//                 Assert.AreEqual(new Vector3(2, 2, 2), result[0].Scale);
//
//                 Assert.AreEqual(visTime, result[1].Time);
//                 Assert.AreEqual(Vector3.zero, result[1].Scale);
//             }
//
//             //                   L        C
//             //   V 1.0 -|                 x  -   -   -   -
//             //     0.0 -| -   -  x--------- 
//             //
//             //   S 4.0 -| -  x--------
//             //     2.0 -|             --------
//             //     0.0 -|                     --------x
//             //
//             // Out 4.0 -|
//             //     2.0 -|                 x---
//             //     0.0 -| -  x___________x    --------x
//             [Test]
//             public void IfLastInvisible_AndNextVisible_TransitionFromLastScaleToVisibleIsCreated() {
//                 const bool lastVisible = false;
//                 const bool currentVisible = true;
//
//                 // These value have no effect in this test, but we have to supply something
//                 const double visTime = 1.0;
//                 const double scaleTime = 2.0;
//                 const double lastScaleTime = 0.0;
//
//                 // The value with which we transition from invisible to visible is interpolated between last scale and next scale
//                 var scale = Vector3.zero;
//                 var lastScale = new Vector3(4, 4, 4);
//
//                 var result = MergeVisibilityAndScaleTrackMerger.mergedSamplesForNextVisibilityChange(
//                         visTime,
//                         currentVisible,
//                         scaleTime,
//                         scale,
//                         lastVisible,
//                         lastScaleTime,
//                         lastScale
//                     )
//                     .ToArray();
//
//                 Assert.AreEqual(2, result.Length);
//
//                 Assert.AreEqual(visTime.nextSmaller(), result[0].Time);
//                 Assert.AreEqual(Vector3.zero, result[0].Scale);
//
//                 Assert.AreEqual(visTime, result[1].Time);
//                 Assert.AreEqual(new Vector3(2, 2, 2), result[1].Scale);
//             }
//         }
//     }
// }