using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityGLTF.Timeline;

namespace Tests.Editor
{
    public class VisibilityTrackMergeHelperTests
    {
        public class MergeSamplesForNextVisibilityChangeTests
        {
            //                  L            C
            //   V 1.0 -|-   -  x------------x  -   -   -
            //     0.0 -|
            [Test]
            public void IfLastVisibleAndCurrentVisible_NoSamplesAreReturned() {

                bool? lastVisible = true;
                bool currentVisible = true;

                // These value have no effect in this test, but we have to supply something
                double visTime = 1.0;
                double scaleTime = 2.0;
                double lastScaleTime = 0.5;

                Vector3 scale = Vector3.one;
                Vector3? lastScale = null;

                var result = MergeVisibilityAndScaleTracksCurrentState.mergedSamplesForNextVisibilityChange(
                    visTime,
                    currentVisible,
                    scaleTime,
                    scale,
                    lastVisible,
                    lastScaleTime,
                    lastScale
                );
                
                Assert.IsEmpty(result);
                
                // var scaleTimes = new double[] { 0, 0.1, 0.3, 0.4, 0.5, 0.7, 0.8, 0.9, 1 };
                //
                // var scaleValues = new Vector3[] {
                //     Vector3.one, Vector3.one, new Vector3(2, 2, 2), Vector3.one, new Vector3(2, 2, 2), Vector3.one,
                //     Vector3.one, Vector3.zero, Vector3.zero
                // };
                // var visTimes = new double[] { 0, 0.2, 0.5, 0.8, 1 };
                // var visValues = new bool[] { false, true, false, true, true };
                //
                // var expectedTimes = new double[] {
                //     0,
                //     // 0.1 is not recorded since we are invisible
                //     0.2.nextSmaller(), 0.2, 0.3, 0.4, 0.5.nextSmaller(), 0.5,
                //     // neither has a sample at 0.6 
                //     // 0.7 is not recorded since we are invisible
                //     0.8.nextSmaller(), 0.8, 0.9, 1
                // };
                // var expectedResult = new Vector3[] {
                //     Vector3.zero, Vector3.zero, new(1.5f, 1.5f, 1.5f), new Vector3(2, 2, 2), Vector3.one,
                //     new Vector3(2, 2, 2), Vector3.zero, Vector3.zero, Vector3.one, Vector3.zero, Vector3.zero
                // };

                // scaleTrack = Substitute.For<AnimationTrack<Transform, Vector3>>();
                // scaleTrack.Times.Returns(scaleTimes);
                // scaleTrack.Values.Returns(scaleValues);
                //
                // visibilityTrack = Substitute.For<AnimationTrack<GameObject, bool>>();
                // visibilityTrack.Times.Returns(visTimes);
                // visibilityTrack.Values.Returns(visValues);

                // var result = GLTFRecorder.mergeVisibilityAndScaleTracks(visibilityTrack, scaleTrack);
                // Assert.IsNotNull(result);
                // Assert.AreEqual(InterpolationType.LINEAR, result!.Value.interpolation);
                // Assert.AreEqual(expectedTimes, result.Value.times);
                // Assert.AreEqual(expectedResult, result.Value.mergedScales);
            }

            //                   L            C
            //   V 1.0 -|
            //     0.0 -| -   -  x------------x  -   -   - 
            [Test]
            public void IfLastInvisibleAndCurrentInvisible_NoSamplesAreReturned() {

                bool? lastVisible = false;
                bool currentVisible = false;

                // These value have no effect in this test, but we have to supply something
                double visTime = 1.0;
                double scaleTime = 2.0;
                double lastScaleTime = 0.5;

                Vector3 scale = Vector3.one;
                Vector3? lastScale = null;

                var result = MergeVisibilityAndScaleTracksCurrentState.mergedSamplesForNextVisibilityChange(
                    visTime,
                    currentVisible,
                    scaleTime,
                    scale,
                    lastVisible,
                    lastScaleTime,
                    lastScale
                );

                Assert.IsEmpty(result);
            }
            
            //                   L            C
            //   V 1.0 -|
            //     0.0 -|                     x  -   -   - 
            [Test]
            public void IfNoLastVisibleAndCurrentInvisible_NoSamplesAreReturned() {

                bool? lastVisible = null;
                bool currentVisible = false;

                // These value have no effect in this test, but we have to supply something
                double visTime = 1.0;
                double scaleTime = 2.0;
                double lastScaleTime = 0.5;

                Vector3 scale = Vector3.one;
                Vector3? lastScale = null;

                var result = MergeVisibilityAndScaleTracksCurrentState.mergedSamplesForNextVisibilityChange(
                    visTime,
                    currentVisible,
                    scaleTime,
                    scale,
                    lastVisible,
                    lastScaleTime,
                    lastScale
                );

                Assert.IsEmpty(result);
            }
            
