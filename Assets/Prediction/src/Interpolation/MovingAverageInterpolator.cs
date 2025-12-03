using Mirror;
using Prediction.data;
using Prediction.utils;
using UnityEngine;

namespace Prediction.Interpolation
{
    public class MovingAverageInterpolator: VisualsInterpolationsProvider
    {
        RingBuffer<PhysicsStateRecord> buffer = new RingBuffer<PhysicsStateRecord>(50);
        public RingBuffer<PhysicsStateRecord> averagedBuffer = new RingBuffer<PhysicsStateRecord>(3);

        private Transform target;
        private double time = 0;
        private double tickInterval = Time.fixedDeltaTime;
        private bool interpStarted = false;
        
        //TODO: adaptive window size: depeding on server lateness
        public int slidingWindowTickSize = 8;
        public int startAfterBfrTicks = 2;
        
        public void Update(float deltaTime)
        {
            if (!CanStartInterpolation())
                return;
            
            time += deltaTime;
            
            float interpolationProgress = 1f;
            PhysicsStateRecord psr = null;
            if (!interpStarted)
            {
                psr = GetInterpolationStartState();
                time = GetTime(psr);
                interpStarted = true;
            }
            else
            {
                psr = GetNextInterpolationTarget(time);
                double targetTime = GetTime(psr);
                interpolationProgress = (float)((targetTime - time) / tickInterval);
            }
            ApplyState(psr, interpolationProgress);
        }

        void ApplyState(PhysicsStateRecord psr, float t)
        {
            target.position = Vector3.Lerp(target.position, psr.position, t);
            //TODO: rotation and the other stuff
        }

        double GetTime(PhysicsStateRecord record)
        {
            return record.tickId * tickInterval;
        }

        bool CanStartInterpolation()
        {
            return averagedBuffer.GetFill() >= startAfterBfrTicks;
        }
        
        PhysicsStateRecord GetInterpolationStartState()
        {
            return averagedBuffer.GetStart();
        }
        
        PhysicsStateRecord GetNextInterpolationTarget(double time)
        {
            int fill = averagedBuffer.GetFill();
            int index = averagedBuffer.GetStartIndex();
            do
            {
                PhysicsStateRecord psr = averagedBuffer.Get(index);
                if (GetTime(psr) > time)
                {
                    return psr;
                }

                index++;
                index %= averagedBuffer.GetFill();
                fill--;
            }
            while(fill > 0);

            //NOTE: should not be possible
            return null;
        }
        
        public void Add(PhysicsStateRecord record)
        {
            buffer.Add(record);
            //TODO: implement a configurable type or average here   
            averagedBuffer.Add(GetNextProcessedState());
        }

        PhysicsStateRecord GetNextProcessedState()
        {
            PhysicsStateRecord psr = new PhysicsStateRecord();
            psr.tickId = buffer.GetEnd().tickId;
            
            if (buffer.GetFill() < slidingWindowTickSize)
            {
                for (int i = buffer.GetStartIndex(); i < buffer.GetEndIndex(); ++i)
                {
                    PhysicsStateRecord b = buffer.Get(i);
                    psr.position += b.position;
                }
                psr.position /= buffer.GetFill();
                return psr;
            }
            
            int index = buffer.GetEndIndex();
            for (int i = 0; i < slidingWindowTickSize; ++i)
            {
                index--;
                if (index < 0)
                {
                    index = buffer.GetCapacity() - 1;
                }
                
                PhysicsStateRecord r = buffer.Get(index);
                psr.position += r.position;
            }
            
            psr.position /= slidingWindowTickSize;
            return psr;
        }

        public void SetInterpolationTarget(Transform t)
        {
            target = t;
        }
    }
}