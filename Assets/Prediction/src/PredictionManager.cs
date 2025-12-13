using System;
using System.Collections.Generic;
using Assets.Scripts.Systems.Events;
using Prediction.data;
using Prediction.Interpolation;
using Prediction.policies.singleInstance;
using Prediction.Simulation;
using UnityEngine;

namespace Prediction
{
    //TODO: decouple the implementation from Time.fixedDeltaTime, have it be configurable
    public class PredictionManager: MonoBehaviour
    {
        public static bool DEBUG = false;
        //TODO: guard singleton
        public static PredictionManager Instance;
        public static Func<VisualsInterpolationsProvider> INTERPOLATION_PROVIDER = () => new MovingAverageInterpolator();
        public static SingleSnapshotInstanceResimChecker SNAPSHOT_INSTANCE_RESIM_CHECKER = new SimpleConfigurableResimulationDecider();
        public static PhysicsController PHYSICS_CONTROLLED = new RewindablePhysicsController(); // new SimplePhysicsControllerKinematic();
        public static Func<double> ROUND_TRIP_GETTER;
        
        [SerializeField] private GameObject localGO;
        private ClientPredictedEntity localEntity;
        private uint localEntityId;
        
        //TODO: protected
        public Dictionary<ServerPredictedEntity, uint> _serverEntityToId = new Dictionary<ServerPredictedEntity, uint>();
        private Dictionary<uint, ServerPredictedEntity> _idToServerEntity = new Dictionary<uint, ServerPredictedEntity>();
        private Dictionary<ServerPredictedEntity, int> _entityToOwnerConnId = new Dictionary<ServerPredictedEntity, int>();
        private Dictionary<int, ServerPredictedEntity> _connIdToEntity = new Dictionary<int, ServerPredictedEntity>();
        //TODO: protected
        public Dictionary<uint, ClientPredictedEntity> _clientEntities = new Dictionary<uint, ClientPredictedEntity>();
        private HashSet<GameObject> _predictedEntities = new HashSet<GameObject>();
        
        private bool isClient;
        private bool isServer;
        private uint tickId;
        private uint lastClientAppliedTick;
        private bool setup = false;
        
        public Action<uint, PredictionInputRecord>       clientStateSender;
        public Action<uint, uint, PhysicsStateRecord>    serverStateSender;
        public bool autoTrackRigidbodies = true;
        
        private void Awake()
        {
            Instance = this;
        }

        public void Setup(bool isServer, bool isClient)
        {
            setup = true;
            this.isServer = isServer;
            this.isClient = isClient;
            PHYSICS_CONTROLLED.Setup(isServer);
        }

        private void OnDestroy()
        {
            Instance = null;
        }

        public void SetEntityOwner(ServerPredictedEntity entity, int ownerId)
        {
            if (entity == null)
                return;
            
            int prevConnId = _entityToOwnerConnId.GetValueOrDefault(entity, 0);
            if (prevConnId != 0)
            {
                _connIdToEntity.Remove(prevConnId);
            }
            
            _entityToOwnerConnId[entity] = ownerId;
            if (ownerId != 0)
            {
                _connIdToEntity[ownerId] = entity;
            }
        }
        
        public void AddPredictedEntity(uint id, ServerPredictedEntity entity)
        {
            if (entity == null)
                return;
            
            _serverEntityToId[entity] = id;
            _idToServerEntity[id] = entity;
            _predictedEntities.Add(entity.gameObject);
            if (DEBUG)
                Debug.Log($"[PredictionManager][AddPredictedEntity] SERVER ({id})=>({entity})");
        }

        public void AddPredictedEntity(uint id, ClientPredictedEntity entity)
        {
            if (entity == null)
                return;
            
            if (DEBUG)
                Debug.Log($"[PredictionManager][AddPredictedEntity] CLIENT ({id})=>({entity})");
            
            _clientEntities[id] = entity;
            _predictedEntities.Add(entity.gameObject);
            if (autoTrackRigidbodies)
            {
                PHYSICS_CONTROLLED.Track(entity.rigidbody);
            }
        }
        
        private void RemovePredictedEntity(ServerPredictedEntity entity)
        {
            if (entity == null)
                return;
            
            SetEntityOwner(entity, 0);
            if (_serverEntityToId.ContainsKey(entity))
            {
                uint id = _serverEntityToId[entity];
                _serverEntityToId.Remove(entity);
                _idToServerEntity.Remove(id);
            }
            
            _serverEntityToId.Remove(entity);
            _entityToOwnerConnId.Remove(entity);
            _predictedEntities.Remove(entity.gameObject);
            if (DEBUG)
                Debug.Log($"[PredictionManager][RemovePredictedEntity] entity:{entity}");
        }

        public void RemovePredictedEntity(uint id)
        {
            if (DEBUG)
                Debug.Log($"[PredictionManager][RemovePredictedEntity]({id})");
            
            ClientPredictedEntity ent = _clientEntities.GetValueOrDefault(id, null);
            _clientEntities.Remove(id);
            if (ent != null)
            {
                _predictedEntities.Remove(ent.gameObject);
                if (autoTrackRigidbodies)
                {
                    PHYSICS_CONTROLLED.Untrack(ent.rigidbody);
                }
            }
            if (id == localEntityId)
            {
                UnsetLocalEntity(id);
            }
            RemovePredictedEntity(_idToServerEntity.GetValueOrDefault(id));
        }
        
        public bool IsPredicted(GameObject entity)
        {
            return _predictedEntities.Contains(entity);
        }

        public bool IsPredicted(Rigidbody entity)
        {
            if (!entity)
                return false;
            return _predictedEntities.Contains(entity.gameObject);
        }
        
