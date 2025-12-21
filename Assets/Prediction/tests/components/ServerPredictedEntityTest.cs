#if (UNITY_EDITOR) 
using System.Linq;
using NUnit.Framework;
using Prediction.data;
using Prediction.Tests.mocks;
using UnityEngine;

namespace Prediction.Tests
{
    //TODO: fix
    public class ServerPredictedEntityTest
    {
        public static GameObject test;
        public static Rigidbody rigidbody;
        public static MockPredictableControllableComponent component;
        public static ServerPredictedEntity entity;
        public static MockPhysicsController physicsController;
        
        static Vector3[] inputs =
        {
            Vector3.zero,
            Vector3.down, 
            Vector3.down, 
            Vector3.left, 
            Vector3.left, 
            Vector3.up, 
            Vector3.right, 
            Vector3.up, 
            Vector3.right, 
            Vector3.up,
        };
        static PredictionInputRecord[] reports = GeneratePlayerInputReports(inputs);
        //TODO: extract in test utils.
        private static Vector3[] positions = ClientPredictedEntityTest.ComputePosStream(inputs, null);
            
        [SetUp]
        public void SetUp()
        {
            test = new GameObject("test");
            test.transform.position = Vector3.zero;
            rigidbody = test.AddComponent<Rigidbody>();
            
            component = new MockPredictableControllableComponent();
            component.rigidbody = rigidbody;
            
            entity = new ServerPredictedEntity(0 , 20, rigidbody, test, new []{component}, new[]{component});
            physicsController = new MockPhysicsController();

            entity.useBuffering = false;
        }

        static PredictionInputRecord[] GeneratePlayerInputReports(Vector3[] inputs)
        {
            PredictionInputRecord[] reports = new PredictionInputRecord[inputs.Length];
            for (int i = 0; i < reports.Length; i++)
            {
                reports[i] = new PredictionInputRecord(3, 0);
                reports[i].WriteReset();
                reports[i].WriteNextScalar(inputs[i].x);
                reports[i].WriteNextScalar(inputs[i].y);
                reports[i].WriteNextScalar(inputs[i].z);
            }
            return reports;
        }

        void AssertPostServerTickState(int tickId, Vector3 position, Vector3 inputApplied, PhysicsStateRecord state, bool skipInput = false)
        {
            Assert.AreEqual(tickId, entity.GetTickId());
            Assert.AreEqual(tickId, state.tickId);
            Assert.AreEqual(position, state.position);
            if (!skipInput)
                Assert.AreEqual(inputApplied, component.stateVector);
        }
        
        void AssertPostServerTickState(int tickId, PhysicsStateRecord state)
        {
            AssertPostServerTickState(tickId, positions[tickId], inputs[tickId], state);
        }
        
        [Test]
        public void TestHappyPath()
        {
            for (int i = 1; i < reports.Length; i++)
            {
                Assert.AreEqual(i-1, entity.GetTickId());
                entity.BufferClientTick((uint) i, reports[i]);
                entity.ServerSimulationTick();
                var state = entity.SamplePhysicsState();

                AssertPostServerTickState(i, positions[i], inputs[i], state);
                Assert.AreEqual(i, component.forceApplyCallCount);
                Assert.AreEqual(i, component.inputLoadCallCount);
            }
        }

