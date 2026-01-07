using System.Collections.Generic;
using Mirror;
using Prediction;
using Prediction.data;
using Prediction.policies.singleInstance;
using Prediction.Wrappers;
using UnityEngine;

namespace DefaultNamespace
{
    public class PredictionMirrorBridge : NetworkBehaviour
    {
        public static PredictionMirrorBridge Instance;
        public static bool MSG_DEBUG = false;
        public static bool PRED_DEBUG = false;

        public NetworkManager manager;
        PredictionManager predictionManager = new PredictionManager();
        [SerializeField] private TMPro.TMP_Text serverText;
        [SerializeField] private GameObject sharedGOPrefab;
        public GameObject sharedGO;
        [SyncVar] public PredictedNetworkBehaviour sharedPredMono;
        public PredictedNetworkBehaviour localPredMono;
        
        private Dictionary<int, PredictedNetworkBehaviour> originalOwnership = new Dictionary<int, PredictedNetworkBehaviour>();
        
        public int resimCounter = 0;
        private bool setSendRate = false;
        public int sendRateMultiplier = 1;
        
        private DebugResimChecker resimulationDecider = new DebugResimChecker();
        private void Awake()
        {
            SimpleConfigurableResimulationDecider.LOG_RESIMULATIONS = true;
            SimpleConfigurableResimulationDecider.LOG_ALL_CHECKS = false;
            
            Instance = this;
            PlayerController.spawned.AddEventListener(OnSpawned);
            PlayerController.despawned.AddEventListener(OnDespawned);
        }
        
        private void OnDestroy()
        {
            PlayerController.spawned.RemoveEventListener(OnSpawned);
            PlayerController.despawned.RemoveEventListener(OnDespawned);
        }

        private void FixedUpdate()
        {
            predictionManager.Tick();
            
            if (!setSendRate)
            {
                manager.sendRate = Mathf.CeilToInt(1f / Time.fixedDeltaTime) * sendRateMultiplier;   
            }
            
            if (Input.GetKeyDown(KeyCode.X) && sharedPredMono)
            {
                ((PredictedEntity)sharedPredMono).ApplyClientForce(rb =>
                {
                    Debug.Log($"[FAKE_IT_TILL_YOU_TEST_IT] (goID:{rb.gameObject.GetInstanceID()})");
                    rb.AddForce(Vector3.left * 5000);
                });
            }
        }

        private void Start()
        {
            PredictionManager.ROUND_TRIP_GETTER = () => NetworkTime.rtt;
            if (isClient)
            {
                Debug.Log($"[PredictionMirrorBridge][clientStateSender] SETUP CLIENT SENDER CALLBACK");
                predictionManager.clientStateSender = (tickId, data) =>
                {
                    if (MSG_DEBUG)
                        Debug.Log($"[PredictionMirrorBridge][clientStateSender] SEND client_report: tickId:{tickId} data:{data}");
                    
                    ReportToServerUnreliable(tickId, data);
                };
                predictionManager.clientHeartbeadSender = (tickId) =>
                {
                    if (MSG_DEBUG)
                        Debug.Log($"[PredictionMirrorBridge][clientHeartbeadSender] SEND client_heartbeat: tickId:{tickId}");

                    ReportHeartbeatToServerUnreliable(tickId);
                };
            }
            
            if (isServer)
            {
                Debug.Log($"[PredictionMirrorBridge][clientStateSender] SETUP SERVER SEND CALLBACK");
                predictionManager.connectionsIterator = () => NetworkServer.connections.Keys;
                predictionManager.serverStateSender = (connId, entityId, data) =>
                {
                    if (MSG_DEBUG)
                        Debug.Log($"[PredictionMirrorBridge][clientStateSender] SEND server_report: netId:{entityId} tickId:{data.tickId} data:{data}");
                    
                    NetworkConnectionToClient netconn = NetworkServer.connections.GetValueOrDefault(connId, null);
                    if (netconn != null)
                    {
                        TargetedReportFromServerUnreliable(netconn, entityId, data);
                    }
                    else if (connId != 0)
                    {
                        //TODO: report?
                    }
                };
                
                predictionManager.serverWorldStateSender = (connId, data) =>
                {
                    if (MSG_DEBUG)
                        Debug.Log($"[PredictionMirrorBridge][serverWorldStateSender] SEND server_world_report: connId:{connId} data:{data}");
                    
                    NetworkConnectionToClient netconn = NetworkServer.connections.GetValueOrDefault(connId, null);
                    if (netconn != null)
                    {
                        TargetedWorldReportFromServerUnreliable(netconn, data);
                    }
                    else if (connId != 0)
                    {
                        //TODO: report?
                    }
                };

                predictionManager.serverSetControlledLocally = (connId, entityId, owned) =>
                {
                    NetworkConnectionToClient netconn = NetworkServer.connections.GetValueOrDefault(connId, null);
                    if (netconn != null)
                    {
                        UpdateLocalOwnership(netconn, entityId, owned);
                    }
                    else if (connId != 0)
                    {
                        //TODO: report?
                    }
                };
                
                sharedGO = Instantiate(sharedGOPrefab, Vector3.one, Quaternion.identity);
                NetworkServer.Spawn(sharedGO);
                sharedPredMono = sharedGO.GetComponent<PredictedNetworkBehaviour>();
            }
            predictionManager.Setup(isServer, isClient);
        }
        
