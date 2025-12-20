using Prediction.data;
using UnityEngine;

namespace Prediction.policies.singleInstance
{
    public class SimpleConfigurableResimulationDecider : SingleSnapshotInstanceResimChecker
    {
        public float distResimThreshold;
        private float maxResimulationDelta;
        private float maxAngleDelta;
        private float maxVeloAngleDelta;
        private float maxAngularVeloMagDelta;

        public SimpleConfigurableResimulationDecider()
        {
            //distResimThreshold = 1f;
            //distResimThreshold = 0.1f;
            //distResimThreshold = 0.01f;
            distResimThreshold = 0.001f;  //This was optimum at some point?
            //distResimThreshold = 0.0001f; //lots of resims
            maxResimulationDelta = 1f;
            
            maxAngleDelta = 1f;
            maxVeloAngleDelta = 0;
            maxAngularVeloMagDelta = 0;
        }

        public SimpleConfigurableResimulationDecider(float dist)
        {
            distResimThreshold = dist;
            maxResimulationDelta = 1f;
            maxAngleDelta = 1f;
            maxVeloAngleDelta = 0;
            maxAngularVeloMagDelta = 0;
        }
        
        public SimpleConfigurableResimulationDecider(float distResimThreshold, float maxResimulationDelta, float maxAngleDelta, float maxVeloAngleDelta, float maxAngularVeloMagDelta)
        {
            this.distResimThreshold = distResimThreshold;
            this.maxResimulationDelta = maxResimulationDelta;
            
            this.maxAngleDelta = maxAngleDelta;
            this.maxVeloAngleDelta = maxVeloAngleDelta;
            this.maxAngularVeloMagDelta = maxAngularVeloMagDelta;
        }

        public virtual PredictionDecision Check(PhysicsStateRecord local, PhysicsStateRecord server)
        {
            if (distResimThreshold > 0)
            {
                float dist = (local.position - server.position).magnitude;
                if (dist > maxResimulationDelta)
                {
                    //return PredictionDecision.SNAP;
                }
                if (dist > distResimThreshold)
                {
                    return PredictionDecision.RESIMULATE;
                }
            }

            if (maxAngleDelta > 0)
            {
                if (Quaternion.Angle(local.rotation, server.rotation) > maxAngleDelta)
                {
                    return PredictionDecision.RESIMULATE;
                }
            }
            
            if (maxVeloAngleDelta > 0)
            {
                if (Vector3.Angle(local.velocity, server.velocity) > maxVeloAngleDelta)
                {
                    return PredictionDecision.RESIMULATE;
                }
            }
            
            if (maxAngularVeloMagDelta > 0)
            {
                if ((local.velocity.magnitude - server.velocity.magnitude) > maxAngularVeloMagDelta)
                {
                    return PredictionDecision.RESIMULATE;
                }
            }
            
            return PredictionDecision.NOOP;
        }
    }
}