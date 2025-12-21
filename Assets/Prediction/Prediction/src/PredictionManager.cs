using System;
using System.Collections.Generic;
using Assets.Scripts.Systems.Events;
using Prediction.data;
using Prediction.Interpolation;
using Prediction.policies.singleInstance;
using Prediction.Simulation;
using Prediction.wrappers;
using UnityEngine;

namespace Prediction
{
    //TODO: decouple the implementation from Time.fixedDeltaTime, have it be configurable
    public class PredictionManager
    {
        public static bool DEBUG = false;
        //TODO: guard singleton
        public static PredictionManager Instance;
        //TODO: validate presence of all static providers
        public static Func<VisualsInterpolationsProvider> INTERPOLATION_PROVIDER = () => new MovingAverageInterpolator();
        public static SingleSnapshotInstanceResimChecker SNAPSHOT_INSTANCE_RESIM_CHECKER = new SimpleConfigurableResimulationDecider();
        public static PhysicsController PHYSICS_CONTROLLED = new RewindablePhysicsController();
        
        //TODO: do we still need this?
        public static Func<double> ROUND_TRIP_GETTER;
        
        [SerializeField] private GameObject localGO;
        private ClientPredictedEntity localEntity;
        private uint localEntityId;
        
        //TODO: protected
        public Dictionary<ServerPredictedEntity, uint> _serverEntityToId = new Dictionary<ServerPredictedEntity, uint>();
        private Dictionary<uint, ServerPredictedEntity> _idToServerEntity = new Dictionary<uint, ServerPredictedEntity>();
        private Dictionary<ServerPredictedEntity, int> _entityToOwnerConnId = new Dictionary<ServerPredictedEntity, int>();
        private Dictionary<int, ServerPredictedEntity> _connIdToEntity = new Dictionary<int, ServerPredictedEntity>();
        public Dictionary<int, uint> _connIdToLatestTick = new Dictionary<int, uint>();
        //TODO: protected
        public Dictionary<uint, ClientPredictedEntity> _clientEntities = new Dictionary<uint, ClientPredictedEntity>();
        public HashSet<PredictedEntity> _predictedEntities = new HashSet<PredictedEntity>();
        private HashSet<GameObject> _predictedEntitiesGO = new HashSet<GameObject>();
        
        private bool isClient;
        private bool isServer;
        private uint tickId;
        //TODO: package private
        public uint lastClientAppliedTick;
        private bool setup = false;
        public bool autoTrackRigidbodies = true;
        public bool useServerWorldStateMessage = true;

        //NOTE: heartbeats are only sent when no predicted entity is controlled locally
        //         tickId
        public Action<uint>                       clientHeartbeadSender;
        //            tickId, inputData
        public Action<uint, PredictionInputRecord>       clientStateSender;
        // connectionId, entityId, state
        public Action<int, uint, PhysicsStateRecord>    serverStateSender;
        // connectionId, world state
        public Action<int, WorldStateRecord> serverWorldStateSender;
        public Func<IEnumerable<int>> connectionsIterator;
        private WorldStateRecord _worldStateRecord = new WorldStateRecord();
        
        public bool snapOnSimSkip = false;
        public bool protectFromOversimulation = true;
        public uint maxResimulationOverbudget = 2;

        public uint totalResimulationsDueToAuthority = 0;
        public uint totalResimulationsDueToFollowers = 0;
        public uint totalResimulationsDueToBoth = 0;
        
        public uint totalResimulations = 0;
        public uint totalResimulationSteps = 0;
        public uint totalResimulationStepsOverbudget = 0;
        public uint totalDesyncToSnapCount = 0;
        
        public uint totalResimulationsTriggeredByLocalAuthority = 0;
        public uint totalResimulationsTriggeredByFollowers = 0;
        public uint totalResimulationsTriggeredByBoth = 0;
        public uint totalResimulationsSkipped = 0;
        
        public PredictionManager()
        {
            Instance = this;
        }

        public void Setup(bool isServer, bool isClient)
        {
            setup = true;
            this.isServer = isServer;
            this.isClient = isClient;
            Validate();
            PHYSICS_CONTROLLED.Setup(isServer);
            Debug.Log($"[PredictionManager] isServer:{isServer} isClient:{isClient}");
        }

