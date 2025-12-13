#if (UNITY_EDITOR) 
using Prediction.Simulation;
using UnityEngine;

namespace Prediction.Tests.mocks
{
    public class MockPhysicsController : PhysicsController
    {
        public MockPhysicsController()
        {
            Physics.simulationMode = SimulationMode.Script;
        }
        
        public void Setup(bool isServer)
        {
        }

        public void Simulate()
        {
        }

        public void BeforeResimulate(ClientPredictedEntity entity)
        {
        }

        public void Rewind(uint ticks)
        {
        }

        public void Resimulate(ClientPredictedEntity entity)
        {
        }

        public void AfterResimulate(ClientPredictedEntity entity)
        {
        }

        public void Track(Rigidbody rigidbody)
        {
        }

        public void Untrack(Rigidbody rigidbody)
        {
        }
    }
}
#endif