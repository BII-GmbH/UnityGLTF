using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityGLTF.Timeline;

namespace Tests.Editor
{
    public class VisibilityTrackMergeHelperTests
    {

        public class MergeTests
        {
            private static void assertSequenceEqual((float Time, Vector3 Scale)[] expected, (float Time, Vector3 Scale)[] gotten) {
                
                Assert.AreEqual(
                    expected.Length,
                    gotten.Length,
                    "Expected and gotten arrays have different lengths"
                );

                for (var i = 0; i < expected.Length; i++) {
                    Assert.AreEqual(
                        expected[i].Time,
                        gotten[i].Time,
                        $"Time mismatch at index {i}:"
                    );
                    Assert.AreEqual(
                        expected[i].Scale,
                        gotten[i].Scale,
                        $"Scale mismatch at index {i}:"
                    );
                }
            }
            
            [Test] public void IfVisibilityHasMoreEntriesThanScale_TheyAreCorrectlyAppendedToEnd() {
                
                var scale = new Vector3(2, 2, 2);
                var scaleTimes = new[] {
                    0f, 0.1f
                };
                
                var scaleValues = new[] {
                    Vector3.one,
                    scale
                };
                var visTimes = new[] {
                    0f, 0.1f, 0.5f, 0.8f, 1f
                };
                var visValues = new[] { false, true, false, true, true };
                
                var expectedResult = new (float Time, Vector3 Scale)[] {
                    (0, Vector3.zero),          
                    (0.1f.nextSmaller(), Vector3.zero),         
                    (0.1f, scale),   
                    (0.5f.nextSmaller(), scale),          
                    (0.5f, Vector3.zero),          
                    (0.8f.nextSmaller(), Vector3.zero),   
                    (0.8f, scale),   
                    (1, scale)
                };
                
                var uut = new MergeVisibilityAndScaleTrackMerger(visTimes, visValues, scaleTimes, scaleValues);

                var gotten = uut.Merge().ToArray();
                assertSequenceEqual(expectedResult, gotten);
            }

            [Test] public void IfScaleHasMoreEntriesThanVisibility_ButLastInvisible_TheseSamplesAreIgnored() {
                
                var scale = new Vector3(2, 2, 2);
                var scaleTimes = new[] {
                    0, 0.1f, 0.5f, 0.8f, 1
                };
                
                var scaleValues = new[] {
                    Vector3.one,
                    scale,
                    new (3,3,3),
                    new (4,4,4),
                    new (5,5,5),
                };
                var visTimes = new[] {
                    0, 0.1f
                };
                var visValues = new[] { true, false };
                
                var expectedResult = new (float Time, Vector3 Scale)[] {
                    (0, Vector3.one),          
                    (0.1f.nextSmaller(), scale),         
                    (0.1f, Vector3.zero)
                };
                
                var uut = new MergeVisibilityAndScaleTrackMerger(visTimes, visValues, scaleTimes, scaleValues);

                assertSequenceEqual(expectedResult, uut.Merge().ToArray());
            }
            
            [Test] public void IfScaleHasMoreEntriesThanVisibility_AndLastVisible_ThenSamplesAreAppendedToEnd() {
                
                var scale = new Vector3(2, 2, 2);
                var scaleTimes = new[] {
                    0, 0.1f, 0.5f, 0.8f, 1
                };
                
                var scaleValues = new[] {
                    Vector3.one,
                    scale,
                    new (3,3,3),
                    new (4,4,4),
                    new (5,5,5),
                };
                var visTimes = new[] {
                    0, 0.1f
                };
                var visValues = new[] { false, true };
                
                var expectedResult = new (float Time, Vector3 Scale)[] {
                    (0, Vector3.zero),          
                    (0.1f.nextSmaller(), Vector3.zero),         
                    (0.1f, scale),
                    (0.5f, new Vector3(3,3,3)),
                    (0.8f, new Vector3(4,4,4)),
                    (1.0f, new Vector3(5,5,5)),
                };
                
                var uut = new MergeVisibilityAndScaleTrackMerger(visTimes, visValues, scaleTimes, scaleValues);
                
                var gotten = uut.Merge().ToArray();
                assertSequenceEqual(expectedResult, gotten);
            }
            
