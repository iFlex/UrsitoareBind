using Prediction;
using Prediction.data;
using Prediction.policies.singleInstance;
using UnityEngine;

namespace DefaultNamespace
{
    public class DebugResimChecker : SimpleConfigurableResimulationDecider
    {
        public static bool PRED_DEBUG = false;
        private float maxdist = 0;
        private float totalBreakingDist = 0;
        private int breakingDistCount = 0;
        private int directSnapCount = 0;
        
        public override PredictionDecision Check(uint entityId, uint tickId, PhysicsStateRecord l, PhysicsStateRecord s)
        {
            float dist = (l.position - s.position).magnitude;
            if (dist > maxdist)
            {
                maxdist = dist;
            }
            
            PredictionDecision outcome = base.Check(entityId, tickId, l, s);
            if (outcome == PredictionDecision.RESIMULATE)
            {
                totalBreakingDist += dist;
                breakingDistCount++;
            }
            if (outcome == PredictionDecision.SNAP)
            {
                directSnapCount++;
            }
            if (PRED_DEBUG)
                Debug.Log($"[PredictionMirrorBridge][DebugResimCheck]{((l.tickId != s.tickId) ? "ERR_WARNING" : "")} tick_local:{l.tickId} tick_server:{s.tickId} distance:{dist} avgBreakDist:{(breakingDistCount  > 0 ? totalBreakingDist / breakingDistCount : 0)} maxDist:{maxdist} breakCount:{breakingDistCount} directToSnap:{directSnapCount}");
            return outcome;
        }
    }
}