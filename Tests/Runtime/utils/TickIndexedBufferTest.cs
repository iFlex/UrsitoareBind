#if (UNITY_EDITOR) 
using NUnit.Framework;
using Prediction.utils;

namespace Prediction.Tests
{
    public class TickIndexedBufferTest
    {
        [Test]
        public void TestAddAndRetrieve()
        {
            TickIndexedBuffer<int> test = new TickIndexedBuffer<int>(5);
            test.Add(0, 3);
            test.Add(1, 2);
            test.Add(2, 1);
            
            Assert.AreEqual(3, test.Get(0));
            Assert.AreEqual(2, test.Get(1));
            Assert.AreEqual(1, test.Get(2));
            Assert.AreEqual(3, test.GetFill());
            Assert.AreEqual(5, test.GetCapacity());
            Assert.AreEqual(2, test.GetRange());
            
            test.Clear();
            Assert.AreEqual(0, test.GetFill());
            Assert.AreEqual(5, test.GetCapacity());
            Assert.AreEqual(0, test.GetRange());
            Assert.AreEqual(0, test.Get(1));
            Assert.AreEqual(0, test.Get(2));
            Assert.AreEqual(0, test.Get(0));
            
            test.Add(1, 10);
            test.Add(2, 11);
            test.Add(4, 12);
            test.Add(5, 13);
            
            Assert.AreEqual(0, test.Get(0));
            Assert.AreEqual(10, test.Get(1));
            Assert.AreEqual(11, test.Get(2));
            Assert.AreEqual(0, test.Get(3));
            Assert.AreEqual(12, test.Get(4));
            Assert.AreEqual(13, test.Get(5));
            Assert.AreEqual(0, test.Get(6));
        }

        [Test]
        public void TestAddPastStartEdge()
        {
            TickIndexedBuffer<int> test = new TickIndexedBuffer<int>(5);
            test.Add(2, 10);
            test.Add(3, 11);
            test.Add(1, 12);
            test.Add(5, 13);
            
            Assert.AreEqual(4, test.GetFill());
            Assert.AreEqual(5, test.GetCapacity());
            Assert.AreEqual(4, test.GetRange());
            Assert.AreEqual(0, test.Get(0));
            Assert.AreEqual(12, test.Get(1));
            Assert.AreEqual(10, test.Get(2));
            Assert.AreEqual(11, test.Get(3));
            Assert.AreEqual(0, test.Get(4));
            Assert.AreEqual(13, test.Get(5));
        }

        [Test]
        public void TestAddPastCapacity()
        {
            TickIndexedBuffer<int> test = new TickIndexedBuffer<int>(3);
            test.Add(2, 10);
            test.Add(3, 11);
            test.Add(1, 12);
            test.Add(5, 13);
            
            Assert.AreEqual(3, test.GetFill());
            Assert.AreEqual(3, test.GetCapacity());
            Assert.AreEqual(3, test.GetRange());
            Assert.AreEqual(0, test.Get(0));
            Assert.AreEqual(0, test.Get(1));
            Assert.AreEqual(10, test.Get(2));
            Assert.AreEqual(11, test.Get(3));
            Assert.AreEqual(13, test.Get(5));
        }
        
        [Test]
        public void TestAddPastCapacity2()
        {
            TickIndexedBuffer<uint> test = new TickIndexedBuffer<uint>(10);
            for (uint i = 0; i < 101; ++i)
            {
                test.Add(i, 200 - i);
            }
            Assert.AreEqual(100, test.GetEndTick());
            Assert.AreEqual(91, test.GetStartTick());
            Assert.AreEqual(100, test.GetEnd());
            Assert.AreEqual(109, test.GetStart());
        }
        