        public void SetLocalEntity(uint id)
        {
            if (DEBUG)
                Debug.Log($"[PredictionManager][SetLocalEntity]({id})");
            
            localEntity = _clientEntities.GetValueOrDefault(id, null);
            localEntityId = 0;
            if (localEntity != null)
            {
                //FUDO: consider moving the id fetching mechanic inside entity
                localEntityId = id;
                localGO = localEntity.gameObject;
                localEntity.physicsController = PHYSICS_CONTROLLED;
                localEntity.SetSingleStateEligibilityCheckHandler(SNAPSHOT_INSTANCE_RESIM_CHECKER.Check);
                
                lastClientAppliedTick = 0;
            }
        }

        public void UnsetLocalEntity(uint id)
        {
            if (localEntityId == id)
            {
                UnsetLocalEntity();
            }
        }

        void UnsetLocalEntity()
        {
            localEntity = null;
            localEntityId = 0;
        }

        public ClientPredictedEntity GetLocalEntity()
        {
            return localEntity;
        }

        private PhysicsStateRecord specialHostRecord = new PhysicsStateRecord();
        private void FixedUpdate()
        {    
            if (!setup) 
                return;
            
            tickId++;
            ClientPreSimTick();
            ServerPreSimTick();
            PHYSICS_CONTROLLED.Simulate();
            CommonPostSimTick();
        }

        void ClientPreSimTick()
        {
            if (isClient)
            {
                foreach (KeyValuePair<uint, ClientPredictedEntity> pair in _clientEntities)
                {
                    if (pair.Value.isControlledLocally)
                    {
                        if (pair.Value != localEntity)
                        {
                            Debug.LogError($"[PredictionManager] ClientEntity != LocalEntity. Client:{pair.Value} Local:{localEntity}");
                            continue;
                        }
                        
                        if (DEBUG)
                            Debug.Log($"[PredictionManager][ClientPreSimTick] Client:{pair.Value} tick:{tickId}");
                        
                        PredictionInputRecord tickInputRecord = pair.Value.ClientSimulationTick(tickId);
                        lastClientAppliedTick = tickId;
                        if (!isServer)
                        {
                            try
                            {
                                if (DEBUG)
                                    Debug.Log($"[PredictionManager][ClientPreSimTick][SEND] goID:{pair.Value.gameObject.GetInstanceID()} Client:{pair.Value} tick:{tickId}");
                                clientStateSender?.Invoke(tickId, tickInputRecord);
                            }
                            catch (Exception e)
                            {
                                EntityProcessingError err;
                                err.exception = e;
                                err.entity = pair.Value;
                                onClientStateSendError.Dispatch(err);
                            }   
                        }        
                    }
                    else if (!isServer)
                    {
                        //Only run this on the pure client yo... 
                        pair.Value.ClientFollowerSimulationTick();
                    }
                }
            }
        }

        void CommonPostSimTick()
        {
            foreach (KeyValuePair<uint, ClientPredictedEntity> pair in _clientEntities)
            {
                pair.Value.SamplePhysicsState(tickId);
            }
        }

        void ServerPreSimTick()
        {
            if (isServer)
            {
                foreach (KeyValuePair<ServerPredictedEntity, uint> pair in _serverEntityToId)
                {
                    ServerPredictedEntity entity = pair.Key;
                    uint id = pair.Value;
                    
                    PhysicsStateRecord state;
                    if (isClient && id == localEntityId)
                    {
                        entity.PopulatePhysicsStateRecord(tickId, specialHostRecord);
                        state = specialHostRecord;
                        state.tickId = tickId;
                    }
                    else
                    {
                        state = entity.ServerSimulationTick();
                    }
                    
                    try
                    {
                        serverStateSender?.Invoke(id, tickId, state);
                    }
                    catch (Exception e)
                    {
                        EntityProcessingError err;
                        err.exception = e;
                        err.entity = entity;
                        onServerStateSendError.Dispatch(err);
                    }
                }
            }
        }

        public void OnFollowerServerStateReceived(uint entityId, PhysicsStateRecord stateRecord)
        {
            if (!isClient)
                return;
            
            if (entityId == localEntityId)
            {
                OnServerStateReceived(stateRecord);
                return;
            }
            
            ClientPredictedEntity entity = _clientEntities.GetValueOrDefault(entityId, null);
            if (entity != null)
            {
                entity.BufferFollowerServerTick(stateRecord);
            }
        }
        
        public void OnServerStateReceived(PhysicsStateRecord serverState)
        {
            if (isClient && !isServer)
                localEntity?.BufferServerTick(lastClientAppliedTick, serverState);
        }


        public void OnClientStateReceived(int connId, uint clientTickId, PredictionInputRecord tickInputRecord)
        {
            if (!isServer)
                return;
            
            ServerPredictedEntity entity = _connIdToEntity.GetValueOrDefault(connId);
            if (DEBUG)
                Debug.Log($"[PredictionManager][OnClientStateReceiver] connId:{connId} clientTickId:{clientTickId} tickInputRecord:{tickInputRecord} ENTITY:{entity}");
            entity?.BufferClientTick(clientTickId, tickInputRecord);
        }

        public static uint GetServerTickDelay()
        {
            return (uint) Mathf.CeilToInt((float)(ROUND_TRIP_GETTER() / Time.fixedDeltaTime));
        }
        
        public struct EntityProcessingError
        {
            public Exception exception;
            public AbstractPredictedEntity entity;
        }
        
        public SafeEventDispatcher<EntityProcessingError> onServerStateSendError = new();
        public SafeEventDispatcher<EntityProcessingError> onClientStateSendError = new();
    }
}