using System;

namespace Prediction.utils
{
    public class RingBuffer<T>
    {
        public T emptyValue = default(T);
        private T[] buffer;
        private int start;
        private int end;
        private int fill = 0;
        
        public RingBuffer(int capacity)
        {
            buffer = new T[capacity];
            start = 0;
            end = 0;
            fill = 0;
        }
        
        public int GetCapacity()
        {
            return buffer.Length;
        }
        
        public int GetFill()
        {
            return fill;
        }
        
        public int GetStartIndex()
        {
            return start;
        }
        
        public int GetEndIndex()
        {
            return end;
        }
        
        public void Add(T item)
        {
            buffer[end++] = item;
            
            end %= buffer.Length;
            if (fill == buffer.Length)
            {
                start++;
                start %= buffer.Length;
            }
            else
            {
                fill++;
            }
        }

        public void Set(int index, T data)
        {
            buffer[index % buffer.Length] = data;
        }

        public T PopStart()
        {
            if (fill == 0)
            {
                return emptyValue;
            }
            
            T item = buffer[start];
            
            start++;
            start %= buffer.Length;
            fill--;
            return item;
        }
        
        public T PopEnd()
        {
            if (fill == 0)
            {
                return emptyValue;
            }
            
            end--;
            end %= buffer.Length;
            if (end < 0)
            {
                end = buffer.Length + end;
            }
            
            T item = buffer[end];
            fill--;
            return item;
        }

        public T Get(int index)
        {
            if (buffer.Length == 0)
                throw new Exception("EMPTY_BUFFER");
            if (index < 0)
                throw new Exception("NEGATIVE_INDEX");
            
            index %= buffer.Length;
            return buffer[index];
        }

        public T GetEnd()
        {
            if (fill == 0)
                return emptyValue;
            
            if (end - 1 < 0)
            {
                return buffer[buffer.Length - 1];
            }
            return buffer[end - 1];
        }

        public T GetStart()
        {
            if (fill == 0)
                return emptyValue;
            return buffer[start];
        }
        
        public T GetWithLocalIndex(int localIndex)
        {
            if (localIndex >= fill)
            {
                return emptyValue;
            }
            
            int index = (start + localIndex) % buffer.Length;
            return buffer[index];
        }

        public void Clear()
        {
            start = end = fill = 0;
        }
    }
}