        [Test]
        public void TestAddAndConsume()
        {
            TickIndexedBuffer<int> test = new TickIndexedBuffer<int>(5);
            
            test.Add(1, 10);
            test.Add(2, 11);
            Assert.AreEqual(1, test.GetNextTick(0));
            Assert.AreEqual(10, test.Remove(1));  // 2
            Assert.AreEqual(1, test.GetFill());
            Assert.AreEqual(0, test.GetRange());
            
            test.Add(4, 12);
            test.Add(5, 13);
            test.Add(6, 14);                     // 2, 4, 5, 6
            Assert.AreEqual(4, test.GetFill());
            Assert.AreEqual(4, test.GetRange());
            
            Assert.AreEqual(2, test.GetNextTick(1));  
            Assert.AreEqual(11, test.Remove(2)); // 4, 5, 6
            Assert.AreEqual(3, test.GetFill());       
            Assert.AreEqual(2, test.GetRange());
            
            test.Add(8, 15);                     // 4, 5, 6, 8
            Assert.AreEqual(4, test.GetNextTick(2));
            Assert.AreEqual(12, test.Remove(4)); // 5, 6, 8
            Assert.AreEqual(3, test.GetFill());
            Assert.AreEqual(3, test.GetRange());
            
            test.Add(3, 11);                     // 3, 5, 6, 8
            Assert.AreEqual(5, test.GetNextTick(4));
            Assert.AreEqual(13, test.Remove(5)); // 3, 6, 8
            Assert.AreEqual(3, test.GetFill());
            Assert.AreEqual(5, test.GetRange());
            
            test.Add(9, 16);                     // 3, 6, 8, 9
            Assert.AreEqual(6, test.GetNextTick(5));
            Assert.AreEqual(14, test.Remove(6)); // 3, 8, 9
            Assert.AreEqual(3, test.GetFill());
            Assert.AreEqual(6, test.GetRange());
            
            test.Add(10, 17);                    // 3, 8, 9, 10
            Assert.AreEqual(8, test.GetNextTick(6));
            Assert.AreEqual(15, test.Remove(8)); // 3, 9, 10
            Assert.AreEqual(3, test.GetFill());
            Assert.AreEqual(7, test.GetRange());
            
            test.Add(11, 18);                    // 3, 9, 10, 11
            Assert.AreEqual(9, test.GetNextTick(8));
            Assert.AreEqual(16, test.Remove(9)); // 3, 10, 11
            Assert.AreEqual(3, test.GetFill());
            Assert.AreEqual(8, test.GetRange());
            
            Assert.AreEqual(11, test.Remove(3)); // 10, 11
            Assert.AreEqual(10, test.GetNextTick(9)); 
            Assert.AreEqual(17, test.Remove(10)); // 11
            Assert.AreEqual(1, test.GetFill());
            Assert.AreEqual(0, test.GetRange());
            
            Assert.AreEqual(11, test.GetNextTick(10));
            Assert.AreEqual(18, test.Remove(11)); //
            Assert.AreEqual(0, test.GetFill());
            Assert.AreEqual(0, test.GetRange());
            Assert.AreEqual(0, test.GetNextTick(11));
            
            test.Add(14, 19);
            test.Add(15, 20);
            Assert.AreEqual(2, test.GetFill());
            Assert.AreEqual(1, test.GetRange());
            Assert.AreEqual(14, test.GetNextTick(11));
            Assert.AreEqual(19, test.Remove(14)); // 15
            Assert.AreEqual(1, test.GetFill());
            Assert.AreEqual(0, test.GetRange());
            
            test.Add(16, 21);
            Assert.AreEqual(15, test.GetNextTick(14));
            Assert.AreEqual(20, test.Remove(15)); // 16
            Assert.AreEqual(1, test.GetFill());
            Assert.AreEqual(0, test.GetRange());
            
            Assert.AreEqual(16, test.GetNextTick(15));
            Assert.AreEqual(21, test.Remove(16)); //
            Assert.AreEqual(0, test.GetFill());
            Assert.AreEqual(0, test.GetRange());
            Assert.AreEqual(0, test.GetNextTick(16));
        }

