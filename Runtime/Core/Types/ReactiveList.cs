using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace Shtl.Mvvm
{
    public class ReactiveList<TElement> : IReactiveValue, IList<TElement>, IReactiveListCount, IReadOnlyList<TElement>
    {
        private static readonly Func<int, TElement> _defaultFactory = _ => (TElement)Activator.CreateInstance(typeof(TElement));

        private Action<int, TElement> _onElementAdded;
        private Action<int, TElement> _onElementReplaced;
        private Action<int, TElement> _onElementRemoved;
        private Action<ReactiveList<TElement>> _onContentChanged;

        [CanBeNull] private List<TElement> _list;
        public IReadOnlyList<TElement> Value => _list;
        private bool _isBound;

        public ReactiveList() { }
        public ReactiveList(IReadOnlyCollection<TElement> initCollection) => GetOrCreateListInternal().AddRange(initCollection);

        public void Connect(
            Action<ReactiveList<TElement>> onContentChanged,
            Action<int, TElement> onElementAdded,
            Action<int, TElement> onElementReplaced,
            Action<int, TElement> onElementRemoved
        )
        {
            if (_isBound)
            {
                throw new InvalidOperationException("Already bound");
            }

            _onContentChanged = onContentChanged;
            _onElementAdded = onElementAdded;
            _onElementReplaced = onElementReplaced;
            _onElementRemoved = onElementRemoved;
            _isBound = true;

            if (_list != null)
            {
                _onContentChanged?.Invoke(this);
            }
        }

        void IReactiveValue.Dispose()
        {
            Unbind();
            _list = default;
        }

        public void ResizeAndFill(int newSize, Func<int, TElement> factoryMethod = null)
        {
            var list = GetOrCreateListInternal(newSize);
            var factory = factoryMethod ?? _defaultFactory;

            while (list.Count > newSize)
            {
                RemoveAtInternal(list, list.Count - 1);
            }

            list.Capacity = newSize;

            while (list.Count < newSize)
            {
                var newIndex = list.Count;
                var item = factory(newIndex);
                AddInternal(list, item);
            }

            _onContentChanged?.Invoke(this);
        }

        public void Add(TElement item) => AddInternal(GetOrCreateListInternal(), item);

        public void AddRange(IEnumerable<TElement> range)
        {
            var list = GetOrCreateListInternal();
            foreach (var element in range)
            {
                AddInternal(list, element);
            }
            _onContentChanged?.Invoke(this);
        }

        public void Insert(int index, TElement item)
        {
            GetOrCreateListInternal().Insert(index, item);
            _onElementAdded?.Invoke(index, item);
        }

        public bool Remove(TElement item)
        {
            if (_list == null)
            {
                return false;
            }

            int index = IndexOf(item);
            if (index >= 0)
            {
                RemoveAtInternal(_list, index);
                return true;
            }

            return false;
        }

        public void RemoveAt(int index)
        {
            if (_list == null)
            {
                return;
            }
            RemoveAtInternal(_list, index);
        }

        public void Clear()
        {
            if (_list == null)
            {
                return;
            }

            while (_list.Count > 0)
            {
                RemoveAtInternal(_list, _list.Count - 1);
            }
            _onContentChanged?.Invoke(this);
        }

        public void Sort(IComparer<TElement> comparer)
        {
            if (_list == null)
            {
                return;
            }

            _list.Sort(comparer);
            _onContentChanged?.Invoke(this);
        }

        public TElement this[int index]
        {
            get => _list![index];
            set
            {
                _list![index] = value;
                _onElementReplaced?.Invoke(index, value);
            }
        }

        public int Count => _list?.Count ?? 0;
        public bool Contains(TElement item) => _list!.Contains(item);
        public int IndexOf(TElement item) => _list!.IndexOf(item);

        public void CopyTo(TElement[] array, int arrayIndex) => _list!.CopyTo(array, arrayIndex);
        bool ICollection<TElement>.IsReadOnly => false;

        public void Unbind()
        {
            _onContentChanged = default;
            _onElementAdded = default;
            _onElementReplaced = default;
            _onElementRemoved = default;
            _isBound = false;
        }

        public IEnumerator<TElement> GetEnumerator() => _list?.GetEnumerator() ?? Enumerable.Empty<TElement>().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_list)?.GetEnumerator() ?? Enumerable.Empty<TElement>().GetEnumerator();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddInternal(IList<TElement> list, TElement item)
        {
            list.Add(item);
            _onElementAdded?.Invoke(_list!.Count - 1, item);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RemoveAtInternal(IList<TElement> list, int index)
        {
            var item = list[index];
            list.RemoveAt(index);
            _onElementRemoved?.Invoke(index, item);
        }

        private List<TElement> GetOrCreateListInternal() => _list ??= new List<TElement>();
        private List<TElement> GetOrCreateListInternal(int capacity) => _list ??= new List<TElement>(capacity);
    }
}
