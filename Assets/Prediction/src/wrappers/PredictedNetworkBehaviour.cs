using Mirror;
using Prediction.Interpolation;
using UnityEngine;

namespace Prediction.wrappers
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(PredictedEntityVisuals))]
    public class PredictedNetworkBehaviour : NetworkBehaviour, PredictedEntity
    {
        //FUDO: can we make components serializable?
        [SerializeField] private MonoBehaviour[] components;
        [SerializeField] private int bufferSize;
        [SerializeField] private Rigidbody _rigidbody;
        //TODO: private set but serializable...
        public PredictedEntityVisuals visuals;// { get; private set; }
        
        public ClientPredictedEntity clientPredictedEntity { get; private set; }
        public ServerPredictedEntity serverPredictedEntity { get; private set; }
        public bool isReady { get; private set; }
        
        [SerializeField] private bool initialConfig = false;

        private void SetReady(bool ready)
        {
            if (isReady != ready && ready)
            {
                initialConfig = true;
                ((PredictedEntity)this).Register();
            }

            isReady = ready;
        }
        void OnEnable()
        {
            if (initialConfig)
            {
                ((PredictedEntity)this).Register();
            }
        }

        void OnDisable()
        {
            ((PredictedEntity)this).Deregister();
        }
        
        public override void OnStartServer()
        {
            ConfigureAsServer();
            if (!isOwned)
            {
                SetReady(true);
            }
        }
    
        public override void OnStartClient()
        {
            if (isServer)
            {
                return;
            }
            
            ConfigureAsClient(isOwned);
            if (!isOwned)
            {
                SetReady(true);
            }
        }

        public override void OnStartAuthority()
        {
            if (isServer)
            {
                ConfigureAsClient(true);
            }
            SetControlledLocally(isOwned);
            SetReady(true);
        }

        //TODO: use common methods instead of duplicating the code here...
        void ConfigureAsServer()
        {
            serverPredictedEntity = new ServerPredictedEntity(bufferSize, _rigidbody, visuals.gameObject, WrapperHelpers.GetControllableComponents(components), WrapperHelpers.GetComponents(components));
        }

        void ConfigureAsClient(bool controlledLocally)
        {
            clientPredictedEntity = new ClientPredictedEntity(30, _rigidbody, visuals.gameObject, WrapperHelpers.GetControllableComponents(components), WrapperHelpers.GetComponents(components));
            //TODO: configurable interpolator
            visuals.SetClientPredictedEntity(clientPredictedEntity, new MovingAverageInterpolator());
            SetControlledLocally(controlledLocally);
        }
        
        public bool IsControlledLocally()
        {
            if (clientPredictedEntity == null)
                return true;
            return clientPredictedEntity.isControlledLocally;
        }

        public bool IsServer()
        {
            return isServer;
        }

        public bool IsClient()
        {
            return isClient;
        }

        public void SetControlledLocally(bool controlledLocally)
        {
            Debug.Log($"[PredictedNetworkBehaviour][SetControlledLocally]({netId}):{controlledLocally}");
            ((PredictedEntity)this).RegisterControlledLocally();
            visuals.Reset();
            clientPredictedEntity?.SetControlledLocally(controlledLocally);
        }

        public void ResetClient()
        {
            Debug.Log($"[PredictedNetworkBehaviour][ResetClient]({netId})");
            visuals.Reset();
            clientPredictedEntity?.Reset();
        }
        
        public void Reset()
        {
            Debug.Log($"[PredictedNetworkBehaviour][Reset]({netId})");
            ResetClient();
            serverPredictedEntity?.Reset();
        }
        
        public ClientPredictedEntity GetClientEntity()
        {
            return clientPredictedEntity;
        }

        public ServerPredictedEntity GetServerEntity()
        {
            return serverPredictedEntity;
        }
        
        public virtual uint GetId()
        {
            return netId;
        }

        public virtual int GetOwnerId()
        {
            return (netIdentity.connectionToClient == null) ? 0 : netIdentity.connectionToClient.connectionId;
        }
    }
}