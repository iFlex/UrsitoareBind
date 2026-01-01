using System;
using System.Collections.Generic;
using Assets.Scripts.Systems.Events;
using Prediction.data;
using Prediction.policies.singleInstance;
using Prediction.utils;
using UnityEngine;

namespace Prediction
{
    //TODO: document in readme
    public class ClientPredictedEntity : AbstractPredictedEntity
    {
        public static bool DEBUG = false;
        public static bool LOG_USED_INPUTS = false;
        
        //STATE TRACKING
        public GameObject gameObject;
        // entityId, tickId, localHist, serverHist
        public Func<uint, uint, RingBuffer<PhysicsStateRecord>, TickIndexedBuffer<PhysicsStateRecord>, PredictionDecision>
            resimulationEligibilityCheckHook
        {
            get; 
            private set; 
        }
        
        // entityId, tickId, localSnapshot, serverSnapshot
        public Func<uint, uint, PhysicsStateRecord, PhysicsStateRecord, PredictionDecision> singleStateResimulationEligibilityHook
        {
            get;
            private set;
        }
        
        //STATE
        //TODO: make visible for testing in tests assembly
        public RingBuffer<PredictionInputRecord> localInputBuffer;
        //TODO: make visible for testing in tests assembly
        public RingBuffer<PhysicsStateRecord> localStateBuffer;
        //TODO: make visible for testing in tests assembly
        public TickIndexedBuffer<PhysicsStateRecord> serverStateBuffer;
        private bool isServer;
        
        //This is used exclusively in follower mode (predicted entity not controlled by user).
        public bool isControlledLocally { get; private set; }
        
        //STATS
        public uint lastTick = 0;
        public uint totalTicks = 0;
        public uint ticksAsFollower = 0;
        public uint ticksAsLocalAuthority = 0;
        public uint resimTicks = 0;
        public uint resimTicksAsAuthority = 0;
        public uint resimTicksAsFollower = 0;
        public uint maxServerDelay = 0;
        
        public ClientPredictedEntity(uint id, bool isServer, int bufferSize, Rigidbody rb, GameObject visuals, PredictableControllableComponent[] controllablePredictionContributors, PredictableComponent[] predictionContributors) : base(id, rb, visuals, controllablePredictionContributors, predictionContributors)
        {
            rigidbody = rb;
            gameObject = rb.gameObject;
            detachedVisualsIdentity = visuals;
            this.isServer = isServer;
            
            localInputBuffer = new RingBuffer<PredictionInputRecord>(bufferSize);
            localStateBuffer = new RingBuffer<PhysicsStateRecord>(bufferSize);
            
            this.controllablePredictionContributors = controllablePredictionContributors ?? Array.Empty<PredictableControllableComponent>();
            this.predictionContributors = predictionContributors ?? Array.Empty<PredictableComponent>();
            
            serverStateBuffer = new TickIndexedBuffer<PhysicsStateRecord>(bufferSize);
            serverStateBuffer.emptyValue = null;
            
            for (int i = 0; i < controllablePredictionContributors.Length; i++)
            {
                totalFloatInputs += controllablePredictionContributors[i].GetFloatInputCount();
                totalBinaryInputs += controllablePredictionContributors[i].GetBinaryInputCount();
            }
            
            for (int i = 0; i < bufferSize; i++)
            {
                localInputBuffer.Add(new PredictionInputRecord(totalFloatInputs, totalBinaryInputs));
                localStateBuffer.Add(new PhysicsStateRecord());
            }
        }
        
        public void SetCustomEligibilityCheckHandler(Func<uint, uint, RingBuffer<PhysicsStateRecord>, TickIndexedBuffer<PhysicsStateRecord>, PredictionDecision> handler)
        {
            resimulationEligibilityCheckHook = handler;
            singleStateResimulationEligibilityHook = null;
        }

        public void SetSingleStateEligibilityCheckHandler(Func<uint, uint, PhysicsStateRecord, PhysicsStateRecord, PredictionDecision> handler)
        {
            resimulationEligibilityCheckHook = _defaultResimulationEligibilityCheck;
            singleStateResimulationEligibilityHook = handler;
        }

