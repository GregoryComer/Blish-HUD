﻿using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;

namespace Blish_HUD.Controls {

    public class ControlEnumerator<T> : IEnumerator<T> {

        private readonly IEnumerator<T>       _inner;
        private readonly ReaderWriterLockSlim _rwLock;

        public ControlEnumerator(IEnumerator<T> inner, ReaderWriterLockSlim rwLock) {
            _inner  = inner;
            _rwLock = rwLock;

            _rwLock.EnterReadLock();
        }

        public bool MoveNext() {
            return _inner.MoveNext();
        }

        public void Reset() {
            _inner.Reset();
        }

        public object Current => _inner.Current;

        T IEnumerator<T>.Current => _inner.Current;

        public void Dispose() {
            _rwLock.ExitReadLock();
        }

    }

    public class ControlCollection<T> : IList<T>
        where T : Control {

        private readonly List<T>              _innerList;
        private readonly ReaderWriterLockSlim _listLock  = new ReaderWriterLockSlim();

        public bool IsReadOnly => false;

        public ControlCollection() {
            _innerList = new List<T>();
        }

        public ControlCollection(IEnumerable<T> existingControls) {
            _innerList = new List<T>(existingControls);
        }

        public IEnumerator<T> GetEnumerator() {
            return new ControlEnumerator<T>(_innerList.GetEnumerator(), _listLock);
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public void Add(T item) {
            if (this.Contains(item) || item == null) return;
            
            _listLock.EnterWriteLock();
            _innerList.Add(item);
            _listLock.ExitWriteLock();
        }

        public void AddRange(IEnumerable<T> items) {
            _listLock.EnterWriteLock();
            _innerList.AddRange(items);
            _listLock.ExitWriteLock();
        }

        public void Clear() {
            _listLock.EnterWriteLock();
            _innerList.Clear();
            _listLock.ExitWriteLock();
        }

        public bool Contains(T item) {
            _listLock.EnterReadLock();

            try {
                return _innerList.Contains(item);
            } finally {
                _listLock.ExitReadLock();
            }
        }

        public void CopyTo(T[] array, int arrayIndex) {
            _listLock.EnterReadLock();

            try {
                _innerList.CopyTo(array, arrayIndex);
            } finally {
                _listLock.ExitReadLock();
            }
        }

        public bool Remove(T item) {
            _listLock.EnterWriteLock();

            try {
                return _innerList.Remove(item);
            } finally {
                _listLock.ExitWriteLock();
            }
        }

        public int Count {
            get {
                _listLock.EnterReadLock();

                try {
                    return _innerList.Count;
                } finally {
                    _listLock.ExitReadLock();
                }
            }
        }

        public IReadOnlyCollection<T> AsReadOnly() {
            return new ReadOnlyCollection<T>(this);
        }

        public int IndexOf(T item) {
            _listLock.EnterReadLock();

            try {
                return _innerList.Count;
            } finally {
                _listLock.ExitReadLock();
            }
        }

        public void Insert(int index, T item) {
            _listLock.EnterWriteLock();
            _innerList.Insert(index, item);
            _listLock.ExitWriteLock();
        }

        public void RemoveAt(int index) {
            _listLock.EnterWriteLock();
            _innerList.RemoveAt(index);
            _listLock.ExitWriteLock();
        }

        public T this[int index] {
            get {
                _listLock.EnterReadLock();

                try {
                    return _innerList[index];
                } finally {
                    _listLock.ExitReadLock();
                }
            }
            set {
                _listLock.EnterWriteLock();
                _innerList[index] = value;
                _listLock.ExitWriteLock();
            }
        }

        ~ControlCollection() {
            _listLock?.Dispose();
        }

    }
}
