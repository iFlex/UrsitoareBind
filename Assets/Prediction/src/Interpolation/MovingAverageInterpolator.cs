using Mirror;
using Prediction.data;
using Prediction.utils;
using UnityEngine;

namespace Prediction.Interpolation
{
    public class MovingAverageInterpolator: VisualsInterpolationsProvider
    {
        RingBuffer<PhysicsStateRecord> buffer = new RingBuffer<PhysicsStateRecord>(3);
        RingBuffer<PhysicsStateRecord> averagedBuffer = new RingBuffer<PhysicsStateRecord>(3);

        private Transform target;
        private double time = 0;
        private double tickInterval = Time.fixedDeltaTime;
        private bool interpStarted = false;
        public int startAfterTicks = 3;
        
        public void Update(float deltaTime)
        {
            Debug.Log($"[MovingAverageInterpolator][Update] canInterp:{CanStartInterpolation()} average:{averagedBuffer.GetFill()} buff:{buffer.GetFill()}");
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
            return averagedBuffer.GetFill() >= startAfterTicks;
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
            Debug.Log($"[MovingAverageInterpolator][Add] add:{record} fill:{buffer.GetFill()}");
            buffer.Add(record);
            //TODO: implement a configurable type or average here   
            averagedBuffer.Add(GetNextProcessedState());
        }

        PhysicsStateRecord GetNextProcessedState()
        {
            int index = buffer.GetEndIndex();
            PhysicsStateRecord psr = new PhysicsStateRecord();
            psr.tickId = buffer.GetEnd().tickId;
            
            for (int i = 0; i < startAfterTicks; ++i)
            {
                PhysicsStateRecord r = buffer.Get(index);
                psr.position += r.position;
                index--;
                if (index < 0)
                {
                    index = buffer.GetCapacity() - 1;
                }
            }
            
            psr.position /= startAfterTicks;
            return psr;
        }

        public void SetInterpolationTarget(Transform t)
        {
            target = t;
            Debug.Log($"[MovingAverageInterpolator][SetInterpolationTarget] set:{t}");
        }
    }
}