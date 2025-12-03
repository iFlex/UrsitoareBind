using System;
using Assets.Scripts.Systems.Events;
using Prediction.data;
using Prediction.Interpolation;
using Prediction.Simulation;
using Prediction.utils;
using UnityEngine;

namespace Prediction
{
    //TODO: document in readme
    //TODO: add ability to switch from locally controlled to not locally controlled
    public class ClientPredictedEntity : AbstractPredictedEntity
    {
        //STATE TRACKING
        public uint maxAllowedAvgResimPerTick = 1;
        public GameObject gameObject;
        
        //TODO: make visible for testing in tests assembly
        public RingBuffer<PredictionInputRecord> localInputBuffer;
        //TODO: make visible for testing in tests assembly
        public RingBuffer<PhysicsStateRecord> localStateBuffer;
        
        //This is used exclusively in follower mode (predicted entity not controlled by user).
        private uint lastAppliedFollowerTick = 0;
        
        Func<uint, RingBuffer<PhysicsStateRecord>, TickIndexedBuffer<PhysicsStateRecord>, PredictionDecision>
            resimulationEligibilityCheckHook;
        Func<PhysicsStateRecord, PhysicsStateRecord, PredictionDecision> singleStateResimulationEligibilityHook;
        public PhysicsController physicsController;
        
        //TODO: make visible for testing in tests assembly
        public TickIndexedBuffer<PhysicsStateRecord> serverStateBuffer;
        //TODO: proper wire in...?
        public VisualsInterpolationsProvider interpolationsProvider;
            
        public uint totalTicks = 0;
        public uint totalResimulationSteps = 0;
        public uint totalResimulationStepsOverbudget = 0;
        public uint totalResimulations = 0;
        public uint totalSimulationSkips = 0;
        public uint totalDesyncToSnapCount = 0;
        
        public bool snapOnSimSkip = false;
        public bool protectFromOversimulation = false;
        public bool isControlledLocally = false;
        
        public ClientPredictedEntity(int bufferSize, Rigidbody rb, GameObject visuals, PredictableControllableComponent[] controllablePredictionContributors, PredictableComponent[] predictionContributors) : base(rb, visuals, controllablePredictionContributors, predictionContributors)
        {
            rigidbody = rb;
            detachedVisualsIdentity = visuals;
            
            localInputBuffer = new RingBuffer<PredictionInputRecord>(bufferSize);
            localStateBuffer = new RingBuffer<PhysicsStateRecord>(bufferSize);
            this.controllablePredictionContributors = controllablePredictionContributors ?? Array.Empty<PredictableControllableComponent>();
            this.predictionContributors = predictionContributors ?? Array.Empty<PredictableComponent>();
            
            serverStateBuffer = new TickIndexedBuffer<PhysicsStateRecord>(bufferSize);
            
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
            PredictionInputRecord inputRecord = SampleInput(tickId);
            LoadInput(inputRecord);
            ApplyForces();
            totalTicks++;
            if (totalResimulationStepsOverbudget > 0)
            {
                totalResimulationStepsOverbudget--;
            }
            return inputRecord;
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
            if (isControlledLocally)
            {
                interpolationsProvider?.Add(stateData);
            }
            newStateReached.Dispatch(true);
        }

        bool AddServerState(uint lastAppliedTick, PhysicsStateRecord serverRecord)
        {
            //TODO: use lastAppliedTick to determine how old the update is and do stuff about it
            serverStateBuffer.Add(serverRecord.tickId, serverRecord);
            return serverRecord.tickId == serverStateBuffer.GetEndTick();
        }

        public void BufferFollowerServerTick(PhysicsStateRecord lastArrivedServerState)
        {
            if (AddServerState(lastAppliedFollowerTick, lastArrivedServerState))
            {
                if (!isControlledLocally)
                {
                    interpolationsProvider?.Add(lastArrivedServerState);
                }
                //TODO: snapshot interpolation when not locally controlled
            }
            SnapTo(serverStateBuffer.GetEnd());
            lastAppliedFollowerTick = serverStateBuffer.GetEndTick();
        }
        
