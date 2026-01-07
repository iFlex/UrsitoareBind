#if (UNITY_EDITOR) 
using System.Collections.Generic;
using NUnit.Framework;
using Prediction.data;
using Prediction.policies.singleInstance;
using Prediction.Tests.mocks;
using UnityEngine;

namespace Prediction.Tests
{
    public class ClientPredictedEntityTest
    {
        public static GameObject test;
        public static Rigidbody rigidbody;
        public static MockPredictableControllableComponent component;
        public static ClientPredictedEntity entity;
        public static MockPhysicsController physicsController;
        public static SimpleConfigurableResimulationDecider resimDecider = new SimpleConfigurableResimulationDecider();
        
        [SetUp]
        public void SetUp()
        {
            test = new GameObject("test");
            test.transform.position = Vector3.zero;
            rigidbody = test.AddComponent<Rigidbody>();
            
            component = new MockPredictableControllableComponent();
            component.rigidbody = rigidbody;
            physicsController = new MockPhysicsController();
            
            entity = new ClientPredictedEntity(0, false, 20, rigidbody, test, new []{component}, new[]{component});
            entity.SetSingleStateEligibilityCheckHandler(resimDecider.Check);
            entity.SetControlledLocally(true);
        }

        [TearDown]
        public void TearDown()
        {
            entity.resimulation.Clear();
            entity.resimulationStep.Clear();
            
            rigidbody = null;
            GameObject.Destroy(test);
            test = null;
            component = null;
            entity = null;
            physicsController = null;
        }

        void AssertInputStateAtTick(uint tickId, Vector3 expectedInputVector)
        {
            PredictionInputRecord inputRecord = entity.localInputBuffer.Get((int) tickId);
            inputRecord.ReadReset();
            Vector3 buffered = new Vector3(inputRecord.ReadNextScalar(), inputRecord.ReadNextScalar(), inputRecord.ReadNextScalar());
            Assert.AreEqual(expectedInputVector, buffered);
        }

        public static Vector3[] ComputePosStream(Vector3[] stream, Dictionary<int, Vector3> positionCorrections)
        {
            Vector3[] output = new Vector3[stream.Length];
            Vector3 accumulator = Vector3.zero;
            bool switched = false;
            for (int i = 0; i < stream.Length; i++)
            {
                if (positionCorrections != null && positionCorrections.ContainsKey(i))
                {
                    accumulator = positionCorrections[i];
                }
                else
                {
                    accumulator += stream[i];
                }
                output[i] = accumulator;
            }
            return output;
        }

        public static PhysicsStateRecord[] GenerateServerStates(Vector3[] inputs, Rigidbody rigidbody)
        {
            PhysicsStateRecord[] states = new PhysicsStateRecord[inputs.Length];
            for (int i = 0; i < inputs.Length; i++)
            {
                rigidbody.position += inputs[i];
                states[i] = new PhysicsStateRecord();
                states[i].From(rigidbody);
                states[i].tickId = (uint) i;
            }
            rigidbody.position = Vector3.zero;
            return states;
        }

        public static Vector3 GetInputFromInputRecord(PredictionInputRecord record)
        {
            record.ReadReset();
            return new Vector3(record.ReadNextScalar(), record.ReadNextScalar(), record.ReadNextScalar());
        }
        
        [Test]
        public void TestTickAdvanceAndInputReport()
        {
            var inputs = new [] { Vector3.zero, Vector3.left, Vector3.right, Vector3.up, Vector3.down };
            var serverTicks = GenerateServerStates(inputs, rigidbody);

            //TODO: modernize test, generate all expected positions instead of sampling physics rb & check
            for (uint tickId = 1; tickId < inputs.Length; ++tickId)
            {
                component.inputVector = inputs[tickId];
                PredictionInputRecord inputRecord = entity.ClientSimulationTick(tickId);
                entity.SamplePhysicsState(tickId);
                
                inputRecord.ReadReset();
                Vector3 sampled = new Vector3(inputRecord.ReadNextScalar(), inputRecord.ReadNextScalar(), inputRecord.ReadNextScalar());
                //Correct sampling done by tick
                Assert.AreEqual(component.inputVector, sampled);
                //Correct setting of the state
                Assert.AreEqual(component.stateVector, sampled);
                //Force application call
                Assert.AreEqual(tickId, component.forceApplyCallCount);
                
                //Buffered state
                Assert.AreEqual(serverTicks[tickId], entity.localStateBuffer.Get((int)tickId));
            }
        }

