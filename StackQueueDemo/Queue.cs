namespace System.Collections.Generic
{
	using System;
	using System.Diagnostics;

	// A simple Queue of generic objects.  Internally it is implemented as a 
	// circular buffer, so Enqueue can be O(n).  Dequeue is O(1).
	[DebuggerDisplay("Count = {Count}")]
    [Runtime.InteropServices.ComVisible(false)]
    public class Queue<T> : IEnumerable<T>, ICollection, IReadOnlyCollection<T>
    {
        private T[] _array;
        private int _head;       // First valid element in the queue
        private int _tail;       // Last valid element in the queue
        private int _size;       // Number of elements.
        private int _version;
        private Object _syncRoot;

        private const int _MinimumGrow = 4;
        private const int _ShrinkThreshold = 32;
        private const int _GrowFactor = 200;  // double each time
        private const int _DefaultCapacity = 4;
        static T[] _emptyArray = new T[0];

        // Creates a queue with room for capacity objects. The default initial
        // capacity and grow factor are used.
        public Queue()
        {
            _array = _emptyArray;
        }

        // Creates a queue with room for capacity objects. The default grow factor
        // is used.
        public Queue(int capacity)
        {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            _array = new T[capacity];
            _head = 0;
            _tail = 0;
            _size = 0;
        }

        // Fills a Queue with the elements of an ICollection.  Uses the enumerator
        // to get each of the elements.
        public Queue(IEnumerable<T> collection)
        {
            if (collection == null)
                throw new ArgumentNullException(nameof(collection));

            _array = new T[_DefaultCapacity];
            _size = 0;
            _version = 0;

            using (IEnumerator<T> en = collection.GetEnumerator())
            {
                while (en.MoveNext())
                {
                    Enqueue(en.Current);
                }
            }
        }


        public int Count
        {
            get { return _size; }
        }

        bool ICollection.IsSynchronized
        {
            get { return false; }
        }

        Object ICollection.SyncRoot
        {
            get
            {
                if (_syncRoot == null)
                {
                    Threading.Interlocked.CompareExchange<Object>(ref _syncRoot, new Object(), null);
                }
                return _syncRoot;
            }
        }

        // Removes all Objects from the queue.
        public void Clear()
        {
            if (_head < _tail)
                Array.Clear(_array, _head, _size);
            else
            {
                Array.Clear(_array, _head, _array.Length - _head);
                Array.Clear(_array, 0, _tail);
            }

            _head = 0;
            _tail = 0;
            _size = 0;
            _version++;
        }

        // CopyTo copies a collection into an Array, starting at a particular
        // index into the array.
        public void CopyTo(T[] array, int arrayIndex)
        {
            if (array == null)
            {
                throw new ArgumentNullException(nameof(array));
            }

            if (arrayIndex < 0 || arrayIndex > array.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(arrayIndex));
            }

            int arrayLen = array.Length;
            if (arrayLen - arrayIndex < _size)
            {
                throw new ArgumentException();
            }

            int numToCopy = (arrayLen - arrayIndex < _size) ? (arrayLen - arrayIndex) : _size;
            if (numToCopy == 0) return;

            int firstPart = (_array.Length - _head < numToCopy) ? _array.Length - _head : numToCopy;
            Array.Copy(_array, _head, array, arrayIndex, firstPart);
            numToCopy -= firstPart;
            if (numToCopy > 0)
            {
                Array.Copy(_array, 0, array, arrayIndex + _array.Length - _head, numToCopy);
            }
        }

        void ICollection.CopyTo(Array array, int index)
        {
            if (array == null)
            {
                throw new ArgumentNullException(nameof(array));
            }

            if (array.Rank != 1)
            {
                throw new ArgumentException();
            }

            if (array.GetLowerBound(0) != 0)
            {
                throw new ArgumentException();
            }

            int arrayLen = array.Length;
            if (index < 0 || index > arrayLen)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            if (arrayLen - index < _size)
            {
                throw new ArgumentException();
            }

            int numToCopy = (arrayLen - index < _size) ? arrayLen - index : _size;
            if (numToCopy == 0) return;

            try
            {
                int firstPart = (_array.Length - _head < numToCopy) ? _array.Length - _head : numToCopy;
                Array.Copy(_array, _head, array, index, firstPart);
                numToCopy -= firstPart;

                if (numToCopy > 0)
                {
                    Array.Copy(_array, 0, array, index + _array.Length - _head, numToCopy);
                }
            }
            catch (ArrayTypeMismatchException)
            {
                throw new ArgumentException();
            }
        }

        // Adds item to the tail of the queue.
        public void Enqueue(T item)
        {
            if (_size == _array.Length)
            {
                int newcapacity = (int)((long)_array.Length * (long)_GrowFactor / 100);
                if (newcapacity < _array.Length + _MinimumGrow)
                {
                    newcapacity = _array.Length + _MinimumGrow;
                }
                SetCapacity(newcapacity);
            }

            _array[_tail] = item;
            _tail = (_tail + 1) % _array.Length;
            _size++;
            _version++;
        }

        // GetEnumerator returns an IEnumerator over this Queue.  This
        // Enumerator will support removing.
        public Enumerator GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new Enumerator(this);
        }

        // Removes the object at the head of the queue and returns it. If the queue
        // is empty, this method simply returns null.
        public T Dequeue()
        {
            if (_size == 0)
                throw new InvalidOperationException();

            T removed = _array[_head];
            _array[_head] = default(T);
            _head = (_head + 1) % _array.Length;
            _size--;
            _version++;
            return removed;
        }

        // Returns the object at the head of the queue. The object remains in the
        // queue. If the queue is empty, this method throws an 
        // InvalidOperationException.
        public T Peek()
        {
            if (_size == 0)
                throw new InvalidOperationException();

            return _array[_head];
        }

        // Returns true if the queue contains at least one object equal to item.
        // Equality is determined using item.Equals().
        // Exceptions: ArgumentNullException if item == null.
        public bool Contains(T item)
        {
            int index = _head;
            int count = _size;

            EqualityComparer<T> c = EqualityComparer<T>.Default;
            while (count-- > 0)
            {
                if (((Object)item) == null)
                {
                    if (((Object)_array[index]) == null)
                        return true;
                }
                else if (_array[index] != null && c.Equals(_array[index], item))
                {
                    return true;
                }
                index = (index + 1) % _array.Length;
            }

            return false;
        }

        internal T GetElement(int i)
        {
            return _array[(_head + i) % _array.Length];
        }

        // Iterates over the objects in the queue, returning an array of the
        // objects in the Queue, or an empty array if the queue is empty.
        // The order of elements in the array is first in to last in, the same
        // order produced by successive calls to Dequeue.
        public T[] ToArray()
        {
            T[] arr = new T[_size];
            if (_size == 0)
                return arr;

            if (_head < _tail)
            {
                Array.Copy(_array, _head, arr, 0, _size);
            }
            else
            {
                Array.Copy(_array, _head, arr, 0, _array.Length - _head);
                Array.Copy(_array, 0, arr, _array.Length - _head, _tail);
            }

            return arr;
        }


        // PRIVATE Grows or shrinks the buffer to hold capacity objects. Capacity
        // must be >= _size.
        private void SetCapacity(int capacity)
        {
            T[] newarray = new T[capacity];
            if (_size > 0)
            {
                if (_head < _tail)
                {
                    Array.Copy(_array, _head, newarray, 0, _size);
                }
                else
                {
                    Array.Copy(_array, _head, newarray, 0, _array.Length - _head);
                    Array.Copy(_array, 0, newarray, _array.Length - _head, _tail);
                }
            }

            _array = newarray;
            _head = 0;
            _tail = (_size == capacity) ? 0 : _size;
            _version++;
        }

        public void TrimExcess()
        {
            int threshold = (int)(((double)_array.Length) * 0.9);
            if (_size < threshold)
            {
                SetCapacity(_size);
            }
        }

        // Implements an enumerator for a Queue.  The enumerator uses the
        // internal version number of the list to ensure that no modifications are
        // made to the list while an enumeration is in progress.
        public struct Enumerator : IEnumerator<T>, IEnumerator
        {
            private Queue<T> _q;
            private int _index;   // -1 = not started, -2 = ended/disposed
            private int _version;
            private T _currentElement;

            internal Enumerator(Queue<T> q)
            {
                _q = q;
                _version = _q._version;
                _index = -1;
                _currentElement = default(T);
            }

            public void Dispose()
            {
                _index = -2;
                _currentElement = default(T);
            }

            public bool MoveNext()
            {
                if (_version != _q._version) throw new InvalidOperationException();

                if (_index == -2)
                    return false;

                _index++;

                if (_index == _q._size)
                {
                    _index = -2;
                    _currentElement = default(T);
                    return false;
                }

                _currentElement = _q.GetElement(_index);
                return true;
            }

            public T Current
            {
                get
                {
                    if (_index < 0)
                    {
                        if (_index == -1)
                            throw new InvalidOperationException();
                        else
                            throw new InvalidOperationException();
                    }
                    return _currentElement;
                }
            }

            Object IEnumerator.Current
            {
                get
                {
                    if (_index < 0)
                    {
                        if (_index == -1)
                            throw new InvalidOperationException();
                        else
                            throw new InvalidOperationException();
                    }
                    return _currentElement;
                }
            }

            void IEnumerator.Reset()
            {
                if (_version != _q._version) throw new InvalidOperationException();
                _index = -1;
                _currentElement = default(T);
            }
        }
    }
}