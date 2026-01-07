using Prediction.data;

namespace Prediction.policies.singleInstance
{
    public interface SingleSnapshotInstanceResimChecker
    {
        PredictionDecision Check(uint entityId, uint tickId, PhysicsStateRecord local, PhysicsStateRecord server);
    }
}