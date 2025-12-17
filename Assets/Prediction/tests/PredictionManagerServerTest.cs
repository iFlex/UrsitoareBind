#if (UNITY_EDITOR)
using System.Collections.Generic;
using NUnit.Framework;
using Prediction.data;
using Prediction.policies.singleInstance;
using Prediction.Tests.mocks;
using UnityEngine;

namespace Prediction.Tests
{
    public class PredictionManagerServerTest
    {
        GameObject server1;
        Rigidbody serverRigidbody1;
        MockPredictableControllableComponent serverComponent1;
        ServerPredictedEntity serverEntity1;
        
        GameObject server2;
        Rigidbody serverRigidbody2;
        MockPredictableControllableComponent serverComponent2;
        ServerPredictedEntity serverEntity2;
        
        MockPhysicsController physicsController;
        SimpleConfigurableResimulationDecider resimDecider = new SimpleConfigurableResimulationDecider();
        PredictionManager manager;
        
        //TODO: these tests may not be relevant
        [SetUp]
        public void SetUp()
        {
            physicsController = new MockPhysicsController();
            
            server1 = new GameObject("test");
            server1.transform.position = Vector3.zero;
            serverRigidbody1 = server1.AddComponent<Rigidbody>();
            serverComponent1 = new MockPredictableControllableComponent();
            serverComponent1.rigidbody = serverRigidbody1;
            serverEntity1 = new ServerPredictedEntity(1, 20, serverRigidbody1, server1, new []{serverComponent1}, new[]{serverComponent1});
            serverEntity1.useBuffering = false;
            
            server2 = new GameObject("test2");
            server2.transform.position = Vector3.zero;
            serverRigidbody2 = server2.AddComponent<Rigidbody>();
            serverComponent2 = new MockPredictableControllableComponent();
            serverComponent2.rigidbody = serverRigidbody2;
            serverEntity2 = new ServerPredictedEntity(2, 20, serverRigidbody2, server2, new []{serverComponent2}, new[]{serverComponent2});
            serverEntity2.useBuffering = false;

            PredictionManager.PHYSICS_CONTROLLED = physicsController;
            
            manager = new PredictionManager();
            PredictionManager.PHYSICS_CONTROLLED = physicsController;
            manager.serverStateSender = (a, b, c) => { };
            manager.serverWorldStateSender = (a, b) => { };
            manager.connectionsIterator = () =>
            {
                return new int[] { 1, 2, 3 };
            };
            manager.Setup(true, false);
            manager.AddPredictedEntity(serverEntity1);
            manager.AddPredictedEntity(serverEntity2);
            manager.SetEntityOwner(serverEntity1, 1);
            manager.SetEntityOwner(serverEntity2, 2);
        }

        public class StateUpdate
        {
            public int connId;
            public uint entityId;
            public PhysicsStateRecord state;

            public static StateUpdate FindBy(int connID, uint entityID, List<StateUpdate> states)
            {
                foreach (StateUpdate state in states)
                {
                    if (state.connId == connID && state.entityId == entityID)
                    {
                        return state;
                    }
                }
                return null;
            }
        }
        
