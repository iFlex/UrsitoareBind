#if (UNITY_EDITOR)
using System.Collections.Generic;
using NUnit.Framework;
using Prediction.data;
using Prediction.policies.singleInstance;
using Prediction.Tests.mocks;
using Prediction.Tests.Mocks;
using UnityEngine;

namespace Prediction.Tests
{
    public class PredictionManagerClientTest
    {
        GameObject test;
        Rigidbody rigidbody;
        MockPhysicsController physicsController;
        MockPredictableControllableComponent component;
        SimpleConfigurableResimulationDecider resimDecider = new SimpleConfigurableResimulationDecider();
        PredictionManager manager;
        
        [SetUp]
        public void SetUp()
        {
            test = new GameObject("test");
            test.transform.position = Vector3.zero;
            rigidbody = test.AddComponent<Rigidbody>();
            
            component = new MockPredictableControllableComponent();
            component.rigidbody = rigidbody;
            physicsController = new MockPhysicsController();
            
        }

        [TearDown]
        public void TeaDown()
        {
            rigidbody = null;
            GameObject.Destroy(test);
            test = null;
            component = null;
            physicsController = null;
        }

        [Test]
        public void TestResimulationDecision()
        {
            PredictionManager tstManager = new PredictionManager();
            MockClientPredictedEntity mock1 = new MockClientPredictedEntity(false, 20, rigidbody, test, new PredictableControllableComponent[0], new PredictableComponent[0]);
            MockClientPredictedEntity mock2 = new MockClientPredictedEntity(false, 20, rigidbody, test, new PredictableControllableComponent[0], new PredictableComponent[0]);
            MockClientPredictedEntity mock3 = new MockClientPredictedEntity(false, 20, rigidbody, test, new PredictableControllableComponent[0], new PredictableComponent[0]);
            MockClientPredictedEntity mock4 = new MockClientPredictedEntity(false, 20, rigidbody, test, new PredictableControllableComponent[0], new PredictableComponent[0]);
            MockClientPredictedEntity mock5 = new MockClientPredictedEntity(false, 20, rigidbody, test, new PredictableControllableComponent[0], new PredictableComponent[0]);
            MockClientPredictedEntity mock6 = new MockClientPredictedEntity(false, 20, rigidbody, test, new PredictableControllableComponent[0], new PredictableComponent[0]);
            
            tstManager.AddPredictedEntity(1, mock1);
            tstManager.AddPredictedEntity(2, mock2);
            tstManager.AddPredictedEntity(3, mock3);
            tstManager.AddPredictedEntity(4, mock4);
            tstManager.AddPredictedEntity(5, mock5);
            tstManager.AddPredictedEntity(6, mock6);
            
            mock1._predictionDecision = PredictionDecision.NOOP;       
            mock2._predictionDecision = PredictionDecision.SNAP;
            mock3._predictionDecision = PredictionDecision.RESIMULATE;
            mock4._predictionDecision = PredictionDecision.SNAP;
            mock5._predictionDecision = PredictionDecision.RESIMULATE;
            mock6._predictionDecision = PredictionDecision.NOOP;
            
            mock1._fromTick = 1;
            mock2._fromTick = 2;
            mock3._fromTick = 3;
            mock4._fromTick = 4;
            mock5._fromTick = 5;
            mock6._fromTick = 6;

            tstManager.SetLocalEntity(1);
            mock1.SetControlledLocally(true);
            Assert.AreEqual(PredictionDecision.RESIMULATE, tstManager.ComputePredictionDecision(out uint resimFrom1));
            Assert.AreEqual(3, resimFrom1);
            
            tstManager.SetLocalEntity(2);
            mock2.SetControlledLocally(true);
            Assert.AreEqual(PredictionDecision.RESIMULATE, tstManager.ComputePredictionDecision(out uint resimFrom2));
            Assert.AreEqual(3, resimFrom2);
            
            tstManager.SetLocalEntity(3);
            mock3.SetControlledLocally(true);
            Assert.AreEqual(PredictionDecision.RESIMULATE, tstManager.ComputePredictionDecision(out uint resimFrom3));
            Assert.AreEqual(3, resimFrom3);
            
            mock1._predictionDecision = PredictionDecision.NOOP;       
            mock2._predictionDecision = PredictionDecision.SNAP;
            mock3._predictionDecision = PredictionDecision.SNAP;
            mock4._predictionDecision = PredictionDecision.SNAP;
            mock5._predictionDecision = PredictionDecision.SNAP;
            mock6._predictionDecision = PredictionDecision.NOOP;
            
            Assert.AreEqual(PredictionDecision.SNAP, tstManager.ComputePredictionDecision(out uint resimFrom4));
            
            tstManager.SetLocalEntity(1);
            mock1.SetControlledLocally(true);
            Assert.AreEqual(PredictionDecision.SNAP, tstManager.ComputePredictionDecision(out uint resimFrom5));
            
            mock1._predictionDecision = PredictionDecision.SNAP;       
            mock2._predictionDecision = PredictionDecision.NOOP;
            mock3._predictionDecision = PredictionDecision.NOOP;
            mock4._predictionDecision = PredictionDecision.NOOP;
            mock5._predictionDecision = PredictionDecision.NOOP;
            mock6._predictionDecision = PredictionDecision.NOOP;
                
            Assert.AreEqual(PredictionDecision.SNAP, tstManager.ComputePredictionDecision(out uint resimFrom6));
            
            tstManager.SetLocalEntity(3);
            mock3.SetControlledLocally(true);
            
            Assert.AreEqual(PredictionDecision.SNAP, tstManager.ComputePredictionDecision(out uint resimFrom7));
            
            mock1._predictionDecision = PredictionDecision.RESIMULATE;
            Assert.AreEqual(PredictionDecision.RESIMULATE, tstManager.ComputePredictionDecision(out uint resimFrom8));
            Assert.AreEqual(1, resimFrom8);
            
            tstManager.SetLocalEntity(1);
            mock1.SetControlledLocally(true);
            Assert.AreEqual(PredictionDecision.RESIMULATE, tstManager.ComputePredictionDecision(out uint resimFrom9));
            Assert.AreEqual(1, resimFrom9);
            
            mock1._predictionDecision = PredictionDecision.NOOP;
            Assert.AreEqual(PredictionDecision.NOOP, tstManager.ComputePredictionDecision(out uint resimFrom10));
        }
        
