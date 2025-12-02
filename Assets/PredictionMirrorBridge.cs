using System.Collections.Generic;
using Mirror;
using Prediction;
using Prediction.data;
using Prediction.Simulation;
using UnityEngine;

namespace DefaultNamespace
{
    //TODO: problem 1: why is the ball moving differently on the client vs server when rotation is fixed?
    public class PredictionMirrorBridge : NetworkBehaviour
    {
        //TODO: configurable fake server latency in tick counts to test high ping scenarios
        public static bool MSG_DEBUG = false;
        public static bool PRED_DEBUG = false;
        
        [SerializeField] PredictionManager predictionManager;
        [SerializeField] private TMPro.TMP_Text serverText;
        
        public bool reliable = false;
        public int resimCounter = 0;

        private DebugResimChecker resimulationDecider = new DebugResimChecker();
        private void Awake()
        {
            PlayerController.spawned.AddEventListener(OnSpawned);
            PlayerController.despawned.AddEventListener(OnDespawned);
        }

        private void OnDestroy()
        {
            PlayerController.spawned.RemoveEventListener(OnSpawned);
            PlayerController.despawned.RemoveEventListener(OnDespawned);
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
                predictionManager.SetResimulationChecker(resimulationDecider);
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
                predictionManager.serverStateSender = (entityNetId, data) =>
                {
                    if (MSG_DEBUG)
                        Debug.Log($"[PredictionMirrorBridge][clientStateSender] SEND server_report: netId:{entityNetId} tickId:{data.tickId} data:{data}");
                    if (reliable)
                    {
                        ReportFromServerReliable(entityNetId, data);
                    }
                    else
                    {
                        ReportFromServerUnreliable(entityNetId, data);
                    }
                };
            }
        }

        void OnSpawned(PlayerController entity)
        {
            if (isServer)
            {
                predictionManager.AddPredictedEntity(entity.netId, entity.serverPredictedEntity);   
                predictionManager.SetEntityOwner(entity.serverPredictedEntity, entity.netIdentity.connectionToClient.connectionId);
            }
            if (isClient && entity.clientPredictedEntity != null)
            {
                predictionManager.AddPredictedEntity(entity.netId, entity.clientPredictedEntity);
                if (entity.netIdentity.isOwned)
                {
                    predictionManager.SetLocalEntity(entity.netIdentity.netId, entity.clientPredictedEntity);
                    //TODO: handle character change...
                    entity.clientPredictedEntity.predictionAcceptable.AddEventListener(b =>
                    {
                        if (b)
                        {
                            //Debug.Log($"[Prediction][PredictionAcceptable]");
                        }
                    });
                    entity.clientPredictedEntity.resimulation.AddEventListener(b =>
                    {
                        if (b)
                        {
                            resimCounter++;
                            if (PRED_DEBUG)
                                Debug.Log($"[Prediction][ResimulationStart]({resimCounter}) tickId:{entity.clientPredictedEntity.GetTotalTicks()} avgResim:{entity.clientPredictedEntity.GetAverageResimPerTick()}");
                        }
                    });
                }
            }
            ktl?.DetectAllBodies();
        }
        
        void OnDespawned(PlayerController entity)
        {
            if (isServer)
            {
                predictionManager.RemovePredictedEntity(entity.serverPredictedEntity);  
            }
            if (isClient)
            {
                predictionManager.RemovePredictedEntity(entity.netId);
                if (predictionManager.GetLocalEntity() == entity.clientPredictedEntity)
                {
                    predictionManager.SetLocalEntity(0, null);
                }
            }
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
    }
}