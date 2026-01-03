using System.Collections.Generic;
using Prediction.data;
using Prediction.utils;
using UnityEngine;

namespace Prediction.Simulation
{
    //TODO: UNIT TEST!
    public class RewindablePhysicsController : PhysicsController
    {
        public static bool DEBUG_STEP = false;
        public static bool LOG_STEP = false;
        
        public static RewindablePhysicsController Instance;
        
        public int bufferSize = 60;
        private uint tickId = 1;
        private Dictionary<Rigidbody, RingBuffer<PhysicsStateRecord>> worldHistory = new();
        private ClientPredictedEntity mainResimulationEntity;

        public RewindablePhysicsController()
        {
        }
        
        public RewindablePhysicsController(int bufferSize)
        {
            this.bufferSize = bufferSize;
        }

        public uint GetTick()
        {
            return tickId;
        }
        
        public void Setup(bool isServer)
        {
            Physics.simulationMode = SimulationMode.Script;
            Instance = this;
            
            //TODO: can i configure the physics engine here?
        }

        void LogState()
        {
            foreach (KeyValuePair<Rigidbody, RingBuffer<PhysicsStateRecord>> pair in worldHistory)
            {
                PhysicsStateRecord psr = pair.Value.Get((int) tickId);
                psr.From(pair.Key);   
                psr.tickId = tickId;
                Debug.Log($"[PHYSICS_CONTROLLER] gid:{pair.Key.gameObject.GetInstanceID()} pt:{tickId} p:{pair.Key.position.ToString("F10")} r:{pair.Key.rotation.ToString("F10")} v:{pair.Key.linearVelocity.ToString("F10")} a:{pair.Key.angularVelocity.ToString("F10")} accF:{pair.Key.GetAccumulatedForce().ToString("F10")} accT:{pair.Key.GetAccumulatedTorque().ToString("F10")}");
            }
        }
        
        public void Simulate()
        {
            if (LOG_STEP)
            {
                LogState();
            }
            
            _SimStep();
            
            if (LOG_STEP)
            {
                LogState();
            }
        }
        
        public void Resimulate(ClientPredictedEntity entity)
        {
            _SimStep();
            if (DEBUG_STEP)
            {
                Debug.Break();
            }
        }

        private void _SimStep()
        {
            Physics.Simulate(Time.fixedDeltaTime);
            SampleWorldState();
            tickId++;
        }
        
        public void BeforeResimulate(ClientPredictedEntity entity)
        {
            mainResimulationEntity = entity;
        }
    
        public bool Rewind(uint ticks)
        {
            if (tickId <= ticks)
                return false;
            
            tickId -= ticks;
            ApplyWorldState(tickId);
            //NOTE: at this point the current tickId was reached!
            tickId++;
            return true;
        }

        public void AfterResimulate(ClientPredictedEntity entity)
        {
            mainResimulationEntity = null;
        }

        void SampleWorldState()
        {
            foreach (KeyValuePair<Rigidbody, RingBuffer<PhysicsStateRecord>> pair in worldHistory)
            {
                PhysicsStateRecord psr = pair.Value.Get((int) tickId);
                psr.From(pair.Key);   
                psr.tickId = tickId;
            }
        }

        void ApplyWorldState(uint pos)
        {
            foreach (KeyValuePair<Rigidbody, RingBuffer<PhysicsStateRecord>> pair in worldHistory)
            {
                PhysicsStateRecord psr = pair.Value.Get((int) pos);
                psr.To(pair.Key);
            }
        }
        
        public void Track(Rigidbody rigidbody)
        {
            RingBuffer<PhysicsStateRecord> ringBuffer = new RingBuffer<PhysicsStateRecord>(bufferSize);
            for (int i = 0; i < bufferSize; i++)
            {
                ringBuffer.Set(i, (new PhysicsStateRecord()).Empty());
            }
            worldHistory[rigidbody] = ringBuffer;
            //rigidbody.maxDepenetrationVelocity = MAX_DEPENTRATION_SPEED;
            //rigidbody.solverIterations = SOLVER_ITERATIONS;
            //rigidbody.solverVelocityIterations = VELO_SOLVER_ITERATIONS;
            rigidbody.interpolation = RigidbodyInterpolation.None;
        }

        public void Untrack(Rigidbody rigidbody)
        {
            worldHistory.Remove(rigidbody);
        }
    }
}