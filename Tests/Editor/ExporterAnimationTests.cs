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
            // a list without duplicates, thus no potential to remove entries
            float[] times = { 0,1,2,3,4,5,6 };
            object[] values = { 0,1,2,3,4,5,6 };
            var (retTimes, retValues) = AnimationFilteringUtils.RemoveUnneededKeyframes(times, values);
            
            // list is already optimal, we should not do anything
            Assert.ReferenceEquals(retTimes, times);
            Assert.ReferenceEquals(retValues, values);
        }
        
    }
    
    
}