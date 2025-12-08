using System.Collections.Generic;
using Mirror;
using Prediction;
using Prediction.data;
using Prediction.Simulation;
using Prediction.wrappers;
using UnityEngine;

namespace DefaultNamespace
{
    //TODO: problem 1: why is the ball moving differently on the client vs server when rotation is fixed?
    public class PredictionMirrorBridge : NetworkBehaviour
    {
        public static PredictionMirrorBridge Instance;
        //TODO: configurable fake server latency in tick counts to test high ping scenarios
        //TODO: track owner connection of client

        public static bool MSG_DEBUG = false;
        public static bool PRED_DEBUG = false;

        public NetworkManager manager;
        [SerializeField] PredictionManager predictionManager;
        [SerializeField] private TMPro.TMP_Text serverText;
        [SerializeField] private GameObject sharedGOPrefab;
        public GameObject sharedGO;
        public PredictedNetworkBehaviour sharedPredMono;
        public PredictedNetworkBehaviour localPredMono;
        
        private Dictionary<int, PredictedNetworkBehaviour> originalOwnership = new Dictionary<int, PredictedNetworkBehaviour>();
        private Dictionary<int, PredictedNetworkBehaviour> ownership = new Dictionary<int, PredictedNetworkBehaviour>();
        private Dictionary<uint, int> entityIdToOwner = new Dictionary<uint, int>();
        
        public bool reliable = false;
        public int resimCounter = 0;
        private bool setSendRate = false;
        public int sendRateMultiplier = 1;
        
        private DebugResimChecker resimulationDecider = new DebugResimChecker();
        private void Awake()
        {
            Instance = this;
            //TODO: offer auto wiring for this in module
            PlayerController.spawned.AddEventListener(OnSpawned);
            PlayerController.despawned.AddEventListener(OnDespawned);
        }
        
        private void OnDestroy()
        {
            //TODO: offer auto wiring for this in module
            PlayerController.spawned.RemoveEventListener(OnSpawned);
            PlayerController.despawned.RemoveEventListener(OnDespawned);
        }

        private void FixedUpdate()
        {
            if (!setSendRate)
            {
                manager.sendRate = Mathf.CeilToInt(1f / Time.fixedDeltaTime) * sendRateMultiplier;   
            }
        }

        private SimplePhysicsControllerKinematic ktl;
        private void Start()
        {
            PhysicsController ctl = new SimplePhysicsController();
            predictionManager.Setup(isServer, isClient, ctl);

            //ktl = new SimplePhysicsControllerKinematic();
            //predictionManager.Setup(isServer, isClient, ktl);
            ktl?.DetectAllBodies();
            
            if (isClient)
            {
                predictionManager.clientStateSender = (tickId, data) =>
                {
                    if (MSG_DEBUG)
                        Debug.Log($"[PredictionMirrorBridge][clientStateSender] SEND client_report: tickId:{tickId} data:{data}");
                    
                    if (reliable)
                    {
                        ReportToServerReliable(tickId, data);
                    }
                    else
                    {
                        ReportToServerUnreliable(tickId, data);
                    }
                };
            }
            
            if (isServer)
            {
                predictionManager.serverStateSender = (entityNetId, serverTick, data) =>
                {
                    if (MSG_DEBUG)
                        Debug.Log($"[PredictionMirrorBridge][clientStateSender] SEND server_report: netId:{entityNetId} tickId:{data.tickId} data:{data}");
                    /*
                    if (reliable)
                    {
                        ReportFromServerReliable(entityNetId, data);
                    }
                    else
                    {
                        //ReportFromServerUnreliable(entityNetId, data);
                    }
                    */
                    SendTargetedReportFromServer(entityNetId, serverTick, data, reliable);
                };
                
                sharedGO = Instantiate(sharedGOPrefab, Vector3.one, Quaternion.identity);
                sharedPredMono = sharedGO.GetComponent<PredictedNetworkBehaviour>();
                NetworkServer.Spawn(sharedGO);
            }
        }

        void OnSpawned(PlayerController entity)
        {
            if (isServer)
            {
                int connId = entity.netIdentity.connectionToClient == null ? 0 : entity.netIdentity.connectionToClient.connectionId;
                if (entity.gameObject != sharedPredMono.gameObject)
                {
                    ownership[connId] = entity.predictedMono;   
                    originalOwnership[connId] = entity.predictedMono;
                    entityIdToOwner[entity.netId] = connId;
                }
                if (entity.isOwned)
                {
                    localPredMono = entity.predictedMono;
                }
            }
            if (isClient && entity.predictedMono.clientPredictedEntity != null)
            {
                if (entity.netIdentity.isOwned)
                {
                    localPredMono = entity.predictedMono;
                    entity.predictedMono.SetControlledLocally(true);
                }
            }
            ktl?.DetectAllBodies();
        }
        
        void OnDespawned(PlayerController entity)
        {
            ktl?.DetectAllBodies();
        }

        void Update()
        {
            if (serverText)
            {
                serverText.text = "";
                //TODO: make _serverEntityToId private again
                foreach (KeyValuePair<ServerPredictedEntity, uint> pair in predictionManager._serverEntityToId)
                {
                    serverText.text += $"id:{pair.Value} skipped:{pair.Key.ticksWithoutInput} range:{pair.Key.BufferSize()} inputJumps:{pair.Key.inputJumps}\n";
                }
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                PredictedEntityVisuals.SHOW_DBG = !PredictedEntityVisuals.SHOW_DBG;
            }
        }
        