        public PredictionInputRecord ClientSimulationTick(uint tickId)
        {
            if (!isControlledLocally)
            {
                throw new Exception("COMPONENT_MISUSE: NON locally controlled entity called ClientSimulationTick");
            }
            
            PredictionInputRecord inputRecord = SampleInput(tickId);
            LoadInput(inputRecord);
            ApplyForces();

            lastTick = tickId;
            ticksAsLocalAuthority++;
            totalTicks++;
            uint serverDelay = GetServerDelay();
            if (serverDelay > maxServerDelay)
            {
                maxServerDelay = serverDelay;
            }
            return inputRecord;
        }

        public PredictionInputRecord GetLastInput()
        {
            return localInputBuffer.Get((int)lastTick);
        }
    
        //TODO: unit test
        public void ClientFollowerSimulationTick(uint tickId)
        {
            if (isControlledLocally)
            {
                throw new Exception("COMPONENT_MISUSE: locally controlled entity called ClientFollowerSimulationTick");
            }
            
            PhysicsStateRecord psr = serverStateBuffer.GetEnd();
            if (psr != null)
            {
                PredictionInputRecord input = psr.input;
                if (input != null)
                {
                    LoadInput(input);
                    if (DEBUG)
                        Debug.Log($"[ClientPredictedEntiy][ClientFollowerSimulationTick][OK] entityId:{id} withInput:{input} psr:{psr}");
                }
                else
                {
                    if (DEBUG)
                        Debug.Log($"[ClientPredictedEntiy][ClientFollowerSimulationTick][NO_INPT] entityId:{id} Missing input");
                }
            }
            else
            {
                if (DEBUG)
                    Debug.Log($"[ClientPredictedEntiy][ClientFollowerSimulationTick][MISSING] entityId:{id} Missing end of buffer...");
            }
            ApplyForces();
            ticksAsFollower++;
            totalTicks++;
        }

        PredictionInputRecord SampleInput(uint tickId)
        {
            //TODO: correctly convert tick to index!
            PredictionInputRecord inputData = localInputBuffer.Get((int)tickId);
            SampleInput(inputData);
            if (LOG_USED_INPUTS)
            {
                Debug.Log($"[SIMULATION][INPUT] i:{id} t:{tickId} input:{inputData}");
            }
            return inputData;
        }
        
        void SampleInput(PredictionInputRecord inputRecord)
        {
            inputRecord.WriteReset();
            //NOTE: sampling in the exact same order all the time on both server and client is critical!
            for (int i = 0; i < controllablePredictionContributors.Length; ++i)
            {
                //NOTE: this samples the input of each component and stores it in the inputRecord as a side effect.
                //TODO: what about write skips?
                //TODO: what about adding & removing components?
                //TODO: what about new components? how can we keep it backward compatible?
                controllablePredictionContributors[i].SampleInput(inputRecord);
            }
        }
        
        public void SamplePhysicsState(uint tickId)
        {
            //TODO: correctly convert tick to index!
            PhysicsStateRecord stateData = localStateBuffer.Get((int)tickId);
            //NOTE: this samples the physics state of the predicted entity and stores it in the localStateBuffer as a side effect.
            PopulatePhysicsStateRecord(tickId, stateData);
            newStateReached.Dispatch(stateData);
            
            if (LOG_VELOCITIES_ALL || (LOG_VELOCITIES && isControlledLocally))
            {
                LogState(tickId, false);
            }
        }

        void LogState(uint tickId, bool isResim)
        {
            //TODO: we should maybe just offer hooks for the host application to use instead of direct logging here...
            PhysicsStateRecord serverState = serverStateBuffer.Get(tickId);
            Debug.Log($"{(isResim ? "[RESIMULATION]" : "[SIMULATION]")}[DATA] i:{id} t:{tickId} v:{rigidbody.linearVelocity.magnitude} av:{rigidbody.angularVelocity.magnitude} pos:{rigidbody.position} rot:{rigidbody.rotation} SERVER(v:{(serverState == null ? "x" : serverState.velocity.magnitude)} av:{(serverState == null ? "x" : serverState.angularVelocity.magnitude)} pos:{(serverState == null ? "x" : serverState.position)} rot:{(serverState == null ? "x" : serverState.rotation)})");
        }