            //                   L            C
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

                bool? lastVisible = true;
                bool currentVisible = false;

                // These value have no effect in this test, but we have to supply something
                double visTime = 1.0;
                double scaleTime = 2.0;
                double lastScaleTime = 0.0;

                Vector3 scale = new Vector3(2,2,2);
                Vector3? lastScale = new Vector3(4,4,4);

                var result = MergeVisibilityAndScaleTracksCurrentState.mergedSamplesForNextVisibilityChange(
                    visTime,
                    currentVisible,
                    scaleTime,
                    scale,
                    lastVisible,
                    lastScaleTime,
                    lastScale
                ).ToArray();

                Assert.AreEqual(2, result.Length);
                
                Assert.AreEqual(visTime.nextSmaller(), result[0].Time);
                Assert.AreEqual(new Vector3(3,3,3), result[0].Scale);
                
                Assert.AreEqual(visTime, result[1].Time);
                Assert.AreEqual(Vector3.zero, result[1].Scale);
                
            }
            
            // if no last scale is there, next scale is used, everything else feels very weird!
            [Test]
            public void IfLastVisibleButNoLastScale_ButNextInvisible_TransitionFromScaleToInvisibleIsCreated() {

                bool? lastVisible = true;
                bool currentVisible = false;

                // These value have no effect in this test, but we have to supply something
                double visTime = 1.0;
                double scaleTime = 2.0;
                double? lastScaleTime = null;
                Vector3? lastScale = null;

                Vector3 scale = new Vector3(4,4,4);

                var result = MergeVisibilityAndScaleTracksCurrentState.mergedSamplesForNextVisibilityChange(
                    visTime,
                    currentVisible,
                    scaleTime,
                    scale,
                    lastVisible,
                    lastScaleTime,
                    lastScale
                ).ToArray();

                Assert.AreEqual(2, result.Length);
                
                Assert.AreEqual(visTime.nextSmaller(), result[0].Time);
                Assert.AreEqual(new Vector3(4,4,4), result[0].Scale);
                
                Assert.AreEqual(visTime, result[1].Time);
                Assert.AreEqual(Vector3.zero, result[1].Scale);
                
            }
            
            // if no last scale is there, next scale is used, everything else feels very weird!
            [Test]
            public void IfLastInvisible_AndNextVisible_TransitionFromLastScaleToVisibleIsCreated() {

                bool? lastVisible = false;
                bool currentVisible = true;

                // These value have no effect in this test, but we have to supply something
                double visTime = 1.0;
                double scaleTime = 2.0;
                double lastScaleTime = 0.0;

                // The value with which we transition from invisible to visible is interpolated between last scale and next scale
                Vector3 scale = new Vector3(2,2,2);
                Vector3? lastScale = new Vector3(4,4,4);

                var result = MergeVisibilityAndScaleTracksCurrentState.mergedSamplesForNextVisibilityChange(
                    visTime,
                    currentVisible,
                    scaleTime,
                    scale,
                    lastVisible,
                    lastScaleTime,
                    lastScale
                ).ToArray();

                Assert.AreEqual(2, result.Length);
                
                Assert.AreEqual(visTime.nextSmaller(), result[0].Time);
                Assert.AreEqual(Vector3.zero, result[0].Scale);
                
                Assert.AreEqual(visTime, result[1].Time);
                Assert.AreEqual(new Vector3(3,3,3), result[1].Scale);
            }
            
            // if no last scale is there, next scale is used, everything else feels very weird!
            [Test]
            public void IfNoLastVisible_AndNextVisible_TransitionFromLastScaleToVisibleIsCreated() {

                bool? lastVisible = null;
                bool currentVisible = true;

                // These value have no effect in this test, but we have to supply something
                double visTime = 1.0;
                double scaleTime = 2.0;
                double lastScaleTime = 0.0;

                // The value with which we transition from invisible to visible is interpolated between last scale and next scale
                Vector3 scale = new Vector3(2,2,2);
                Vector3? lastScale = new Vector3(4,4,4);

                var result = MergeVisibilityAndScaleTracksCurrentState.mergedSamplesForNextVisibilityChange(
                    visTime,
                    currentVisible,
                    scaleTime,
                    scale,
                    lastVisible,
                    lastScaleTime,
                    lastScale
                ).ToArray();

                Assert.AreEqual(2, result.Length);
                
                Assert.AreEqual(visTime.nextSmaller(), result[0].Time);
                Assert.AreEqual(Vector3.zero, result[0].Scale);
                
                Assert.AreEqual(visTime, result[1].Time);
                Assert.AreEqual(new Vector3(3,3,3), result[1].Scale);
            }
        }
    }
}