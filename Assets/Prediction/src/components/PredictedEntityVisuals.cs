using Prediction.data;
using Prediction.Interpolation;
using UnityEngine;

namespace Prediction
{
    public class PredictedEntityVisuals : MonoBehaviour
    {
        public static bool SHOW_DBG = true;
        [SerializeField] public GameObject visualsEntity;
        [SerializeField] private bool debug = false;
        [SerializeField] private GameObject serverGhostPrefab;
        [SerializeField] private GameObject clientGhostPrefab;
        
        public VisualsInterpolationsProvider interpolationProvider { get; private set; }
        private ClientPredictedEntity clientPredictedEntity;
        
        private GameObject serverGhost;
        private GameObject clientGhost;
        public bool hasVIP = false;
        
        public double currentTimeStep = 0;
        public double targetTime = 0;
        public double artifficialDelay = 1f;
        private bool visualsDetached = false;

        public void SetInterpolationProvider(VisualsInterpolationsProvider provider)
        {
            interpolationProvider = provider;
            hasVIP = interpolationProvider != null;
        }
        
        public void SetClientPredictedEntity(ClientPredictedEntity clientPredictedEntity, bool detachVisuals)
        {
            this.clientPredictedEntity = clientPredictedEntity;
            currentTimeStep -= artifficialDelay;
            
            //TODO: listen for destruction events
            if (detachVisuals)
            {
                visualsDetached = true;
                visualsEntity.transform.SetParent(null);
            }
            
            interpolationProvider?.SetInterpolationTarget(visualsEntity.transform);
            if (debug)
            {
                serverGhost = Instantiate(serverGhostPrefab, Vector3.zero, Quaternion.identity);
                clientGhost = Instantiate(clientGhostPrefab, Vector3.zero, Quaternion.identity, clientPredictedEntity.gameObject.transform);
            }
            
            clientPredictedEntity.newInterpolationStateReached.AddEventListener(AggregateState);
        }

        void AggregateState(PhysicsStateRecord state)
        {
            interpolationProvider?.Add(state);
        }

        private PhysicsStateRecord rec;
        void Update()
        {
            //TODO: make this more efficient
            if (serverGhost)
                serverGhost.SetActive(SHOW_DBG);
            if (clientGhost)
                clientGhost.SetActive(SHOW_DBG);
            if (!visualsDetached)
                return;

            rec = clientPredictedEntity.serverStateBuffer.GetEnd();
            if (debug)
            {
                if (rec != null && serverGhost)
                {
                    serverGhost.transform.position = rec.position;
                    serverGhost.transform.rotation = rec.rotation;   
                }
            }
            
            if (clientPredictedEntity.isControlledLocally)
            {
                interpolationProvider.Update(Time.deltaTime);
            }
            else
            {
                transform.position = rec.position;
                transform.rotation = rec.rotation;
            }
        }

        public void Reset()
        {
            interpolationProvider?.Reset();
        }
    }
}