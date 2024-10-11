#nullable enable
using System;
using System.Linq;
using GLTF.Schema;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
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
            var times = new double[3] { 0, 0.5, 1 };
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
            var times = new double[3] { 0, 0.5, 1 };
            var values = new bool[3] { false, true, false };
            var expectedResult = new Vector3[3] { Vector3.zero, Vector3.one, Vector3.zero };
            
            visibilityTrack = Substitute.For<AnimationTrack<GameObject, bool>>();
            visibilityTrack.Times.Returns(times);
            visibilityTrack.Values.Returns(values);
            
            var result = GLTFRecorder.mergeVisibilityAndScaleTracks(visibilityTrack, scaleTrack);
            Assert.IsNotNull(result);
            Assert.AreEqual(InterpolationType.STEP, result!.Value.interpolation);
            Assert.AreEqual(times, result!.Value.times);
            Assert.AreEqual(expectedResult, result!.Value.mergedScales);
        }
        
        [Test]
        public void IfBothSampledAtTheSameTime_And_VisibilityTrackIsReturned() {
            var scaleTimes = new double[] {
                0, 0.1, 0.3, 0.4, 0.5, 0.7, 0.8, 0.9, 1
            };
            
            var scaleValues = new Vector3[] {
                Vector3.one,
                Vector3.one,
                new Vector3(2,2,2),
                Vector3.one,
                new Vector3(2,2,2),
                Vector3.one,
                Vector3.one,
                Vector3.zero,
                Vector3.zero
            };
            var visTimes = new double[] {
                0, 0.2, 0.5, 0.8, 1
            };
            var visValues = new bool[] { false, true, false, true, true };
            
            var expectedTimes = new double[] {
                0, 
                // 0.1 is not recorded since we are invisible
                0.2.nextSmaller(),
                0.2,
                0.3, 
                0.4,
                0.5.nextSmaller(),
                0.5,
                // neither has a sample at 0.6 
                // 0.7 is not recorded since we are invisible
                0.8.nextSmaller(),
                0.8,
                0.9,
                1
            };
            var expectedResult = new Vector3[] {
                Vector3.zero,          
                Vector3.zero,         
                new (1.5f,1.5f,1.5f), 
                new Vector3(2,2,2),   
                Vector3.one,          
                new Vector3(2,2,2),   
                Vector3.zero,         
                Vector3.zero,         
                Vector3.one,          
                Vector3.zero,         
                Vector3.zero
            };
            
            scaleTrack = Substitute.For<AnimationTrack<Transform, Vector3>>();
            scaleTrack.Times.Returns(scaleTimes);
            scaleTrack.Values.Returns(scaleValues);
            
            visibilityTrack = Substitute.For<AnimationTrack<GameObject, bool>>();
            visibilityTrack.Times.Returns(visTimes);
            visibilityTrack.Values.Returns(visValues);
            
            var result = GLTFRecorder.mergeVisibilityAndScaleTracks(visibilityTrack, scaleTrack);
            Assert.IsNotNull(result);
            Assert.AreEqual(InterpolationType.LINEAR, result!.Value.interpolation);
            Assert.AreEqual(expectedTimes, result.Value.times);
            Assert.AreEqual(expectedResult, result.Value.mergedScales);
        }
        
        
        
        [Test]
        public void IfBothTracksExist_ResultIsCorrect() {
            
            // Test Samples:
            // 0 : Start
            // 0.1: Same Sample Time
            // 0.2: Same Sample Time
            // 0.3: Next: Scale
            // 0.4: Next: Scale
            // 0.5: Next: Scale
            // 0.6: Next: Visibility
            // 0.7: Next: Scale
            // 0.8: Next: Scale
            // 0.9: Next: Scale
            // 1: End
            
            
            var scaleTimes = new double[] {
                0, 0.1, 0.3, 0.4, 0.5, 0.7, 0.8, 0.9, 1
            };
            
            var scaleValues = new Vector3[] {
                Vector3.one,
                Vector3.one,
                new Vector3(2,2,2),
                Vector3.one,
                new Vector3(2,2,2),
                Vector3.one,
                Vector3.one,
                Vector3.zero,
                Vector3.zero
            };
            var visTimes = new double[] {
                0, 0.2, 0.5, 0.8, 1
            };
            var visValues = new bool[] { false, true, false, true, true };
            
            var expectedTimes = new double[] {
                0, 
                // 0.1 is not recorded since we are invisible
                0.2.nextSmaller(),
                0.2,
                0.3, 
                0.4,
                0.5.nextSmaller(),
                0.5,
                // neither has a sample at 0.6 
                // 0.7 is not recorded since we are invisible
                0.8.nextSmaller(),
                0.8,
                0.9,
                1
            };
            var expectedResult = new Vector3[] {
                Vector3.zero,          
                Vector3.zero,         
                new (1.5f,1.5f,1.5f), 
                new Vector3(2,2,2),   
                Vector3.one,          
                new Vector3(2,2,2),   
                Vector3.zero,         
                Vector3.zero,         
                Vector3.one,          
                Vector3.zero,         
                Vector3.zero
            };
            
            scaleTrack = Substitute.For<AnimationTrack<Transform, Vector3>>();
            scaleTrack.Times.Returns(scaleTimes);
            scaleTrack.Values.Returns(scaleValues);
            
            visibilityTrack = Substitute.For<AnimationTrack<GameObject, bool>>();
            visibilityTrack.Times.Returns(visTimes);
            visibilityTrack.Values.Returns(visValues);
            
            var result = GLTFRecorder.mergeVisibilityAndScaleTracks(visibilityTrack, scaleTrack);
            Assert.IsNotNull(result);
            Assert.AreEqual(InterpolationType.LINEAR, result!.Value.interpolation);
            Assert.AreEqual(expectedTimes, result.Value.times);
            Assert.AreEqual(expectedResult, result.Value.mergedScales);
        }
    }
}