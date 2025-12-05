using UnityEngine;

namespace Prediction.wrappers
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(PredictedEntityVisuals))]
    public class PredictedNetworkBehaviour : MonoBehaviour
    {
        [SerializeField] private int bufferSize;
        [SerializeField] private Rigidbody _rigidbody;
        [SerializeField] private PredictedEntityVisuals visuals;
        
        public ClientPredictedEntity clientPredictedEntity { get; private set; }
        public ServerPredictedEntity serverPredictedEntity { get; private set; }
        
        void Awake()
        {
            //TODO
            //TODO: use common methods instead of duplicating the code here...
        }
        
        public void Reset()
        {
            clientPredictedEntity?.Reset();
            serverPredictedEntity?.Reset();
            visuals.Reset();
        }
    }
}