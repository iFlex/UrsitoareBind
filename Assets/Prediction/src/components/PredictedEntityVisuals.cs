using Prediction.data;
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
        
        private ClientPredictedEntity clientPredictedEntity;
        [SerializeField] private GameObject follow;
        
        private GameObject serverGhost;
        private GameObject clientGhost;
        public bool hasVIP = false;
        
        public double currentTimeStep = 0;
        public double targetTime = 0;
        public double artifficialDelay = 1f;
        private bool visualsDetached = false;
        
        public void SetClientPredictedEntity(ClientPredictedEntity clientPredictedEntity, bool detachVisuals)
        {
            this.clientPredictedEntity = clientPredictedEntity;
            follow = clientPredictedEntity.gameObject;
            currentTimeStep -= artifficialDelay;
            
            //TODO: listen for destruction events
            if (detachVisuals)
            {
                visualsDetached = true;
                visualsEntity.transform.SetParent(null);
            }
            
            clientPredictedEntity.interpolationsProvider?.SetInterpolationTarget(visualsEntity.transform);
            hasVIP = clientPredictedEntity.interpolationsProvider != null;
            if (debug)
            {
                serverGhost = Instantiate(serverGhostPrefab, Vector3.zero, Quaternion.identity);
                clientGhost = Instantiate(clientGhostPrefab, Vector3.zero, Quaternion.identity, follow.transform);
            }
            
            clientPredictedEntity.newStateReached.AddEventListener(OnNewStateReached);
        }

        void OnNewStateReached(bool ign)
        {
            targetTime += Time.fixedDeltaTime;
        }

        //TODO: configurable
        private float defaultLerpFactor = 20f;
        void Update()
        {
            if (debug)
            {
                PhysicsStateRecord rec = clientPredictedEntity?.serverStateBuffer.GetEnd();
                if (rec != null && serverGhost)
                {
                    serverGhost.transform.position = rec.position;
                    serverGhost.transform.rotation = rec.rotation;   
                }
                if (serverGhost)
                    serverGhost.SetActive(SHOW_DBG);
                if (clientGhost)
                    clientGhost.SetActive(SHOW_DBG);
            }
            
            if (!follow || !visualsDetached)
                return;
            
            clientPredictedEntity.interpolationsProvider.Update(Time.deltaTime);
        }
    }
}