        [Test]
        public void TestBufferingReceivedMultiPacketsPerTick()
        {
            entity.ServerSimulationTick();
            Assert.AreEqual(0, entity.GetTickId());
            entity.ServerSimulationTick();
            Assert.AreEqual(0, entity.GetTickId());
            
            entity.BufferClientTick(1, reports[1]);
            entity.BufferClientTick(2, reports[2]);
            entity.BufferClientTick(3, reports[3]);
            entity.ServerSimulationTick();
            AssertPostServerTickState(1, entity.SamplePhysicsState());
            
            entity.BufferClientTick(4, reports[4]);
            entity.ServerSimulationTick();
            AssertPostServerTickState(2, entity.SamplePhysicsState());
            entity.ServerSimulationTick();
            AssertPostServerTickState(3, entity.SamplePhysicsState());
            entity.BufferClientTick(5, reports[5]);
            entity.BufferClientTick(6, reports[6]);
            entity.BufferClientTick(7, reports[7]);
            entity.ServerSimulationTick();
            AssertPostServerTickState(4, entity.SamplePhysicsState());
            entity.ServerSimulationTick();
            AssertPostServerTickState(5, entity.SamplePhysicsState());
            entity.BufferClientTick(8, reports[8]);
            entity.BufferClientTick(9, reports[9]);
            entity.ServerSimulationTick();
            AssertPostServerTickState(6, entity.SamplePhysicsState());
            entity.ServerSimulationTick();
            AssertPostServerTickState(7, entity.SamplePhysicsState());
            entity.ServerSimulationTick();
            AssertPostServerTickState(8, entity.SamplePhysicsState());
            entity.ServerSimulationTick();
            AssertPostServerTickState(9, entity.SamplePhysicsState());
            
            entity.ServerSimulationTick();
            AssertPostServerTickState(9, positions[9] + inputs[9], Vector3.zero, entity.SamplePhysicsState(), true);
            entity.ServerSimulationTick();
            AssertPostServerTickState(9, positions[9] + inputs[9] * 2, Vector3.zero, entity.SamplePhysicsState(), true);
            entity.ServerSimulationTick();
            AssertPostServerTickState(9, positions[9] + inputs[9] * 3, Vector3.zero, entity.SamplePhysicsState(), true);
            Assert.AreEqual(14, component.forceApplyCallCount);
            Assert.AreEqual(9, component.inputLoadCallCount);
        }
        
        [Test]
        public void TestOutOfOrderTicks()
        {
            entity.BufferClientTick(2, reports[2]);
            entity.BufferClientTick(1, reports[1]);
            entity.BufferClientTick(3, reports[3]);
            entity.ServerSimulationTick();
            AssertPostServerTickState(1, entity.SamplePhysicsState());
            entity.ServerSimulationTick();
            AssertPostServerTickState(2, entity.SamplePhysicsState());
            entity.ServerSimulationTick();
            AssertPostServerTickState(3, entity.SamplePhysicsState());
            entity.BufferClientTick(4, reports[4]);
            entity.BufferClientTick(5, reports[5]);
            entity.ServerSimulationTick();
            AssertPostServerTickState(4, entity.SamplePhysicsState());
            entity.ServerSimulationTick();
            AssertPostServerTickState(5, entity.SamplePhysicsState());
            entity.BufferClientTick(7, reports[7]);
            entity.BufferClientTick(6, reports[6]);
            entity.ServerSimulationTick();
            AssertPostServerTickState(6, entity.SamplePhysicsState());
            entity.ServerSimulationTick();
            AssertPostServerTickState(7, entity.SamplePhysicsState());
            entity.BufferClientTick(9, reports[9]);
            entity.BufferClientTick(8, reports[8]);
            entity.ServerSimulationTick();
            AssertPostServerTickState(8, entity.SamplePhysicsState());
            entity.ServerSimulationTick();
            AssertPostServerTickState(9, entity.SamplePhysicsState());
        }
        
        [Test]
        public void TestDroppedMessages()
        {
            Vector3[] serverInput =
            {
                Vector3.zero,
                inputs[1], //1
                inputs[1], //2
                inputs[3], //3
                inputs[4],  //4
                inputs[4], //5
                inputs[6],  //6
                inputs[7], //7
                inputs[7], //8
                inputs[9] //9
            };
            Vector3[] serverPos = ClientPredictedEntityTest.ComputePosStream(serverInput, null);
            
            int[] skip = new int[]{ 2, 5, 8};
            int lastTicked = 0;
            for (int i = 0; i < reports.Length; i++)
            {
                bool doSkip = skip.Contains(i);
                if (!doSkip)
                    entity.BufferClientTick((uint) i, reports[i]);
                
                entity.ServerSimulationTick();
                var state = entity.SamplePhysicsState();
                if (!doSkip)
                {
                    lastTicked = i;
                    AssertPostServerTickState(i, serverPos[i], inputs[i], state);
                }
                else
                {
                    AssertPostServerTickState(lastTicked, serverPos[i], serverInput[i], state);
                }
            }
            
            Assert.AreEqual(reports.Length, component.forceApplyCallCount);
            Assert.AreEqual(reports.Length - skip.Length - 1, component.inputLoadCallCount);    
        }
        
