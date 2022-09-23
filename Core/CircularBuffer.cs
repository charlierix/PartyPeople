using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game.Core
{
    /// <summary>
    /// This is a fixed sized buffer that can safely be added to as often as you'd like
    /// </summary>
    public class CircularBuffer<T>
    {
        private readonly T[] _buffer;

        private int _index = -1;        // starting at -1 so the first addition is zero
        private int _max = -1;      // useful when first filling the buffer (because _max will be less than _buffer.Length)

        public CircularBuffer(int size)
        {
            _buffer = new T[size];
        }

        public int CurrentSize => Math.Min(_max + 1, _buffer.Length);

        public (T value, bool is_populated) LastestItem
        {
            get
            {
                if (_index < 0)
                    return (default(T), false);
                else
                    return (_buffer[_index], true);
            }
        }

        public void Add(T item)
        {
            _index++;
            if (_index >= _buffer.Length)
                _index = 0;

            _max = Math.Max(_index, _max);

            _buffer[_index] = item;
        }

        public void Clear()
        {
            _index = -1;
            _max = -1;

            for (int i = 0; i < _buffer.Length; i++)
                _buffer[i] = default(T);        // free up memory if it's a reference type
        }

        /// <summary>
        /// Returns the items going back in time.  First item returned is the most recently added, going back to
        /// desired count or buffer size
        /// </summary>
        public IEnumerable<T> GetLatestEntries(int? count = null)
        {
            if (_index < 0)
                yield break;

            int count_final = count ?? _buffer.Length;      //NOTE: if they ask for more than is contained in the array, it will just return the full array's contents

            for (int i = _index; i >= 0; i--)
            {
                count_final--;
                if (count_final < 0)
                    yield break;

                yield return _buffer[i];
            }

            for (int i = Math.Min(_max, _buffer.Length - 1); i > _index; i--)
            {
                count_final--;
                if (count_final < 0)
                    yield break;

                yield return _buffer[i];
            }
        }
    }
}