        [Test]
        public void TestHappyPath()
        {
            var inputs = new [] { Vector3.zero, Vector3.left, Vector3.left, Vector3.right, Vector3.up, Vector3.up, Vector3.right, Vector3.up, Vector3.right, Vector3.down, Vector3.left };
            var serverTicks = GenerateServerStates(inputs, rigidbody);
            int serverDelay = 3;
            
            for (uint tickId = 1; tickId < inputs.Length; ++tickId)
            {
                component.inputVector = inputs[tickId];
                PredictionInputRecord inputRecord = entity.ClientSimulationTick(tickId);
                entity.SamplePhysicsState(tickId);
                if (tickId >= serverDelay)
                {
                    entity.BufferServerTick(tickId, serverTicks[tickId - serverDelay]);
                }
            }

            Assert.AreEqual(inputs.Length - 1, component.forceApplyCallCount);
            for (int i = 1; i < inputs.Length; ++i)
            {
                AssertInputStateAtTick((uint) i, inputs[i]);
                if (i < inputs.Length - 1)
                    Assert.AreEqual(serverTicks[i], entity.localStateBuffer.Get(i));
                if (i < inputs.Length - serverDelay)
                    Assert.AreEqual(serverTicks[i], entity.serverStateBuffer.Get((uint) i));
            }
        }

        [Test]
        public void TestServerOutOfOrderMessages()
        {
            var inputs = new [] { Vector3.left, Vector3.left, Vector3.right, Vector3.up, Vector3.up, Vector3.right, Vector3.up, Vector3.right, Vector3.down, Vector3.left };
            var serverTicks = GenerateServerStates(inputs, rigidbody);
            List<int[]> serverScramble = new List<int[]>();
            serverScramble.Add(new int[0]);
            serverScramble.Add(new int[0]);
            serverScramble.Add(new int[0]);
            serverScramble.Add(new [] { 0 });
            serverScramble.Add(new [] { 2 });
            serverScramble.Add(new [] { 4 });
            serverScramble.Add(new [] { 1, 5 });
            serverScramble.Add(new [] { 3, 6 });
            serverScramble.Add(new [] { 7 });
            serverScramble.Add(new [] { 8 });
                
            for (uint tickId = 0; tickId < inputs.Length; ++tickId)
            {
                component.inputVector = inputs[tickId];
                entity.ClientSimulationTick(tickId);
                
                int[] server = serverScramble[(int) tickId];
                foreach (int i in server)
                {
                    entity.BufferServerTick(tickId, serverTicks[i]);
                }
            }
            //TODO: tests!!
        }
        
        [Test]
        public void TestServerDriftAndResimulate()
        {
            var inputs = new []       { Vector3.zero, Vector3.right, Vector3.up, Vector3.right, Vector3.up, Vector3.right,  Vector3.up, Vector3.right, Vector3.up, Vector3.right, Vector3.up, Vector3.right,   Vector3.up, Vector3.right, Vector3.up, Vector3.right, Vector3.up };
            var serverInputs = new [] { Vector3.zero, Vector3.right, Vector3.up, Vector3.right, Vector3.up,    Vector3.up,  Vector3.up, Vector3.right, Vector3.up, Vector3.right, Vector3.up, Vector3.up,      Vector3.up, Vector3.right, Vector3.up, Vector3.right, Vector3.up };
            var serverTicks = GenerateServerStates(serverInputs, rigidbody);
            Dictionary<int, Vector3> posCorrections = new Dictionary<int, Vector3>();
            posCorrections.Add(5, serverTicks[5].position);
            posCorrections.Add(11, serverTicks[11].position);
            
            int serverDelay = 3;
            for (uint tickId = 1; tickId < inputs.Length; ++tickId)
            {
                component.inputVector = inputs[tickId];
                entity.ClientSimulationTick(tickId);
                entity.SamplePhysicsState(tickId);
                if (tickId > serverDelay)
                {
                    entity.BufferServerTick(tickId, serverTicks[tickId - serverDelay]);
                }

                if (tickId >= 5 && tickId < 8)
                {
                    Assert.AreEqual(PredictionDecision.NOOP, entity.GetPredictionDecision(tickId, out uint ignore));
                }
                if (tickId >= 8)
                {
                    Assert.AreEqual(PredictionDecision.RESIMULATE, entity.GetPredictionDecision(tickId, out uint from));
                    Assert.AreEqual(tickId - serverDelay, from);
                }
            }
        }
        
