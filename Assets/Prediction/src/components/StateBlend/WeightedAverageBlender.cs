using Prediction.data;
using Prediction.utils;
using UnityEngine;

namespace Prediction.StateBlend
{
    public class WeightedAverageBlender : FollowerStateBlender
    {
        public void Reset()
        {
            //ONLY IF NEEDED
        }

        public bool BlendStep(ClientPredictedEntity.FollowerState state, RingBuffer<PhysicsStateRecord> blendedStateBuffer, RingBuffer<PhysicsStateRecord> followerStateBuffer,
            TickIndexedBuffer<PhysicsStateRecord> serverStateBuffer)
        {
            PhysicsStateRecord svState = serverStateBuffer.Get(state.tickId);
            int prevTick = (int) state.tickId - 1;
            PhysicsStateRecord prevState = followerStateBuffer.Get(prevTick); 
            PhysicsStateRecord svPrevState = serverStateBuffer.Get((uint) prevTick);
            PhysicsStateRecord blendState = blendedStateBuffer.Get((int)state.tickId);
            blendState.tickId = state.tickId;
            
            //Was there no movement on the server?
            
            if (svState == null || svPrevState == null)
            {
                blendState.From(prevState, state.tickId);
                return true;
            }
            
            bool noSvMovementEdgecase = (svState.position - svPrevState.position).magnitude < 0.001f;
            if (noSvMovementEdgecase)
            {
                blendState.From(prevState, state.tickId);
                return true;
            }
            
            //Blend
            float serverBias = (float)(state.tickId - state.overlapWithAuthorityStart) / (state.overlapWithAuthorityEnd - state.overlapWithAuthorityStart);
            blendState.position = Vector3.Lerp(prevState.position, svState.position, serverBias);
            blendState.rotation = Quaternion.Lerp(prevState.rotation, svState.rotation, serverBias);
            blendState.velocity = Vector3.Lerp(prevState.velocity, svState.velocity, serverBias);
            blendState.angularVelocity = Vector3.Lerp(prevState.angularVelocity, svState.angularVelocity, serverBias);
            return true;
        }

        public void SetSmoothingFactor(float factor)
        {
            //TODO - modify window based on this.
        }
    }
}