            [Test] public void IfScaleStartsAfterVisibility_FirstScaleIsUsed() {
                
                var scale = new Vector3(2, 2, 2);
                var scaleTimes = new[] {
                    0.5f, 0.8f, 1.0f
                };
                
                var scaleValues = new[] {
                    scale,
                    new (4,4,4),
                    new (5,5,5),
                };
                var visTimes = new[] {
                    0, 0.1f, 0.3f, 0.5f, 0.8f
                };
                var visValues = new[] { false, true, false, true, true };
                
                var expectedResult = new (float Time, Vector3 Scale)[] {
                    (0, Vector3.zero),
                    (0.1f.nextSmaller(), Vector3.zero),          
                    (0.1f, scale),
                    (0.3f.nextSmaller(), scale),         
                    (0.3f, Vector3.zero),
                    (0.5f.nextSmaller(), Vector3.zero),          
                    (0.5f, scale),
                    (0.8f, new Vector3(4,4,4)),
                    (1.0f, new Vector3(5,5,5)),
                };
                
                var uut = new MergeVisibilityAndScaleTrackMerger(visTimes, visValues, scaleTimes, scaleValues);

                assertSequenceEqual(expectedResult, uut.Merge().ToArray());
            }
            
            [Test] public void IfVisibilityStartsAfterScale_FirstVisibilityIsUsed() {
                
                var scaleTimes = new[] {
                    0.0f, 0.2f, 0.3f, 0.5f, 0.8f
                };
                
                var scaleValues = new Vector3[] {
                    new (2,2,2),
                    new (4,4,4),
                    new (5,5,5),
                    new (6,6,6),
                    new (5,5,5),
                };
                var visTimes = new[] {
                    0.3f, 0.5f
                };
                // since vis only has two potential value, it is much more likely that the implementation 
                // would still pass this test even if the value is hardcoded - avoid that by testing
                // both possibilities pass
                var visValues1 = new[] { false, true };
                var visValues2 = new[] { true, true };
                
                var expectedResult1 = new (float Time, Vector3 Scale)[] {
                    (0.5f.nextSmaller(), Vector3.zero),          
                    (0.5f, new Vector3(6,6,6)),
                    (0.8f, new Vector3(5,5,5)),
                };
                var expectedResult2 = new (float Time, Vector3 Scale)[] {
                    (0,  new Vector3(2,2,2)),
                    (0.2f, new Vector3(4,4,4)),
                    (0.3f, new Vector3(5,5,5)),
                    (0.5f, new Vector3(6,6,6)),
                    (0.8f, new Vector3(5,5,5)),
                };
                
                
                var uut1 = new MergeVisibilityAndScaleTrackMerger(visTimes, visValues1, scaleTimes, scaleValues);
                var uut2 = new MergeVisibilityAndScaleTrackMerger(visTimes, visValues2, scaleTimes, scaleValues);

                assertSequenceEqual(expectedResult1, uut1.Merge().ToArray());
                assertSequenceEqual(expectedResult2, uut2.Merge().ToArray());
            }
            
        }

        public class MergeSamplesForBothTests
        {
            //               L            C
            //   V 1.0 -| -  x------------x  -   -   -
            //     0.0 -|
            //   S 2.0 -| -  -  -  -  -  -x  -   -   -
            //     0.0 -|             
            [Test]
            public void IfLastVisibleAndCurrentVisible_ScaleSampleIsReturned() {
                const bool lastVisible = true;
                const bool currentVisible = true;

                const float time = 1.0f;

                var scale = new Vector3(2, 2, 2);

                var result = MergeVisibilityAndScaleTrackMerger.handleBothSampledAtSameTime(
                        time,
                        currentVisible,
                        scale,
                        lastVisible,
                        lastTime: 0.0f
                    )
                    .ToArray();

                Assert.AreEqual(1, result.Length);

                Assert.AreEqual(time, result[0].Time);
                Assert.AreEqual(new Vector3(2, 2, 2), result[0].Scale);
            }

