using Assets.Scripts.Systems.Events;
using Prediction.data;
using Prediction.utils;
using UnityEngine;

namespace Prediction
{
    //TODO: unit test
    //TODO: document in readme
    public class ServerPredictedEntity : AbstractPredictedEntity
    {
        public GameObject gameObject;
        private PhysicsStateRecord serverStateBfr = new PhysicsStateRecord();
        private uint tickId;
        
        private uint _waitTicksBeforeSimStart;
        private uint waitTicksBeforeSimStart;
        
        TickIndexedBuffer<PredictionInputRecord> inputQueue;
        public int bufferFullThreshold = 0; //Number of ticks to buffer before starting to send out the updates
        public int bufferRefillThreshold = 0;
        private bool bufferFilling = true;
        public bool useBuffering = true;

        public uint ticksWithoutInput = 0;
        
        public ServerPredictedEntity(int bufferSize, Rigidbody rb, GameObject visuals, PredictableControllableComponent[] controllablePredictionContributors, PredictableComponent[] predictionContributors) : base(rb, visuals, controllablePredictionContributors, predictionContributors)
        {
            //TODO: configurable how much to wait before sim start...
            _waitTicksBeforeSimStart = 0;
            waitTicksBeforeSimStart = _waitTicksBeforeSimStart;
            
            inputQueue = new TickIndexedBuffer<PredictionInputRecord>(bufferSize);
            inputQueue.emptyValue = null;
        }

        private uint lastAppliedTick = 0;
        public int inputJumps = 0;
        public PhysicsStateRecord ServerSimulationTick()
        {
            PredictionInputRecord nextInput = TakeNextInput();
            if (nextInput != null)
            {
                int delta = (int)(tickId > lastAppliedTick ? tickId - lastAppliedTick : lastAppliedTick - tickId);
                lastAppliedTick = tickId;
                if (delta > 1)
                {
                    inputJumps++;
                }
                //TODO: validate input, should happen in LoadInput
                LoadInput(nextInput);
            }
            else
            {
                ticksWithoutInput++;
            }
            ApplyForces();
            Tick();
            PopulatePhysicsStateRecord(GetTickId(), serverStateBfr);
            return serverStateBfr;
        }

        public void BufferClientTick(uint clientTickId, PredictionInputRecord inputRecord)
        {
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
                //TODO: notify late update dropped
            }
        }
        
        public bool ValidateState(uint tickId, PredictionInputRecord input)
        {
            throw new System.NotImplementedException();
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
        
        public PredictionInputRecord TakeNextInput()
        {
            if (useBuffering && !CanUseBuffer())
                return null;
            
            uint newTick = inputQueue.GetNextTick(tickId);
            if (newTick > 0)
            {
                tickId = newTick;
                return inputQueue.Remove(tickId);
            }
            return inputQueue.emptyValue;
        }

        //TODO: unit test the buffering
        //TODO: careful to ensure old data is purged constantly, otherwise it will impact the buffering
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
        
        public SafeEventDispatcher<bool> simulationStarted = new();
        public SafeEventDispatcher<bool> firstTickArrived = new();
    }
}