        [Test]
        public void TestExactlyOneResimNeededForOneMissedInput()
        {
            var inputs = new []       { Vector3.zero, Vector3.right, Vector3.up, Vector3.right, Vector3.up,    Vector3.right, Vector3.up, Vector3.right, Vector3.up };
            var serverInputs = new [] { Vector3.zero, Vector3.right, Vector3.up, Vector3.right, Vector3.right, Vector3.right, Vector3.up, Vector3.right, Vector3.up };
            var serverTicks = GenerateServerStates(serverInputs, rigidbody);
            Dictionary<int, Vector3> posCorrections = new Dictionary<int, Vector3>();
            posCorrections.Add(4, serverTicks[4].position);
            
            int serverDelay = 1;
            
            for (uint tickId = 1; tickId < inputs.Length; ++tickId)
            {
                component.inputVector = inputs[tickId];
                entity.ClientSimulationTick(tickId);
                entity.SamplePhysicsState(tickId);
                if (tickId > serverDelay)
                {
                    entity.BufferServerTick(tickId, serverTicks[tickId - serverDelay]);
                }

                if (tickId == 4)
                {
                    Assert.AreEqual(PredictionDecision.NOOP, entity.GetPredictionDecision(tickId, out uint ignore));
                }
                if (tickId >= 5)
                {
                    Assert.AreEqual(PredictionDecision.RESIMULATE, entity.GetPredictionDecision(tickId, out uint from));
                    Assert.AreEqual(tickId - serverDelay, from);
                }
            }
        }

        [Test]
        public void TestResimulationDecision()
        {
            var serverInputs = new [] { Vector3.zero, Vector3.right, Vector3.up, Vector3.right, Vector3.right, Vector3.right, Vector3.up, Vector3.right, Vector3.up };
            var serverTicks = GenerateServerStates(serverInputs, rigidbody);
            
            component.inputVector = Vector3.zero;
            entity.ClientSimulationTick(1);
            entity.SamplePhysicsState(1);
            Assert.AreEqual(PredictionDecision.NOOP, entity.GetPredictionDecision(1, out uint ignore0));
            Assert.AreEqual(0, ignore0);
            
            entity.BufferServerTick(1, serverTicks[2]);
            entity.BufferServerTick(1, serverTicks[3]);
            
            Assert.AreEqual(PredictionDecision.NOOP, entity.GetPredictionDecision(1, out uint ignore1));
            Assert.AreEqual(PredictionDecision.NOOP, entity.GetPredictionDecision(2, out uint ignore2));
            Assert.AreEqual(PredictionDecision.NOOP, entity.GetPredictionDecision(3, out uint ignore3));
            Assert.AreEqual(PredictionDecision.RESIMULATE, entity.GetPredictionDecision(4, out uint ignore4));
            Assert.AreEqual(PredictionDecision.RESIMULATE, entity.GetPredictionDecision(5, out uint ignore5));
            Assert.AreEqual(0, ignore1);
            Assert.AreEqual(0, ignore2);
            Assert.AreEqual(0, ignore3);
            Assert.AreEqual(3, ignore4);
            Assert.AreEqual(3, ignore5);
        }

        [Test]
        public void TestSnapToServerNoData()
        {
            rigidbody.position = Vector3.one * 10;
            entity.SnapToServer(1);
            Assert.AreEqual(Vector3.one * 10, rigidbody.position);
        }
        
        [Test]
        public void TestSnapToServer()
        {
            var serverInputs = new [] { Vector3.zero, Vector3.right, Vector3.up, Vector3.right, Vector3.right, Vector3.right, Vector3.up, Vector3.right, Vector3.up };
            var serverTicks = GenerateServerStates(serverInputs, rigidbody);
            rigidbody.position = Vector3.one * 10;
            entity.BufferServerTick(0, serverTicks[1]);
            entity.BufferServerTick(0, serverTicks[2]);
            entity.BufferServerTick(0, serverTicks[3]);
            
            entity.SnapToServer(1);
            Assert.AreEqual(serverTicks[1].position, rigidbody.position);
            entity.SnapToServer(2);
            Assert.AreEqual(serverTicks[2].position, rigidbody.position);
            entity.SnapToServer(3);
            Assert.AreEqual(serverTicks[3].position, rigidbody.position);
            entity.SnapToServer(4);
            Assert.AreEqual(serverTicks[3].position, rigidbody.position);
        }
        
        //TODO: test larger packet drop? test repeated package?
    }
}
#endif