        public uint resimChecksSkippedDueToLackOfServerData = 0;
        public uint resimChecksSkippedDueToServerAheadOfClient = 0;
        public static bool LOG_VELOCITIES = false;
        public static bool LOG_VELOCITIES_ALL = false;
        public uint lastCheckedServerTickId = 0;
        
        private DesyncEvent devt;
        public virtual PredictionDecision GetPredictionDecision(uint lastAppliedTick, out uint fromTick)
        {
            fromTick = 0;
            //NOTE: .GetEnt() is always behind lastAppliedTick, so should be fine...
            PhysicsStateRecord serverState = serverStateBuffer.GetEnd();
            //NOTE: somehow the server reports are in the future. Don't resimulate until we get there too
            if (serverState == null)
            {
                devt.reason = DesyncReason.MISSING_SERVER_COMPARISON;
                devt.tickId = lastAppliedTick;
                potentialDesync.Dispatch(devt);
                
                //TODO: unit test
                resimChecksSkippedDueToLackOfServerData++;
                return PredictionDecision.NOOP;
            }

            if (lastAppliedTick <= serverState.tickId)
            {
                devt.reason = DesyncReason.SERVER_AHEAD_OF_CLIENT;
                devt.tickId = lastAppliedTick;
                potentialDesync.Dispatch(devt);
                
                //TODO: unit test?
                resimChecksSkippedDueToServerAheadOfClient++;
                return PredictionDecision.NOOP;
            }

            if (serverState.tickId > lastCheckedServerTickId && (serverState.tickId - lastCheckedServerTickId) > 1)
            {
                devt.reason = DesyncReason.GAP_IN_SERVER_STREAM;
                devt.tickId = lastAppliedTick;
                devt.gapSize = serverState.tickId - lastCheckedServerTickId;
                potentialDesync.Dispatch(devt);
            }
            
            lastCheckedServerTickId = serverState.tickId;
            fromTick = serverState.tickId;
            return resimulationEligibilityCheckHook(id, serverState.tickId, localStateBuffer, serverStateBuffer);
        }

        public uint lastSvTickId = 0;
        public uint oldServerTickCount = 0;
        bool AddServerState(uint lastAppliedTick, PhysicsStateRecord serverRecord)
        {
            if (DEBUG)
                Debug.Log($"[ClientPreditedEntity][AddServerState]({id}) data:{serverRecord}");
            
            //TODO: use lastAppliedTick to determine how old the update is and do stuff about it
            if (serverRecord.tickId < serverStateBuffer.GetEndTick())
            {
                oldServerTickCount++;
            }
            serverStateBuffer.Add(serverRecord.tickId, serverRecord);
            lastSvTickId = serverStateBuffer.GetEndTick();
            return serverRecord.tickId == serverStateBuffer.GetEndTick();
        }
        
        public void BufferServerTick(uint lastAppliedTick, PhysicsStateRecord serverState)
        {
            if (DEBUG)
                Debug.Log($"[ClientPredictedEntity][BufferServerTick](goId:{gameObject.GetInstanceID()}) lastTick:{lastAppliedTick}  serverState:{serverState}");

            AddServerState(lastAppliedTick, serverState);
        }

        PredictionDecision _defaultResimulationEligibilityCheck(uint entityId, uint tickId, RingBuffer<PhysicsStateRecord> clientStates,
            TickIndexedBuffer<PhysicsStateRecord> serverStates)
        {
            //TODO: ensure correct conversion from uint tick to index
            PhysicsStateRecord localState = clientStates.Get((int)tickId);
            PhysicsStateRecord serverState = serverStates.Get(tickId);
            return singleStateResimulationEligibilityHook.Invoke(entityId, tickId, localState, serverState);
        }

        public uint countMissingServerHistory = 0;
        public void SnapToServer(uint tickId)
        {
            PhysicsStateRecord state = serverStateBuffer.Get(tickId);
            if (state == null)
            {
                //TODO: do we need to do something?
                countMissingServerHistory++;

                devt.reason = DesyncReason.SNAP_TO_SERVER_NO_DATA;
                devt.tickId = tickId;
                potentialDesync.Dispatch(devt);
                return;
            }
            
            SnapTo(state);
        }

