using Mirror;
using UnityEngine;

namespace Prediction.wrappers
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(PredictedEntityVisuals))]
    public class PredictedNetworkBehaviour : NetworkBehaviour, PredictedEntity
    {
        //FUDO: can we make components serializable?
        [SerializeField] private MonoBehaviour[] components;
        [SerializeField] private int bufferSize = 50;
        [SerializeField] private Rigidbody _rigidbody;
        //TODO: private set but serializable...
        public PredictedEntityVisuals visuals;// { get; private set; }
        
        public ClientPredictedEntity clientPredictedEntity { get; private set; }
        public ServerPredictedEntity serverPredictedEntity { get; private set; }
        public bool isReady { get; private set; }
        
        [SerializeField] private bool dbgIsLocallyControlled;
        [SerializeField] private int dbgGOID;
        [SerializeField] private uint dbgNetID;
        
        private void SetReady(bool ready)
        {
            isReady = ready;
        }
        
        void OnEnable()
        {
            if (isReady)
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
            if (isServerOnly)
            {
                SetReady(true);
            }
        }
    
        public override void OnStartClient()
        {
            if (isServer)
            {
                ConfigureAsServerClient(false);
            }
            else
            {
                ConfigureAsClient(false);
            }
            SetReady(true);
        }

        public override void OnStartAuthority()
        {
            SetControlledLocally(true);
        }

        void ConfigureAsServer()
        {
            serverPredictedEntity = new ServerPredictedEntity(netId, bufferSize, _rigidbody, visuals.gameObject, WrapperHelpers.GetControllableComponents(components), WrapperHelpers.GetComponents(components));
            ((PredictedEntity)this).Register();
        }

        void ConfigureAsClient(bool controlledLocally)
        {
            clientPredictedEntity = new ClientPredictedEntity(netId, false, bufferSize, _rigidbody, visuals.gameObject, WrapperHelpers.GetControllableComponents(components), WrapperHelpers.GetComponents(components));
            visuals.SetClientPredictedEntity(clientPredictedEntity, PredictionManager.INTERPOLATION_PROVIDER());
            ((PredictedEntity)this).Register();
            SetControlledLocally(controlledLocally);
        }

        void ConfigureAsServerClient(bool controlledLocally)
        {
            clientPredictedEntity = new ClientPredictedEntity(netId, true, bufferSize, _rigidbody, visuals.gameObject, WrapperHelpers.GetControllableComponents(components), WrapperHelpers.GetComponents(components));
            ((PredictedEntity)this).Register();
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

        public Rigidbody GetRigidbody()
        {
            return _rigidbody;
        }

        public PredictedEntityVisuals GetVisualsControlled()
        {
            return visuals;
        }

        public void SetControlledLocally(bool controlledLocally)
        {
            Debug.Log($"[PredictedNetworkBehaviour][SetControlledLocally]({netId}) goID:{gameObject.GetInstanceID()} local:{controlledLocally}");
            ((PredictedEntity)this).RegisterControlledLocally(controlledLocally);
            visuals.Reset();
            clientPredictedEntity?.SetControlledLocally(controlledLocally);
            visuals.SetControlledLocally(controlledLocally);
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

        private void OnCollisionEnter(Collision other)
        {
            if (IsClient() && !IsServer())
            {
                if (PredictionManager.Instance.IsPredicted(other.rigidbody))
                {
                    Debug.Log($"[PredictedNetworkBehaviour][OnCollisionEnter] this:{gameObject}({gameObject.GetInstanceID()}) other:{other}:({other.rigidbody})::{other.rigidbody.gameObject.GetInstanceID()}");
                    //GetClientEntity().MarkInteractionWithLocalAuthority();
                }
            }
        }
        //TODO: on collision stay?

        void Update()
        {
            dbgGOID = gameObject.GetInstanceID();
            dbgNetID = netId;
            dbgIsLocallyControlled = IsControlledLocally();
        }
    }
}