namespace Prediction.data
{
    public class WorldStateRecord
    {
        public uint tickId;
        public uint[] entityIDs = new uint[0];
        public PhysicsStateRecord[] states = new PhysicsStateRecord[0];
        public int fill = 0;

        public void WriteReset()
        {
            fill = 0;
        }

        public void Resize(int totalSize)
        {
            if (totalSize == states.Length)
            {
                return;
            }
            
            WriteReset();
            entityIDs = new uint[totalSize];
            states = new PhysicsStateRecord[totalSize];
        }
        
        public void Set(uint id, PhysicsStateRecord stateRecord)
        {
            entityIDs[fill] = id;
            states[fill] = stateRecord;
            fill++;
        }
    }
}