using System.Collections.Generic;

namespace Prediction.utils
{
    //TODO: prevent duplication
    public class TickIndexedBuffer<T>
    {
        public T emptyValue = default(T);
        private int capacity;
        private Dictionary<uint, T> storage;
        private uint start;
        private uint end;
        
        public TickIndexedBuffer(int capacity)
        {
            this.capacity = capacity;
            storage = new Dictionary<uint, T>(capacity);
            start = end = 0;
        }

        void PopOldest()
        {   
            if (GetFill() == 1)
            {
                storage.Remove(start);
                start = end = 0;
                return;
            }

            if (GetFill() == 2)
            {
                storage.Remove(start);
                start = end;
                return;
            }
            
            uint nextStart = GetNextTick(start);
            storage.Remove(start);
            start = nextStart;
        }

        void PopNewest()
        {
            if (GetFill() == 1)
            {
                storage.Remove(end);
                start = end = 0;
                return;
            }

            if (GetFill() == 2)
            {
                storage.Remove(end);
                end = start;
                return;
            }
            
            uint nextEnd = GetPrevTick(end);
            storage.Remove(end);
            end = nextEnd;
        }
        
        public void Add(uint tickId, T item)
        {
            if (GetFill() == capacity)
            {
                PopOldest();
            }
            else if (GetFill() == 0)
            {
                start = tickId;
                end = tickId;
            }

            if (tickId > end)
            {
                end = tickId;
            }
            if (tickId < start)
            {
                start = tickId;
            }
            storage[tickId] = item;
        }

        public T Remove(uint tickId)
        {
            T data = storage.GetValueOrDefault(tickId, emptyValue);
            if (tickId == start)
            {
                PopOldest();
                return data;
            }
            if (tickId == end)
            {
                PopNewest();
                return data;
            }
            
            storage.Remove(tickId);
            return data;
        }

        public bool Contains(uint tickId)
        {
            return storage.ContainsKey(tickId);
        }

        public uint GetEndTick()
        {
            return end;
        }

        public T GetEnd()
        {
            return storage.GetValueOrDefault(end, emptyValue);
        }

        public uint GetStartTick()
        {
            return start;
        }
        
        public T GetStart()
        {
            return storage.GetValueOrDefault(start, emptyValue);
        }
        
        public T Get(uint tickId)
        {
            return storage.GetValueOrDefault(tickId, emptyValue);
        }

        public void Clear()
        {
            start = end = 0;
            storage.Clear();
        }

        public int GetFill()
        {
            return storage.Count;
        }

        public int GetCapacity()
        {
            return capacity;
        }

        public uint GetRange()
        {
            return end - start;
        }

        public uint GetPrevTick(uint tickId)
        {
            if (tickId < start)
                return 0;
            
            return FindClosestTick(tickId, false);
        }
        
        public uint GetNextTick(uint tickId)
        {
            if (tickId > end)
                return 0;

            return FindClosestTick(tickId, true);
        }

        uint FindClosestTick(uint tickId, bool forwards)
        {
            //TODO: optimize...
            uint closestTick = 0;
            uint minDelta = uint.MaxValue;
            foreach (uint key in storage.Keys)
            {
                uint delta;
                if (forwards)
                {
                    delta = (key > tickId) ? key - tickId : end;
                }
                else
                {
                    delta = (key < tickId) ? tickId - key : end;
                }
                
                if (delta < minDelta)
                {
                    closestTick = key;
                    minDelta = delta;
                }
            }
            return closestTick;
        }
    }
}