        [Test]
        public void TestServerDriftAndResimulate()
        {
            PredictionManager tstManager = new PredictionManager();
            PredictionManager.PHYSICS_CONTROLLED = physicsController;
            tstManager.Setup(false, true);
            
            MockClientPredictedEntity mock1 = new MockClientPredictedEntity(false, 20, rigidbody, test, new []{component}, new[]{component});
            mock1.SetControlledLocally(true);
            mock1.SetSingleStateEligibilityCheckHandler(resimDecider.Check);
            mock1.decisionPassThrough = true;
            
            tstManager.AddPredictedEntity(1, mock1);
            tstManager.SetLocalEntity(1);
            
            var inputs = new []       { Vector3.zero, Vector3.right, Vector3.up, Vector3.right, Vector3.up, Vector3.right,  Vector3.up, Vector3.right, Vector3.up, Vector3.right, Vector3.up, Vector3.right,   Vector3.up, Vector3.right, Vector3.up, Vector3.right, Vector3.up };
            var serverInputs = new [] { Vector3.zero, Vector3.right, Vector3.up, Vector3.right, Vector3.up,    Vector3.up,  Vector3.up, Vector3.right, Vector3.up, Vector3.right, Vector3.up, Vector3.up,      Vector3.up, Vector3.right, Vector3.up, Vector3.right, Vector3.up };
            var serverTicks = ClientPredictedEntityTest.GenerateServerStates(serverInputs, rigidbody);
            Dictionary<int, Vector3> posCorrections = new Dictionary<int, Vector3>();
            posCorrections.Add(5, serverTicks[5].position);
            posCorrections.Add(11, serverTicks[11].position);
            
            int serverDelay = 3;
            for (uint tickId = 1; tickId < inputs.Length; ++tickId)
            {
                component.inputVector = inputs[tickId];
                tstManager.FixedUpdate();
                if (tickId > serverDelay)
                {
                    tstManager.OnServerStateReceived(1, serverTicks[tickId - serverDelay]);
                }
                Assert.AreEqual(tickId, tstManager.lastClientAppliedTick);
            }
            
            Assert.AreEqual(serverTicks[serverTicks.Length - 1].position, rigidbody.position);
            Assert.AreEqual(2, tstManager.totalResimulations);
            Vector3[] expectedPosStream = ClientPredictedEntityTest.ComputePosStream(serverInputs, posCorrections);
            for (int i = 1; i < inputs.Length; ++i)
            {
                //TODO: can we make it sample on the same tick?
                if (i < inputs.Length - 1)
                    Assert.AreEqual(expectedPosStream[i], mock1.localStateBuffer.Get(i).position);
                Assert.AreEqual(inputs[i], ClientPredictedEntityTest.GetInputFromInputRecord(mock1.localInputBuffer.Get(i)));
                if (i < inputs.Length - serverDelay)
                    Assert.AreEqual(serverTicks[i].position, mock1.serverStateBuffer.Get((uint) i).position);
            }
        }
        