        [Command(requiresAuthority = false, channel = Channels.Unreliable)]
        void ReportToServerUnreliable(uint tickId, PredictionInputRecord data, NetworkConnectionToClient sender = null)
        {
            if (MSG_DEBUG)
                Debug.Log($"[PredictionMirrorBridge][ReportToServer] Received client_report: tickId:{tickId} sender:{sender} data:{data}");
            predictionManager.OnClientStateReceived(sender.connectionId, tickId, data);
        }
        
        [Command(requiresAuthority = false)]
        void ReportToServerReliable(uint tickId, PredictionInputRecord data, NetworkConnectionToClient sender = null)
        {
            if (MSG_DEBUG)
                Debug.Log($"[PredictionMirrorBridge][ReportToServer] Received client_report: tickId:{tickId} sender:{sender} data:{data}");
            predictionManager.OnClientStateReceived(sender.connectionId, tickId, data);
        }
        
        [Server]
        void SendTargetedReportFromServer(uint entityNetId, uint serverTickId, PhysicsStateRecord data, bool reliable)
        {
            int owner = entityIdToOwner.GetValueOrDefault(entityNetId, 0);
            TargetedReportFromServerUnreliable(NetworkServer.connections[owner], entityNetId, data);
            
            data.tickId = serverTickId;
            foreach (NetworkConnectionToClient clientConn in NetworkServer.connections.Values)
            {
                if (clientConn.connectionId != owner)
                {
                    TargetedReportFromServerUnreliable(clientConn, entityNetId, data);
                }
            }
        }
        
        [TargetRpc(channel = Channels.Unreliable)]
        void TargetedReportFromServerUnreliable(NetworkConnectionToClient receiver, uint entityNetId, PhysicsStateRecord data)
        {
            if (MSG_DEBUG)
                Debug.Log($"[PredictionMirrorBridge][ReportFromServer] Received serrver_report: netId:{entityNetId} tickId:{data.tickId} data:{data}");
            
            data.tmpServerTime = NetworkClient.connection.remoteTimeStamp;
            predictionManager.OnFollowerServerStateReceived(entityNetId, data);
        }
        
        [ClientRpc(channel = Channels.Unreliable)]
        void ReportFromServerUnreliable(uint entityNetId, PhysicsStateRecord data)
        {
            if (MSG_DEBUG)
                Debug.Log($"[PredictionMirrorBridge][ReportFromServer] Received serrver_report: netId:{entityNetId} tickId:{data.tickId} data:{data}");
            
            data.tmpServerTime = NetworkClient.connection.remoteTimeStamp;
            predictionManager.OnFollowerServerStateReceived(entityNetId, data);
        }
        
        [ClientRpc]
        void ReportFromServerReliable(uint entityNetId, PhysicsStateRecord data)
        {
            if (MSG_DEBUG)
                Debug.Log($"[PredictionMirrorBridge][ReportFromServer] Received serrver_report: netId:{entityNetId} tickId:{data.tickId} data:{data}");
            
            data.tmpServerTime = NetworkClient.connection.remoteTimeStamp;
            predictionManager.OnFollowerServerStateReceived(entityNetId, data);
        }

        public PredictedNetworkBehaviour GetOriginalOwnedObject(int connectionId)
        {
            return originalOwnership.GetValueOrDefault(connectionId, null);
        }

        public PredictedNetworkBehaviour GetOwnerObject(int connectionId)
        {
            return ownership.GetValueOrDefault(connectionId, null);
        }
        
        [Server]
        public void SwitchOwnership(int connectionId, PredictedNetworkBehaviour newObject)
        {
            if (entityIdToOwner.ContainsKey(newObject.netId))
            {
                Debug.Log($"[PredictionMirrorBridge][SwitchOwnership] BUSY conn:{connectionId} newObj:{newObject}");
                return;
            }
            
            PredictedNetworkBehaviour pnb = GetOwnerObject(connectionId);
            pnb.SetControlledLocally(false);
            
            Debug.Log($"[PredictionMirrorBridge][SwitchOwnership] conn:{connectionId} oldObj:{pnb} newObj:{newObject}");
            
            //NOTE, since this is just for show, set it to some stupid number
            predictionManager.SetEntityOwner(pnb.serverPredictedEntity, 9999);

            ownership[connectionId] = newObject;
            entityIdToOwner[newObject.netId] = connectionId;
            
            newObject.Reset();
            
            if (newObject == sharedPredMono && newObject.clientPredictedEntity == null)
            {
                Debug.Log($"[PredictionMirrorBridge][SwitchOwnership][SERVER_INIT] connId:{connectionId}");
                newObject.OnStartAuthority();
            }
            predictionManager.SetEntityOwner(newObject.serverPredictedEntity, connectionId);
            UpdateControlledObject(NetworkServer.connections[connectionId], newObject);
        }

        [TargetRpc]
        public void UpdateControlledObject(NetworkConnectionToClient conn, PredictedNetworkBehaviour newObj)
        {
            Debug.Log($"[PredictionMirrorBridge][UpdateControlledObject] conn:{conn} newObj:{newObj} currentObj:{localPredMono}");
            localPredMono.Reset();
            
            localPredMono = newObj;
            localPredMono.SetControlledLocally(true);
            predictionManager.SetLocalEntity(newObj.netId, newObj.clientPredictedEntity);
            SingletonUtils.localCPE = localPredMono.clientPredictedEntity;
        }
    }
}