        public void BufferServerTick(uint lastAppliedTick, PhysicsStateRecord serverState)
        {
            if (AddServerState(lastAppliedTick, serverState))
            {
                //NOTE: somehow the server reports are in the future. Don't resimulate until we get there too
                if (lastAppliedTick < serverState.tickId)
                    return;
                
                var resimulateFromLocalState = localStateBuffer.Get((int)serverState.tickId);
                PredictionDecision decision = resimulationEligibilityCheckHook(serverState.tickId, localStateBuffer, serverStateBuffer);
                switch (decision)
                {
                    case PredictionDecision.NOOP:
                        predictionAcceptable.Dispatch(true);
                        break;
                    
                    case PredictionDecision.RESIMULATE:
                        Resimulate(lastAppliedTick, resimulateFromLocalState, serverState);
                        break;
                    
                    case PredictionDecision.SNAP:
                        totalDesyncToSnapCount++;
                        SnapTo(serverState);
                        break;
                }
                //TODO: consider a decision where we need to pause simulation on client to let server catch up...
            }
        }

        void Resimulate(uint lastAppliedTick, PhysicsStateRecord local, PhysicsStateRecord server)
        {
            if (CanResiumlate())
            {
                ResimulateFrom(local.tickId, lastAppliedTick, server);
            }
            else
            {
                totalSimulationSkips++;
                if (snapOnSimSkip)
                {
                    SnapTo(server);
                }
            }
        }

        PredictionDecision _defaultResimulationEligibilityCheck(uint tickId, RingBuffer<PhysicsStateRecord> clientStates,
            TickIndexedBuffer<PhysicsStateRecord> serverStates)
        {
            //TODO: ensure correct conversion from uint tick to index
            PhysicsStateRecord localState = clientStates.Get((int)tickId);
            PhysicsStateRecord serverState = serverStates.Get(tickId);
            return singleStateResimulationEligibilityHook.Invoke(localState, serverState);
        }

        void SnapTo(PhysicsStateRecord serverState)
        {
            rigidbody.position = serverState.position;
            rigidbody.rotation = serverState.rotation;
            rigidbody.linearVelocity = serverState.velocity;
            rigidbody.angularVelocity = serverState.angularVelocity;
        }
        
        void ResimulateFrom(uint startTick, uint lastAppliedTick, PhysicsStateRecord startState)
        {
            physicsController.BeforeResimulate(this);
            resimulation.Dispatch(true);
            
            //Apply Server State
            SnapTo(startState);
            //TODO: check conversion to int
            PhysicsStateRecord record = localStateBuffer.Get((int) startTick);
            PopulatePhysicsStateRecord(startTick, record);
            
            uint index = startTick + 1;
            while (index <= lastAppliedTick)
            {
                resimulationStep.Dispatch(true);
                
                //TODO: correct conversion of tickId to index plz
                PredictionInputRecord inputData = localInputBuffer.Get((int) index);
                LoadInput(inputData);
                ApplyForces();
                physicsController.Resimulate(this);
                //TODO: check conversion to int
                record = localStateBuffer.Get((int) index);
                PopulatePhysicsStateRecord(index, record);

                index++;
                totalResimulationSteps++;
                totalResimulationStepsOverbudget++;
                resimulationStep.Dispatch(false);
            }
            
            totalResimulations++;
            resimulation.Dispatch(false);
            physicsController.AfterResimulate(this);
        }
        
        public uint GetTotalTicks()
        {
            return totalTicks;
        }
        
        public uint GetAverageResimPerTick()
        {
            if (totalTicks == 0)
                return 0;
            return totalResimulationSteps / totalTicks;
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

        public uint GetServerDelay()
        {
            return totalTicks - serverStateBuffer.GetEndTick();
        }

        public SafeEventDispatcher<bool> newStateReached = new();
        public SafeEventDispatcher<bool> predictionAcceptable = new();
        public SafeEventDispatcher<bool> resimulation = new();
        public SafeEventDispatcher<bool> resimulationStep = new();
    }
}