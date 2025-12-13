using System;
using Assets.Scripts.Systems.Events;
using Prediction.data;
using Prediction.Simulation;
using Prediction.StateBlend;
using Prediction.utils;
using UnityEngine;

namespace Prediction
{
    //TODO: document in readme
    //TODO: add ability to switch from locally controlled to not locally controlled
    public class ClientPredictedEntity : AbstractPredictedEntity
    {
        public static bool DEBUG = false;
        
        //STATE TRACKING
        public uint maxAllowedAvgResimPerTick = 1;
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
        
        //TODO: module private?
        public PhysicsController physicsController;
        
        //STATE
        //TODO: make visible for testing in tests assembly
        public RingBuffer<PredictionInputRecord> localInputBuffer;
        //TODO: make visible for testing in tests assembly
        public RingBuffer<PhysicsStateRecord> localStateBuffer;
        //TODO: make visible for testing in tests assembly
        public TickIndexedBuffer<PhysicsStateRecord> serverStateBuffer;
        //TODO: make visible for testing in tests assembly
        public RingBuffer<PhysicsStateRecord> blendedFollowStateBuffer;
        public RingBuffer<PhysicsStateRecord> followerStateBuffer;

        public FollowerStateBlender followerStateBlender = new WeightedAverageBlender();
        public FollowerState followerState = new FollowerState();
        
        private bool isServer;
        
        //This is used exclusively in follower mode (predicted entity not controlled by user).
        public bool snapOnSimSkip = false;
        public bool protectFromOversimulation = false;
        public bool predictFollowers = true;
        public bool isControlledLocally { get; private set; }
        
        //STATS
        public uint totalTicks = 0;
        public uint totalResimulationSteps = 0;
        public uint totalResimulationStepsOverbudget = 0;
        public uint totalResimulations = 0;
        public uint totalSimulationSkips = 0;
        public uint totalDesyncToSnapCount = 0;
        
        public ClientPredictedEntity(bool isServer, int bufferSize, Rigidbody rb, GameObject visuals, PredictableControllableComponent[] controllablePredictionContributors, PredictableComponent[] predictionContributors) : base(rb, visuals, controllablePredictionContributors, predictionContributors)
        {
            rigidbody = rb;
            gameObject = rb.gameObject;
            detachedVisualsIdentity = visuals;
            this.isServer = isServer;
            
            localInputBuffer = new RingBuffer<PredictionInputRecord>(bufferSize);
            localStateBuffer = new RingBuffer<PhysicsStateRecord>(bufferSize);
            blendedFollowStateBuffer = new RingBuffer<PhysicsStateRecord>(bufferSize);
            followerStateBuffer = new RingBuffer<PhysicsStateRecord>(bufferSize);
            
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
                blendedFollowStateBuffer.Add(new PhysicsStateRecord());
                followerStateBuffer.Add(new PhysicsStateRecord());
            }
            
            followerState.Reset();
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
            totalTicks++;
            if (totalResimulationStepsOverbudget > 0)
            {
                totalResimulationStepsOverbudget--;
            }
            return inputRecord;
        }

        public void ClientFollowerSimulationTick()
        {
            if (isControlledLocally)
            {
                throw new Exception("COMPONENT_MISUSE: locally controlled entity called ClientFollowerSimulationTick");
            }
            
            if (!TickOverlapWithAuthority())
            {
                if (followerState.lastAppliedServerTick < serverStateBuffer.GetEndTick())
                {
                    totalServerFollowerTicks++;
                    SnapTo(serverStateBuffer.GetEnd());
                    followerState.lastAppliedServerTick = serverStateBuffer.GetEndTick();
                    followerState.tickId = followerState.lastAppliedServerTick;
                    if (DEBUG)
                        Debug.Log($"[ClientPredictedEntity][Blend][ClientFollowerSimulationTick]({gameObject.GetInstanceID()}) FOLLOW_SERVER tickId:{followerState.tickId} data:{serverStateBuffer.GetEnd()}");
                }
            }
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

            if (!isControlledLocally)
            {
                stateData = followerStateBuffer.Get((int)followerState.tickId);
                PopulatePhysicsStateRecord(followerState.tickId, stateData);
                if (DEBUG)
                    Debug.Log($"[ClientPredictedEntity][SamplePhysicsState] ({gameObject.GetInstanceID()}) tickId:{followerState.tickId} PhysicsSampled:{stateData}");
                //TODO: can we reuse the localStateBuffer? it's a waste to have a completely separate one...
                followerState.Sample();
            }
        }

        bool AddServerState(uint lastAppliedTick, PhysicsStateRecord serverRecord)
        {
            //TODO: use lastAppliedTick to determine how old the update is and do stuff about it
            serverStateBuffer.Add(serverRecord.tickId, serverRecord);
            return serverRecord.tickId == serverStateBuffer.GetEndTick();
        }

        public void BufferFollowerServerTick(PhysicsStateRecord lastArrivedServerState)
        {
            if (isControlledLocally)
                throw new Exception("COMPONENT_MISSUSE. Called BufferFollowerServerTick on locally controlled component");
            AddServerState(followerState.lastAppliedServerTick, lastArrivedServerState);
        }
        