        [Test]
        public void CheckUpdates_WithClientInput()
        {
            List<StateUpdate> sentStates = new List<StateUpdate>();
            manager.serverStateSender = (connId, entityId, state) =>
            {
                StateUpdate su = new StateUpdate();
                su.connId = connId;
                su.entityId = entityId;
                su.state = new PhysicsStateRecord();
                su.state.From(state);
                sentStates.Add(su);
            };
            
            manager.FixedUpdate();
            //No input = no movement
            Assert.AreEqual(Vector3.zero, serverRigidbody1.position);
            Assert.AreEqual(Vector3.zero, serverRigidbody2.position);
            //2 entities, 3 connections
            Assert.AreEqual(6, sentStates.Count);
            
            StateUpdate su11 = StateUpdate.FindBy(1, 1, sentStates);
            StateUpdate su12 = StateUpdate.FindBy(1, 2, sentStates);
            StateUpdate su21 = StateUpdate.FindBy(2, 1, sentStates);
            StateUpdate su22 = StateUpdate.FindBy(2, 2, sentStates);
            StateUpdate su31 = StateUpdate.FindBy(3, 1, sentStates);
            StateUpdate su32 = StateUpdate.FindBy(3, 2, sentStates);
            
            Assert.AreEqual(0, su11.state.tickId);
            Assert.AreEqual(0, su12.state.tickId);
            Assert.AreEqual(0, su21.state.tickId);
            Assert.AreEqual(0, su22.state.tickId);
            Assert.AreEqual(1, su31.state.tickId);
            Assert.AreEqual(1, su32.state.tickId);
            sentStates.Clear();

            PredictionInputRecord pir1 = new PredictionInputRecord(3, 0);
            pir1.WriteReset();
            pir1.WriteNextScalar(1);
            pir1.WriteNextScalar(0);
            pir1.WriteNextScalar(0);
            
            PredictionInputRecord pir2 = new PredictionInputRecord(3, 0);
            pir2.WriteReset();
            pir2.WriteNextScalar(-1);
            pir2.WriteNextScalar(0);
            pir2.WriteNextScalar(0);
            
            manager.OnClientStateReceived(1, 10, pir1);
            manager.OnClientStateReceived(2, 15, pir2);
            manager.FixedUpdate();
            
            //No input = no movement
            Assert.AreEqual(-Vector3.left, serverRigidbody1.position);
            Assert.AreEqual(Vector3.left, serverRigidbody2.position);
            Assert.AreEqual(6, sentStates.Count);
            
            su11 = StateUpdate.FindBy(1, 1, sentStates);
            su12 = StateUpdate.FindBy(1, 2, sentStates);
            su21 = StateUpdate.FindBy(2, 1, sentStates);
            su22 = StateUpdate.FindBy(2, 2, sentStates);
            su31 = StateUpdate.FindBy(3, 1, sentStates);
            su32 = StateUpdate.FindBy(3, 2, sentStates);
            
            Assert.AreEqual(10, su11.state.tickId);
            Assert.AreEqual(10, su12.state.tickId);
            Assert.AreEqual(15, su21.state.tickId);
            Assert.AreEqual(15, su22.state.tickId);
            Assert.AreEqual(2, su31.state.tickId);
            Assert.AreEqual(2, su32.state.tickId);
            sentStates.Clear();
            manager.FixedUpdate();
            
            su11 = StateUpdate.FindBy(1, 1, sentStates);
            su12 = StateUpdate.FindBy(1, 2, sentStates);
            su21 = StateUpdate.FindBy(2, 1, sentStates);
            su22 = StateUpdate.FindBy(2, 2, sentStates);
            su31 = StateUpdate.FindBy(3, 1, sentStates);
            su32 = StateUpdate.FindBy(3, 2, sentStates);
            
            Assert.AreEqual(10, su11.state.tickId);
            Assert.AreEqual(10, su12.state.tickId);
            Assert.AreEqual(15, su21.state.tickId);
            Assert.AreEqual(15, su22.state.tickId);
            Assert.AreEqual(3, su31.state.tickId);
            Assert.AreEqual(3, su32.state.tickId);
            sentStates.Clear();
            
            manager.OnClientStateReceived(1, 12, pir1);
            manager.OnClientStateReceived(2, 16, pir2);
            manager.FixedUpdate();
            
            su11 = StateUpdate.FindBy(1, 1, sentStates);
            su12 = StateUpdate.FindBy(1, 2, sentStates);
            su21 = StateUpdate.FindBy(2, 1, sentStates);
            su22 = StateUpdate.FindBy(2, 2, sentStates);
            su31 = StateUpdate.FindBy(3, 1, sentStates);
            su32 = StateUpdate.FindBy(3, 2, sentStates);
            
            Assert.AreEqual(12, su11.state.tickId);
            Assert.AreEqual(12, su12.state.tickId);
            Assert.AreEqual(16, su21.state.tickId);
            Assert.AreEqual(16, su22.state.tickId);
            Assert.AreEqual(4, su31.state.tickId);
            Assert.AreEqual(4, su32.state.tickId);
        }
        
        // More tests about this...
    }
}
#endif