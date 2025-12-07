namespace Prediction.wrappers
{
    public interface PredictedEntity
    {
        uint GetId();
        int GetOwnerId();
        ClientPredictedEntity GetClientEntity();
        ServerPredictedEntity GetServerEntity();
        void SetControlledLocally(bool controlledLocally);
        bool IsControlledLocally();
        bool IsServer();
        bool IsClient();
        void RegisterControlledLocally()
        {
            if (IsClient())
            {
                PredictionManager.Instance.SetLocalEntity(GetId(), GetClientEntity());    
            }
        }
        
        void Register()
        {
            if (IsClient())
            {
                PredictionManager.Instance.AddPredictedEntity(GetId(), GetClientEntity());
            }
            if (IsServer())
            {
                PredictionManager.Instance.AddPredictedEntity(GetId(), GetServerEntity());
                PredictionManager.Instance.SetEntityOwner(GetServerEntity(), GetOwnerId());
            }
        }

        void Deregister()
        {
            PredictionManager.Instance.RemovePredictedEntity(GetId());
        }
    }
}