        [Test]
        public void TestExactlyOneResimNeededForOneMissedInput()
        {
            //TODO: move to setup?
            PredictionManager tstManager = new PredictionManager();
            PredictionManager.PHYSICS_CONTROLLED = physicsController;
            tstManager.Setup(false, true);
            
            MockClientPredictedEntity mock1 = new MockClientPredictedEntity(false, 20, rigidbody, test, new []{component}, new[]{component});
            mock1.SetControlledLocally(true);
            mock1.SetSingleStateEligibilityCheckHandler(resimDecider.Check);
            mock1.decisionPassThrough = true;
            
            tstManager.AddPredictedEntity(1, mock1);
            tstManager.SetLocalEntity(1);
            
            var inputs = new []       { Vector3.zero, Vector3.right, Vector3.up, Vector3.right, Vector3.up,    Vector3.right, Vector3.up, Vector3.right, Vector3.up };
            var serverInputs = new [] { Vector3.zero, Vector3.right, Vector3.up, Vector3.right, Vector3.right, Vector3.right, Vector3.up, Vector3.right, Vector3.up };
            var serverTicks = ClientPredictedEntityTest.GenerateServerStates(serverInputs, rigidbody);
            Dictionary<int, Vector3> posCorrections = new Dictionary<int, Vector3>();
            posCorrections.Add(4, serverTicks[4].position);
            
            int serverDelay = 1;
            
            for (uint tickId = 1; tickId < inputs.Length; ++tickId)
            {
                component.inputVector = inputs[tickId];
                tstManager.FixedUpdate();
                if (tickId > serverDelay)
                {
                    tstManager.OnServerStateReceived(1, serverTicks[tickId - serverDelay]);
                }
            }
            
            //TODO: move this test in PredicitonManager test
            Assert.AreEqual(1, tstManager.totalResimulations);
            Assert.AreEqual(serverDelay, tstManager.totalResimulationSteps);
            Vector3[] expectedPosStream = ClientPredictedEntityTest.ComputePosStream(serverInputs, posCorrections);
            for (int i = 1; i < inputs.Length; ++i)
            {
                //TODO: can we make it sample on the same tick?
                if (i < inputs.Length - 1)
                    Assert.AreEqual(expectedPosStream[i], mock1.localStateBuffer.Get(i).position);
                Assert.AreEqual(inputs[i], ClientPredictedEntityTest.GetInputFromInputRecord(mock1.localInputBuffer.Get(i)));
                if (i < inputs.Length - serverDelay)
                    Assert.AreEqual(serverTicks[i].position, mock1.serverStateBuffer.Get((uint) i).position);
            }
        }
        
        [Test]
        public void TestMultiResimulationPrevention()
        {
            PredictionManager tstManager = new PredictionManager();
            PredictionManager.PHYSICS_CONTROLLED = physicsController;
            tstManager.Setup(false, true);
            
            MockClientPredictedEntity mock1 = new MockClientPredictedEntity(false, 20, rigidbody, test, new []{component}, new[]{component});
            mock1.SetControlledLocally(true);
            mock1.SetSingleStateEligibilityCheckHandler(resimDecider.Check);
            mock1.decisionPassThrough = true;
            
            tstManager.AddPredictedEntity(1, mock1);
            tstManager.SetLocalEntity(1);
            tstManager.protectFromOversimulation = true;
            
            int predictionAcceptable = 0;
            PhysicsStateRecord psr = new PhysicsStateRecord();
            Vector3 serverPos = Vector3.zero;
            psr.rotation = Quaternion.identity;
            for (uint tickId = 5; tickId < 105; ++tickId)
            {
                component.inputVector = Vector3.right * 2;
                serverPos += Vector3.left * 2;
                psr.position = serverPos;
                tstManager.FixedUpdate();
                
                PhysicsStateRecord spsr = new PhysicsStateRecord();
                spsr.From(psr);
                spsr.tickId = tickId - 4;
                tstManager.OnServerStateReceived(1, psr);
            }
            Assert.AreEqual(0, predictionAcceptable);
            Assert.AreEqual(67, tstManager.totalSimulationSkips); //33% resimulations
            Assert.AreEqual(33, tstManager.totalResimulations);
        }
    }
}
#endif