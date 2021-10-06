namespace System.Collections.Generic
{
    using System;
    using System.Diagnostics;

    // A simple stack of objects.  Internally it is implemented as an array,
    // so Push can be O(n).  Pop is O(1).

    [DebuggerDisplay("Count = {Count}")]
    [Runtime.InteropServices.ComVisible(false)]
    public class Stack<T> : IEnumerable<T>, ICollection, IReadOnlyCollection<T>
    {
        private T[] _array;     // Storage for stack elements
        private int _size;           // Number of items in the stack.
        private int _version;        // Used to keep enumerator in sync w/ collection.
        private Object _syncRoot;

        private const int _defaultCapacity = 4;
        static T[] _emptyArray = new T[0];

        public Stack()
        {
            _array = _emptyArray;
            _size = 0;
            _version = 0;
        }

        // Create a stack with a specific initial capacity.  The initial capacity
        // must be a non-negative number.
        public Stack(int capacity)
        {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            _array = new T[capacity];
            _size = 0;
            _version = 0;
        }

        // Fills a Stack with the contents of a particular collection.  The items are
        // pushed onto the stack in the same order they are read by the enumerator.
        public Stack(IEnumerable<T> collection)
        {
            if (collection == null)
                throw new ArgumentNullException(nameof(collection));

            ICollection<T> c = collection as ICollection<T>;
            if (c != null)
            {
                int count = c.Count;
                _array = new T[count];
                c.CopyTo(_array, 0);
                _size = count;
            }
            else
            {
                _size = 0;
                _array = new T[_defaultCapacity];

                using (IEnumerator<T> en = collection.GetEnumerator())
                {
                    while (en.MoveNext())
                    {
                        Push(en.Current);
                    }
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

        // Removes all Objects from the Stack.
        public void Clear()
        {
            Array.Clear(_array, 0, _size); // Don't need to doc this but we clear the elements so that the gc can reclaim the references.
            _size = 0;
            _version++;
        }

        public bool Contains(T item)
        {
            int count = _size;

            EqualityComparer<T> c = EqualityComparer<T>.Default;
            while (count-- > 0)
            {
                if (((Object)item) == null)
                {
                    if (((Object)_array[count]) == null)
                        return true;
                }
                else if (_array[count] != null && c.Equals(_array[count], item))
                {
                    return true;
                }
            }
            return false;
        }

        // Copies the stack into an array.
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

            if (array.Length - arrayIndex < _size)
            {
                throw new ArgumentException();
            }

            Array.Copy(_array, 0, array, arrayIndex, _size);
            Array.Reverse(array, arrayIndex, _size);
        }

        void ICollection.CopyTo(Array array, int arrayIndex)
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

            if (arrayIndex < 0 || arrayIndex > array.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(arrayIndex));
            }

            if (array.Length - arrayIndex < _size)
            {
                throw new ArgumentException();
            }

            try
            {
                Array.Copy(_array, 0, array, arrayIndex, _size);
                Array.Reverse(array, arrayIndex, _size);
            }
            catch (ArrayTypeMismatchException)
            {
                throw new ArgumentException();
            }
        }

        // Returns an IEnumerator for this Stack.
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

        public void TrimExcess()
        {
            int threshold = (int)(((double)_array.Length) * 0.9);
            if (_size < threshold)
            {
                T[] newarray = new T[_size];
                Array.Copy(_array, 0, newarray, 0, _size);
                _array = newarray;
                _version++;
            }
        }

        // Returns the top object on the stack without removing it.  If the stack
        // is empty, Peek throws an InvalidOperationException.
        public T Peek()
        {
            if (_size == 0)
                throw new InvalidOperationException();

            return _array[_size - 1];
        }

        // Pops an item from the top of the stack.  If the stack is empty, Pop
        // throws an InvalidOperationException.
        public T Pop()
        {
            if (_size == 0)
                throw new InvalidOperationException();

            _version++;
            T item = _array[--_size];
            _array[_size] = default(T);     // Free memory quicker.
            return item;
        }

        // Pushes an item to the top of the stack.
        public void Push(T item)
        {
            if (_size == _array.Length)
            {
                T[] newArray = new T[(_array.Length == 0) ? _defaultCapacity : 2 * _array.Length];
                Array.Copy(_array, 0, newArray, 0, _size);
                _array = newArray;
            }
            _array[_size++] = item;
            _version++;
        }

        // Copies the Stack to an array, in the same order Pop would return the items.
        public T[] ToArray()
        {
            T[] objArray = new T[_size];
            int i = 0;
            while (i < _size)
            {
                objArray[i] = _array[_size - i - 1];
                i++;
            }
            return objArray;
        }

        public struct Enumerator : IEnumerator<T>, IEnumerator
        {
            private Stack<T> _stack;
            private int _index;
            private int _version;
            private T currentElement;

            internal Enumerator(Stack<T> stack)
            {
                _stack = stack;
                _version = _stack._version;
                _index = -2;
                currentElement = default(T);
            }

            public void Dispose()
            {
                _index = -1;
            }

            public bool MoveNext()
            {
                bool retval;

                if (_version != _stack._version) throw new InvalidOperationException();

                if (_index == -2)
                {  // First call to enumerator.
                    _index = _stack._size - 1;
                    retval = (_index >= 0);
                    if (retval)
                        currentElement = _stack._array[_index];
                    return retval;
                }
                if (_index == -1)
                {  // End of enumeration.
                    return false;
                }

                retval = (--_index >= 0);
                if (retval)
                    currentElement = _stack._array[_index];
                else
                    currentElement = default(T);
                return retval;
            }

            public T Current
            {
                get
                {
                    if (_index == -2) throw new InvalidOperationException();
                    if (_index == -1) throw new InvalidOperationException();
                    return currentElement;
                }
            }

            Object IEnumerator.Current
            {
                get
                {
                    if (_index == -2) throw new InvalidOperationException();
                    if (_index == -1) throw new InvalidOperationException();
                    return currentElement;
                }
            }

            void IEnumerator.Reset()
            {
                if (_version != _stack._version) throw new InvalidOperationException();
                _index = -2;
                currentElement = default(T);
            }
        }
    }
}