        public void BufferServerTick(uint lastAppliedTick, PhysicsStateRecord serverState)
        {
            if (!isControlledLocally)
                throw new Exception("COMPONENT_MISSUSE. Called BufferServerTick on non locally controlled component");
            
            if (DEBUG)
                Debug.Log($"[ClientPredictedEntity][BufferServerTick](goId:{gameObject.GetInstanceID()}) lastTick:{lastAppliedTick}  serverState:{serverState}");
            if (AddServerState(lastAppliedTick, serverState))
            {
                //NOTE: somehow the server reports are in the future. Don't resimulate until we get there too
                if (lastAppliedTick < serverState.tickId)
                    return;
                
                var resimulateFromLocalState = localStateBuffer.Get((int)serverState.tickId);
                PredictionDecision decision = resimulationEligibilityCheckHook(serverState.tickId, localStateBuffer, serverStateBuffer);
                if (DEBUG)
                    Debug.Log($"[ClientPredictedEntity][Resimulate](goId:{gameObject.GetInstanceID()}) lat:{lastAppliedTick} decision:{decision} resims:{totalResimulations}");
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
            blendedFollowStateBuffer.Clear();
            followerState.Reset();
            followerStateBlender?.Reset();
            isControlledLocally = false;
            //TODO: consider if this is needed? it probably is
            //interpolationsProvider.Clear();
            
            totalTicks = 0;
            totalResimulationSteps = 0;
            totalResimulationStepsOverbudget = 0;
            totalResimulations = 0;
            totalSimulationSkips = 0;
            totalDesyncToSnapCount = 0;
        }

        public uint totalInteractionsWithLocalAuthority = 0;
        public uint totalBlendedFollowerTicks = 0;
        public uint totalBlendedFollowerTicksSnapTo = 0;
        public uint totalServerFollowerTicks = 0;
        public bool subsequentCollisionsExtendInterval = true;
        public uint blendIntervalMultiplier = 3;
        
        //NOTE: external entry point
        public void MarkInteractionWithLocalAuthority()
        {
            if (!predictFollowers)
                return;
            
            totalInteractionsWithLocalAuthority++;
            if (!subsequentCollisionsExtendInterval && followerState.overlappingWithLocalAuthority)
                return;
            
            bool isSubsequentInteraction = followerState.overlappingWithLocalAuthority;
            //TODO: wire this getter in or wire in the prediction manager (however that's coupling)
            uint serverLatencyInTicks = PredictionManager.GetServerTickDelay() * blendIntervalMultiplier;
            if (DEBUG)
                Debug.Log($"[ClientPredictedEntity][Blend][MarkInteractionWithLocalAuthority](goId:{gameObject.GetInstanceID()}) delay:{serverLatencyInTicks} totalInter:{totalInteractionsWithLocalAuthority} tickId:{followerState.tickId}");

            if (!isSubsequentInteraction)
            {
                followerState.overlapWithAuthorityStart = followerState.tickId;
            }
            followerState.overlapWithAuthorityEnd = followerState.tickId + serverLatencyInTicks;
            
            //TODO: think well about subsequent colisions, we don't want to overwrite them with the SnapTo phase...
            //For now only the first collision in blended window triggers this
            //TODO: consider externally applied forces
            followerState.overlappingWithLocalAuthority = true;
            interactedWithLocalAuthority.Dispatch(true);
        }

        bool TickOverlapWithAuthority()
        {
            if (DEBUG)
                Debug.Log($"[ClientPredictedEntity][Blend][TickOverlapWithAuthority][1](goId:{gameObject.GetInstanceID()}) overlapping:{followerState.overlappingWithLocalAuthority} tickId:{followerState.tickId} end:{followerState.overlapWithAuthorityEnd}");
            
            if (!followerState.overlappingWithLocalAuthority)
                return false;

            if (followerState.tickId > followerState.overlapWithAuthorityEnd)
            {
                followerState.Cancel();
                return false;
            }
            
            if (followerStateBlender != null && followerState.tickId != followerState.overlapWithAuthorityStart)
            {
                totalBlendedFollowerTicks++;
                if (followerStateBlender.BlendStep(followerState, blendedFollowStateBuffer, followerStateBuffer,
                        serverStateBuffer))
                {
                    totalBlendedFollowerTicksSnapTo++;
                    PhysicsStateRecord record = blendedFollowStateBuffer.Get((int) followerState.tickId);
                    SnapTo(record);
                    if (DEBUG)
                        Debug.Log($"[ClientPredictedEntity][Blend][TickOverlapWithAuthority][2](goId:{gameObject.GetInstanceID()}) BlendedTick({record}) tickId:{followerState.tickId}");
                }
                else if (DEBUG)
                {
                    Debug.Log($"[ClientPredictedEntity][Blend][TickOverlapWithAuthority][2](goId:{gameObject.GetInstanceID()}) BlendedTick(APPLY_SKIPPED) tickId:{followerState.tickId}");
                }
            }
            else if (DEBUG)
            {
                Debug.Log($"[ClientPredictedEntity][Blend][TickOverlapWithAuthority][2](goId:{gameObject.GetInstanceID()}) BlendedTick([SKIP]) tickId:{followerState.tickId}");
            }
            return true;
        }

        public class FollowerState
        {
            public uint lastAppliedServerTick;
            public bool overlappingWithLocalAuthority;
            public uint overlapWithAuthorityEnd;
            public uint overlapWithAuthorityStart;
            public uint tickId;
            
            public void Reset()
            {
                //TODO: do we need both tick and lastApplied?
                tickId = 0;
                lastAppliedServerTick = 0;
                
                overlappingWithLocalAuthority = false;
                overlapWithAuthorityEnd = 0;
                overlapWithAuthorityStart = 0;
            }
            
            public void Cancel()
            {
                overlappingWithLocalAuthority = false;
            }

            public void Sample()
            {
                tickId++;
            }
        }
        
        public SafeEventDispatcher<bool> interactedWithLocalAuthority = new();
        public SafeEventDispatcher<PhysicsStateRecord> newStateReached = new();
        public SafeEventDispatcher<bool> predictionAcceptable = new();
        public SafeEventDispatcher<bool> resimulation = new();
        public SafeEventDispatcher<bool> resimulationStep = new();
    }
}