            //                   L            C
            //   V 1.0 -|
            //     0.0 -| -   -  x------------x  -   -   - 
            [Test]
            public void IfLastInvisibleAndCurrentInvisible_NoSamplesAreReturned() {
                const bool lastVisible = false;
                const bool currentVisible = false;

                const float time = 1.0f;

                var scale = Vector3.one;

                var result = MergeVisibilityAndScaleTrackMerger.handleBothSampledAtSameTime(
                    time,
                    currentVisible,
                    scale,
                    lastVisible,
                    lastTime: 0.0f
                );
                Assert.IsEmpty(result);
            }

            //                   L        C
            //   V 1.0 -| -   -  x---------
            //     0.0 -|                 x  -  -  -  -  - 
            //
            //   S 3.0 -| -  x------------x  -  -  -  -  -
            //     0.0 -|                  
            //
            // Out 3.0 -| -  x------------x 
            //     0.0 -|                  x__________   
            [Test]
            public void IfLastVisible_ButNextInvisible_TransitionFromScaleToInvisibleIsCreated() {
                const bool lastVisible = true;
                const bool currentVisible = false;

                const float visTime = 1.0f;

                var scale = new Vector3(3, 3, 3);

                var result = MergeVisibilityAndScaleTrackMerger.handleBothSampledAtSameTime(
                        visTime,
                        currentVisible,
                        scale,
                        lastVisible,
                        lastTime: 0.0f
                    )
                    .ToArray();

                Assert.AreEqual(2, result.Length);

                Assert.AreEqual(visTime.nextSmaller(), result[0].Time);
                Assert.AreEqual(new Vector3(3, 3, 3), result[0].Scale);

                Assert.AreEqual(visTime, result[1].Time);
                Assert.AreEqual(Vector3.zero, result[1].Scale);
            }

            //          L C
            //   V 1.0 -|
            //     0.0 -|x  -  -  -  -  - 
            //
            //   S 3.0 -|x  -  -  -  -  -
            //     0.0 -|                  
            //
            // Out 3.0 -| 
            //     0.0 -|x_______________   
            [Test]
            public void IfLastVisibleAndCurrentTimeIsZero_ButNextInvisible_NoSmallerSampleIsEmitted() {
                const bool lastVisible = true;
                const bool currentVisible = false;

                const float timeZero = 0.0f;

                var scale = new Vector3(3, 3, 3);

                var result = MergeVisibilityAndScaleTrackMerger.handleBothSampledAtSameTime(
                        timeZero,
                        currentVisible,
                        scale,
                        lastVisible,
                        lastTime: 0.0f
                    )
                    .ToArray();

                Assert.AreEqual(1, result.Length);

                Assert.AreEqual(timeZero, result[0].Time);
                Assert.AreEqual(Vector3.zero, result[0].Scale);
            }

            //                   L        C
            //   V 1.0 -|                 x  -   -   -   -
            //     0.0 -| -   -  x--------- 
            //
            //   S 2.0 -| -   -   -   -   x--------
            //     0.0 -|
            //
            // Out 2.0 -|                 x-----------
            //     0.0 -|        x_______x
            [Test]
            public void IfLastInvisible_AndNextVisible_TransitionFromInvisibleToScaleIsCreated() {
                const bool lastVisible = false;
                const bool currentVisible = true;

                const float visTime = 1.0f;

                var scale = new Vector3(2, 2, 2);

                var result = MergeVisibilityAndScaleTrackMerger.handleBothSampledAtSameTime(
                        visTime,
                        currentVisible,
                        scale,
                        lastVisible,
                        lastTime: 0.0f
                    )
                    .ToArray();

                Assert.AreEqual(2, result.Length);

                Assert.AreEqual(visTime.nextSmaller(), result[0].Time);
                Assert.AreEqual(Vector3.zero, result[0].Scale);

                Assert.AreEqual(visTime, result[1].Time);
                Assert.AreEqual(new Vector3(2, 2, 2), result[1].Scale);
            }

