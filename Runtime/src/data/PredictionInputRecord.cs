namespace Prediction.data
{
    public class PredictionInputRecord
    {
        public float[] scalarInput;
        public bool[] binaryInput;

        public int scalarFillIndex;
        public int binaryFillIndex;

        private int scalarReadIndex;
        private int binaryReadIndex;

        public PredictionInputRecord()
        {
            //Serialization constructor...
        }
        
        public PredictionInputRecord(int floatCapacity, int binaryCapacity)
        {
            scalarInput = new float[floatCapacity];
            binaryInput = new bool[binaryCapacity];
        }

        public void WriteReset()
        {
            scalarFillIndex = 0;
            binaryFillIndex = 0;
            ReadReset();
        }
        
        public void ReadReset()
        {
            scalarReadIndex = 0;
            binaryReadIndex = 0;
        }

        public float ReadNextScalar()
        {
            if (scalarReadIndex >= scalarInput.Length)
                return 0;
            return scalarInput[scalarReadIndex++];
        }

        public bool ReadNextBool()
        {
            if (binaryReadIndex >= binaryInput.Length)
                return false;
            return binaryInput[binaryReadIndex++];
        }

        public void WriteNextScalar(float value)
        {
            if (scalarFillIndex >= scalarInput.Length)
                return;
            
            scalarInput[scalarFillIndex++] = value;
        }

        public void WriteNextBinary(bool binary)
        {
            if (binaryFillIndex >= binaryInput.Length)
                return;
            
            binaryInput[binaryFillIndex++] = binary;
        }

        public override string ToString()
        {
            string data = $"PredictionInputRecord(f_tot:{scalarFillIndex} b_tot:{binaryFillIndex} | ";
            for (int i = 0; i < scalarFillIndex; ++i)
            {
                data += scalarInput[i] + "f ";
            }
            for (int i = 0; i < binaryFillIndex; ++i)
            {
                data += binaryInput[i] + " ";
            }
            data += ")";
            return data;
        }
    }
}