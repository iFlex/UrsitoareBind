using Sector0.Events;
using Prediction.data;
using Prediction.utils;
using UnityEngine;

namespace Prediction
{
    //TODO: document in readme
    public class ServerPredictedEntity : AbstractPredictedEntity
    {
        public static bool DEBUG = false;
        public static bool APPLY_FORCES_TO_EACH_CATCHUP_INPUT = false;
        public static bool USE_BUFFERING = true;
        public static int BUFFER_FULL_THRESHOLD = 3; //Number of ticks to buffer before starting to send out the updates
        public static bool CATCHUP = true;
        public static bool SERVER_LOG_VELOCITIES = false;
        public static bool LOG_CLIENT_INUPTS = false;
        
        public GameObject gameObject;
        private PhysicsStateRecord serverStateBfr = new PhysicsStateRecord();
        private uint tickId;
        
        //TODO: package private
        public TickIndexedBuffer<PredictionInputRecord> inputQueue;
        
        //NOTE: Possible to buffer user inputs if needed to try and ensure a closer to client simulation on the server at the
        //cost of delaying the server behind the client by a small margin. The more you buffer, the more the server is delayed, the less reliable is the client image.

        private bool bufferFilled = false;

        
        //NOTE: if the client updates buffer grows past a certain threshold
        //that means the server has fallen behind time wise. So we should snap ahead to the latest client state.
        public int catchupSections = 3;
        public int ticksPerCatchupSection = 0;
        
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
        public uint totalBufferingTicks = 0;
        public uint totalMissingInputTicks = 0;
        
        public ServerPredictedEntity(uint id, int bufferSize, Rigidbody rb, GameObject visuals, PredictableControllableComponent[] controllablePredictionContributors, PredictableComponent[] predictionContributors) : base(id, rb, visuals, controllablePredictionContributors, predictionContributors)
        {
            gameObject = rb.gameObject;
            inputQueue = new TickIndexedBuffer<PredictionInputRecord>(bufferSize);
            inputQueue.emptyValue = null;

            ticksPerCatchupSection = Mathf.FloorToInt(bufferSize / catchupSections) + 1;
        }

        DesyncEvent devt = new DesyncEvent();
        private bool noInputAvailableForTick = false;
        void HandleTickInput()
        {
            //NOTE: this also loads TickId with the latest value
            int inputsToApply = 0;
            uint maxDelay = inputQueue.GetRange();
            if (maxDelay > maxClientDelay)
            {
                maxClientDelay = maxDelay;
            }
            
            inputsToApply = GetInputsCount();
            noInputAvailableForTick = inputsToApply == 0;
            if (inputsToApply > 1)
            {
                catchupTicks += (uint) inputsToApply - 1U;
                
                devt.reason = DesyncReason.MULTIPLE_INPUTS_PER_FRAME;
                devt.tickId = tickId;
                potentialDesync.Dispatch(devt);
            }
            while (inputsToApply > 0)
            {
                inputsToApply--;
                //NOTE: last applied input must not be removed as it will be when SamplePhysicsState runs...
                //TODO: get rid of the side effect
                PredictionInputRecord nextInput = TakeNextInput_TickSideEffect(inputsToApply > 0);
                if (DEBUG)
                    Debug.Log($"[ServerPredictedEntity][ServerSimulationTick] id:{id} goID:{gameObject.GetInstanceID()} lastAppliedTick:{lastAppliedTick} buffRange:{inputQueue.GetRange()} buffFill:{inputQueue.GetFill()} nextInput:{nextInput}");
                
                if (nextInput != null)
                {
                    if (LOG_CLIENT_INUPTS)
                    {
                        Debug.Log($"[SV][SIMULATION][INPUT] i:{id} t:{tickId} input:{nextInput}");
                    }
                    int delta = (int)(tickId > lastAppliedTick ? tickId - lastAppliedTick : lastAppliedTick - tickId);
                    lastAppliedTick = tickId;
                    if (delta > 1)
                    {
                        inputJumps++;
                        
                        devt.reason = DesyncReason.INPUT_JUMP;
                        devt.tickId = tickId;
                        potentialDesync.Dispatch(devt);
                    }
                
                    if (ValidateState(TickDeltaToTimeDelta(delta), nextInput))
                    {
                        LoadInput(nextInput);
                    }
                    else
                    {
                        invalidInputs++;
                        
                        devt.reason = DesyncReason.INVALID_INPUT;
                        devt.tickId = tickId;
                        potentialDesync.Dispatch(devt);
                    }
                    
                    if (APPLY_FORCES_TO_EACH_CATCHUP_INPUT)
                    {
                        ApplyForces();
                    }
                }
            }
            if (noInputAvailableForTick)
            {
                ticksWithoutInput++;
                
                devt.reason = DesyncReason.NO_INPUT_FOR_SERVER_TICK;
                devt.tickId = tickId;
                potentialDesync.Dispatch(devt);
            }
            if (!APPLY_FORCES_TO_EACH_CATCHUP_INPUT)
            {
                ApplyForces();
            }
        }

        public uint ServerSimulationTick()
        {
            if (CanUseBuffer())
            {
                HandleTickInput();
            }
            else
            {
                if (inputQueue.GetFill() > 0)
                {
                    totalBufferingTicks++;
                    
                    devt.reason = DesyncReason.INPUT_BUFFERED;
                    devt.tickId = tickId;
                    potentialDesync.Dispatch(devt);
                }
                else
                {
                    totalMissingInputTicks++;
                    
                    devt.reason = DesyncReason.NO_INPUT_FOR_SERVER_TICK;
                    devt.tickId = tickId;
                    potentialDesync.Dispatch(devt);
                }
                ApplyForces();
            }
            return GetTickId();
        }

