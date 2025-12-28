using System;
using Prediction.data;
using Prediction.utils;
using UnityEngine;

namespace Prediction.Interpolation
{
    //TODO: do we need a common interpolator class with the buffering logic? can this live in the visuals class?
    public class MovingAverageInterpolator: VisualsInterpolationsProvider
    {
        public static int DEBUG_COUNTER = 0;
        public static int FOLLOWER_SMOOTH_WINDOW = 6;
        
        RingBuffer<PhysicsStateRecord> buffer = new RingBuffer<PhysicsStateRecord>(200);
        public RingBuffer<PhysicsStateRecord> averagedBuffer = new RingBuffer<PhysicsStateRecord>(3);

        private Transform target;
        private double tickInterval = Time.fixedDeltaTime;
        
        private double time = 0;
        private bool interpStarted = false;
        private uint smoothingTick = 0;
        
        
        public int slidingWindowTickSize = 6;
        public int startAfterBfrTicks = 2;
        public float MinVisualDelay = 0.5f;
        public uint minVisualTickDelay = 2;
        public bool autosizeWindow = false;
        public Func<uint> GetServerTickLag;
        
        public int debugCounterLocal;
        
        
        public MovingAverageInterpolator()
        {
            debugCounterLocal = DEBUG_COUNTER;
            DEBUG_COUNTER++;
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

        private uint pervTick = 0;
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
                if (psr == null)
                {
                    Debug.Log($"[LERP]({debugCounterLocal}) WARNING. no data");
                    return;
                }
                
                time = GetTime(psr);
                interpStarted = true;
                pervTick = psr.tickId;
            }
            else
            {
                psr = GetNextInterpolationTarget(time);
                if (psr == null)
                {
                    Debug.Log($"[LERP]({debugCounterLocal}) WARNING. no data");
                    return;
                }
                
                double targetTime = GetTime(psr);
                interpolationProgress = (float)((targetTime - time) / tickInterval);
                uint delta = psr.tickId - pervTick;
                if (delta > 1)
                {
                    //Debug.Log($"[LERP]({debugCounterLocal}) WARNING. Smooth Lerb skipped {delta} ticks.");
                }
                pervTick = psr.tickId;
                //Debug.Log($"[LERP]({debugCounterLocal}) time:{time} targetTime:{targetTime} ttik:{psr.tickId} it:{interpolationProgress}  tickInterval:{tickInterval} deltaTime:{Time.deltaTime}");
            }
            ApplyState(psr, interpolationProgress);
        }

        void ApplyState(PhysicsStateRecord psr, float t)
        {
            target.position = Vector3.Lerp(target.position, psr.position, t);
            target.rotation = Quaternion.Lerp(target.rotation, psr.rotation, t);
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
            averagedBuffer.Add(GetNextProcessedState());
        }
        
        private Vector4 _rotationAvgAccumulator = Vector4.zero;
        void AddToWindow(PhysicsStateRecord accumulator, PhysicsStateRecord newItem)
        {
            accumulator.position += newItem.position;
            accumulator.velocity += newItem.velocity;
            accumulator.angularVelocity += newItem.angularVelocity;
            _rotationAvgAccumulator += new Vector4(newItem.rotation.x, newItem.rotation.y, newItem.rotation.z, newItem.rotation.w);
        }

        void FinalizeWindow(PhysicsStateRecord accumulator, int count)
        {
            accumulator.position /= count;
            accumulator.velocity /= count;
            accumulator.angularVelocity /= count;
            accumulator.rotation = NormalizeQuaternion(_rotationAvgAccumulator / count);
            _rotationAvgAccumulator = Vector4.zero;
        }
        
        private Quaternion NormalizeQuaternion(Vector4 v)
        {
            float lengthSq = v.x * v.x + v.y * v.y + v.z * v.z + v.w * v.w;
            if (lengthSq < Mathf.Epsilon) return Quaternion.identity;
        
            float length = Mathf.Sqrt(lengthSq);
            return new Quaternion(v.x / length, v.y / length, v.z / length, v.w / length);
        }
        
        PhysicsStateRecord GetNextProcessedState()
        {
            PhysicsStateRecord psr = new PhysicsStateRecord();
            psr.tickId = smoothingTick;
            smoothingTick++;
            
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

        public void Reset()
        {
            buffer.Clear();
        }

        public void SetControlledLocally(bool isLocalAuthority)
        {
            if (isLocalAuthority)
            {
                slidingWindowTickSize = 6;
            }
            else
            {
                //TODO: adapt to ping
                //TODO: remove coupling to Time.fixedDeltaTime
                //int ticks = Mathf.CeilToInt((float) (PredictionManager.ROUND_TRIP_GETTER() / Time.fixedDeltaTime) * 0.55f);
                //slidingWindowTickSize = 12; //Math.Max(12, ticks);
                //NOTE: not sure we really need this, set it the same as the client
                slidingWindowTickSize = FOLLOWER_SMOOTH_WINDOW;
            }
        }
    }
}