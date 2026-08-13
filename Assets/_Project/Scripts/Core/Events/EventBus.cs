using System;
using System.Collections.Generic;

namespace Game.Core.Events
{
    /// <summary>Pub/sub cho tầng meta. KHÔNG dùng trong trận đấu — combat dùng CombatEventQueue.</summary>
    public interface IEventBus
    {
        void Subscribe<T>(Action<T> handler) where T : struct;
        void Unsubscribe<T>(Action<T> handler) where T : struct;
        void Publish<T>(T evt) where T : struct;
        void Clear();
    }

    public sealed class EventBus : IEventBus
    {
        private readonly Dictionary<Type, List<Delegate>> _handlers = new();
        // Buffer tái sử dụng để publish không cấp phát và an toàn khi handler tự huỷ đăng ký
        private readonly List<Delegate> _dispatchBuffer = new(16);

        public void Subscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null) return;
            var type = typeof(T);
            if (!_handlers.TryGetValue(type, out var list))
            {
                list = new List<Delegate>(4);
                _handlers[type] = list;
            }
            if (!list.Contains(handler)) list.Add(handler);
        }

        public void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null) return;
            if (_handlers.TryGetValue(typeof(T), out var list)) list.Remove(handler);
        }

        public void Publish<T>(T evt) where T : struct
        {
            if (!_handlers.TryGetValue(typeof(T), out var list) || list.Count == 0) return;

            _dispatchBuffer.Clear();
            _dispatchBuffer.AddRange(list);
            for (int i = 0; i < _dispatchBuffer.Count; i++)
            {
                if (_dispatchBuffer[i] is Action<T> h) h(evt);
            }
        }

        public void Clear() => _handlers.Clear();
    }
}
