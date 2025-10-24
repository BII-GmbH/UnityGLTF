using System;
using System.Collections.Generic;
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
            private static readonly TimeSpan sampleTimeStep = TimeSpan.FromMilliseconds(100);
            
            private static void assertSequenceEqual((ulong Time, Vector3 Scale)[] expected, (ulong Time, Vector3 Scale)[] gotten) {
                
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
                var scaleTimes = new ulong[] {
                    0, 1
                };
                
                var scaleValues = new[] {
                    Vector3.one,
                    scale
                };
                var visTimes = new ulong[] {
                    0, 1, 5, 8, 10
                };
                var visValues = new[] { false, true, false, true, true };
                
                var expectedResult = new (ulong Time, Vector3 Scale)[] {
                    (0ul, Vector3.zero),          
                    (1ul, Vector3.zero),         
                    (2ul, scale),   
                    (5ul, scale),          
                    (6ul, Vector3.zero),          
                    (8ul, Vector3.zero),   
                    (9ul, scale),   
                    (10ul, scale)
                };
                
                var uut = new MergeVisibilityAndScaleTrackMerger(visTimes, visValues, scaleTimes, scaleValues);

                var gotten = uut.Merge().ToArray();
                assertSequenceEqual(expectedResult, gotten);
            }

            [Test] public void IfScaleHasMoreEntriesThanVisibility_AndLastInvisible_TheseSamplesAreStillEmitted() {
                
                var scale = new Vector3(2, 2, 2);
                var scaleTimes = new ulong[] {
                    0, 1, 5, 8, 10
                };
                
                var scaleValues = new[] {
                    Vector3.one,
                    scale,
                    new (3,3,3),
                    new (4,4,4),
                    new (5,5,5),
                };
                var visTimes = new ulong[] {
                    0, 1
                };
                var visValues = new[] { true, false };
                
                var expectedResult = new (ulong Time, Vector3 Scale)[] {
                    (0, Vector3.one),          
                    (1, scale),         
                    (2, Vector3.zero),
                    (5, Vector3.zero),
                    (8, Vector3.zero),
                    (10, Vector3.zero)
                };
                
                var uut = new MergeVisibilityAndScaleTrackMerger(visTimes, visValues, scaleTimes, scaleValues);

                assertSequenceEqual(expectedResult, uut.Merge().ToArray());
            }
            
            [Test] public void IfScaleHasMoreEntriesThanVisibility_AndLastVisible_ThenSamplesAreAppendedToEnd() {
                
                var scale = new Vector3(2, 2, 2);
                var scaleTimes = new ulong[] {
                    0, 1, 5, 8, 10
                };
                
                var scaleValues = new[] {
                    Vector3.one,
                    scale,
                    new (3,3,3),
                    new (4,4,4),
                    new (5,5,5),
                };
                var visTimes = new ulong[] {
                    0, 1
                };
                var visValues = new[] { false, true };
                
                var expectedResult = new (ulong Time, Vector3 Scale)[] {
                    (0, Vector3.zero),          
                    (1, Vector3.zero),         
                    (2, scale),
                    (5, new Vector3(3,3,3)),
                    (8, new Vector3(4,4,4)),
                    (10, new Vector3(5,5,5)),
                };
                
                var uut = new MergeVisibilityAndScaleTrackMerger(visTimes, visValues, scaleTimes, scaleValues);
                
                var gotten = uut.Merge().ToArray();
                assertSequenceEqual(expectedResult, gotten);
            }
            
            [Test] public void IfScaleStartsAfterVisibility_FirstScaleIsUsed() {
                
                var scale = new Vector3(2, 2, 2);
                var scaleTimes = new ulong[] {
                    5, 8, 10
                };
                
                var scaleValues = new[] {
                    scale,
                    new (4,4,4),
                    new (5,5,5),
                };
                var visTimes = new ulong[] {
                    0, 1, 3, 5, 8
                };
                var visValues = new[] { false, true, false, true, true };
                
                var expectedResult = new (ulong Time, Vector3 Scale)[] {
                    (0, Vector3.zero),
                    (1, Vector3.zero),          
                    (2, scale),
                    (3, scale),         
                    (4, Vector3.zero),
                    (5, Vector3.zero),          
                    (6, scale),
                    (8, new Vector3(4,4,4)),
                    (10, new Vector3(5,5,5)),
                };
                
                var uut = new MergeVisibilityAndScaleTrackMerger(visTimes, visValues, scaleTimes, scaleValues);

                assertSequenceEqual(expectedResult, uut.Merge().ToArray());
            }
            
            [Test] public void IfVisibilityStartsAfterScale_FirstVisibilityIsUsed() {
             
                //               0  1  2  3  4  5  6  7  8  9  10
                //               |  |  |  |  |  |  |  |  |  |  |
                //  V1 1.0 -|    -     -     -  x--------------x
                //     0.0 -|    -     -  x-----   -     -     -
            
                //  V2 1.0 -|    -     -  x-----   -     -     -
                //     0.0 -|    -     -        x  -     -     -
            
                //   S 8.0 -|    -     -     -  x__-     -     -
                //     6.0 -|             x--/     \-----x------ 
                //     4.0 -|          x
                //     2.0 -|    x---/
                //     0.0 -|         
            
                // Res 8.0 -|    -     -     -     x____\-     -
                //  1  6.0 -|                     /      x-----x 
                //     4.0 -|                    /
                //     2.0 -|                    |
                //     0.0 -|    x-----x--x-----x
            
                // Res 8.0 -|    -     -     -  x__-     -     -
                //  2  6.0 -|             x--/     \-----x-----x 
                //     4.0 -|          x
                //     2.0 -|    x---/
                //     0.0 -|         
                
                
                var scaleTimes = new ulong[] {
                    0, 2, 3, 5, 8
                };
                
                var scaleValues = new Vector3[] {
                    new (2,2,2),
                    new (4,4,4),
                    new (6,6,6),
                    new (8,8,8),
                    new (6,6,6),
                };
                var visTimes = new ulong[] {
                    3, 5, 10
                };
                // since vis only has two potential value, it is much more likely that the implementation 
                // would still pass this test even if the value is hardcoded - avoid that by testing
                // both possibilities pass
                var visValues1 = new[] { false, true, true };
                var visValues2 = new[] { true, true, true };
                
                var expectedResult1 = new (ulong Time, Vector3 Scale)[] {
                    ( 0, Vector3.zero),
                    ( 2, Vector3.zero),          
                    ( 3, Vector3.zero),          
                    ( 5, Vector3.zero),          
                    ( 6, new Vector3(8,8,8)),
                    ( 8, new Vector3(6,6,6)),
                    (10, new Vector3(6,6,6)),
                };
                var expectedResult2 = new (ulong Time, Vector3 Scale)[] {
                    ( 0,  new Vector3(2,2,2)),
                    ( 2, new Vector3(4,4,4)),
                    ( 3, new Vector3(6,6,6)),
                    ( 5, new Vector3(8,8,8)),
                    ( 8, new Vector3(6,6,6)),
                    (10, new Vector3(6,6,6)),
                };
                
                
                var uut1 = new MergeVisibilityAndScaleTrackMerger(visTimes, visValues1, scaleTimes, scaleValues);
                var uut2 = new MergeVisibilityAndScaleTrackMerger(visTimes, visValues2, scaleTimes, scaleValues);

                assertSequenceEqual(expectedResult1, uut1.Merge().ToArray());
                assertSequenceEqual(expectedResult2, uut2.Merge().ToArray());
            }
            
            
            [Test] public void IfInsertedVisibilityToggleSampleConflictsWithScale_TheScaleIsEmittedCorrectly() {
             
                //               0  1  2  3  4  5  6  7  8  9  10
                //               |  |  |  |  |  |  |  |  |  |  |
                //  V  1.0 -|    -     -     -  x--------------x    
                //     0.0 -|    x-------------/   -     -     -
            
                //   S 8.0 -|    -     -     -  x  -     -     -
                //     6.0 -|             x--/   \-x------------ 
                //     4.0 -|          x
                //     2.0 -|    x---/
                //     0.0 -|         
            
                // Res 8.0 -|    -     -     -     -     -     -
                //     6.0 -|    -     -     -     x-----------x 
                //     4.0 -|    -     -     -   / -     -     -
                //     2.0 -|    -     -     -  |  -     -     -
                //     0.0 -|    x--------------x  -     -     -
            
                var scaleTimes = new ulong[] {
                    0, 2, 3, 5, 6
                };
                
                var scaleValues = new Vector3[] {
                    new (2,2,2),
                    new (4,4,4),
                    new (6,6,6),
                    new (8,8,8),
                    new (6,6,6),
                };
                var visTimes = new ulong[] {
                    0, 5, 10
                };
                
                var visValues1 = new[] { false, true, true };
                
                var expectedResult1 = new (ulong Time, Vector3 Scale)[] {
                    (0, Vector3.zero),     
                    (2, Vector3.zero),
                    (3, Vector3.zero),     
                    (5, Vector3.zero),          
                    (6, new Vector3(6,6,6)),
                    (10, new Vector3(6,6,6)),
                };
                
                var uut1 = new MergeVisibilityAndScaleTrackMerger(visTimes, visValues1, scaleTimes, scaleValues);

                assertSequenceEqual(expectedResult1, uut1.Merge().ToArray());
            }
            
            
            [Test] public void IfInsertedSampleToInvisibleConflictsWithScaleSample_ThenScaleIsNotEmitted() {
             
                //               0  1  2  3  4  5  6  7  8  9  10
                //               |  |  |  |  |  |  |  |  |  |  |
                //  V  1.0 -|    x--------------\  -     -     -      
                //     0.0 -|    -     -     -  x--------------x
            
                //   S 8.0 -|    -     -     -  x  -     -     -
                //     6.0 -|             x--/   \-x------------ 
                //     4.0 -|          x
                //     2.0 -|    x---/
                //     0.0 -|         
            
                // Res 8.0 -|    -     -     -  x  -     -     -
                //     6.0 -|    -     -  x--/   \ -     -     -  
                //     4.0 -|    -     x     -    \      -     -
                //     2.0 -|    x---/ -     -     \     -     -
                //     0.0 -|    -     -     -     x-----------x  
            
                var scaleTimes = new ulong[] {
                    0, 2, 3, 5, 6
                };
                
                var scaleValues = new Vector3[] {
                    new (2,2,2),
                    new (4,4,4),
                    new (6,6,6),
                    new (8,8,8),
                    new (6,6,6),
                };
                var visTimes = new ulong[] {
                    0, 5, 10
                };
                // since vis only has two potential value, it is much more likely that the implementation 
                // would still pass this test even if the value is hardcoded - avoid that by testing
                // both possibilities pass
                var visValues1 = new[] { true, false, false };
                
                var expectedResult1 = new (ulong Time, Vector3 Scale)[] {
                    (0,  new Vector3(2,2,2)),
                    (2, new Vector3(4,4,4)),
                    (3, new Vector3(6,6,6)),
                    (5, new Vector3(8,8,8)),
                    (6, new Vector3(0,0,0)),
                    (10, new Vector3(0,0,0)),
                };
                
                var uut1 = new MergeVisibilityAndScaleTrackMerger(visTimes, visValues1, scaleTimes, scaleValues);
                assertSequenceEqual(expectedResult1, uut1.Merge().ToArray());
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

                const ulong time = 1;

                var scale = new Vector3(2, 2, 2);

                var result = new List<(ulong Time, Vector3 Scale)>();
                MergeVisibilityAndScaleTrackMerger.handleBothSampledAtSameTime(
                        result,
                        time,
                        currentVisible,
                        scale,
                        lastVisible,
                        lastTime: 0,
                        out var emittedExtraSample
                );

                Assert.AreEqual(1, result.Count);
                Assert.That(emittedExtraSample, Is.False);

                Assert.AreEqual(time, result[0].Time);
                Assert.AreEqual(new Vector3(2, 2, 2), result[0].Scale);
            }

            //                   L            C
            //   V 1.0 -|
            //     0.0 -| -   -  x------------x  -   -   - 
            [Test]
            public void IfLastInvisibleAndCurrentInvisible_ZeroScaleSampleIsReturned() {
                const bool lastVisible = false;
                const bool currentVisible = false;

                const ulong time = 1;

                var scale = Vector3.one;

                var result = new List<(ulong Time, Vector3 Scale)>();
                MergeVisibilityAndScaleTrackMerger.handleBothSampledAtSameTime(
                    result,
                    time,
                    currentVisible,
                    scale,
                    lastVisible,
                    lastTime: 0,
                    out var emittedExtraSample
                );
                Assert.That(result, Has.Count.EqualTo(1));

                Assert.AreEqual(time, result[0].Time);
                Assert.AreEqual(Vector3.zero, result[0].Scale);
                Assert.That(emittedExtraSample, Is.False);
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

                const ulong visTime = 1;

                var scale = new Vector3(3, 3, 3);

                var result = new List<(ulong Time, Vector3 Scale)>();
                MergeVisibilityAndScaleTrackMerger.handleBothSampledAtSameTime(
                        result,
                        visTime,
                        currentVisible,
                        scale,
                        lastVisible,
                        lastTime: 0,
                        out var emittedExtraSample
                );

                Assert.AreEqual(2, result.Count);
                Assert.That(emittedExtraSample, Is.True);

                Assert.AreEqual(visTime, result[0].Time);
                Assert.AreEqual(new Vector3(3, 3, 3), result[0].Scale);

                Assert.AreEqual(visTime + 1, result[1].Time);
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
            public void IfCurrentTimeIsZeroAndInitiallyVisible_ButNextInvisible_NoVisibleSampleIsEmitted() {
                const bool lastVisible = true;
                const bool currentVisible = false;

                const ulong timeZero = 0;

                var scale = new Vector3(3, 3, 3);

                var result = new List<(ulong Time, Vector3 Scale)>();
                MergeVisibilityAndScaleTrackMerger.handleBothSampledAtSameTime(
                        result,
                        timeZero,
                        currentVisible,
                        scale,
                        lastVisible,
                        lastTime: 0,
                        out var emittedExtraSample
                );

                Assert.AreEqual(1, result.Count);
                Assert.That(emittedExtraSample, Is.False);

                Assert.AreEqual(timeZero, result[0].Time);
                Assert.AreEqual(Vector3.zero, result[0].Scale);
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
            public void IfCurrentTimeIsZeroAndInitiallyInvisible_ButNextVisible_VisibleSampleIsEmitted() {
                const bool lastVisible = false;
                const bool currentVisible = true;

                const ulong visTime = 0;

                var scale = new Vector3(2, 2, 2);

                var result = new List<(ulong Time, Vector3 Scale)>();
                MergeVisibilityAndScaleTrackMerger.handleBothSampledAtSameTime(
                        result,
                        visTime,
                        currentVisible,
                        scale,
                        lastVisible,
                        lastTime: 0,
                        out var emittedExtraSample
                );

                Assert.AreEqual(1, result.Count);              
                Assert.That(emittedExtraSample, Is.False);


                Assert.AreEqual(visTime, result[0].Time);
                Assert.AreEqual(new Vector3(2, 2, 2), result[0].Scale);
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

                const ulong visTime = 1;

                var scale = new Vector3(2, 2, 2);

                var result = new List<(ulong Time, Vector3 Scale)>();
                MergeVisibilityAndScaleTrackMerger.handleBothSampledAtSameTime(
                        result,
                        visTime,
                        currentVisible,
                        scale,
                        lastVisible,
                        lastTime: 0,
                        out var emittedExtraSample
                );

                Assert.AreEqual(2, result.Count);              
                Assert.That(emittedExtraSample, Is.True);


                Assert.AreEqual(visTime, result[0].Time);
                Assert.AreEqual(Vector3.zero, result[0].Scale);

                Assert.AreEqual(visTime + 1, result[1].Time);
                Assert.AreEqual(new Vector3(2, 2, 2), result[1].Scale);
            }
        }
        
        public class MergeSamplesForNextVisibilityChangeTests
        {
            //                  L            C
            //   V 1.0 -|-   -  x------------x  -   -   -
            //     0.0 -|
            [Test]
            public void IfLastVisibleAndCurrentVisible_SampleIsStillReturned() {
                const bool lastVisible = true;
                const bool currentVisible = true;

                const ulong visTime = 2;
                const ulong scaleTime = 3;
                const ulong lastScaleTime = 1;

                var scale = Vector3.one;
                var lastScale = Vector3.one;

                var result = new List<(ulong Time, Vector3 Scale)>();
                MergeVisibilityAndScaleTrackMerger.mergedSamplesForNextVisibilityChange(
                    result,
                    visTime,
                    currentVisible,
                    scaleTime,
                    scale,
                    lastVisibleTime: 0,
                    lastVisible,
                    lastScaleTime,
                    lastScale,
                    lastRecordedTime: lastScaleTime,
                    out var emittedExtraSample
                );
                
                Assert.That(result, Has.Count.EqualTo(1));
                
                Assert.AreEqual(visTime, result[0].Time);
                Assert.AreEqual(scale, result[0].Scale);
                
                Assert.That(emittedExtraSample, Is.False);

            }

            //                   L            C
            //   V 1.0 -|
            //     0.0 -| -   -  x------------x  -   -   - 
            [Test]
            public void IfLastInvisibleAndCurrentInvisible_InvisibleSampleIsReturnedAnyway() {
                const bool lastVisible = false;
                const bool currentVisible = false;

                const ulong visTime = 2;
                const ulong scaleTime = 3;
                const ulong lastScaleTime = 1;

                var scale = Vector3.one;
                var lastScale = Vector3.one;

                var result = new List<(ulong Time, Vector3 Scale)>();
                MergeVisibilityAndScaleTrackMerger.mergedSamplesForNextVisibilityChange(
                    result,
                    visTime,
                    currentVisible,
                    scaleTime,
                    scale,
                    lastVisibleTime: 0,
                    lastVisible,
                    lastScaleTime,
                    lastScale,
                    lastRecordedTime: visTime,
                    out var emittedExtraSample
                );
                
                Assert.That(result, Has.Count.EqualTo(1));
                
                Assert.AreEqual(visTime, result[0].Time);
                Assert.AreEqual(Vector3.zero, result[0].Scale);
                
                Assert.That(emittedExtraSample, Is.False);
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

                const ulong visTime = 1;
                const ulong scaleTime = 2;
                const ulong lastScaleTime = 0;

                var scale = Vector3.zero;
                var lastScale = new Vector3(4, 4, 4);

                var result = new List<(ulong Time, Vector3 Scale)>();
                MergeVisibilityAndScaleTrackMerger.mergedSamplesForNextVisibilityChange(
                        result,
                        visTime,
                        currentVisible,
                        scaleTime,
                        scale,
                        lastVisibleTime: 0,
                        lastVisible,
                        lastScaleTime,
                        lastScale,
                        lastRecordedTime: visTime,
                        out var emittedExtraSample
                );

                Assert.AreEqual(2, result.Count);
                Assert.That(emittedExtraSample, Is.True);

                Assert.AreEqual(visTime, result[0].Time);
                Assert.AreEqual(new Vector3(2, 2, 2), result[0].Scale);

                Assert.AreEqual(visTime + 1, result[1].Time);
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

                const ulong visTime = 1;
                const ulong scaleTime = 2;
                const ulong lastScaleTime = 0;

                var scale = Vector3.zero;
                var lastScale = new Vector3(4, 4, 4);

                var result = new List<(ulong Time, Vector3 Scale)>();
                MergeVisibilityAndScaleTrackMerger.mergedSamplesForNextVisibilityChange(
                        result,
                        visTime,
                        currentVisible,
                        scaleTime,
                        scale,
                        lastVisibleTime:0,
                        lastVisible,
                        lastScaleTime,
                        lastScale,
                        lastRecordedTime: visTime,
                        out var emittedExtraSample
                );

                Assert.AreEqual(2, result.Count);
                Assert.That(emittedExtraSample, Is.True);

                Assert.AreEqual(visTime, result[0].Time);
                Assert.AreEqual(new Vector3(2, 2, 2), result[0].Scale);

                Assert.AreEqual(visTime + 1, result[1].Time);
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
                
                const ulong visTime = 2;
                const ulong scaleTime = 3;
                const ulong lastScaleTime = 1;

                // The value with which we transition from invisible to visible is interpolated between last scale and next scale
                var scale = Vector3.zero;
                var lastScale = new Vector3(4, 4, 4);

                var result = new List<(ulong Time, Vector3 Scale)>();
                MergeVisibilityAndScaleTrackMerger.mergedSamplesForNextVisibilityChange(
                        result,
                        visTime,
                        currentVisible,
                        scaleTime,
                        scale,
                        lastVisibleTime: 0,
                        lastVisible,
                        lastScaleTime,
                        lastScale,
                        lastRecordedTime: visTime,
                        out var emittedExtraSample
                );

                Assert.AreEqual(2, result.Count);
                Assert.That(emittedExtraSample, Is.True);

                Assert.AreEqual(visTime, result[0].Time);
                Assert.AreEqual(Vector3.zero, result[0].Scale);

                Assert.AreEqual(visTime + 1, result[1].Time);
                Assert.AreEqual(new Vector3(2, 2, 2), result[1].Scale);
            }
        }
    }
}