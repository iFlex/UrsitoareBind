using UnityEngine;

namespace Prediction.Simulation
{
    public interface PhysicsController
    {
        void Setup(bool isServer);
        void Simulate();
        void BeforeResimulate(ClientPredictedEntity entity);
        bool Rewind(uint ticks);
        void Resimulate(ClientPredictedEntity entity);
        void AfterResimulate(ClientPredictedEntity entity);
        void Track(Rigidbody rigidbody);
        void Untrack(Rigidbody rigidbody);
    }
}