        int GetInputsCount()
        {
            if (!CATCHUP)
                return 1;
            return Mathf.FloorToInt(inputQueue.GetFill() / ticksPerCatchupSection) + 1;
        }
        
        public PhysicsStateRecord SamplePhysicsState()
        {
            preSampleState.Dispatch(true);
            PopulatePhysicsStateRecord(GetTickId(), serverStateBfr);
            serverStateBfr.input = inputQueue.Remove(GetTickId());
            UpdateBufferStateOnRemoval();
            stateSampled.Dispatch(true);
            
            if (DEBUG)
                Debug.Log($"[ServerPredictedEntity][SamplePhysicsState]({id}) input:{serverStateBfr}");
            
            if (SERVER_LOG_VELOCITIES)
            {
                //TODO: this can be done via events in the host application...
                Debug.Log($"[SV][SIMULATION][DATA] i:{id} t:{tickId} p:{rigidbody.position.ToString("F10")} r:{rigidbody.rotation.ToString("F10")} v:{rigidbody.linearVelocity.ToString("F10")} a:{rigidbody.angularVelocity.ToString("F10")}");
            }
            return serverStateBfr;
        }

        public float TickDeltaToTimeDelta(int delta)
        {
            //TODO: have fixedDeltaTime be configurable, pass that in on instantiation
            return delta * Time.fixedDeltaTime;
        }

        public uint clUpdateCount = 0;
        public uint clAddedUpdateCount = 0;
        private ClientInput cevt;
        public void BufferClientTick(uint clientTickId, PredictionInputRecord inputRecord)
        {
            if (DEBUG)
                Debug.Log($"[ServerPredictedEntity][BufferClientTick]({gameObject.GetInstanceID()}) clientTickId:{clientTickId} tickId:{tickId} input:{inputRecord}");
            
            if (inputQueue.GetFill() == 0)
            {
                firstTickArrived.Dispatch(true);
            }

            clUpdateCount++;
            if (clientTickId > tickId)
            {
                if (inputQueue.GetFill() == inputQueue.GetCapacity())
                {
                    devt.reason = DesyncReason.TICK_OVERFLOW;
                    devt.tickId = tickId;
                    potentialDesync.Dispatch(devt);
                }
                
                clAddedUpdateCount++;
                inputQueue.Add(clientTickId, inputRecord);
            }
            else
            {
                lateTickCount++;

                devt.reason = DesyncReason.LATE_TICK;
                devt.tickId = tickId;
                potentialDesync.Dispatch(devt);
            }

            if (!bufferFilled)
            {
                bufferFilled = inputQueue.GetFill() >= BUFFER_FULL_THRESHOLD;
            }

            if (LOG_CLIENT_INUPTS)
            {
                cevt.tickId = tickId;
                cevt.input = inputRecord;
                inputReceived.Dispatch(cevt);   
            }
        }
        
        public void ResetClientState()
        {
            //NOTE: use this when changing the controller of the plane.
            tickId = 0;
            inputQueue.Clear();
        }
        
        public uint GetTickId()
        {
            return tickId;
        }

        public PredictionInputRecord TakeNextInput_TickSideEffect(bool remove)
        {
            if (!CanUseBuffer())
            {
                return null;
            }

            uint newTick = inputQueue.GetNextTick(tickId);
            if (newTick > 0)
            {
                tickId = newTick;
                if (remove)
                {
                    var r = inputQueue.Remove(tickId);
                    UpdateBufferStateOnRemoval();
                    return r;
                }
                return inputQueue.Get(newTick);
            }
            return inputQueue.emptyValue;
        }

        //TODO: unit test the buffering
        bool CanUseBuffer()
        {
            if (!USE_BUFFERING)
                return true;
            
            return bufferFilled && inputQueue.GetFill() > 0;
        }

        void UpdateBufferStateOnRemoval()
        { 
            if (inputQueue.GetFill() == 0)
                bufferFilled = false;
        }

        public int BufferFill()
        {
            return inputQueue.GetFill();
        }
        
        public uint BufferSize()
        {
            return inputQueue.GetRange();
        }
        
        //NOTE: call this when you change the owner of the object
        public void Reset()
        {
            inputQueue.Clear();
            bufferFilled = false;
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
            bufferFilled = false;
        }

        public enum DesyncReason
        {
            NO_INPUT_FOR_SERVER_TICK = 0,
            INPUT_BUFFERED = 1,
            INPUT_JUMP = 2,
            MULTIPLE_INPUTS_PER_FRAME = 3,
            INVALID_INPUT = 4,
            LATE_TICK = 5,
            TICK_OVERFLOW = 6,
        }
        public struct DesyncEvent
        {
            public uint tickId;
            public DesyncReason reason;
        }

        public struct ClientInput
        {
            public uint tickId;
            public PredictionInputRecord input;
        }
        
        public SafeEventDispatcher<bool> preSampleState = new();
        public SafeEventDispatcher<bool> firstTickArrived = new();
        public SafeEventDispatcher<DesyncEvent> potentialDesync = new();
        public SafeEventDispatcher<bool> stateSampled = new();
        public SafeEventDispatcher<ClientInput> inputReceived = new();
    }
}