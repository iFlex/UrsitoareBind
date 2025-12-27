using Prediction.data;
using UnityEngine;

namespace Prediction.policies.singleInstance
{
    public class SimpleConfigurableResimulationDecider : SingleSnapshotInstanceResimChecker
    {
        public float distResimThreshold;
        public float rotationResimThreshold;
        public float veloResimThreshold;
        public float angVeloResimThreshold;
        
        public SimpleConfigurableResimulationDecider()
        {
            distResimThreshold = 0.0001f;
            rotationResimThreshold = 0.0001f;
            veloResimThreshold = 0.0001f;
            angVeloResimThreshold = 0.0001f;
        }

        public SimpleConfigurableResimulationDecider(float distResimThreshold, float rotResimThreshold, float veloResimThreshold, float angVeloResimThreshold)
        {
            this.distResimThreshold = distResimThreshold;
            this.rotationResimThreshold = rotResimThreshold;
            this.veloResimThreshold = veloResimThreshold;
            this.angVeloResimThreshold = angVeloResimThreshold;
        }

        public virtual PredictionDecision Check(PhysicsStateRecord local, PhysicsStateRecord server)
        {
            if (distResimThreshold > 0)
            {
                float dist = (local.position - server.position).magnitude;
                if (dist > distResimThreshold)
                {
                    return PredictionDecision.RESIMULATE;
                }
            }

            if (rotationResimThreshold > 0)
            {
                if (Quaternion.Angle(local.rotation, server.rotation) > rotationResimThreshold)
                {
                    return PredictionDecision.RESIMULATE;
                }
            }
            
            if (veloResimThreshold > 0)
            {
                float vdelta = (local.velocity - server.velocity).magnitude;
                if (vdelta > veloResimThreshold)
                {
                    return PredictionDecision.RESIMULATE;
                }
            }
            
            if (angVeloResimThreshold > 0)
            {
                float avdelta = (local.angularVelocity - server.angularVelocity).magnitude;
                if (avdelta > angVeloResimThreshold)
                {
                    return PredictionDecision.RESIMULATE;
                }
            }
            
            return PredictionDecision.NOOP;
        }
    }
}