        void Validate()
        {
            if (isClient)
            {
                if (clientHeartbeadSender == null)
                {
                    throw new Exception(
                        "INVALID_CONFIG: isClient = true but no clientHeartbeadSender provided");
                }
                if (clientStateSender == null)
                {
                    throw new Exception(
                        "INVALID_CONFIG: isClient = true but no clientStateSender provided");
                }
                if (INTERPOLATION_PROVIDER == null)
                {
                    throw new Exception(
                        "INVALID_CONFIG: isClient = true but no Interpolation provider present");
                }

                if (SNAPSHOT_INSTANCE_RESIM_CHECKER == null)
                {
                    throw new Exception(
                        "INVALID_CONFIG: isClient = true but no Snapshot Resimulation Checker provided");
                }
            }

            if (isServer)
            {
                if (connectionsIterator == null)
                {
                    throw new Exception("INVALID_CONFIG: no connectionsIterator provided");
                }
                if (useServerWorldStateMessage && serverWorldStateSender == null)
                {
                    throw new Exception(
                        "INVALID_CONFIG: useServerWorldStateMessage = true but no serverWorldStateSender provided. Please provide a hook for sending world state packets.");
                }
                if (!useServerWorldStateMessage && serverStateSender == null)
                {
                    throw new Exception(
                        "INVALID_CONFIG: useServerWorldStateMessage = false but no serverStateSender provided. Please provide a hook for sending individual state packets.");
                }   
            }

            if (PHYSICS_CONTROLLED == null)
            {
                throw new Exception(
                    "INVALID_CONFIG: No Physics Controller provided.");
            }
        }

        private void Cleanup()
        {
            Instance = null;
        }

        public void SetEntityOwner(ServerPredictedEntity entity, int ownerId)
        {
            if (entity == null)
                return;
            
            int prevConnId = _entityToOwnerConnId.GetValueOrDefault(entity, 0);
            Debug.Log($"[PredictionManager][SetEntityOwner] SERVER ({entity.id}) prev:{prevConnId} ownerId:{ownerId} entity:{entity}");

            if (prevConnId == ownerId)
                return;
            
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
        
        public void AddPredictedEntity(ServerPredictedEntity entity)
        {
            if (entity == null)
                return;
            
            uint id = entity.id;
            Debug.Log($"[PredictionManager][AddPredictedEntity] SERVER ({id})=>({entity})");
            
            _serverEntityToId[entity] = id;
            _idToServerEntity[id] = entity;
            _predictedEntitiesGO.Add(entity.gameObject);
            _predictedEntities.Add(entity.gameObject.GetComponent<PredictedEntity>());
            
            if (useServerWorldStateMessage)
            {
                _worldStateRecord.Resize(_serverEntityToId.Count);
            }
        }

        public void AddPredictedEntity(ClientPredictedEntity entity)
        {
            if (entity == null)
                return;

            uint id = entity.id;
            Debug.Log($"[PredictionManager][AddPredictedEntity] CLIENT ({id})=>({entity})");
            
            _clientEntities[id] = entity;
            _predictedEntitiesGO.Add(entity.gameObject);
            _predictedEntities.Add(entity.gameObject.GetComponent<PredictedEntity>());
            entity.SetSingleStateEligibilityCheckHandler(SNAPSHOT_INSTANCE_RESIM_CHECKER.Check);
            
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
            _predictedEntitiesGO.Remove(entity.gameObject); 
            _predictedEntities.Remove(entity.gameObject.GetComponent<PredictedEntity>());
            if (useServerWorldStateMessage)
            {
                _worldStateRecord.Resize(_serverEntityToId.Count);
            }
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
                _predictedEntitiesGO.Remove(ent.gameObject);
                _predictedEntities.Remove(ent.gameObject.GetComponent<PredictedEntity>());
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
            return _predictedEntitiesGO.Contains(entity);
        }

        public bool IsPredicted(Rigidbody entity)
        {
            if (!entity)
                return false;
            return _predictedEntitiesGO.Contains(entity.gameObject);
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
                lastClientAppliedTick = 0;
            }
        }

