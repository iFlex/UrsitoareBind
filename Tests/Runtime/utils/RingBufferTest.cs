#if (UNITY_EDITOR) 
using NUnit.Framework;
using Prediction.utils;

namespace Prediction.Tests
{
    public class RingBufferTest
    {
        //TODO
        [Test]
        public void TestAddAndRetrieve()
        {
            RingBuffer<int> test = new RingBuffer<int>(3);
            Assert.AreEqual(0, test.GetFill());
            Assert.AreEqual(0, test.GetStartIndex());
            Assert.AreEqual(0, test.GetEndIndex());

            test.Add(2);
            Assert.AreEqual(0, test.GetStartIndex());
            Assert.AreEqual(1, test.GetEndIndex());
            
            test.Add(3);
            Assert.AreEqual(0, test.GetStartIndex());
            Assert.AreEqual(2, test.GetEndIndex());
            Assert.AreEqual(2, test.GetFill());
            
            test.Add(4);
            Assert.AreEqual(2, test.Get(0));
            Assert.AreEqual(3, test.Get(1));
            Assert.AreEqual(4, test.Get(2));    
            Assert.AreEqual(2, test.Get(3));    
            Assert.AreEqual(3, test.Get(4));    
            Assert.AreEqual(4, test.Get(5));    
            Assert.AreEqual(2, test.Get(300));    
            Assert.AreEqual(3, test.Get(301));    
            Assert.AreEqual(4, test.Get(302));
            Assert.AreEqual(0, test.GetStartIndex());
            Assert.AreEqual(0, test.GetEndIndex());
            Assert.AreEqual(3, test.GetFill());
            
            test.Add(5);
            Assert.AreEqual(5, test.Get(0));
            Assert.AreEqual(3, test.Get(1));
            Assert.AreEqual(4, test.Get(2));  
            Assert.AreEqual(3, test.GetWithLocalIndex(0));
            Assert.AreEqual(4, test.GetWithLocalIndex(1));
            Assert.AreEqual(5, test.GetWithLocalIndex(2));
            Assert.AreEqual(1, test.GetStartIndex());
            Assert.AreEqual(1, test.GetEndIndex());
            
            test.Add(6);
            Assert.AreEqual(5, test.Get(0));
            Assert.AreEqual(6, test.Get(1));
            Assert.AreEqual(4, test.Get(2));  
            Assert.AreEqual(4, test.GetWithLocalIndex(0));
            Assert.AreEqual(5, test.GetWithLocalIndex(1));
            Assert.AreEqual(6, test.GetWithLocalIndex(2));  
            Assert.AreEqual(2, test.GetStartIndex());
            Assert.AreEqual(2, test.GetEndIndex());
            Assert.AreEqual(3, test.GetFill());
            
            test.Add(7);
            Assert.AreEqual(5, test.Get(0));
            Assert.AreEqual(6, test.Get(1));
            Assert.AreEqual(7, test.Get(2)); 
            Assert.AreEqual(5, test.GetWithLocalIndex(0));
            Assert.AreEqual(6, test.GetWithLocalIndex(1));
            Assert.AreEqual(7, test.GetWithLocalIndex(2));  
            Assert.AreEqual(5, test.Get(3));    
            Assert.AreEqual(6, test.Get(4));    
            Assert.AreEqual(7, test.Get(5));   
            Assert.AreEqual(0, test.GetStartIndex());
            Assert.AreEqual(0, test.GetEndIndex());
            Assert.AreEqual(3, test.GetFill());
        }
        
        //TODO: test GetEndIndex
        //TODO: test add & remove
        //TODO: test pop start
        //TODO: test pop end
        //TODO: test get with local index
        //TODO: test get start & end
    }
}
#endif