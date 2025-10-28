#nullable enable
using System;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityGLTF;
using UnityGLTF.Timeline;

namespace Tests.Editor
{
    public class RecorderTests
    {
        // Tests for testing that merging visibility and scale tracks works correctly

		private AnimationTrack<GameObject, bool>? visibilityTrack;
		private AnimationTrack<Transform, Vector3>? scaleTrack;

        private static readonly TimeSpan testTimeStep = TimeSpan.FromMilliseconds(20);
        
        [SetUp]
        public void Setup() {
            visibilityTrack = null;
            scaleTrack = null;
        }
        
        [Test]
        public void IfBothTracksAreNull_NullIsReturned() {
            var result = GLTFRecorder.mergeVisibilityAndScaleTracks(visibilityTrack, scaleTrack);
            Assert.IsNull(result);
        }
        
        [Test]
        public void IfVisibilityTrackIsNull_ScaleTrackIsReturned() {
            var times = new ulong[3] { 0, 5, 10 };
            var values = new Vector3[3] { Vector3.one, Vector3.one, Vector3.one };
            
            scaleTrack = Substitute.For<AnimationTrack<Transform, Vector3>>();
            scaleTrack.Times.Returns(times);
            scaleTrack.Values.Returns(values);
            
            var result = GLTFRecorder.mergeVisibilityAndScaleTracks(visibilityTrack, scaleTrack);
            Assert.IsNotNull(result);
            Assert.AreEqual(times, result!.Value.times);
            Assert.AreEqual(values, result!.Value.mergedScales);
        }
        
        [Test]
        public void IfScaleTrackIsNull_VisibilityTrackWithInstantaneousSwapsIsReturned() {
            var times = new ulong[3] { 0, 5, 10 };
            var values = new bool[3] { false, true, false };
            var expectedTimes = new ulong[] { 0, 4, 5, 9, 10 };
            var expectedResult = new Vector3[] { Vector3.zero, Vector3.zero,Vector3.one, Vector3.one, Vector3.zero };
            
            visibilityTrack = Substitute.For<AnimationTrack<GameObject, bool>>();
            visibilityTrack.Times.Returns(times);
            visibilityTrack.Values.Returns(values);
            
            var result = GLTFRecorder.mergeVisibilityAndScaleTracks(visibilityTrack, scaleTrack);
            Assert.IsNotNull(result);
            Assert.AreEqual(AnimationInterpolationType.LINEAR, result!.Value.interpolation);
            Assert.AreEqual(expectedTimes, result!.Value.times);
            Assert.AreEqual(expectedResult, result!.Value.mergedScales);
        }
    }
}