        public void SetLocalEntity(AbstractPredictedEntity entity)
        {
            SetLocalEntity(entity.id);
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
        //TODO: package private
        public void Tick()
        {    
            if (!setup) 
                return;
            
            tickId++;
            ClientPreSimTick();
            ServerPreSimTick();
            PHYSICS_CONTROLLED.Simulate();
            ClientPostSimTick();
            ServerPostSimTick();
        }

        int PredictionDecisionToInt(PredictionDecision decision)
        {
            switch (decision)
            {
                case PredictionDecision.NOOP: return 0;
                case PredictionDecision.SNAP: return 1;
                case PredictionDecision.RESIMULATE: return 2;
            }
            return 0;
        }

        PredictionDecision IntToPredictionDecision(int code)
        {
            switch (code)
            {
                case 1: return PredictionDecision.SNAP;
                case 2: return PredictionDecision.RESIMULATE;
            }
            return PredictionDecision.NOOP;
        }
        
        //TODO: package private
        public PredictionDecision ComputePredictionDecision(out uint resimFromTickId)
        {
            int decisionCode = 0;
            resimFromTickId = uint.MaxValue;
            
            bool localAsksResimulation = false;
            int totalResimulationDecisions = 0;
            
            foreach (KeyValuePair<uint, ClientPredictedEntity> pair in _clientEntities)
            {
                PredictionDecision decision =
                    pair.Value.GetPredictionDecision(lastClientAppliedTick, out uint localFromTick);
                int crnt = PredictionDecisionToInt(decision);
                if (crnt > decisionCode)
                {
                    decisionCode = crnt;
                }
                if (decision == PredictionDecision.RESIMULATE)
                {
                    totalResimulationDecisions++;
                    if (pair.Value == localEntity)
                    {
                        localAsksResimulation = true;
                    } 
                    resimFromTickId = Math.Min(resimFromTickId, localFromTick);
                }
            }
            
            if (totalResimulationDecisions == 1 && localAsksResimulation)
            {
                totalResimulationsDueToAuthority++;
            }
            if (totalResimulationDecisions > 1 && localAsksResimulation)
            {
                totalResimulationsDueToBoth++;
            }
            if (!localAsksResimulation && totalResimulationDecisions > 0)
            {
                totalResimulationsDueToFollowers++;
            }
            
            return IntToPredictionDecision(decisionCode);
        }

        void ClientResimulationCheckPass()
        {
            if (isClient && !isServer)
            {
                PredictionDecision decision = ComputePredictionDecision(out uint fromTick);
                
                //OVERSIMULATION PROTECTION
                if (protectFromOversimulation 
                    && maxResimulationOverbudget > 0
                    && decision == PredictionDecision.RESIMULATE 
                    && totalResimulationStepsOverbudget > maxResimulationOverbudget)
                {
                    decision = PredictionDecision.NOOP;
                    totalResimulationsSkipped++;
                }
                
                switch (decision)
                {
                    case PredictionDecision.NOOP:
                        if (totalResimulationStepsOverbudget > 0)
                        {
                            totalResimulationStepsOverbudget--;
                        }
                        break;
                    
                    case PredictionDecision.RESIMULATE:
                        Resimulate(fromTick);
                        break;
                    
                    case PredictionDecision.SNAP:
                        Snap();
                        break;
                }
            }
        }
        
        //TODO: unit test this
        public bool correctWholeWorldWhenResimulating = true;
        void Resimulate(uint startTick)
        {
            PHYSICS_CONTROLLED.BeforeResimulate(null);
            PHYSICS_CONTROLLED.Rewind(lastClientAppliedTick - startTick);
            resimulation.Dispatch(true);
            
            //Snap to correct state reported by server for all relevant objects
            foreach (KeyValuePair<uint, ClientPredictedEntity> pair in _clientEntities)
            {
                if (correctWholeWorldWhenResimulating || pair.Value.GetPredictionDecision(lastClientAppliedTick, out uint localFromTick) == PredictionDecision.RESIMULATE)
                {
                    pair.Value.SnapToServer(startTick);
                    pair.Value.PostResimulationStep(startTick);
                }
            }
            
            uint index = startTick + 1;
            while (index <= lastClientAppliedTick)
            {
                foreach (KeyValuePair<uint, ClientPredictedEntity> pair in _clientEntities)
                {
                    pair.Value.PreResimulationStep(index);
                }
                PHYSICS_CONTROLLED.Resimulate(null);
                foreach (KeyValuePair<uint, ClientPredictedEntity> pair in _clientEntities)
                {
                    pair.Value.PostResimulationStep(index);
                }
                
                index++;
                totalResimulationSteps++;
                totalResimulationStepsOverbudget++;
            }
            
            totalResimulations++;
            resimulation.Dispatch(false);
            PHYSICS_CONTROLLED.AfterResimulate(null);
        }

        //TODO: unit test this
        void Snap()
        {
            foreach (KeyValuePair<uint, ClientPredictedEntity> pair in _clientEntities)
            {
                if (pair.Value.GetPredictionDecision(lastClientAppliedTick, out uint localFromTick) == PredictionDecision.SNAP)
                {
                    pair.Value.SnapToServer(localFromTick);
                }
            } 
        }
        
        void ClientPreSimTick()
        {
            if (isClient)
            {
                //Uses latest update for each follower
                ClientResimulationCheckPass();
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
                                    Debug.Log($"[PredictionManager][ClientPreSimTick][SEND] (id:{pair.Value.id}) tick:{tickId} data:{tickInputRecord} sndr:{clientStateSender}");
                                clientStateSender?.Invoke(tickId, tickInputRecord);
                            }
                            catch (Exception e)
                            {
                                EntityProcessingError err;
                                err.exception = e;
                                err.entityId = pair.Value.id;
                                onClientStateSendError.Dispatch(err);
                            }   
                        }
                    }
                    else if (!isServer)
                    {
                        //Only run this on the pure client yo... 
                        //Uses tickId 
                        pair.Value.ClientFollowerSimulationTick(tickId);
                    }
                }

