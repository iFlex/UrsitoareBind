using Prediction.data;
using UnityEngine;

namespace Prediction
{
    public abstract class AbstractPredictedEntity : PredictableComponent
    {
        public Rigidbody rigidbody;
        protected GameObject detachedVisualsIdentity;
        public uint id { get; private set; }

        protected PredictableControllableComponent[] controllablePredictionContributors;
        protected PredictableComponent[] predictionContributors;

        protected int totalFloatInputs = 0;
        protected int totalBinaryInputs = 0;
        
        protected AbstractPredictedEntity(uint identifier, Rigidbody rb, GameObject visuals, PredictableControllableComponent[] controllablePredictionContributors, PredictableComponent[] predictionContributors)
        {
            id = identifier;
            rigidbody = rb;
            detachedVisualsIdentity = visuals;
            this.controllablePredictionContributors = controllablePredictionContributors;
            this.predictionContributors = predictionContributors;
            
            for (int i = 0; i < controllablePredictionContributors.Length; i++)
            {
                totalFloatInputs += controllablePredictionContributors[i].GetFloatInputCount();
                totalBinaryInputs += controllablePredictionContributors[i].GetBinaryInputCount();
            }
        }
        
        public void PopulatePhysicsStateRecord(uint tickId, PhysicsStateRecord stateData)
        {
            stateData.tickId = tickId;
            stateData.position = rigidbody.position;
            stateData.rotation = rigidbody.rotation;
            stateData.velocity = rigidbody.linearVelocity;
            stateData.angularVelocity = rigidbody.angularVelocity;
        }

        public bool ValidateState(float deltaTime, PredictionInputRecord input)
        {
            for (int i = 0; i < controllablePredictionContributors.Length; ++i)
            {
                if (!controllablePredictionContributors[i].ValidateInput(deltaTime, input))
                {
                    return false;
                }
            }
            return true;
        }
        
        public void LoadInput(PredictionInputRecord input)
        {
            for (int i = 0; i < controllablePredictionContributors.Length; ++i)
            {
                controllablePredictionContributors[i].LoadInput(input);
            }
        }

        public void ApplyForces()
        {
            for (int i = 0; i < predictionContributors.Length; ++i)
            {
                predictionContributors[i].ApplyForces();
            }
        }
        
        public override int GetHashCode()
        {
            return (int) id;
        }
    }
}