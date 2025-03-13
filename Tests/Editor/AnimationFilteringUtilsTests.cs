using NUnit.Framework;
using UnityGLTF;

namespace Tests.Editor
{
    public class AnimationFilteringUtilsTests
    {

        [Test]
        public void RemoveUnneededKeyframes_WhenSingleElement_ThenNothingHappens() {
            const float singleTime = 10.0f;
            const int singleValue = 42;
            float[] times = { singleTime };
            object[] values = { singleValue };
            var (retTimes, retValues) = AnimationFilteringUtils.RemoveUnneededKeyframes(times, values);

            Assert.AreEqual(retTimes, times);
            Assert.AreEqual(retValues, values);

            Assert.AreEqual(1, times.Length);

            Assert.AreEqual(singleTime, times[0]);
            Assert.AreEqual(singleValue, values[0]);
        }

        [Test]
        public void RemoveUnneededKeyframes_WhenAlreadyOptimal_ThenNothingHappens() {
            float[] times = { 0, 1, 2, 3, 4, 5, 6 };
            object[] values = { 0, 1, 2, 3, 4, 5, 6 };
            var (retTimes, retValues) = AnimationFilteringUtils.RemoveUnneededKeyframes(times, values);

            // list is already optimal, we should not do anything
            Assert.That(ReferenceEquals(retTimes, times));
            Assert.That(ReferenceEquals(retValues, values));
        }

        [Test]
        public void RemoveUnneededKeyframes_WhenDoubleRepeatedValues_ThenNothingHappens() {
            float[] times = { 0, 1, 2, 3, 4, 5, 6 };
            object[] values = { 0, 1, 1, 3, 4, 5, 5 };
            var (retTimes, retValues) = AnimationFilteringUtils.RemoveUnneededKeyframes(times, values);

            // list is already optimal, we should not do anything
            Assert.That(ReferenceEquals(retTimes, times));
            Assert.That(ReferenceEquals(retValues, values));
        }

        [Test]
        public void RemoveUnneededKeyframes_WhenTripleRepeatedValues_ThenUnnecessaryValuesAreRemoved() {
            float[] expectedTimes = { 0, 1, 3, 4, 6 };
            object[] expectedValues = { 0, 1, 1, 5, 5 };

            float[] times = { 0, 1, 2, 3, 4, 5, 6 };
            object[] values = { 0, 1, 1, 1, 5, 5, 5 };
            var (retTimes, retValues) = AnimationFilteringUtils.RemoveUnneededKeyframes(times, values);

            // list is already optimal, we should not do anything
            Assert.AreEqual(expectedTimes, retTimes);
            Assert.AreEqual(expectedValues, retValues);
        }

        [Test]
        public void RemoveUnneededKeyframes_WhenMoreThanThreeRepeatedValues_ThenUnnecessaryValuesAreRemoved() {
            float[] expectedTimes = { 0, 1, 6 };
            object[] expectedValues = { 0, 1, 1 };

            float[] times = { 0, 1, 2, 3, 4, 5, 6 };
            object[] values = { 0, 1, 1, 1, 1, 1, 1 };
            var (retTimes, retValues) = AnimationFilteringUtils.RemoveUnneededKeyframes(times, values);

            // list is already optimal, we should not do anything
            Assert.AreEqual(expectedTimes, retTimes);
            Assert.AreEqual(expectedValues, retValues);
        }

        [Test]
        public void
            RemoveUnneededKeyframes_WhenManyValuesAreSameButIntermittentDifferentValues_ThenOnlyUnnecessaryValuesAreRemoved() {
            float[] expectedTimes = { 0, 1, 3, 4, 5, 7 };
            object[] expectedValues = { 0, 1, 1, 4, 1, 1 };

            float[] times = { 0, 1, 2, 3, 4, 5, 6, 7 };
            object[] values = { 0, 1, 1, 1, 4, 1, 1, 1 };
            var (retTimes, retValues) = AnimationFilteringUtils.RemoveUnneededKeyframes(times, values);

            // list is already optimal, we should not do anything
            Assert.AreEqual(expectedTimes, retTimes);
            Assert.AreEqual(expectedValues, retValues);
        }

        // The method has a branch to handle different-length arrays, but i do not understand why it exists
        // and what it is supposed to do, so I won't write tests for this branch.
        // Test that it does not throw at least
        [Test]
        public void RemoveUnneededKeyframes_WhenLengthDiffers_ThenNoExceptionIsThrown() {
            float[] times = { 0, 1, 2, 3, 4, 5, 6, 7 };
            object[] values = { 0, 1, 4, 1, 1 };
            Assert.DoesNotThrow(() => _ = AnimationFilteringUtils.RemoveUnneededKeyframes(times, values));
        }
        
        
        // The method has a branch to handle different-length arrays, but i do not understand why it exists
        // and what it is supposed to do, so I won't write tests for this branch.
        // Test that it does not throw at least, even if the array length is almost
        // identical to the point where integer rounding may return 1.
        [Test]
        public void RemoveUnneededKeyframes_WhenLengthDiffersSlightly_ThenNoExceptionIsThrown() {
            float[] times   = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            object[] values = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            Assert.DoesNotThrow(() => _ = AnimationFilteringUtils.RemoveUnneededKeyframes(times, values));
        }
    }
}