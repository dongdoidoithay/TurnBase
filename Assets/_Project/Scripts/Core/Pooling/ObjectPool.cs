using System;
using System.Collections.Generic;

namespace Game.Core.Pooling
{
    public interface IPoolable
    {
        /// <summary>Reset TOÀN BỘ trạng thái. Bỏ sót ở đây là nguồn bug "object cũ còn dính dữ liệu".</summary>
        void OnReturnToPool();
        void OnTakeFromPool();
    }

    /// <summary>Pool generic 0-alloc. Ngân sách hiệu năng plan.md §11.8.</summary>
    public sealed class ObjectPool<T> where T : class
    {
        private readonly Stack<T> _available;
        private readonly Func<T> _factory;
        private readonly Action<T> _onTake;
        private readonly Action<T> _onReturn;
        private readonly int _maxSize;

        public int CountAvailable => _available.Count;
        public int CountTotal { get; private set; }

        public ObjectPool(Func<T> factory, int prewarm = 0, int maxSize = 1024,
                          Action<T> onTake = null, Action<T> onReturn = null)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _onTake = onTake;
            _onReturn = onReturn;
            _maxSize = maxSize;
            _available = new Stack<T>(prewarm > 0 ? prewarm : 8);

            for (int i = 0; i < prewarm; i++)
            {
                _available.Push(_factory());
                CountTotal++;
            }
        }

        public T Take()
        {
            T item;
            if (_available.Count > 0) item = _available.Pop();
            else { item = _factory(); CountTotal++; }

            _onTake?.Invoke(item);
            (item as IPoolable)?.OnTakeFromPool();
            return item;
        }

        public void Return(T item)
        {
            if (item == null) return;
            if (_available.Count >= _maxSize) return; // vượt trần thì bỏ, để GC dọn

            (item as IPoolable)?.OnReturnToPool();
            _onReturn?.Invoke(item);
            _available.Push(item);
        }

        public void Clear()
        {
            _available.Clear();
            CountTotal = 0;
        }
    }
}
