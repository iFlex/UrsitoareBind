using System;
using System.Collections.Generic;
using Assets.Scripts.Systems.Events;
using Prediction.data;
using Prediction.policies.singleInstance;
using Prediction.Simulation;
using UnityEngine;

namespace Prediction
{
    //TODO: decoule the implementation from Time.fixedDeltaTime, have it be configurable
    public class PredictionManager: MonoBehaviour
    {
        [SerializeField] private GameObject localGO;
        private ClientPredictedEntity localEntity;
        private uint localEntityId;
        
        public Dictionary<ServerPredictedEntity, uint> _serverEntityToId = new Dictionary<ServerPredictedEntity, uint>();
        private Dictionary<ServerPredictedEntity, int> _entityToOwnerConnId = new Dictionary<ServerPredictedEntity, int>();
        private Dictionary<int, ServerPredictedEntity> _connIdToEntity = new Dictionary<int, ServerPredictedEntity>();
        private Dictionary<uint, ClientPredictedEntity> _nonControlledPredictedEntities = new Dictionary<uint, ClientPredictedEntity>();
        
        private bool isClient;
        private bool isServer;
        private uint tickId;
        private uint lastClientAppliedTick;
        private bool setup = false;
        private PhysicsController physicsController;
        
        public Action<uint, PredictionInputRecord> clientStateSender;
        public Action<uint, PhysicsStateRecord>    serverStateSender;
        
        private SingleSnapshotInstanceResimChecker singleSnapshotInstanceResimChecker;
        
        public void Setup(bool isServer, bool isClient, PhysicsController physicsController)
        {
            setup = true;
            this.isServer = isServer;
            this.isClient = isClient;
            this.physicsController = physicsController;
            physicsController.Setup(isServer);
            singleSnapshotInstanceResimChecker ??= new SimpleConfigurableResimulationDecider();
        }

        public void SetResimulationChecker(SingleSnapshotInstanceResimChecker checker)
        {
            singleSnapshotInstanceResimChecker = checker;    
        }

        public void SetEntityOwner(ServerPredictedEntity entity, int ownerId)
        {
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
            _serverEntityToId[entity] = id;
        }

        public void RemovePredictedEntity(ServerPredictedEntity entity)
        {
            SetEntityOwner(entity, 0);
            _serverEntityToId.Remove(entity);
            _entityToOwnerConnId.Remove(entity);
        }

        public void AddPredictedEntity(uint id, ClientPredictedEntity entity)
        {
            Debug.Log($"[PredictionManager][AddPredictedEntity]({id})=>({entity})");
            _nonControlledPredictedEntities[id] = entity;
        }

        public void RemovePredictedEntity(uint id)
        {
            Debug.Log($"[PredictionManager][RemovePredictedEntity]({id})");
            _nonControlledPredictedEntities.Remove(id);
            if (id == localEntityId)
            {
                SetLocalEntity(0, null);
            }
        }

        public void SetLocalEntity(uint id, ClientPredictedEntity entity)
        {
            localEntity = entity;
            localEntityId = 0;
            if (entity != null)
            {
                //TODO: consider moving the id fetching mechanic inside entity
                localEntityId = id;
                localGO = entity.gameObject;
                entity.physicsController = physicsController;
                entity.SetSingleStateEligibilityCheckHandler(singleSnapshotInstanceResimChecker.Check);   
            }
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
            if (isClient)
            {
                if (localEntity != null)
                {
                    PredictionInputRecord tickInputRecord = localEntity.ClientSimulationTick(tickId);
                    lastClientAppliedTick = tickId;
                    if (!isServer)
                    {
                        try
                        {
                            clientStateSender?.Invoke(tickId, tickInputRecord);
                        }
                        catch (Exception e)
                        {
                            EntityProcessingError err;
                            err.exception = e;
                            err.entity = localEntity;
                            onClientStateSendError.Dispatch(err);
                        }   
                    }
                }
            }
            
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
                        serverStateSender?.Invoke(id, state);
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
            
            physicsController.Simulate();
            if (isClient && localEntity != null)
            {
                localEntity.SamplePhysicsState(tickId);
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
            
            ClientPredictedEntity entity = _nonControlledPredictedEntities.GetValueOrDefault(entityId, null);
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
            entity?.BufferClientTick(clientTickId, tickInputRecord);
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