        [Test]
        public void TestLatencyAndJitter()
        {
            Vector3[] serverInput =
            {
                Vector3.zero, //0
                inputs[1],    //1
                inputs[2],    //2
                inputs[3],    //3
                inputs[4],    //4
                inputs[5],    //5
                inputs[5],    //6
                inputs[6],    //7
                inputs[7],    //8
                inputs[7],     //9
                inputs[8],     //10
                inputs[9],     //11
                inputs[9]     //12
            };
            Vector3[] serverPos = ClientPredictedEntityTest.ComputePosStream(serverInput, null);
            
            entity.BufferClientTick(2, reports[2]);
            entity.BufferClientTick(1, reports[1]);
            entity.ServerSimulationTick();
            AssertPostServerTickState(1, entity.SamplePhysicsState());
            entity.BufferClientTick(3, reports[3]);
            entity.ServerSimulationTick();
            AssertPostServerTickState(2, entity.SamplePhysicsState());
            entity.BufferClientTick(4, reports[4]);
            entity.BufferClientTick(5, reports[5]);
            entity.ServerSimulationTick();
            AssertPostServerTickState(3, entity.SamplePhysicsState());
            entity.ServerSimulationTick();
            AssertPostServerTickState(4, entity.SamplePhysicsState());
            entity.ServerSimulationTick();
            AssertPostServerTickState(5, entity.SamplePhysicsState());
            entity.ServerSimulationTick();
            AssertPostServerTickState(5, serverPos[6], serverInput[6], entity.SamplePhysicsState());
            entity.BufferClientTick(6, reports[6]);
            entity.ServerSimulationTick();
            AssertPostServerTickState(6, serverPos[7], inputs[6], entity.SamplePhysicsState());
            entity.BufferClientTick(7, reports[7]);
            entity.ServerSimulationTick();
            AssertPostServerTickState(7, serverPos[8], inputs[7], entity.SamplePhysicsState());
            entity.ServerSimulationTick();
            AssertPostServerTickState(7, serverPos[9], serverInput[9], entity.SamplePhysicsState());
            entity.BufferClientTick(8, reports[8]);
            entity.BufferClientTick(9, reports[9]);
            entity.ServerSimulationTick();
            AssertPostServerTickState(8, serverPos[10], inputs[8], entity.SamplePhysicsState());
            entity.ServerSimulationTick();
            AssertPostServerTickState(9, serverPos[11], inputs[9], entity.SamplePhysicsState());
            entity.ServerSimulationTick();
            AssertPostServerTickState(9, serverPos[12], serverInput[12], entity.SamplePhysicsState(), true);
        }

        [Test]
        public void TestPacketArrivesTooLate()
        {
            Vector3[] serverInput =
            {
                Vector3.zero, //0
                inputs[1],    //1
                inputs[2],    //2
                inputs[3],    //3
                inputs[4],    //4
                inputs[6],    //5
                inputs[6],    //6
                inputs[7],    //7
                inputs[9],    //8
                inputs[9]     //9
            };
            Vector3[] serverPos = ClientPredictedEntityTest.ComputePosStream(serverInput, null);
        
            entity.BufferClientTick(1, reports[1]);
            entity.BufferClientTick(2, reports[2]);
            entity.ServerSimulationTick();
            AssertPostServerTickState(1, entity.SamplePhysicsState());
            entity.BufferClientTick(3, reports[3]);
            entity.BufferClientTick(1, reports[1]); //Drop
            entity.ServerSimulationTick();
            AssertPostServerTickState(2, entity.SamplePhysicsState());
            entity.BufferClientTick(4, reports[4]);
            entity.ServerSimulationTick();
            AssertPostServerTickState(3, entity.SamplePhysicsState());
            entity.ServerSimulationTick();
            AssertPostServerTickState(4, entity.SamplePhysicsState());
            entity.BufferClientTick(6, reports[6]);
            entity.ServerSimulationTick();
            AssertPostServerTickState(6, serverPos[5], inputs[6], entity.SamplePhysicsState());
            entity.BufferClientTick(5, reports[5]); //Drop
            entity.ServerSimulationTick();
            AssertPostServerTickState(6, serverPos[6], serverInput[6], entity.SamplePhysicsState());
            entity.BufferClientTick(4, reports[4]); //Drop
            entity.BufferClientTick(7, reports[7]);
            entity.ServerSimulationTick();
            AssertPostServerTickState(7, serverPos[7], inputs[7], entity.SamplePhysicsState());
            entity.BufferClientTick(9, reports[9]);
            entity.ServerSimulationTick();
            AssertPostServerTickState(9, serverPos[8], inputs[9], entity.SamplePhysicsState());
            entity.BufferClientTick(8, reports[8]); //Drop
            entity.BufferClientTick(6, reports[6]); //Drop
            entity.BufferClientTick(7, reports[7]); //Drop
            entity.ServerSimulationTick();
            AssertPostServerTickState(9, serverPos[9], serverPos[9], entity.SamplePhysicsState());
        }