        void SnapTo(PhysicsStateRecord serverState)
        {
            serverState.To(rigidbody);
        }

        public void PreResimulationStep(uint tickId)
        {
            if (isControlledLocally)
            {
                PreResimulateAuthorityStep(tickId);
            }
            else
            {
                PreResimulationFollowerStep(tickId);
            }
        }

        void PreResimulateAuthorityStep(uint tickId)
        {
            resimulationStep.Dispatch(true);
            //TODO: correct conversion of tickId to index plz
            PredictionInputRecord inputData = localInputBuffer.Get((int) tickId);
            if (LOG_USED_INPUTS)
            {
                Debug.Log($"[RESIMULATION][INPUT] i:{id} t:{tickId} input:{inputData}");
            }
            LoadInput(inputData);
            ApplyForces();
            resimTicks++;
            resimTicksAsAuthority++;
        }
        
        void PreResimulationFollowerStep(uint tickId)
        {
            resimTicks++;
            resimTicksAsFollower++;
            resimulationStep.Dispatch(true);
            //TODO: do we need anything here? apply input from server?
        }

        public static bool TRACK_RESIM_DISCREPANCIES = true;
        private Dictionary<uint, uint> tickResimCounter = new Dictionary<uint, uint>();
        private PhysicsStateRecord prevResimState = new PhysicsStateRecord();
        private SimpleConfigurableResimulationDecider resimDesyncComparator = new SimpleConfigurableResimulationDecider(float.MinValue, float.MinValue, float.MinValue, float.MinValue);
        private uint resimCounter = 0;
        
        public void PostResimulationStep(uint tickId)
        {
            //TODO: check conversion to int
            PhysicsStateRecord record = localStateBuffer.Get((int) tickId);
            
            if (isControlledLocally && TRACK_RESIM_DISCREPANCIES)
            {
                resimCounter = tickResimCounter.GetValueOrDefault(tickId, 0u) + 1;
                prevResimState.From(record);
                tickResimCounter[tickId] = resimCounter;
            }
            
            PopulatePhysicsStateRecord(tickId, record);
            resimulationStep.Dispatch(false);

            if (isControlledLocally && TRACK_RESIM_DISCREPANCIES && resimCounter > 1)
            {
                resimDesyncComparator.Check(0, tickId, prevResimState, record);
                //TODO: manually log here instead o relying on the logging to be on
            }

            if (LOG_VELOCITIES_ALL || (LOG_VELOCITIES && isControlledLocally))
            {
                LogState(tickId, true);
            }
        }

        public uint GetServerDelay()
        {
            return lastTick - serverStateBuffer.GetEndTick();
        }

        public void SetControlledLocally(bool controlled)
        {
            Reset();
            isControlledLocally = controlled;
        }
        
        public void Reset()
        {
            localInputBuffer.Clear();
            localStateBuffer.Clear();
            serverStateBuffer.Clear();
            onReset.Dispatch(true);
        }
        
        public enum DesyncReason
        {
            MISSING_SERVER_COMPARISON = 0,
            GAP_IN_SERVER_STREAM = 1,
            //LARGE_GAP_IN_SERVER_STREAM ? //todo add this with a good treshold?
            SERVER_AHEAD_OF_CLIENT = 3, //shouldn't be possible
            SNAP_TO_SERVER_NO_DATA = 4
        }
        public struct DesyncEvent
        {
            public uint tickId;
            public DesyncReason reason;
            public uint gapSize;
        }
        
        public SafeEventDispatcher<bool> onReset = new();
        public SafeEventDispatcher<PhysicsStateRecord> newStateReached = new();
        public SafeEventDispatcher<bool> resimulation = new();
        public SafeEventDispatcher<bool> resimulationStep = new();
        
        public SafeEventDispatcher<DesyncEvent> potentialDesync = new();
        
        //TODO: fire this during simulation and resimulation allowing others to hook into it and debug
        public SafeEventDispatcher<PredictionInputRecord> inputUsed = new();
    }
}