        [Test]
        public void TestBoundsUtils()
        {
            TickIndexedBuffer<int> test = new TickIndexedBuffer<int>(5);
            test.Add(1, 10); // 1
            Assert.AreEqual(1, test.GetEndTick());
            Assert.AreEqual(test.GetEndTick(), test.GetStartTick());
            
            test.Add(2, 11); // 1, 2
            test.Add(3, 12); // 1, 2, 3
            Assert.AreEqual(3, test.GetEndTick());
            Assert.AreEqual(1, test.GetStartTick());

            test.Remove(2); // 1, 3
            Assert.AreEqual(3, test.GetEndTick());
            Assert.AreEqual(1, test.GetStartTick());
            
            test.Remove(1); // 3
            Assert.AreEqual(3, test.GetEndTick());
            Assert.AreEqual(3, test.GetStartTick());
            
            test.Add(5, 13);
            test.Add(6, 14);
            test.Add(8, 15);
            test.Add(9, 16); // 3, 5, 6, 8, 9
            
            Assert.AreEqual(9, test.GetEndTick());
            Assert.AreEqual(3, test.GetStartTick());

            test.Remove(3); // 5, 6, 8, 9
            Assert.AreEqual(9, test.GetEndTick());
            Assert.AreEqual(5, test.GetStartTick());

            test.Remove(9); // 5, 6, 8
            Assert.AreEqual(8, test.GetEndTick());
            Assert.AreEqual(5, test.GetStartTick());
            
            test.Add(10, 17); // 5, 6, 8, 10
            Assert.AreEqual(10, test.GetEndTick());
            Assert.AreEqual(5, test.GetStartTick());
            
            test.Remove(8); // 5, 6, 10
            Assert.AreEqual(10, test.GetEndTick());
            Assert.AreEqual(5, test.GetStartTick());

            test.Remove(10); // 5, 6
            Assert.AreEqual(6, test.GetEndTick());
            Assert.AreEqual(5, test.GetStartTick());
            
            test.Remove(6); // 5
            Assert.AreEqual(5, test.GetEndTick());
            Assert.AreEqual(5, test.GetStartTick());

            test.Remove(5);
            Assert.AreEqual(0, test.GetEndTick());
            Assert.AreEqual(0, test.GetStartTick());
            
            test.Add(11, 18);
            test.Add(13, 18);
            test.Add(15, 18);
            test.Add(17, 18); // 11, 13, 15, 17
            
            Assert.AreEqual(17, test.GetEndTick());
            Assert.AreEqual(11, test.GetStartTick());
            
            test.Remove(11); // 13, 15, 17
            Assert.AreEqual(17, test.GetEndTick());
            Assert.AreEqual(13, test.GetStartTick());
            
            test.Remove(15); // 13, 17
            Assert.AreEqual(17, test.GetEndTick());
            Assert.AreEqual(13, test.GetStartTick());
            
            test.Remove(13); // 17
            Assert.AreEqual(17, test.GetEndTick());
            Assert.AreEqual(17, test.GetStartTick());
            
            test.Remove(17);
            Assert.AreEqual(0, test.GetEndTick());
            Assert.AreEqual(0, test.GetStartTick());
        }

        [Test]
        public void TestClear()
        {
            TickIndexedBuffer<int> test = new TickIndexedBuffer<int>(5);
            test.Add(0, 3);
            test.Add(1, 2);
            test.Add(2, 1);
            test.Remove(2);
            test.Add(4, 10);
            test.Add(5, 10);
            
            Assert.AreEqual(4, test.GetFill());
            Assert.AreEqual(5, test.GetRange());
            Assert.AreEqual(5, test.GetCapacity());
            test.Clear();
            Assert.AreEqual(0, test.GetFill());
            Assert.AreEqual(0, test.GetRange());
            Assert.AreEqual(5, test.GetCapacity());
            
            test.Add(1, 2);
            test.Add(2, 1);
            test.Add(4, 10);
            test.Remove(4);
            test.Add(5, 10);
            
            Assert.AreEqual(3, test.GetFill());
            Assert.AreEqual(4, test.GetRange());
            Assert.AreEqual(5, test.GetCapacity());
            test.Clear();
            Assert.AreEqual(0, test.GetFill());
            Assert.AreEqual(0, test.GetRange());
            Assert.AreEqual(5, test.GetCapacity());
        }
    }
}
#endif