                if (localEntity == null)
                {
                    SendSpectatorHeartbeat(tickId);
                }
            }
        }

        void SendSpectatorHeartbeat(uint tickId)
        {
            clientHeartbeadSender?.Invoke(tickId);
        }

        void ClientPostSimTick()
        {
            if (isClient)
            {
                foreach (KeyValuePair<uint, ClientPredictedEntity> pair in _clientEntities)
                {
                    pair.Value.SamplePhysicsState(tickId);
                }
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
                    if (id != localEntityId)
                    {
                        MarkLatestAppliedTickId(entity.ServerSimulationTick(), entity);
                    }
                }
            }
        }

        void ServerPostSimTick()
        {
            if (!isServer)
                return;
            
            if (useServerWorldStateMessage)
            {
                _worldStateRecord.WriteReset();
            }
            
            foreach (KeyValuePair<ServerPredictedEntity, uint> pair in _serverEntityToId)
            {
                ServerPredictedEntity entity = pair.Key;
                uint id = pair.Value;
                PhysicsStateRecord state = entity.SamplePhysicsState();
                if (id == localEntityId)
                {
                    state.input = localEntity.GetLastInput();
                }
                if (DEBUG)
                    Debug.Log($"[PredictionManager][ServerPostSimTick] id:{id} update:{state}");
                
                if (useServerWorldStateMessage)
                {
                    AccumulateWorldState(id, state);
                }
                else
                {
                    SendServerState(id, state);
                }
            }
            if (useServerWorldStateMessage)
            {
                SendWorldState(_worldStateRecord);
            }
        }
        
        void MarkLatestAppliedTickId(uint tickId, ServerPredictedEntity entity)
        {
            //FODO: performance
            if (!_entityToOwnerConnId.ContainsKey(entity))
                return;
            
            int connId = _entityToOwnerConnId[entity];
            _connIdToLatestTick[connId] = tickId;
        }
        
        //FODO: performance
        uint GetLatestAppliedTickForConnection(int connId)
        {
            return _connIdToLatestTick.GetValueOrDefault(connId, tickId);
        }

        public void OnHeartbeatReceived(int connectionId, uint tickId)
        {
            _connIdToLatestTick[connectionId] = tickId;
        }
        
        public void OnServerWorldStateReceived(WorldStateRecord wsr)
        {
            if (DEBUG)
                Debug.Log($"[PredictionManager][OnServerWorldStateReceived] WorldState:{wsr}]");
            
            if (isClient && !isServer)
            {
                for (int i = 0; i < wsr.fill; i++)
                {
                    wsr.states[i].tickId = wsr.tickId;
                    OnServerStateReceived(wsr.entityIDs[i], wsr.states[i]);
                }
            }
        }
        
        public void OnServerStateReceived(uint entityId, PhysicsStateRecord stateRecord)
        {
            if (!isClient)
                return;
            
            if (DEBUG)
                Debug.Log($"[PredictionManager][OnServerStateReceived] entityId:{entityId} stateRecord:{stateRecord}");
            
            ClientPredictedEntity entity = _clientEntities.GetValueOrDefault(entityId, null);
            if (entity != null && (entityId == localEntityId || (isClient && !isServer)))
            {
                entity.BufferServerTick(lastClientAppliedTick, stateRecord);
            }
        }

        public uint clientStatesReceived = 0;
        public void OnClientStateReceived(int connId, uint clientTickId, PredictionInputRecord tickInputRecord)
        {
            if (!isServer)
                return;
            
            if (connId != 0)
                clientStatesReceived++;
            
            ServerPredictedEntity entity = _connIdToEntity.GetValueOrDefault(connId);
            if (DEBUG)
                Debug.Log($"[PredictionManager][OnClientStateReceiver] connId:{connId} clientTickId:{clientTickId} tickInputRecord:{tickInputRecord} ENTITY:{entity}");
            entity?.BufferClientTick(clientTickId, tickInputRecord);
        }

        void SendServerState(uint entityId, PhysicsStateRecord stateRecord)
        {
            IEnumerable<int> connections = connectionsIterator();
            foreach (int connId in connections)
            {
                uint connTickId = GetLatestAppliedTickForConnection(connId);
                try
                {
                    stateRecord.tickId = connTickId;
                    serverStateSender?.Invoke(connId, entityId, stateRecord);
                }
                catch (Exception e)
                {
                    ServerUpdateSendError err;
                    err.exception = e;
                    err.entityId = entityId;
                    err.connId = connId;
                    err.tickId = connTickId;
                    onServerStateSendError.Dispatch(err);
                }   
            }
        }

        void AccumulateWorldState(uint entityId, PhysicsStateRecord stateRecord)
        {
            _worldStateRecord.Set(entityId, stateRecord);
        }

        void SendWorldState(WorldStateRecord record)
        {
            IEnumerable<int> connections = connectionsIterator();
            foreach (int connId in connections)
            {
                uint connTickId = GetLatestAppliedTickForConnection(connId);
                try
                {
                    record.tickId = connTickId;
                    serverWorldStateSender?.Invoke(connId, record);
                }
                catch (Exception e)
                {
                    ServerUpdateSendError err;
                    err.exception = e;
                    err.entityId = 0;
                    err.connId = connId;
                    err.tickId = connTickId;
                    onServerStateSendError.Dispatch(err);
                }   
            }
        }
        
        public static uint GetServerTickDelay()
        {
            return (uint) Mathf.CeilToInt((float)(ROUND_TRIP_GETTER() / Time.fixedDeltaTime));
        }
        
        public struct EntityProcessingError
        {
            public Exception exception;
            public uint entityId;
        }
        
        public struct ServerUpdateSendError
        {
            public Exception exception;
            public int connId;
            public uint entityId;
            public uint tickId;
        }
        
        bool CanResiumlate()
        {
            //return !protectFromOversimulation || () < maxAllowedAvgResimPerTick;
            return !protectFromOversimulation || totalResimulationStepsOverbudget == 0;
        }

        public uint GetResimulationOverbudget()
        {
            return totalResimulationStepsOverbudget;
        }
        
        public uint GetTotalTicks()
        {
            return lastClientAppliedTick;
        }
        
        public uint GetAverageResimPerTick()
        {
            if (lastClientAppliedTick == 0)
                return 0;
            return totalResimulationSteps / lastClientAppliedTick;
        }
        
        public SafeEventDispatcher<ServerUpdateSendError> onServerStateSendError = new();
        public SafeEventDispatcher<EntityProcessingError> onClientStateSendError = new();
        public SafeEventDispatcher<bool> resimulation = new();
        public SafeEventDispatcher<bool> resimulationStep = new();
    }
}