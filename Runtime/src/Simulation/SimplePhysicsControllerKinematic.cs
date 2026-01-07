using System.Collections.Generic;
using Prediction.data;
using UnityEngine;

namespace Prediction.Simulation
{
    public class SimplePhysicsControllerKinematic : PhysicsController
    {
        private Dictionary<Rigidbody, PhysicsStateRecord> trackedBodies = new();

        void SaveStates()
        {
            foreach (KeyValuePair<Rigidbody, PhysicsStateRecord> pair in trackedBodies)
            {
                pair.Value.From(pair.Key);
                pair.Key.isKinematic = true;
            }
        }

        void LoadStates(Rigidbody ignore)
        {
            foreach (KeyValuePair<Rigidbody, PhysicsStateRecord> pair in trackedBodies)
            {
                if (pair.Key == ignore)
                {
                    continue;
                }
                
                pair.Key.isKinematic = false;
                pair.Key.position = pair.Value.position;
                pair.Key.rotation = pair.Value.rotation;
                pair.Key.linearVelocity = pair.Value.velocity;
                pair.Key.angularVelocity = pair.Value.angularVelocity;
            }
        }
        
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
            SaveStates();
            entity.rigidbody.isKinematic = false;
        }

        public bool Rewind(uint ticks)
        {
            return true;
        }

        public void Resimulate(ClientPredictedEntity entity)
        {
            Physics.Simulate(Time.fixedDeltaTime);
        }

        public void AfterResimulate(ClientPredictedEntity entity)
        {
            LoadStates(entity.rigidbody);
        }

        public void Track(Rigidbody rigidbody)
        {
            PhysicsStateRecord record = new PhysicsStateRecord();
            record.From(rigidbody);
            trackedBodies[rigidbody] = record;
        }

        public void Untrack(Rigidbody rigidbody)
        {
            trackedBodies.Remove(rigidbody);
        }
    }
}