using Prediction.Interpolation;
using UnityEngine;

namespace Prediction.wrappers
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(PredictedEntityVisuals))]
    public class PredictedMonoBehaviour : MonoBehaviour, PredictedEntity
    {
        //FUDO: can we make components serializable?
        [SerializeField] private MonoBehaviour[] components;
        [SerializeField] private int bufferSize;
        [SerializeField] private Rigidbody _rigidbody;
        [SerializeField] private PredictedEntityVisuals visuals;

        public ClientPredictedEntity clientPredictedEntity { get; private set; }
        public ServerPredictedEntity serverPredictedEntity { get; private set; }
        
        //TODO - wrap the prediction entities and configure
        void Awake()
        {
            //TODO: check visuals must be a child of this game object
        }

        void OnEnable()
        {
            ((PredictedEntity)this).Register();
        }

        void OnDisable()
        {
            ((PredictedEntity)this).Deregister();
        }

        void ConfigureAsServer()
        {
            serverPredictedEntity = new ServerPredictedEntity((uint) GetInstanceID(), bufferSize, _rigidbody, visuals.gameObject, WrapperHelpers.GetControllableComponents(components), WrapperHelpers.GetComponents(components));
        }

        void ConfigureAsClient(bool controlledLocally)
        {
            //TODO: detect or wire components
            clientPredictedEntity = new ClientPredictedEntity((uint) GetInstanceID(),false, 30, _rigidbody, visuals.gameObject, WrapperHelpers.GetControllableComponents(components), WrapperHelpers.GetComponents(components));
            clientPredictedEntity.gameObject = gameObject;
            //TODO: configurable interpolator
            visuals.SetClientPredictedEntity(clientPredictedEntity, new MovingAverageInterpolator());
        }

        public bool IsControlledLocally()
        {
            if (clientPredictedEntity == null)
                return true;
            return clientPredictedEntity.isControlledLocally;
        }

        public bool IsServer()
        {
            return true;
        }

        public bool IsClient()
        {
            return true;
        }

        public Rigidbody GetRigidbody()
        {
            return _rigidbody;
        }

        public PredictedEntityVisuals GetVisualsControlled()
        {
            return visuals;
        }

        public void ResetClient()
        {
            visuals.Reset();
            clientPredictedEntity?.Reset();
        }
        
        public void Reset()
        {
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
            return (uint) GetInstanceID();
        }

        public virtual int GetOwnerId()
        {
            //TODO: needs a provider here
            return 0;
        }
    }
}