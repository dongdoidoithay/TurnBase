using Game.Combat.Events;
using Game.Combat.Model;
using Game.Core.Maths;
using Game.Core.Random;

namespace Game.Combat.Systems
{
    /// <summary>Poise → Break — plan.md §4.9.</summary>
    public sealed class PoiseSystem
    {
        public const int BREAK_DURATION_TURNS = 1;
        public const int POISE_RECOVER_DELAY = 2;

        private readonly BattleState _state;
        private readonly CombatEventQueue _events;
        private readonly StatusProcessor _status;
        private readonly PassiveProcessor _passive;

        public PoiseSystem(BattleState state, CombatEventQueue events, StatusProcessor status, PassiveProcessor passive)
        {
            _state = state; _events = events; _status = status; _passive = passive;
        }

        /// <summary>Trừ Poise. Trả về true nếu vừa gây Break.</summary>
        public bool DamagePoise(CombatUnit source, CombatUnit target, int amount, IRandomSource rng)
        {
            if (target == null || target.IsDead || amount <= 0) return false;
            if (target.IsBroken) return false; // đang Break thì không trừ tiếp

            target.Poise = GameMath.Max(0, target.Poise - amount);
            target.PoiseRecoverDelay = POISE_RECOVER_DELAY;

            _events.Emit(CombatEventType.PoiseDamaged, source?.Id ?? -1, target.Id,
                         intValue: amount, intValue2: target.Poise);

            if (target.Poise > 0) return false;

            TriggerBreak(source, target, rng);
            return true;
        }

        private void TriggerBreak(CombatUnit source, CombatUnit target, IRandomSource rng)
        {
            target.BrokenTurnsLeft = BREAK_DURATION_TURNS;

            // Mất lượt kế tiếp
            TurnScheduler.ResetAtb(target);

            // Mọi debuff kéo dài thêm 1 lượt (một lần duy nhất)
            _status.ExtendDebuffsOnBreak(target);

            _state.BreakCount++;
            _events.Emit(CombatEventType.PoiseBroken, source?.Id ?? -1, target.Id);
            _passive.TriggerOnBreakTriggered(target, rng);
        }

        /// <summary>Gọi ở cuối lượt của unit: giảm Break, hồi Poise sau delay.</summary>
        public void TickTurnEnd(CombatUnit unit)
        {
            if (unit.BrokenTurnsLeft > 0)
            {
                unit.BrokenTurnsLeft--;
                if (unit.BrokenTurnsLeft == 0)
                {
                    unit.PoiseRecoverDelay = POISE_RECOVER_DELAY;
                    unit.Poise = 0; // bắt đầu hồi từ 0
                }
                return;
            }

            if (unit.Poise >= unit.PoiseMax) return;

            if (unit.PoiseRecoverDelay > 0)
            {
                unit.PoiseRecoverDelay--;
                return;
            }

            // Hồi đầy sau khi hết delay
            unit.Poise = unit.PoiseMax;
        }

        /// <summary>Boss đổi phase → reset Poise (plan.md §4.13.3).</summary>
        public void ResetOnPhaseChange(CombatUnit unit)
        {
            unit.Poise = unit.PoiseMax;
            unit.BrokenTurnsLeft = 0;
            unit.PoiseRecoverDelay = 0;
        }
    }
}
