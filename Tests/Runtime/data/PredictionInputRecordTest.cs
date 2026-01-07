#if (UNITY_EDITOR) 
using NUnit.Framework;
using Prediction.data;

namespace Prediction.Tests.data
{
    public class PredictionInputRecordTest
    {
        [Test]
        public void TestReadWrite() {
            PredictionInputRecord pir = new PredictionInputRecord(3,3);
            pir.ReadReset();
            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual(0, pir.ReadNextScalar());
                Assert.AreEqual(false, pir.ReadNextBool());
            }
            
            pir.WriteReset();
            for (int i = 0; i < 4; i++)
            { 
                //0, 1, 2, 3
                //true, false, true, false
                pir.WriteNextScalar((float) i);
                pir.WriteNextBinary((i % 2 == 0) ? true : false);
            }
            
            pir.ReadReset();
            Assert.AreEqual(0.0, pir.ReadNextScalar());
            Assert.AreEqual(1.0, pir.ReadNextScalar());
            Assert.AreEqual(2.0, pir.ReadNextScalar());
            Assert.AreEqual(true, pir.ReadNextBool());
            Assert.AreEqual(false, pir.ReadNextBool());
            Assert.AreEqual(true, pir.ReadNextBool());
            
            pir.WriteReset();
            for (int i = 5; i < 9; i++)
            {
                //5, 6, 7, 8
                //false, true, false, true
                pir.WriteNextScalar((float) i);
                pir.WriteNextBinary((i % 2 == 0) ? true : false);
            }
            
            pir.ReadReset();
            Assert.AreEqual(5.0, pir.ReadNextScalar());
            Assert.AreEqual(6.0, pir.ReadNextScalar());
            Assert.AreEqual(7.0, pir.ReadNextScalar());
            Assert.AreEqual(false, pir.ReadNextBool());
            Assert.AreEqual(true, pir.ReadNextBool());
            Assert.AreEqual(false, pir.ReadNextBool());
        }
    }
}
#endif