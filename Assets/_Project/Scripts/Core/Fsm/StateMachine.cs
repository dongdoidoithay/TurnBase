using System;
using System.Collections.Generic;

namespace Game.Core.Fsm
{
    public interface IState
    {
        void Enter();
        void Tick();
        void Exit();
    }

    /// <summary>FSM đơn giản dùng cho GameStateMachine và BattleState machine (plan.md §4.2).</summary>
    public sealed class StateMachine
    {
        private readonly Dictionary<Type, IState> _states = new();

        public IState Current { get; private set; }
        public Type CurrentType => Current?.GetType();

        public event Action<Type, Type> OnStateChanged; // (from, to)

        public void Add<T>(T state) where T : IState => _states[typeof(T)] = state;

        public void Change<T>() where T : IState
        {
            if (!_states.TryGetValue(typeof(T), out var next))
                throw new InvalidOperationException($"State {typeof(T).Name} chưa được Add vào StateMachine.");
            ChangeTo(next);
        }

        public void ChangeTo(IState next)
        {
            if (ReferenceEquals(Current, next)) return;

            var from = Current?.GetType();
            Current?.Exit();
            Current = next;
            Current?.Enter();
            OnStateChanged?.Invoke(from, next?.GetType());
        }

        public void Tick() => Current?.Tick();
    }
}