        void OnSpawned(PlayerController entity)
        {
            if (isServer)
            {
                int connId = entity.netIdentity.connectionToClient == null ? 0 : entity.netIdentity.connectionToClient.connectionId;
                Debug.Log($"[PredictionMirrorBridge][OnSpawned] entity:{entity} netId:{entity.netId} connId:{connId}");
                if (entity.gameObject != sharedPredMono.gameObject)
                {
                    originalOwnership[connId] = entity.predictedMono;
                }
                if (entity.isOwned)
                {
                    localPredMono = entity.predictedMono;
                    predictionManager.SetEntityOwner(entity.predictedMono.GetServerEntity(), 0);
                }
                if (entity.netIdentity.connectionToClient != null)
                {
                    predictionManager.SetEntityOwner(entity.predictedMono.GetServerEntity(), connId);
                }
            }
            if (isClient && entity.predictedMono.clientPredictedEntity != null)
            {
                if (entity.netIdentity.isOwned)
                {
                    localPredMono = entity.predictedMono;
                }
            }
        }
        
        void OnDespawned(PlayerController entity)
        {
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                PredictedEntityVisuals.SHOW_DBG = !PredictedEntityVisuals.SHOW_DBG;
            }
        }

        [Command(requiresAuthority = false, channel = Channels.Unreliable)]
        void ReportHeartbeatToServerUnreliable(uint tickId, NetworkConnectionToClient sender = null)
        {
            if (MSG_DEBUG)
                Debug.Log($"[PredictionMirrorBridge][ReportHeartbeatToServerUnreliable] Received client_heartbeat: tickId:{tickId} sender:{sender}");
            predictionManager.OnHeartbeatReceived(sender.connectionId, tickId);
        }

        [Command(requiresAuthority = false, channel = Channels.Unreliable)]
        void ReportToServerUnreliable(uint tickId, PredictionInputRecord data, NetworkConnectionToClient sender = null)
        {
            if (MSG_DEBUG)
                Debug.Log($"[PredictionMirrorBridge][ReportToServerUnreliable] Received client_report: tickId:{tickId} sender:{sender} data:{data}");
            predictionManager.OnClientStateReceived(sender.connectionId, tickId, data);
        }
        
        [TargetRpc(channel = Channels.Unreliable)]
        void TargetedReportFromServerUnreliable(NetworkConnectionToClient receiver, uint entityNetId, PhysicsStateRecord data)
        {
            if (MSG_DEBUG)
                Debug.Log($"[PredictionMirrorBridge][TargetedReportFromServerUnreliable] Received serrver_report: netId:{entityNetId} tickId:{data.tickId} data:{data}");
            predictionManager.OnServerStateReceived(entityNetId, data);
        }
        
        [TargetRpc(channel = Channels.Unreliable)]
        void TargetedWorldReportFromServerUnreliable(NetworkConnectionToClient receiver, WorldStateRecord data)
        {
            if (MSG_DEBUG)
                Debug.Log($"[PredictionMirrorBridge][TargetedWorldReportFromServerUnreliable] Received server_report: data:{data}");
            predictionManager.OnServerWorldStateReceived(data);
        }

        [TargetRpc(channel = Channels.Reliable)]
        void UpdateLocalOwnership(NetworkConnectionToClient receiver, uint entityId, bool owned)
        {
            if (MSG_DEBUG)
                Debug.Log($"[PredictionMirrorBridge][UpdateLocalOwnership] Received server_ownership_report: entity:{entityId} owned:{owned}");
            predictionManager.OnEntityOwnershipChanged(entityId, owned);
        }
        
        public PredictedNetworkBehaviour GetOriginalOwnedObject(int connectionId)
        {
            return originalOwnership.GetValueOrDefault(connectionId, null);
        }
        
        [Server]
        public void SwitchOwnership(int connectionId, PredictedNetworkBehaviour newObject)
        {
            if (predictionManager.GetOwner(newObject.GetServerEntity()) > -1)
            {
                Debug.Log($"[PredictionMirrorBridge][SwitchOwnership] BUSY conn:{connectionId} newObj:{newObject}");
                return;
            }
            
            Debug.Log($"[PredictionMirrorBridge][SwitchOwnership] conn:{connectionId} newObj:{newObject}");
            if (newObject == sharedPredMono && newObject.clientPredictedEntity == null)
            {
                Debug.Log($"[PredictionMirrorBridge][SwitchOwnership][SERVER_INIT] connId:{connectionId}");
                newObject.OnStartAuthority();
            }
            predictionManager.SetEntityOwner(newObject.serverPredictedEntity, connectionId);
        }
    }
}