            //          L C
            //   V 1.0 -|x  -   -
            //     0.0 -|x 
            //
            //   S 2.0 -|x-------
            //     0.0 -|
            //
            // Out 2.0 -|x-------
            //     0.0 -|
            [Test]
            public void IfLastInvisibleAndTimeZero_AndNextVisible_InitialSampleIsScale() {
                const bool lastVisible = false;
                const bool currentVisible = true;

                const float visTime = 0.0f;

                var scale = new Vector3(2, 2, 2);

                var result = MergeVisibilityAndScaleTrackMerger.handleBothSampledAtSameTime(
                        visTime,
                        currentVisible,
                        scale,
                        lastVisible,
                        lastTime: 0.0f
                    )
                    .ToArray();

                Assert.AreEqual(1, result.Length);

                Assert.AreEqual(visTime, result[0].Time);
                Assert.AreEqual(new Vector3(2, 2, 2), result[0].Scale);
            }
        }

        public class MergeSamplesForNextVisibilityChangeTests
        {
            //                  L            C
            //   V 1.0 -|-   -  x------------x  -   -   -
            //     0.0 -|
            [Test]
            public void IfLastVisibleAndCurrentVisible_NoSamplesAreReturned() {
                const bool lastVisible = true;
                const bool currentVisible = true;

                const float visTime = 1.0f;
                const float scaleTime = 2.0f;
                const float lastScaleTime = 0.5f;

                var scale = Vector3.one;
                var lastScale = Vector3.one;

                var result = MergeVisibilityAndScaleTrackMerger.mergedSamplesForNextVisibilityChange(
                    visTime,
                    currentVisible,
                    scaleTime,
                    scale,
                    lastVisibleTime: 0.0f,
                    lastVisible,
                    lastScaleTime,
                    lastScale
                );

                Assert.IsEmpty(result);
            }

            //                   L            C
            //   V 1.0 -|
            //     0.0 -| -   -  x------------x  -   -   - 
            [Test]
            public void IfLastInvisibleAndCurrentInvisible_NoSamplesAreReturned() {
                const bool lastVisible = false;
                const bool currentVisible = false;

                const float visTime = 1.0f;
                const float scaleTime = 2.0f;
                const float lastScaleTime = 0.5f;

                var scale = Vector3.one;
                var lastScale = Vector3.one;

                var result = MergeVisibilityAndScaleTrackMerger.mergedSamplesForNextVisibilityChange(
                    visTime,
                    currentVisible,
                    scaleTime,
                    scale,
                    lastVisibleTime: 0.0f,
                    lastVisible,
                    lastScaleTime,
                    lastScale
                );

                Assert.IsEmpty(result);
            }

