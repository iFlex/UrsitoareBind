using Assets.Scripts.Systems.Events;
using Prediction.data;
using Prediction.utils;
using UnityEngine;

namespace Prediction
{
    //TODO: document in readme
    public class ServerPredictedEntity : AbstractPredictedEntity
    {
        public static bool DEBUG = false;
        
        public GameObject gameObject;
        private PhysicsStateRecord serverStateBfr = new PhysicsStateRecord();
        private uint tickId;
        
        private uint _waitTicksBeforeSimStart;
        private uint waitTicksBeforeSimStart;
        
        //TODO: package private
        public TickIndexedBuffer<PredictionInputRecord> inputQueue;
        
        //NOTE: Possible to buffer user inputs if needed to try and ensure a closer to client simulation on the server at the
        //cost of delaying the server behind the client by a small margin. The more you buffer, the more the server is delayed, the less reliable is the client image.
        
        //TODO: experiment with different settings here, see if it helps smooth things over at higher lag values.
        public int bufferFullThreshold = 0; //Number of ticks to buffer before starting to send out the updates
        public int bufferRefillThreshold = 0;
        private bool bufferFilling = true;
        public bool useBuffering = true;
        
        //NOTE: if the client updates buffer grows past a certain threshold
        //that means the server has fallen behind time wise. So we should snap ahead to the latest client state.
        public int catchupSections = 3;
        public int ticksPerCatchupSection = 1;
        public bool applyForcesToEachCatchupInput = false;
        
        private uint lastAppliedTick = 0;
        //STATS
        public uint invalidInputs = 0;
        public uint ticksWithoutInput = 0;
        public uint lateTickCount = 0;
        public uint totalSnapAheadCounter = 0;
        public int inputJumps = 0;
        public uint catchupTicks = 0;
        public uint catchupBufferWipes = 0;
        public uint maxClientDelay = 0;
        
        public ServerPredictedEntity(uint id, int bufferSize, Rigidbody rb, GameObject visuals, PredictableControllableComponent[] controllablePredictionContributors, PredictableComponent[] predictionContributors) : base(id, rb, visuals, controllablePredictionContributors, predictionContributors)
        {
            gameObject = rb.gameObject;
            //TODO: configurable how much to wait before sim start...
            _waitTicksBeforeSimStart = 0;
            waitTicksBeforeSimStart = _waitTicksBeforeSimStart;
            
            inputQueue = new TickIndexedBuffer<PredictionInputRecord>(bufferSize);
            inputQueue.emptyValue = null;

            ticksPerCatchupSection = Mathf.FloorToInt(bufferSize / catchupSections) + 1;
        }
        
        public uint ServerSimulationTick()
        {
            //NOTE: this also loads TickId with the latest value
            int inputsToApply;
            uint maxDelay = inputQueue.GetRange();
            if (maxDelay > maxClientDelay)
            {
                maxClientDelay = maxDelay;
            }
            
            if (inputQueue.GetFill() == inputQueue.GetCapacity())
            {
                //Buffer full, skip all
                SnapToLatest();
                inputsToApply = 1;
                catchupBufferWipes++;
            }
            else
            {
                inputsToApply = GetInputsCount();  
            }
            
            bool atLeastOneInput = false;
            if (inputsToApply > 1)
            {
                catchupTicks += (uint) inputsToApply - 1U;
            }
            while (inputsToApply > 0)
            {
                inputsToApply--;
                PredictionInputRecord nextInput = TakeNextInput(false);
                if (DEBUG)
                    Debug.Log($"[ServerPredictedEntity][ServerSimulationTick] id:{id} goID:{gameObject.GetInstanceID()} lastAppliedTick:{lastAppliedTick} buffRange:{inputQueue.GetRange()} buffFill:{inputQueue.GetFill()} nextInput:{nextInput}");
                
                if (nextInput != null)
                {
                    atLeastOneInput = true;
                    int delta = (int)(tickId > lastAppliedTick ? tickId - lastAppliedTick : lastAppliedTick - tickId);
                    lastAppliedTick = tickId;
                    if (delta > 1)
                    {
                        inputJumps++;
                    }
                
                    if (ValidateState(TickDeltaToTimeDelta(delta), nextInput))
                    {
                        LoadInput(nextInput);
                    }
                    else
                    {
                        invalidInputs++;
                    }
                    if (applyForcesToEachCatchupInput)
                    {
                        ApplyForces();
                    }
                }
            }
            if (!atLeastOneInput)
            {
                ticksWithoutInput++;
            }
            if (!applyForcesToEachCatchupInput)
            {
                ApplyForces();
            }
            Tick();
            return GetTickId();
        }

