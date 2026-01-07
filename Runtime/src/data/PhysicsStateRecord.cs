using UnityEngine;

namespace Prediction.data
{
    public class PhysicsStateRecord
    {
        public uint tickId;
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        public Vector3 velocity;
        public Vector3 angularVelocity;
        public PredictionInputRecord input;

        public PhysicsStateRecord Empty()
        {
            tickId = 0;
            position = Vector3.zero;
            rotation = Quaternion.identity;
            velocity = Vector3.zero;
            angularVelocity = Vector3.zero;
            input = null;
            return this;
        }

        public void From(Rigidbody rigidbody)
        {
            position = rigidbody.position;
            rotation = rigidbody.rotation;
            velocity = rigidbody.linearVelocity;
            angularVelocity = rigidbody.angularVelocity;
        }

        public void From(PhysicsStateRecord record)
        {
            tickId = record.tickId;
            position = record.position;
            rotation = record.rotation;
            velocity = record.velocity;
            angularVelocity = record.angularVelocity;
        }

        public void To(Rigidbody r)
        {
            r.position = position;
            r.rotation = rotation;
            r.linearVelocity = velocity;
            r.angularVelocity = angularVelocity;
        }
        
        public void From(PhysicsStateRecord record, uint tickOverride)
        {
            From(record);
            tickId = tickOverride;
        }

        public override string ToString()
        {
            return $"t:{tickId} p:{position} r:{rotation} v:{velocity} ang:{angularVelocity} input:{input}";
        }
        
        public override bool Equals(object obj)
        {
            var other = obj as PhysicsStateRecord;

            if (other == null)
            {
                return false;
            }

            return tickId == other.tickId 
                   && position == other.position 
                   && rotation == other.rotation 
                   && velocity == other.velocity 
                   && angularVelocity == other.angularVelocity;
        }

        public override int GetHashCode()
        {
            return (int)tickId + position.GetHashCode() + rotation.GetHashCode() + velocity.GetHashCode() + angularVelocity.GetHashCode();
        }
    }
}