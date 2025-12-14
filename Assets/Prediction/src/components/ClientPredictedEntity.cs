using System;
using Assets.Scripts.Systems.Events;
using Prediction.data;
using Prediction.utils;
using UnityEngine;

namespace Prediction
{
    //TODO: document in readme
    public class ClientPredictedEntity : AbstractPredictedEntity
    {
        public static bool DEBUG = false;
        //STATE TRACKING
        public GameObject gameObject;

        public Func<uint, RingBuffer<PhysicsStateRecord>, TickIndexedBuffer<PhysicsStateRecord>, PredictionDecision>
            resimulationEligibilityCheckHook
        {
            get; 
            private set; 
        }
        
        public Func<PhysicsStateRecord, PhysicsStateRecord, PredictionDecision> singleStateResimulationEligibilityHook
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
        public uint totalTicks = 0;
        public uint ticksAsFollower = 0;
        public uint ticksAsLocalAuthority = 0;
        public uint resimTicksAsAuthority = 0;
        public uint resimTicksAsFollower = 0;
        
        public ClientPredictedEntity(bool isServer, int bufferSize, Rigidbody rb, GameObject visuals, PredictableControllableComponent[] controllablePredictionContributors, PredictableComponent[] predictionContributors) : base(rb, visuals, controllablePredictionContributors, predictionContributors)
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
        
        public void SetCustomEligibilityCheckHandler(Func<uint, RingBuffer<PhysicsStateRecord>, TickIndexedBuffer<PhysicsStateRecord>, PredictionDecision> handler)
        {
            resimulationEligibilityCheckHook = handler;
            singleStateResimulationEligibilityHook = null;
        }

        public void SetSingleStateEligibilityCheckHandler(Func<PhysicsStateRecord, PhysicsStateRecord, PredictionDecision> handler)
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

            ticksAsLocalAuthority++;
            totalTicks++;
            return inputRecord;
        }
        
        public void ClientFollowerSimulationTick()
        {
            if (isControlledLocally)
            {
                throw new Exception("COMPONENT_MISUSE: locally controlled entity called ClientFollowerSimulationTick");
            }
            
            //TODO: get input from last state? :: inputRecord
            //LoadInput(inputRecord);
            //ApplyForces()

            ticksAsFollower++;
            totalTicks++;
        }

        PredictionInputRecord SampleInput(uint tickId)
        {
            //TODO: correctly convert tick to index!
            PredictionInputRecord inputData = localInputBuffer.Get((int)tickId);
            inputData.WriteReset();
            SampleInput(inputData);
            return inputData;
        }
        
        void SampleInput(PredictionInputRecord inputRecord)
        {
            for (int i = 0; i < controllablePredictionContributors.Length; ++i)
            {
                //NOTE: this samples the input of each component and stores it in the inputRecord as a side effect.
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
        }
        
        public virtual PredictionDecision GetPredictionDecision(uint lastAppliedTick, out uint fromTick)
        {
            fromTick = 0;
            PhysicsStateRecord serverState = serverStateBuffer.GetEnd();
            //NOTE: somehow the server reports are in the future. Don't resimulate until we get there too
            if (serverState == null || lastAppliedTick <= serverState.tickId)
                return PredictionDecision.NOOP;
            
            fromTick = serverState.tickId;
            return resimulationEligibilityCheckHook(serverState.tickId, localStateBuffer, serverStateBuffer);
        }

        bool AddServerState(uint lastAppliedTick, PhysicsStateRecord serverRecord)
        {
            //TODO: use lastAppliedTick to determine how old the update is and do stuff about it
            serverStateBuffer.Add(serverRecord.tickId, serverRecord);
            return serverRecord.tickId == serverStateBuffer.GetEndTick();
        }
        
        public void BufferServerTick(uint lastAppliedTick, PhysicsStateRecord serverState)
        {
            if (DEBUG)
                Debug.Log($"[ClientPredictedEntity][BufferServerTick](goId:{gameObject.GetInstanceID()}) lastTick:{lastAppliedTick}  serverState:{serverState}");

            AddServerState(lastAppliedTick, serverState);
        }

        PredictionDecision _defaultResimulationEligibilityCheck(uint tickId, RingBuffer<PhysicsStateRecord> clientStates,
            TickIndexedBuffer<PhysicsStateRecord> serverStates)
        {
            //TODO: ensure correct conversion from uint tick to index
            PhysicsStateRecord localState = clientStates.Get((int)tickId);
            PhysicsStateRecord serverState = serverStates.Get(tickId);
            return singleStateResimulationEligibilityHook.Invoke(localState, serverState);
        }
        
        //TODO: unit test: including edgecases of no data.
        public void SnapToServer(uint tickId)
        {
            PhysicsStateRecord state = serverStateBuffer.Get(tickId);
            if (state == null)
            {
                //TODO: quantiy and log
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
            LoadInput(inputData);
            ApplyForces();
            resimTicksAsAuthority++;
        }
        
        void PreResimulationFollowerStep(uint tickId)
        {
            resimTicksAsFollower++;
            //TODO: do we need anything here?
        }
        
        public void PostResimulationStep(uint tickId)
        {
            //TODO: check conversion to int
            PhysicsStateRecord record = localStateBuffer.Get((int) tickId);
            PopulatePhysicsStateRecord(tickId, record);
            resimulationStep.Dispatch(false);
        }

        public uint GetServerDelay()
        {
            return totalTicks - serverStateBuffer.GetEndTick();
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
            isControlledLocally = false;
            //TODO: consider if this is needed? it probably is
            //interpolationsProvider.Clear();
            
            totalTicks = 0;
        }

        public uint totalInteractionsWithLocalAuthority = 0;
        
        public SafeEventDispatcher<PhysicsStateRecord> newStateReached = new();
        public SafeEventDispatcher<bool> resimulation = new();
        public SafeEventDispatcher<bool> resimulationStep = new();
    }
}