        int GetInputsCount()
        {
            return Mathf.FloorToInt(inputQueue.GetFill() / ticksPerCatchupSection) + 1;
        }
        
        public PhysicsStateRecord SamplePhysicsState()
        {
            PopulatePhysicsStateRecord(GetTickId(), serverStateBfr);
            serverStateBfr.input = inputQueue.Remove(GetTickId());
            if (DEBUG)
                Debug.Log($"[ServerPredictedEntity][SamplePhysicsState]({id}) input:{serverStateBfr}");
            return serverStateBfr;
        }

        public float TickDeltaToTimeDelta(int delta)
        {
            //TODO: have fixedDeltaTime be configurable, pass that in on instantiation
            return delta * Time.fixedDeltaTime;
        }
        
        public void BufferClientTick(uint clientTickId, PredictionInputRecord inputRecord)
        {
            if (DEBUG)
                Debug.Log($"[ServerPredictedEntity][BufferClientTick]({gameObject.GetInstanceID()}) clientTickId:{clientTickId} tickId:{tickId} input:{inputRecord}");
            
            if (inputQueue.GetFill() == 0)
            {
                firstTickArrived.Dispatch(true);
            }

            if (clientTickId > tickId)
            {
                inputQueue.Add(clientTickId, inputRecord);
            }
            else
            {
                lateTickCount++;
            }
        }
        
        public void ResetClientState()
        {
            //NOTE: use this when changing the controller of the plane.
            tickId = 0;
            waitTicksBeforeSimStart = _waitTicksBeforeSimStart;
            inputQueue.Clear();
        }

        public void Tick()
        {
            if (waitTicksBeforeSimStart > 0)
            {
                waitTicksBeforeSimStart--;
                if (waitTicksBeforeSimStart == 0)
                {
                    simulationStarted.Dispatch(true);
                }
            }
        }
        
        public uint GetTickId()
        {
            return tickId;
        }
        
        public PredictionInputRecord TakeNextInput(bool remove)
        {
            if (useBuffering && !CanUseBuffer())
                return null;
            
            uint newTick = inputQueue.GetNextTick(tickId);
            if (newTick > 0)
            {
                tickId = newTick;
                if (remove)
                {
                    return inputQueue.Remove(tickId);
                }
                return inputQueue.Get(newTick);
            }
            return inputQueue.emptyValue;
        }

        //TODO: unit test the buffering
        //TODO: careful to ensure old data is purged constantly, otherwise it will impact the buffering
        //(so far the data is purged, but if we ever implement skipping ahead, then the skipped data needs to be removed)
        bool CanUseBuffer()
        {
            if (!useBuffering)
                return true;
            
            if (bufferFilling)
            {
                bufferFilling = inputQueue.GetFill() < bufferFullThreshold;
                return !bufferFilling;
            }

            bufferFilling = inputQueue.GetFill() <= bufferRefillThreshold;
            return !bufferFilling;
        }

        public uint BufferSize()
        {
            return inputQueue.GetRange();
        }
        
        //NOTE: call this when you change the owner of the object
        public void Reset()
        {
            inputQueue.Clear();
            bufferFilling = true;
            tickId = 0;
            
            invalidInputs = 0;
            ticksWithoutInput = 0;
            lateTickCount = 0;
        }
        
        //TODO: decide if to keep?
        void SnapToLatest()
        {
            if (DEBUG)
                Debug.Log($"[ServerPredictedEntity][SnapToLatest]({gameObject.GetInstanceID()})");
            totalSnapAheadCounter++;

            uint tick = inputQueue.GetEndTick();
            PredictionInputRecord pir = inputQueue.GetEnd();
            inputQueue.Clear();
            inputQueue.Add(tick, pir);
            bufferFilling = true;
        }
        
        public SafeEventDispatcher<bool> simulationStarted = new();
        public SafeEventDispatcher<bool> firstTickArrived = new();
    }
}