        [Test]
        public void TestNoBuffering()
        {
            entity.useBuffering = true;
            entity.bufferFullThreshold = 0;
            
            Vector3[] serverInput =
            {
                Vector3.zero, //0
                inputs[1],    //1
                inputs[2],    //2
                inputs[3],    //3
                inputs[4],    //4
            };
            Vector3[] serverPos = ClientPredictedEntityTest.ComputePosStream(serverInput, null);
            
            entity.ServerSimulationTick();
            PhysicsStateRecord record = entity.SamplePhysicsState();
            Assert.AreEqual(0, record.tickId);
            Assert.AreEqual(Vector3.zero, record.position);
            
            entity.BufferClientTick(1, reports[1]);
            entity.ServerSimulationTick();
            record = entity.SamplePhysicsState();
            Assert.AreEqual(1, record.tickId);
            Assert.AreEqual(serverPos[1], record.position);
            
            entity.BufferClientTick(2, reports[2]);
            entity.ServerSimulationTick();
            record = entity.SamplePhysicsState();
            Assert.AreEqual(2, record.tickId);
            Assert.AreEqual(serverPos[2], record.position);
            
            entity.BufferClientTick(3, reports[3]);
            entity.ServerSimulationTick();
            record = entity.SamplePhysicsState();
            Assert.AreEqual(3, record.tickId);
            Assert.AreEqual(serverPos[3], record.position);
        }
        
