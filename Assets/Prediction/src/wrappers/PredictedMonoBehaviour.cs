using Prediction.Interpolation;
using UnityEngine;

namespace Prediction.wrappers
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(PredictedEntityVisuals))]
    public class PredictedMonoBehaviour : MonoBehaviour
    {
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

        void ConfigureAsServer()
        {
            //TODO: detect or wire components
            serverPredictedEntity = new ServerPredictedEntity(bufferSize, _rigidbody, visuals.gameObject, new PredictableControllableComponent[0], new PredictableComponent[0]);
        }

        void ConfigureAsClient(bool controlledLocally)
        {
            //TODO: detect or wire components
            clientPredictedEntity = new ClientPredictedEntity(30, _rigidbody, visuals.gameObject, new PredictableControllableComponent[0]{}, new PredictableComponent[0]{});
            clientPredictedEntity.gameObject = gameObject;
            //TODO: configurable
            visuals.SetInterpolationProvider(new MovingAverageInterpolator());
            visuals.SetClientPredictedEntity(clientPredictedEntity, visuals);
        }

        public bool IsControlledLocally()
        {
            if (clientPredictedEntity == null)
                return true;
            return clientPredictedEntity.isControlledLocally;
        }
        
        public void SetControlledLocally(bool controlledLocally)
        {
            visuals.Reset();
            clientPredictedEntity?.SetControlledLocally(controlledLocally);
        }
        
        public void Reset()
        {
            visuals.Reset();
            clientPredictedEntity?.Reset();
            serverPredictedEntity?.Reset();
        }
    }
}