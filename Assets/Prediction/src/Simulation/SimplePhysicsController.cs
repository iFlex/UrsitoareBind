using UnityEngine;

namespace Prediction.Simulation
{
    public class SimplePhysicsController : PhysicsController
    {
        public void Setup(bool isServer)
        {
            Physics.simulationMode = SimulationMode.Script;
        }

        public void Simulate()
        {
            Physics.Simulate(Time.fixedDeltaTime);
        }

        public void BeforeResimulate(ClientPredictedEntity entity)
        {
            //NOOP
        }

        public void Rewind(uint ticks)
        {
        }

        public void Resimulate(ClientPredictedEntity entity)
        {
            Physics.Simulate(Time.fixedDeltaTime);
        }

        public void AfterResimulate(ClientPredictedEntity entity)
        {
            //NOOP
        }

        public void Track(Rigidbody rigidbody)
        {
        }

        public void Untrack(Rigidbody rigidbody)
        {
        }
    }
}