        //TODO: reevaluate positions... probably because we haven't taken into account buffer positions.        
        [Test]
        public void TestBuffering()
        {
            entity.useBuffering = true;
            entity.bufferFullThreshold = 3;
            
            Vector3[] serverInput =
            {
                Vector3.zero, //0
                inputs[1],    //1
                inputs[2],    //2
                inputs[3],    //3
                inputs[4],    //4
                inputs[5],    //5
                inputs[6],    //6
                inputs[7],    //7
                inputs[8],    //8
                inputs[9]     //9
            };
            Vector3[] serverPos = ClientPredictedEntityTest.ComputePosStream(serverInput, null);
            
            entity.ServerSimulationTick();
            PhysicsStateRecord record = entity.SamplePhysicsState();
            Assert.AreEqual(0, record.tickId);
            Assert.AreEqual(Vector3.zero, record.position);
            
            entity.BufferClientTick(1, reports[1]);
            entity.ServerSimulationTick();
            record = entity.SamplePhysicsState();
            Assert.AreEqual(0, record.tickId);
            Assert.AreEqual(Vector3.zero, record.position);
            
            entity.BufferClientTick(2, reports[2]);
            entity.ServerSimulationTick();
            record = entity.SamplePhysicsState();
            Assert.AreEqual(0, record.tickId);
            Assert.AreEqual(Vector3.zero, record.position);
            
            entity.BufferClientTick(3, reports[3]);
            entity.ServerSimulationTick();
            record = entity.SamplePhysicsState();
            Assert.AreEqual(1, record.tickId);
            Assert.AreEqual(serverPos[1], record.position);
            
            entity.BufferClientTick(4, reports[4]);
            entity.ServerSimulationTick();
            record = entity.SamplePhysicsState();
            Assert.AreEqual(2, record.tickId);
            Assert.AreEqual(serverPos[2], record.position);
            
            entity.BufferClientTick(5, reports[5]);
            entity.ServerSimulationTick();
            record = entity.SamplePhysicsState();
            Assert.AreEqual(3, record.tickId);
            Assert.AreEqual(serverPos[3], record.position);
            
            entity.BufferClientTick(6, reports[6]);
            entity.ServerSimulationTick();
            record = entity.SamplePhysicsState();
            Assert.AreEqual(4, record.tickId);
            Assert.AreEqual(serverPos[4], record.position);

            for (int i = 5; i <= 6; i++)
            {
                entity.ServerSimulationTick();
                record = entity.SamplePhysicsState();
                Assert.AreEqual(i, record.tickId);
                Assert.AreEqual(serverPos[i], record.position);
            }
            //No more incoming data -> 2 missing inputs buffer again
            for (int i = 7; i < serverInput.Length; i++)
            {
                entity.ServerSimulationTick();
                record = entity.SamplePhysicsState();
                Assert.AreEqual(6, record.tickId);
                //Assert.AreEqual(serverPos[6], record.position);
            }
            
            entity.BufferClientTick(7, reports[7]);
            entity.ServerSimulationTick();
            record = entity.SamplePhysicsState();
            Assert.AreEqual(6, record.tickId);
            //Assert.AreEqual(serverPos[6], record.position);
            
            entity.BufferClientTick(8, reports[8]);
            entity.ServerSimulationTick();
            record = entity.SamplePhysicsState();
            Assert.AreEqual(6, record.tickId);
            //Assert.AreEqual(serverPos[6], record.position);
            
            entity.BufferClientTick(9, reports[9]);
            for (int i = 7; i < serverInput.Length; i++)
            {
                entity.ServerSimulationTick();
                record = entity.SamplePhysicsState();
                Assert.AreEqual(i, record.tickId);
                //Assert.AreEqual(serverPos[i], record.position);
            }
            Assert.AreEqual(4, entity.totalBufferingTicks);
            Assert.AreEqual(4, entity.totalMissingInputTicks);
        }
        
        [Test]
        public void TestBufferSkipAhead()
        {
            int count = 20;
            for (int i = 0; i < count; i++)
            {
                entity.BufferClientTick((uint) i, reports[1]);
            }
            entity.ServerSimulationTick();
            Assert.AreEqual(count - 1, entity.inputQueue.GetFill());
            
            entity.BufferClientTick((uint) count, reports[2]);
            entity.ServerSimulationTick();
            Assert.AreEqual(1, entity.inputQueue.GetFill());
            Assert.AreEqual(reports[2], entity.inputQueue.GetEnd());
            entity.SamplePhysicsState();
            Assert.AreEqual(0, entity.inputQueue.GetFill());
        }
        
        //TODO: test new mechanism for catching up
        [Test]
        public void TestBufferSkipAheadDuringNormalOp()
        {
            int count = 20;
            for (int i = 0; i < count * 2; i++)
            {
                entity.BufferClientTick((uint)i, reports[i % reports.Length]);
                if (i % 2 == 0)
                {
                    entity.ServerSimulationTick();
                }

                if (i > 0 && i % count == 0)
                {
                    Assert.AreEqual(1, entity.inputQueue.GetFill());
                }
                else
                {
                    int chk = (i < count) ? i : 1 + i % count;
                    Assert.AreEqual(chk, entity.inputQueue.GetFill());
                }

                if (i % 2 == 0)
                {
                    entity.SamplePhysicsState();
                }
            }
        }
        
        [Test]
        public void TestBufferNeverSkipAheadDuringNormalOp()
        {
            int count = 20;
            for (int i = 1; i < count * 3; i++)
            {
                entity.BufferClientTick((uint) i, reports[i % reports.Length]);
                entity.ServerSimulationTick();
                entity.SamplePhysicsState();
                Assert.AreEqual(0, entity.inputQueue.GetFill());
            }
        }
        
        //TODO: test buffer too big, skip ahead 2...
        [Test]
        public void TestBufferSkipAheadWhenSlowlyFallingBehind()
        {
        }
    }
}
#endif