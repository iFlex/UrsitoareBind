using Prediction.data;
using Prediction.utils;

namespace Prediction.StateBlend
{
    public class DefaultToClientSimBlender : FollowerStateBlender
    {
        public void Reset()
        {
            //ONLY IF NEEDED
        }
    
        //NOTE: this should fully favor the client's simulation
        //TODO: figure out why it doesn't do that and rather reacts slower than the server...
        public bool BlendStep(ClientPredictedEntity.FollowerState state, RingBuffer<PhysicsStateRecord> blendedStateBuffer, RingBuffer<PhysicsStateRecord> followerStateBuffer,
            TickIndexedBuffer<PhysicsStateRecord> serverStateBuffer)
        {
            int prevTick = (int) state.tickId - 1;
            PhysicsStateRecord prevState  = followerStateBuffer.Get(prevTick); 
            PhysicsStateRecord blendState = blendedStateBuffer.Get((int)state.tickId);
            blendState.tickId = state.tickId;
            blendState.From(prevState, state.tickId);
            return true;
        }

        public void SetSmoothingFactor(float factor)
        {
            //TODO - modify window based on this.
        }
    }
}