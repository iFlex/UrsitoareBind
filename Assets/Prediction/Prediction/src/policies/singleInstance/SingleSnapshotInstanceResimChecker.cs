using Prediction.data;

namespace Prediction.policies.singleInstance
{
    public interface SingleSnapshotInstanceResimChecker
    {
        PredictionDecision Check(uint tickId, uint entityId, PhysicsStateRecord local, PhysicsStateRecord server);
    }
}