            //                   L        C
            //   V 1.0 -| -   -  x---------
            //     0.0 -|                 x  -   -   -   - 
            //
            //   S 4.0 -| -  x--------
            //     2.0 -|             --------
            //     0.0 -|                     --------x
            //
            // Out 4.0 -| -  x--------
            //     2.0 -|             ----x
            //     0.0 -|                  x__________x   
            [Test]
            public void IfLastVisible_ButNextInvisible_TransitionFromLastScaleToInvisibleIsCreated() {
                const bool lastVisible = true;
                const bool currentVisible = false;

                const float visTime = 1.0f;
                const float scaleTime = 2.0f;
                const float lastScaleTime = 0.0f;

                var scale = Vector3.zero;
                var lastScale = new Vector3(4, 4, 4);

                var result = MergeVisibilityAndScaleTrackMerger.mergedSamplesForNextVisibilityChange(
                        visTime,
                        currentVisible,
                        scaleTime,
                        scale,
                        lastVisibleTime: 0.0f,
                        lastVisible,
                        lastScaleTime,
                        lastScale
                    )
                    .ToArray();

                Assert.AreEqual(2, result.Length);

                Assert.AreEqual(visTime.nextSmaller(), result[0].Time);
                Assert.AreEqual(new Vector3(2, 2, 2), result[0].Scale);

                Assert.AreEqual(visTime, result[1].Time);
                Assert.AreEqual(Vector3.zero, result[1].Scale);
            }

            //                   L        C
            //   V 1.0 -| -   -  x---------
            //     0.0 -|                 x  -   -   -   - 
            //
            //   S 4.0 -| -  x--------
            //     2.0 -|             --------
            //     0.0 -|                     --------x
            //
            // Out 4.0 -| -  x--------
            //     2.0 -|             ----x
            //     0.0 -|                  x__________x   
            [Test]
            public void IfLastVisibleAndCurrentTimeIsZero_ButNextInvisible_NoSmallerSampleIsEmitted() {
                const bool lastVisible = true;
                const bool currentVisible = false;

                const float visTime = 1.0f;
                const float scaleTime = 2.0f;
                const float lastScaleTime = 0.0f;

                var scale = Vector3.zero;
                var lastScale = new Vector3(4, 4, 4);

                var result = MergeVisibilityAndScaleTrackMerger.mergedSamplesForNextVisibilityChange(
                        visTime,
                        currentVisible,
                        scaleTime,
                        scale,
                        lastVisibleTime:0.0f,
                        lastVisible,
                        lastScaleTime,
                        lastScale
                    )
                    .ToArray();

                Assert.AreEqual(2, result.Length);

                Assert.AreEqual(visTime.nextSmaller(), result[0].Time);
                Assert.AreEqual(new Vector3(2, 2, 2), result[0].Scale);

                Assert.AreEqual(visTime, result[1].Time);
                Assert.AreEqual(Vector3.zero, result[1].Scale);
            }

            //                   L        C
            //   V 1.0 -|                 x  -   -   -   -
            //     0.0 -| -   -  x--------- 
            //
            //   S 4.0 -| -  x--------
            //     2.0 -|             --------
            //     0.0 -|                     --------x
            //
            // Out 4.0 -|
            //     2.0 -|                 x---
            //     0.0 -| -  x___________x    --------x
            [Test]
            public void IfLastInvisible_AndNextVisible_TransitionFromLastScaleToVisibleIsCreated() {
                const bool lastVisible = false;
                const bool currentVisible = true;

                // These value have no effect in this test, but we have to supply something
                const float visTime = 1.0f;
                const float scaleTime = 2.0f;
                const float lastScaleTime = 0.0f;

                // The value with which we transition from invisible to visible is interpolated between last scale and next scale
                var scale = Vector3.zero;
                var lastScale = new Vector3(4, 4, 4);

                var result = MergeVisibilityAndScaleTrackMerger.mergedSamplesForNextVisibilityChange(
                        visTime,
                        currentVisible,
                        scaleTime,
                        scale,
                        lastVisibleTime: 0.0f,
                        lastVisible,
                        lastScaleTime,
                        lastScale
                    )
                    .ToArray();

                Assert.AreEqual(2, result.Length);

                Assert.AreEqual(visTime.nextSmaller(), result[0].Time);
                Assert.AreEqual(Vector3.zero, result[0].Scale);

                Assert.AreEqual(visTime, result[1].Time);
                Assert.AreEqual(new Vector3(2, 2, 2), result[1].Scale);
            }
        }
    }
}