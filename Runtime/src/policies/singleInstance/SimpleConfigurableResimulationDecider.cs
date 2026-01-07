using System.Runtime.CompilerServices;
using Prediction.data;
using UnityEngine;

namespace Prediction.policies.singleInstance
{
    public class SimpleConfigurableResimulationDecider : SingleSnapshotInstanceResimChecker
    {
        public static bool LOG_RESIMULATIONS = false;
        public static bool LOG_ALL_CHECKS    = false;
        
        public float _avgDistD = 0;
        public float _avgRotD = 0;
        public float _avgVeloD = 0;
        public float _avgAVeloD = 0;
        public int _checkCount = 0;
        
        public float _MaxDistD = 0;
        public float _MaxRotD = 0;
        public float _MaxVeloD = 0;
        public float _MaxAVeloD = 0;
        
        public float distResimThreshold;
        public float rotationResimThreshold;
        public float veloResimThreshold;
        public float angVeloResimThreshold;
        
        public SimpleConfigurableResimulationDecider()
        {
            distResimThreshold = 0.0001f;
            rotationResimThreshold = 0.0001f;
            veloResimThreshold = 0.001f;
            angVeloResimThreshold = 0.001f;
        }

        public SimpleConfigurableResimulationDecider(float distResimThreshold, float rotResimThreshold, float veloResimThreshold, float angVeloResimThreshold)
        {
            this.distResimThreshold = distResimThreshold;
            this.rotationResimThreshold = rotResimThreshold;
            this.veloResimThreshold = veloResimThreshold;
            this.angVeloResimThreshold = angVeloResimThreshold;
        }

        public virtual PredictionDecision Check(uint entityId, uint tickId, PhysicsStateRecord local, PhysicsStateRecord server)
        {
            float distD = (local.position - server.position).magnitude;
            float angD = Quaternion.Angle(local.rotation, server.rotation);
            float vdelta = (local.velocity - server.velocity).magnitude;
            float avdelta = (local.angularVelocity - server.angularVelocity).magnitude;
            
            _avgDistD += distD;
            _avgRotD += angD;
            _avgVeloD += vdelta;
            _avgAVeloD += avdelta;
            _checkCount++;
            
            if (_MaxDistD < distD)
            {
                _MaxDistD = distD;
            }
            if (_MaxRotD < angD)
            {
                _MaxRotD = angD;
            }
            if (_MaxVeloD < vdelta)
            {
                _MaxVeloD = vdelta;
            }
            if (_MaxAVeloD < avdelta)
            {
                _MaxAVeloD = avdelta;
            }
            
            if (distResimThreshold > 0)
            {
                if (distD > distResimThreshold)
                {
                    if (LOG_RESIMULATIONS)
                    {
                        Log(entityId, tickId, distD, angD, vdelta, avdelta, true);
                    }
                    return PredictionDecision.RESIMULATE;
                }
            }

            if (rotationResimThreshold > 0)
            {
                if (angD > rotationResimThreshold)
                {
                    if (LOG_RESIMULATIONS)
                    {
                        Log(entityId, tickId, distD, angD, vdelta, avdelta, true);
                    }
                    return PredictionDecision.RESIMULATE;
                }
            }
            
            if (veloResimThreshold > 0)
            {
                if (vdelta > veloResimThreshold)
                {
                    if (LOG_RESIMULATIONS)
                    {
                        Log(entityId, tickId, distD, angD, vdelta, avdelta, true);
                    }
                    return PredictionDecision.RESIMULATE;
                }
            }
            
            if (angVeloResimThreshold > 0)
            {
                if (avdelta > angVeloResimThreshold)
                {
                    if (LOG_RESIMULATIONS)
                    {
                        Log(entityId, tickId, distD, angD, vdelta, avdelta, true);
                    }
                    return PredictionDecision.RESIMULATE;
                }
            }

            if (LOG_ALL_CHECKS)
            {
                Log(entityId, tickId, distD, angD, vdelta, avdelta, false);
            }
            return PredictionDecision.NOOP;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Log(uint entityId, uint tickId, float distD, float angD, float vdelta, float avdelta, bool isResim)
        {
            if (isResim)
            {
                Debug.Log($"[CHECK][RESIMULATION] i:{entityId}|t:{tickId}|D:{distD.ToString("F10")}|R:{angD.ToString("F10")}|V:{vdelta.ToString("F10")}|AV:{avdelta.ToString("F10")}|");
            }
            else
            {
                Debug.Log($"[CHECK]______________ i:{entityId}|t:{tickId}|D:{distD.ToString("F10")}|R:{angD.ToString("F10")}|V:{vdelta.ToString("F10")}|AV:{avdelta.ToString("F10")}|");
            }
        }
    }
}