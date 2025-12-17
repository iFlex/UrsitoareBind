using System;
using UnityEngine;

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
        bool IsClientOnly()
        {
            return IsClient() && !IsServer();
        }

        Rigidbody GetRigidbody();
        
        void ApplyClientForce(Action<Rigidbody> applier)
        {
            if (IsClientOnly())
            {
                applier.Invoke(GetRigidbody());
                //GetClientEntity().MarkInteractionWithLocalAuthority();
            }
        }
        
        void RegisterControlledLocally(bool isControlled)
        {
            if (IsClient())
            {
                if (isControlled)
                {
                    PredictionManager.Instance.SetLocalEntity(GetId());    
                }
                else
                {
                    PredictionManager.Instance.UnsetLocalEntity(GetId());    
                }
            }
        }
        
        void Register()
        {
            if (IsClient())
            {
                PredictionManager.Instance.AddPredictedEntity(GetClientEntity());
            }
            if (IsServer())
            {
                PredictionManager.Instance.AddPredictedEntity(GetServerEntity());
                PredictionManager.Instance.SetEntityOwner(GetServerEntity(), GetOwnerId());
            }
        }

        void Deregister()
        {
            PredictionManager.Instance.RemovePredictedEntity(GetId());
        }
    }
}