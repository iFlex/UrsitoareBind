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
        
        private ClientPredictedEntity clientPredictedEntity;
        [SerializeField] private GameObject follow;
        
        private GameObject serverGhost;
        private GameObject clientGhost;
        public bool hasVIP = false;
        
        public double currentTimeStep = 0;
        public double targetTime = 0;
        public double artifficialDelay = 1f;
        public void SetClientPredictedEntity(ClientPredictedEntity clientPredictedEntity)
        {
            this.clientPredictedEntity = clientPredictedEntity;
            follow = clientPredictedEntity.gameObject;
            currentTimeStep -= artifficialDelay;
            
            //TODO: listen for destruction events
            visualsEntity.transform.SetParent(null);
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
            if (!follow)
                return;
            
            //TODO: proper integration
            if (clientPredictedEntity.interpolationsProvider != null)
            {
                clientPredictedEntity.interpolationsProvider.Update(Time.deltaTime);
            }
            //else
            /*
            {
                float lerpAmount = 0f;
                if (targetTime < currentTimeStep)
                {
                    lerpAmount = 1;
                    Debug.Log("PREDICTION_LAGGING_BEHIND");
                }
                else
                {
                    lerpAmount = ((float)(targetTime - currentTimeStep)) / Time.fixedDeltaTime;
                }
                
                visualsEntity.transform.position = Vector3.Lerp(visualsEntity.transform.position, follow.transform.position, lerpAmount);
                visualsEntity.transform.rotation = Quaternion.Lerp(visualsEntity.transform.rotation, follow.transform.rotation, lerpAmount);
            }
            */
            if (debug)
            {
                PhysicsStateRecord rec = clientPredictedEntity.serverStateBuffer.GetEnd();
                if (rec != null)
                {
                    serverGhost.transform.position = rec.position;
                    serverGhost.transform.rotation = rec.rotation;   
                }
                serverGhost.SetActive(SHOW_DBG);
                clientGhost.SetActive(SHOW_DBG);
            }
        }
    }
}