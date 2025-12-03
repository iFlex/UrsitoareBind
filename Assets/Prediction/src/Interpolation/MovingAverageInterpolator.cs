using System;
using Mirror;
using Prediction.data;
using Prediction.utils;
using UnityEngine;

namespace Prediction.Interpolation
{
    public class MovingAverageInterpolator: VisualsInterpolationsProvider
    {
        RingBuffer<PhysicsStateRecord> buffer = new RingBuffer<PhysicsStateRecord>(200);
        public RingBuffer<PhysicsStateRecord> averagedBuffer = new RingBuffer<PhysicsStateRecord>(3);

        private Transform target;
        private double time = 0;
        private double tickInterval = Time.fixedDeltaTime;
        private bool interpStarted = false;
        
        public int slidingWindowTickSize = 6;
        public int startAfterBfrTicks = 2;

        public float MinVisualDelay = 0.5f;
        public uint minVisualTickDelay = 2;
        public bool autosizeWindow = false;
        public Func<uint> GetServerTickLag;

        public MovingAverageInterpolator()
        {
            minVisualTickDelay = (uint) Mathf.CeilToInt(MinVisualDelay / Time.fixedDeltaTime);
        }
        
        public void ConfigureWindowAutosizing(Func<uint> serverLatencyFetcher)
        {
            if (serverLatencyFetcher == null)
            {
                autosizeWindow = false;
            }
            else
            {
                autosizeWindow = true;
                GetServerTickLag = serverLatencyFetcher;
            }
        }
        
        public void Update(float deltaTime)
        {
            if (!CanStartInterpolation())
                return;
            
            if (autosizeWindow)
            {
                uint serverTickLag = GetServerTickLag();
                slidingWindowTickSize = (int) Math.Max(minVisualTickDelay, serverTickLag / 2);
            }
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
            target.rotation = psr.rotation;
            //Note, no simulated rigid body for the visuals, no need to look at the physics data.
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

        void AddToWindow(PhysicsStateRecord accumulator, PhysicsStateRecord newItem)
        {
            accumulator.position += newItem.position;
            accumulator.velocity += newItem.velocity;
            accumulator.angularVelocity += newItem.angularVelocity;
            accumulator.rotation = new Quaternion(
                accumulator.rotation.x + newItem.rotation.x,
                accumulator.rotation.y + newItem.rotation.y,
                accumulator.rotation.z + newItem.rotation.z,
                accumulator.rotation.w + newItem.rotation.w);
        }

        void FinalizeWindow(PhysicsStateRecord accumulator, int count)
        {
            accumulator.position /= count;
            accumulator.velocity /= count;
            accumulator.angularVelocity /= count;
            accumulator.rotation = new Quaternion(
                accumulator.rotation.x / count,
                accumulator.rotation.y / count,
                accumulator.rotation.z / count,
                accumulator.rotation.w / count);
        }
        
        PhysicsStateRecord GetNextProcessedState()
        {
            PhysicsStateRecord psr = new PhysicsStateRecord();
            psr.tickId = buffer.GetEnd().tickId;
            
            if (buffer.GetFill() < slidingWindowTickSize)
            {
                for (int i = buffer.GetStartIndex(); i < buffer.GetEndIndex(); ++i)
                {
                    AddToWindow(psr, buffer.Get(i));
                }
                FinalizeWindow(psr, buffer.GetFill());
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
                
                AddToWindow(psr, buffer.Get(index));
            }
            FinalizeWindow(psr, slidingWindowTickSize);
            return psr;
        }

        public void SetInterpolationTarget(Transform t)
        {
            target = t;
        }
    }
}