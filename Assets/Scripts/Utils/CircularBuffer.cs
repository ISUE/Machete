using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Machete
{
    public class CircularBuffer<T>
    {
        public int head;
        public int tail;
        public int _size;
        public T[] data;

        /** 
         * Default Constructor 
         */
        public CircularBuffer()
        {
            head = 0;
            tail = 0;
            _size = 0;
            data = null;
        }

        /**
         * Construct a buffer of size elements.
         * Add plus one to the input if you need
         * access to exactly size elements, since
         * the buffer is full after size - 1
         * insertions.
         */
        public CircularBuffer(int size)
        {
            head = 0;
            tail = 0;
            _size = size;

            data = new T[_size];
        }

        /** 
         * How much the buffer can store.
         */
        public int Size()
        {
            return _size;
        }

        /**
         * How many elements are in the buffer.
         */
        public int Count()
        {
            int ret = tail - head;

            if (ret < 0)
                ret += _size;

            return ret;
        }

        /**
         * Resize buffer, but do not save old data.
         */
        public void Resize(int size)
        {
            head = 0;
            tail = 0;
            _size = size;

            data = new T[_size];
        }

        /**
         * 
         */
        public void Insert(T item)
        {
            // Insert
            data[tail] = item;

            // Increment tail
            tail = (tail + 1) % _size;

            // Push out old data
            if (tail == head)
            {
                head = (head + 1) % _size;
            }
        }

        /**
         * Remove and return element at the end
         */
        public T Pop()
        {
            //Debug.Assert(head != tail);

            T ret = data[head];

            head = (head + 1) % _size;

            return ret;
        }

        /**
         * Reset indices to zero, 
         * which effectively clears the buffer.
         */
        public void Clear()
        {
            head = 0;
            tail = 0;
        }

        /** 
         * Return true uf the buffer is empty,
         * and false otherwise.
         */
        public bool Empty()
        {
            return (head == tail);
        }

        /**
         * Return true if the buffer is full,
         * and false otherwise.
         */
        public bool Full()
        {
            return (head == ((tail + 1) % _size));
        }

        public T GetValue(int idx)
        {
            if (idx < 0)
            {
                idx = tail + idx;

                if (idx < 0)
                    idx += _size;
            }
            else
            {
                idx += head;
            }

            idx = idx % _size;

            return data[idx];
        }

        public void SetValue(int idx, T value)
        {
            if (idx < 0)
            {
                idx = tail + idx;

                if (idx < 0)
                    idx += _size;
            }
            else
            {
                idx += head;
            }

            idx = idx % _size;

            data[idx] = value;
        }

        public T this[int idx]
        {
            get => GetValue(idx);
            set => SetValue(idx, value);
        }

        public void copy(
            List<T> l_out,
            int start,
            int end
            )
        {
            for (int idx = start; idx <= end; idx++)
            {
                l_out.Add((this)[idx]);
            }
        }
    }
}
