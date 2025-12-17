#if (UNITY_EDITOR) 
using NUnit.Framework;
using Prediction.data;
using Prediction.policies.singleInstance;
using Prediction.Tests.mocks;
using UnityEngine;
using Assert = UnityEngine.Assertions.Assert;

namespace Prediction.Tests
{
    //TODO: interpo test should be a PredictionManager test.
    public class ClientServerPredictionInteropTest
    {
        public static GameObject client;
        public static Rigidbody clientRigidbody;
        public static MockPredictableControllableComponent clientComponent;
        public static ClientPredictedEntity clientEntity;
        
        public static GameObject server;
        public static Rigidbody serverRigidbody;
        public static MockPredictableControllableComponent serverComponent;
        public static ServerPredictedEntity serverEntity;
        
        public static MockPhysicsController physicsController;
        public static SimpleConfigurableResimulationDecider resimDecider = new SimpleConfigurableResimulationDecider();
        
        //TODO: these tests may not be relevant
        [SetUp]
        public void SetUp()
        {
            physicsController = new MockPhysicsController();
            
            client = new GameObject("test");
            client.transform.position = Vector3.zero;
            clientRigidbody = client.AddComponent<Rigidbody>();
            
            clientComponent = new MockPredictableControllableComponent();
            clientComponent.rigidbody = clientRigidbody;
            
            clientEntity = new ClientPredictedEntity(0, false,20, clientRigidbody, client, new []{clientComponent}, new[]{clientComponent});
            clientEntity.SetSingleStateEligibilityCheckHandler(resimDecider.Check);
            clientEntity.SetControlledLocally(true);
            
            server = new GameObject("test");
            server.transform.position = Vector3.zero;
            serverRigidbody = server.AddComponent<Rigidbody>();
            
            serverComponent = new MockPredictableControllableComponent();
            serverComponent.rigidbody = serverRigidbody;
            
            serverEntity = new ServerPredictedEntity(0 ,20, serverRigidbody, server, new []{serverComponent}, new[]{serverComponent});
            serverEntity.useBuffering = false;
        }
        
        [Test]
        public void TestHappyPath()
        {
            var inputs = new [] { Vector3.zero, Vector3.right * 5, Vector3.up * 5, Vector3.right * 5, Vector3.up * 5, Vector3.right * 5, Vector3.up * 5, Vector3.right * 5, Vector3.up * 5 };
            for (uint tickId = 1; tickId < inputs.Length; ++tickId)
            {
                clientComponent.inputVector = inputs[tickId];
                PredictionInputRecord record = clientEntity.ClientSimulationTick(tickId);
                clientEntity.SamplePhysicsState(tickId);
                serverEntity.BufferClientTick(tickId, record);
                serverEntity.ServerSimulationTick();
                PhysicsStateRecord serverRecord = serverEntity.SamplePhysicsState();
                clientEntity.BufferServerTick(tickId, serverRecord);
                Assert.AreEqual(serverRecord.position, clientRigidbody.position);
                Assert.AreEqual(serverRigidbody.position, clientRigidbody.position);
            }
        }
        
        [Test]
        public void TestHappyPathWithDelay()
        {
            var inputs = new [] { Vector3.zero, Vector3.right * 5, Vector3.up * 5, Vector3.right * 5, Vector3.up * 5, Vector3.right * 5, Vector3.up * 5, Vector3.right * 5, Vector3.up * 5 };
            
            int resimulationsCounter = 0;
            clientEntity.resimulation.AddEventListener((started) =>
            {
                if (started)
                    resimulationsCounter++;
            });
            //TODO: fix
            uint delay = 3;
            for (uint tickId = 1; tickId < delay; ++tickId)
            {
                clientComponent.inputVector = inputs[tickId];
                PredictionInputRecord record = clientEntity.ClientSimulationTick(tickId);
                clientEntity.SamplePhysicsState(tickId);
                serverEntity.BufferClientTick(tickId, record);
            }
            for (uint tickId = delay; tickId < inputs.Length; ++tickId)
            {
                clientComponent.inputVector = inputs[tickId];
                PredictionInputRecord record = clientEntity.ClientSimulationTick(tickId);
                clientEntity.SamplePhysicsState(tickId);
                serverEntity.BufferClientTick(tickId, record);
                serverEntity.ServerSimulationTick();
                PhysicsStateRecord serverRecord = serverEntity.SamplePhysicsState();
                clientEntity.BufferServerTick(tickId, serverRecord);
            }
            clientComponent.rigidbody = null;
            for (uint tickId = (uint) inputs.Length; tickId < inputs.Length + delay; tickId++)
            {
                clientComponent.inputVector = inputs[tickId % inputs.Length];
                PredictionInputRecord record = clientEntity.ClientSimulationTick(tickId);
                clientEntity.SamplePhysicsState(tickId);
                serverEntity.BufferClientTick(tickId, record);
                serverEntity.ServerSimulationTick();
                PhysicsStateRecord serverRecord = serverEntity.SamplePhysicsState();
                clientEntity.BufferServerTick(tickId, serverRecord);
            }

            Assert.AreEqual(0, resimulationsCounter);
            Assert.AreEqual(serverRigidbody.position, clientRigidbody.position);
        }
    }
}
#endif