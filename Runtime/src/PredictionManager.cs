using System;
using System.Collections.Generic;
using Sector0.Events;
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
        public static bool DO_RESIM = true;
        public static bool DO_SNAP = true;
        
        //TODO: unit test
        public static bool IGNORE_NON_AUTH_RESIM_DECISIONS = false;
        public static bool IGNORE_CONTROLLABLE_FOLLOWER_DECISIONS = true;
        public static bool LOG_PRE_SIM_STATE = false;
        public static bool PREDICTION_ENABLED = true;
        
        //TODO: guard singleton
        public static PredictionManager Instance;
        //TODO: validate presence of all static providers
        public static Func<VisualsInterpolationsProvider> INTERPOLATION_PROVIDER = () => new MovingAverageInterpolator();
        public static SingleSnapshotInstanceResimChecker SNAPSHOT_INSTANCE_RESIM_CHECKER = new SimpleConfigurableResimulationDecider();
        public static PhysicsController PHYSICS_CONTROLLER = new RewindablePhysicsController();
        //TODO: do we still need this?
        public static Func<double> ROUND_TRIP_GETTER;
        
        //TODO: protected
        //TODO: you don't need this anymore if each entity can provide you with their id...
        public Dictionary<ServerPredictedEntity, uint> _serverEntityToId = new Dictionary<ServerPredictedEntity, uint>();
        private Dictionary<uint, ServerPredictedEntity> _idToServerEntity = new Dictionary<uint, ServerPredictedEntity>();
        private Dictionary<ServerPredictedEntity, int> _entityToOwnerConnId = new Dictionary<ServerPredictedEntity, int>();
        private Dictionary<int, ServerPredictedEntity> _connIdToEntity = new Dictionary<int, ServerPredictedEntity>();
        public Dictionary<int, uint> _connIdToLatestTick = new Dictionary<int, uint>();
        //TODO: protected
        public Dictionary<uint, ClientPredictedEntity> _clientEntities = new Dictionary<uint, ClientPredictedEntity>();
        public HashSet<PredictedEntity> _predictedEntities = new HashSet<PredictedEntity>();
        private HashSet<GameObject> _predictedEntitiesGO = new HashSet<GameObject>();

        [SerializeField] private GameObject localGO;
        private ClientPredictedEntity localEntity;
        private uint localEntityId;

        public bool isClient;
        public bool isServer;
        //TODO: package private
        public uint tickId = 1;
        private bool setup = false;
        public bool autoTrackRigidbodies = true;
        public bool useServerWorldStateMessage = false;

        //NOTE: heartbeats are only sent when no predicted entity is controlled locally
        //         tickId
        public Action<uint>                       clientHeartbeadSender;
        //            tickId, inputData
        public Action<uint, PredictionInputRecord>       clientStateSender;
        // connectionId, entityId, state
        public Action<int, uint, PhysicsStateRecord>    serverStateSender;
        // connectionId, world state
        public Action<int, WorldStateRecord> serverWorldStateSender;
        // connectionId, entityId, controlledLocally
        public Action<int, uint, bool>    serverSetControlledLocally;
        
        public Func<IEnumerable<int>> connectionsIterator;
        private WorldStateRecord _worldStateRecord = new WorldStateRecord();
        
        public bool snapOnSimSkip = false;
        //NOTE: either use protectFromOversimulation or TRUST_ALREADY_RESIMULATED_TICKS, no both
        public bool protectFromOversimulation = true;
        public uint maxTickResimulationCount = 1;
        private Dictionary<uint, uint> tickResimCounter = new Dictionary<uint, uint>();

        public uint totalResimulationsDueToAuthority = 0;
        public uint totalResimulationsDueToFollowers = 0;
        public uint totalResimulationsDueToBoth = 0;
        
        public uint totalResimulations = 0;
        public uint totalResimulationSteps = 0;
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
            PHYSICS_CONTROLLER.Setup(isServer);
            Debug.Log($"[PredictionManager] isServer:{isServer} isClient:{isClient}");
        }

        void Validate()
        {
            if (isClient)
            {
                if (clientHeartbeadSender == null)
                {
                    throw new Exception(
                        "INVALID_CONFIG: isClient = true but no clientHeartbeatSender provided");
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
                if (serverSetControlledLocally == null)
                {
                    throw new Exception("INVALID_CONFIG: no serverSetControlledLocally provided");
                }
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

            if (PHYSICS_CONTROLLER == null)
            {
                throw new Exception(
                    "INVALID_CONFIG: No Physics Controller provided.");
            }
        }

        private void Cleanup()
        {
            Instance = null;
        }

        public int GetOwner(ServerPredictedEntity entity)
        {
            return _entityToOwnerConnId.GetValueOrDefault(entity, -1);
        }

        public ServerPredictedEntity GetEntity(int ownerId)
        {
            return _connIdToEntity.GetValueOrDefault(ownerId, null);
        }
        
        //TODO: unit test!
        public void SetEntityOwner(ServerPredictedEntity entity, int ownerId)
        {
            if (!isServer)
                throw new Exception("INVALID_USE: called SetEntityOwner on non server entity");
            
            if (entity == null)
                return;
            
            Debug.Log($"[PredictionManager][SetEntityOwner] SERVER ({entity.id}) ownerId:{ownerId} entity:{entity}");
            if (_connIdToEntity.GetValueOrDefault(ownerId, null) == entity)
            {
                //NOOP
                return;
            }
            
            UnsetOwnership(ownerId);
            UnsetOwnership(entity);
            SetOwnership(entity, ownerId);
        }

        public void UnsetOwnership(ServerPredictedEntity entity)
        {
            if (_entityToOwnerConnId.ContainsKey(entity))
            {
                UnsetOwnership(_entityToOwnerConnId[entity]);    
            }
        }
        
        //TODO: unit test
        public void UnsetOwnership(int ownerId)
        {
            if (!isServer)
                throw new Exception("INVALID_USE: called SetEntityOwner on non server entity");
            
            ServerPredictedEntity entity = _connIdToEntity.GetValueOrDefault(ownerId, null);
            Debug.Log($"[PredictionManager][UnsetOwnership] SERVER ownerId:{ownerId} entity:{entity}");
            if (entity != null)
            {
                _connIdToEntity.Remove(ownerId);
                _entityToOwnerConnId.Remove(entity);
                entity.Reset(); //Prepare for new stream of tickIds
                try
                {
                    serverSetControlledLocally.Invoke(ownerId, entity.id, false);
                }
                catch (Exception e)
                {
                    //TODO: event
                }
            }
        }
    
        //TODO: unit test
        void SetOwnership(ServerPredictedEntity entity, int ownerId)
        {
            if (entity == null)
                return;
            
            _entityToOwnerConnId[entity] = ownerId;
            _connIdToEntity[ownerId] = entity;
            entity.Reset(); //Prepare for new stream of tickIds
            serverSetControlledLocally.Invoke(ownerId, entity.id, true);
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
                PHYSICS_CONTROLLER.Track(entity.rigidbody);
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
                    PHYSICS_CONTROLLER.Untrack(ent.rigidbody);
                }
            }
            if (id == localEntityId && isClient)
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
        
        //TODO: unit test
        void SetLocalEntity(uint id)
        {
            if (!isClient)
                throw new Exception($"INVALID_USAGE: called SetLocalEntity on non client instance!");
            
            if (DEBUG)
                Debug.Log($"[PredictionManager][SetLocalEntity]({id})");

            if (localEntityId == id)
                return;
            
            UnsetLocalEntity();
            localEntity = _clientEntities.GetValueOrDefault(id, null);
            if (localEntity != null)
            {
                //FUDO: consider moving the id fetching mechanic inside entity
                localEntityId = id;
                localGO = localEntity.gameObject;
                localEntity.SetControlledLocally(true);
            }
        }
        
        //TODO: unit test
        void UnsetLocalEntity(uint id)
        {
            if (!isClient)
                throw new Exception($"INVALID_USAGE: called UnsetLocalEntity on non client instance!");
            
            if (localEntityId == id)
            {
                UnsetLocalEntity();
            }
        }

        void UnsetLocalEntity()
        {
            if (!isClient)
                throw new Exception($"INVALID_USAGE: called UnsetLocalEntity on non client instance!");
            
            if (localEntity != null)
            {
                localEntity.SetControlledLocally(false);
            }
            localEntityId = 0;
            localEntity = null;
            localGO = null;
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
            
            ClientPreSimTick();
            ServerPreSimTick();
            PHYSICS_CONTROLLER.Simulate();
            ClientPostSimTick();
            ServerPostSimTick();
            tickId++;
        }

        public void Clear()
        {
            //TODO: unit test
            tickId = 1;
            UnsetLocalEntity();
            // CLEAR ALL TRACKING
            _serverEntityToId.Clear();
            _idToServerEntity.Clear();
            _entityToOwnerConnId.Clear();
            _connIdToEntity.Clear();
            _connIdToLatestTick.Clear();
            _clientEntities.Clear();
            _predictedEntities.Clear();
            _predictedEntitiesGO.Clear();
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

        bool ShouldIgnoreResimulationDecision(ClientPredictedEntity entity)
        {
            return (IGNORE_NON_AUTH_RESIM_DECISIONS && entity.id != localEntityId) ||
                   (IGNORE_CONTROLLABLE_FOLLOWER_DECISIONS && entity.IsControllable() && entity.id != localEntityId);
        }
        
        //TODO: package private
        //TODO: ability to only resimulate for locally controlled object
        public PredictionDecision ComputePredictionDecision(out uint resimFromTickId)
        {
            int decisionCode = 0;
            resimFromTickId = uint.MaxValue;
            
            bool localAsksResimulation = false;
            int totalResimulationDecisions = 0;
            
            foreach (KeyValuePair<uint, ClientPredictedEntity> pair in _clientEntities)
            {
                PredictionDecision decision =
                    pair.Value.GetPredictionDecision(tickId, out uint localFromTick);
                if (ShouldIgnoreResimulationDecision(pair.Value))
                {
                    localFromTick = resimFromTickId;
                    decision = PredictionDecision.NOOP;
                }
                
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
                if (decision == PredictionDecision.RESIMULATE && !CanResiumlate(fromTick))
                {
                    decision = PredictionDecision.NOOP;
                    totalResimulationsSkipped++;
                }
                
                switch (decision)
                {
                    case PredictionDecision.NOOP:
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
        
        //TODO: unit test this!!!
        public bool resimUseAvailableServerTicks = true;
        public bool correctWholeWorldWhenResimulating = true;
        public uint resimSkipNotEnoughHistory = 0;
        public bool resimulating = false;
        public uint maxRewindDistance = 0;
        public uint totalRewindDistance = 0;
        void Resimulate(uint startTick)
        {
            if (!DO_RESIM)
                return;
            
            if (tickId <= startTick)
            {
                //NOTE: this shouldn't be possible
                Debug.Log($"[PredictionManager][Resimulate] tickId:{tickId} startTick:{startTick}. Are you misusing the system? startTick cannot be larger or equal than the current tickId");
                resimSkipNotEnoughHistory++;
                return;
            }

            resimulating = true;
            uint rewind = tickId - startTick;
            if (!PHYSICS_CONTROLLER.Rewind(rewind))
            {
                resimSkipNotEnoughHistory++;
                resimulating = false;
                return;
            }
            
            //TODO: decide what to do with these hooks...
            PHYSICS_CONTROLLER.BeforeResimulate(null);
            if (maxRewindDistance < rewind)
            {
                maxRewindDistance = rewind;
            }
            totalRewindDistance += rewind;
            resimulation.Dispatch(true);
            
            //Snap to correct state reported by server for all relevant objects
            foreach (KeyValuePair<uint, ClientPredictedEntity> pair in _clientEntities)
            {
                if (correctWholeWorldWhenResimulating || pair.Value.GetPredictionDecision(tickId, out uint localFromTick) == PredictionDecision.RESIMULATE)
                {
                    pair.Value.SnapToServer(startTick);
                    pair.Value.PostResimulationStep(startTick);
                }
            }
            //All relevant bodies are now at the end of startTick
            MarkResimulatedTick(startTick);
            
            uint index = startTick + 1;
            while (index < tickId)
            {
                foreach (KeyValuePair<uint, ClientPredictedEntity> pair in _clientEntities)
                {
                    //Note: this will run logic on local authority: fetchInput, loadInput, applyForces
                    pair.Value.PreResimulationStep(index);
                }
                PHYSICS_CONTROLLER.Resimulate(null);
                foreach (KeyValuePair<uint, ClientPredictedEntity> pair in _clientEntities)
                {
                    if (resimUseAvailableServerTicks)
                    {
                        //NOTE: the resimulation step may have caused slightly different position and rotation, and also may have caused triggering of OnCollision events for those positions
                        pair.Value.SnapToServerIfExists(index);
                    }
                    pair.Value.PostResimulationStep(index);
                }
                
                MarkResimulatedTick(index);
                index++;
                totalResimulationSteps++;
                
            }
            
            totalResimulations++;
            resimulation.Dispatch(false);
            PHYSICS_CONTROLLER.AfterResimulate(null);
            resimulating = false;
        }
        
        void MarkResimulatedTick(uint tid)
        {
            if (protectFromOversimulation)
            {
                tickResimCounter[tid] = tickResimCounter.GetValueOrDefault(tid, 0u) + 1;
            }
        }

        //TODO: unit test this
        void Snap()
        {
            if (!DO_SNAP)
                return;
            
            foreach (KeyValuePair<uint, ClientPredictedEntity> pair in _clientEntities)
            {
                if (pair.Value.GetPredictionDecision(tickId, out uint localFromTick) == PredictionDecision.SNAP)
                {
                    pair.Value.SnapToServer(localFromTick);
                }
            } 
        }

        public uint clientSendErrors = 0;
        void ClientPreSimTick()
        {
            if (isClient)
            {
                //Uses latest update for each follower
                if (PREDICTION_ENABLED)
                {
                    ClientResimulationCheckPass();
                }
                foreach (KeyValuePair<uint, ClientPredictedEntity> pair in _clientEntities)
                {
                    if (pair.Key == localEntityId)
                    {
                        if (DEBUG)
                            Debug.Log($"[PredictionManager][ClientPreSimTick] Client:{pair.Value} tick:{tickId}");
                        
                        PredictionInputRecord tickInputRecord;
                        if (PREDICTION_ENABLED || isServer)
                        {
                           tickInputRecord = pair.Value.ClientSimulationTick(tickId);
                        }
                        else
                        {
                            tickInputRecord = pair.Value.SampleInput(tickId);
                        }
                        
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
                                clientSendErrors++;
                                EntityProcessingError err;
                                err.exception = e;
                                err.entityId = pair.Value.id;
                                onClientStateSendError.Dispatch(err);
                            }   
                        }
                    }
                    else if (!isServer)
                    {
                        //Only run this on the pure client
                        pair.Value.ClientFollowerSimulationTick(tickId);
                    }

                    if (LOG_PRE_SIM_STATE)
                    {
                        Debug.Log($"[CL][PRESIMULATION][DATA] i:{pair.Key} t:{tickId} p:{pair.Value.rigidbody.position.ToString("F10")} r:{pair.Value.rigidbody.rotation.ToString("F10")}");
                    }
                }

                if (localEntity == null)
                {
                    SendSpectatorHeartbeat(tickId);
                }
            }
        }

        void SendSpectatorHeartbeat(uint tid)
        {
            try
            {
                clientHeartbeadSender?.Invoke(tid);
            }
            catch (Exception e)
            {
                clientSendErrors++;
                //TODO: event?
            }   
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
        
        void MarkLatestAppliedTickId(uint tid, ServerPredictedEntity entity)
        {
            //FUDO: performance
            if (!_entityToOwnerConnId.ContainsKey(entity))
                return;
            
            int connId = _entityToOwnerConnId[entity];
            _connIdToLatestTick[connId] = tid;
            
            if (LOG_PRE_SIM_STATE)
            {
                Debug.Log($"[SV][PRESIMULATION][DATA] i:{entity.id} t:{tickId} p:{entity.rigidbody.position.ToString("F10")} r:{entity.rigidbody.rotation.ToString("F10")}");
            }
        }
        
        //FODO: performance
        uint GetLatestAppliedTickForConnection(int connId)
        {
            return _connIdToLatestTick.GetValueOrDefault(connId, tickId);
        }

        public void OnHeartbeatReceived(int connectionId, uint tid)
        {
            _connIdToLatestTick[connectionId] = tid;
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
                entity.BufferServerTick(tickId, stateRecord);
            }
        }

        public void OnEntityOwnershipChanged(uint entityId, bool owned)
        {
            if (!isClient)
                throw new Exception("COMPONENT_MISUSE: OnEntityOwnershipChanged called on non client entity");
            if (DEBUG)
                Debug.Log($"[PredictionManager][OnEntityOwnershipChanged] entityId:{entityId} owned:{owned}");

            if (owned)
            {
                SetLocalEntity(entityId);
            }
            else
            {
                UnsetLocalEntity(entityId);
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
        
        bool CanResiumlate(uint tid)
        {
            return !protectFromOversimulation || tickResimCounter.GetValueOrDefault(tid, 0u) < maxTickResimulationCount;
        }
        
        public uint GetTotalTicks()
        {
            return tickId;
        }
        
        public uint GetAverageResimPerTick()
        {
            return totalResimulationSteps / tickId;
        }
        
        public SafeEventDispatcher<ServerUpdateSendError> onServerStateSendError = new();
        public SafeEventDispatcher<EntityProcessingError> onClientStateSendError = new();
        public SafeEventDispatcher<bool> resimulation = new();
        public SafeEventDispatcher<bool> resimulationStep = new();
    }
}