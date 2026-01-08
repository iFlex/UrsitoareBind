using Prediction.data;
using Prediction.Interpolation;
using UnityEngine;

namespace Prediction
{
    public class PredictedEntityVisuals : MonoBehaviour
    {
        //TODO: larger smooth window for followers!
        
        public static bool SHOW_DBG = false;
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

        //NOTE: never call this on the server
        public void SetClientPredictedEntity(ClientPredictedEntity clientPredictedEntity, VisualsInterpolationsProvider provider)
        {
            interpolationProvider = provider;
            this.clientPredictedEntity = clientPredictedEntity;
            clientPredictedEntity.onReset.AddEventListener(OnShouldReset);
            //TODO: what? why artifficial delay?
            currentTimeStep -= artifficialDelay;
            
            visualsDetached = true;
            visualsEntity.transform.SetParent(null);
            
            interpolationProvider.SetInterpolationTarget(visualsEntity.transform);
            if (debug)
            {
                serverGhost = Instantiate(serverGhostPrefab, Vector3.zero, Quaternion.identity);
                clientGhost = Instantiate(clientGhostPrefab, Vector3.zero, Quaternion.identity, clientPredictedEntity.gameObject.transform);
                clientGhost.transform.localPosition = Vector3.zero;
                clientGhost.transform.localRotation = Quaternion.identity;
            }
            
            clientPredictedEntity.newStateReached.AddEventListener(AggregateState);
            SetControlledLocally(false);
        }

        void AggregateState(PhysicsStateRecord state)
        {
            //Debug.Log($"[PredictedEntityVisuals]({GetInstanceID()}) state: {state}");
            interpolationProvider.Add(state);
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

            if (debug)
            {
                rec = clientPredictedEntity.serverStateBuffer.GetEnd();
                if (rec != null && serverGhost)
                {
                    serverGhost.transform.position = rec.position;
                    serverGhost.transform.rotation = rec.rotation;   
                }
            }
            interpolationProvider.Update(Time.deltaTime, PredictionManager.Instance.tickId);
        }

        void OnShouldReset(bool ign)
        {
            Reset();
        }
        
        public void Reset()
        {
            interpolationProvider?.Reset();
        }

        public void SetControlledLocally(bool ctlLoc)
        {
            interpolationProvider?.SetControlledLocally(ctlLoc);
        }
    }
}