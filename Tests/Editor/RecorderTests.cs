#nullable enable
using System;
using System.Linq;
using GLTF.Schema;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityGLTF;
using UnityGLTF.Timeline;
using UnityGLTF.Timeline.Samplers;

namespace Tests.Editor
{
    public class RecorderTests
    {
        // Tests for testing that merging visibility and scale tracks works correctly

		private AnimationTrack<GameObject, bool>? visibilityTrack;
		private AnimationTrack<Transform, Vector3>? scaleTrack;

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
            var times = new float[3] { 0, 0.5f, 1 };
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
        public void IfScaleTrackIsNull_VisibilityTrackIsReturned() {
            var times = new float[3] { 0, 0.5f, 1 };
            var values = new bool[3] { false, true, false };
            var expectedResult = new Vector3[3] { Vector3.zero, Vector3.one, Vector3.zero };
            
            visibilityTrack = Substitute.For<AnimationTrack<GameObject, bool>>();
            visibilityTrack.Times.Returns(times);
            visibilityTrack.Values.Returns(values);
            
            var result = GLTFRecorder.mergeVisibilityAndScaleTracks(visibilityTrack, scaleTrack);
            Assert.IsNotNull(result);
            Assert.AreEqual(AnimationInterpolationType.STEP, result!.Value.interpolation);
            Assert.AreEqual(times, result!.Value.times);
            Assert.AreEqual(expectedResult, result!.Value.mergedScales);
        }
    }
}