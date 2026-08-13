using Game.Combat.Events;
using Game.Combat.Model;
using Game.Core.Maths;
using Game.Core.Random;
using Game.Data;

namespace Game.Combat.Systems
{
    /// <summary>
    /// task-consumable-items.md — 6 vật phẩm tiêu hao (plan.md §7.5). Không có UI chọn target thủ
    /// công ở bất kỳ đâu trong game (xem task §0.4) — mọi item AUTO-TARGET.
    ///
    /// Potion/Ether/Antidote xử lý TRỰC TIẾP (không qua <see cref="ActionResolver.Execute"/>) vì
    /// hiệu ứng của chúng không khớp cách skill thường hoạt động: Potion hồi % MaxHP của TARGET
    /// (khác <see cref="DamageCalculator.CalculateHeal"/> vốn phụ thuộc stat người dùng); Antidote
    /// cần cleanse ĐÚNG nhóm DoT (khác <see cref="StatusProcessor.Cleanse"/> vốn xoá mọi debuff
    /// Cleanse-type, quá rộng); Ether chỉ là 1 phép cộng SP đơn giản, không đáng route qua cả
    /// pipeline skill. Revive Feather/Elemental Bomb TÁI DÙNG <see cref="ActionResolver.Execute"/>
    /// vì <c>TargetMode.DeadAlly</c>/<c>AllEnemies</c> đã đúng 100% nhu cầu, và Elemental Bomb cần
    /// cả damage lẫn Poise reduction mà <see cref="ActionResolver"/> đã làm đúng sẵn.
    /// </summary>
    public sealed class ItemResolver
    {
        private readonly BattleState _state;
        private readonly CombatEventQueue _events;
        private readonly StatusProcessor _status;
        private readonly ActionResolver _resolver;

        public ItemResolver(BattleState state, CombatEventQueue events, StatusProcessor status, ActionResolver resolver)
        {
            _state = state; _events = events; _status = status; _resolver = resolver;
        }

        /// <summary>Điểm vào duy nhất — trả false nếu không có mục tiêu hợp lệ (VD Antidote không
        /// ai mang DoT). CombatSimulation chỉ trừ ItemLoadout/kết thúc lượt khi trả true (Smoke
        /// Bomb xử lý riêng ở CombatSimulation vì cần gọi Finish()).</summary>
        public bool Use(ItemType type, CombatUnit actor, IRandomSource rng) => type switch
        {
            ItemType.Potion => UsePotion(actor),
            ItemType.Ether => UseEther(actor),
            ItemType.Antidote => UseAntidote(actor),
            ItemType.ReviveFeather => UseReviveFeather(actor, rng),
            ItemType.ElementalBomb => UseElementalBomb(actor, rng),
            _ => false,
        };

        private bool UsePotion(CombatUnit actor)
        {
            var target = LowestHpPercentAlly(actor.Side);
            if (target == null) return false;

            int before = target.Hp;
            target.SetHp(target.Hp + GameMath.FloorToInt(target.MaxHp * 0.35f));
            int actual = target.Hp - before;
            _events.Emit(CombatEventType.HealApplied, actor.Id, target.Id, intValue: actual, intValue2: target.Hp);
            return true;
        }

        private bool UseEther(CombatUnit actor)
        {
            var target = LowestSpAlly(actor.Side);
            if (target == null) return false;

            target.AddSp(40);
            _events.Emit(CombatEventType.SpRestored, actor.Id, target.Id, intValue: 40, intValue2: target.Sp);
            return true;
        }

        private bool UseAntidote(CombatUnit actor)
        {
            var target = FirstAllyWithDot(actor.Side);
            if (target == null) return false;

            _status.CleanseGroup(target, StatusGroup.Dot);
            return true;
        }

        private bool UseReviveFeather(CombatUnit actor, IRandomSource rng)
        {
            var skill = new SkillRuntime(new SkillData
            {
                Id = "item_revive_feather",
                Type = SkillType.Heal,
                Target = TargetMode.DeadAlly,
                RevivePercent = 0.4f,
                CommandType = ActionCommandType.SingleTap,
            }, 0);
            bool hadRevivable = HasRevivableAlly(actor.Side);
            if (!hadRevivable) return false;

            _resolver.Execute(actor, skill, -1, CommandGrade.Miss, rng);
            return true;
        }

        private bool UseElementalBomb(CombatUnit actor, IRandomSource rng)
        {
            var enemySide = actor.Side == TeamSide.Player ? TeamSide.Enemy : TeamSide.Player;
            if (_state.CountAlive(enemySide) == 0) return false;

            var skill = new SkillRuntime(new SkillData
            {
                Id = "item_elemental_bomb",
                Type = SkillType.Magical,
                DamageType = DamageType.Magical,
                Element = Element.Neutral,
                Target = TargetMode.AllEnemies,
                IsAoe = true,
                PowerMultiplier = 2f,
                PoiseDamage = 20,
                CommandType = ActionCommandType.SingleTap,
            }, 0);
            _resolver.Execute(actor, skill, -1, CommandGrade.Miss, rng);
            return true;
        }

        // =====================================================================
        // Auto-target — task-consumable-items.md §0.4 (không có UI chọn target thủ công)
        // =====================================================================

        private CombatUnit LowestHpPercentAlly(TeamSide side)
        {
            CombatUnit best = null;
            float bestPct = float.MaxValue;
            for (int i = 0; i < _state.Units.Count; i++)
            {
                var u = _state.Units[i];
                if (u.Side != side || !u.IsAlive) continue;
                float pct = (float)u.Hp / u.MaxHp;
                if (pct < bestPct) { bestPct = pct; best = u; }
            }
            return best;
        }

        private CombatUnit LowestSpAlly(TeamSide side)
        {
            CombatUnit best = null;
            int bestSp = int.MaxValue;
            for (int i = 0; i < _state.Units.Count; i++)
            {
                var u = _state.Units[i];
                if (u.Side != side || !u.IsAlive) continue;
                if (u.Sp < bestSp) { bestSp = u.Sp; best = u; }
            }
            return best;
        }

        private CombatUnit FirstAllyWithDot(TeamSide side)
        {
            for (int i = 0; i < _state.Units.Count; i++)
            {
                var u = _state.Units[i];
                if (u.Side != side || !u.IsAlive) continue;
                for (int s = 0; s < u.Statuses.Count; s++)
                    if (StatusTable.Get(u.Statuses[s].Id).Group == StatusGroup.Dot) return u;
            }
            return null;
        }

        private bool HasRevivableAlly(TeamSide side)
        {
            for (int i = 0; i < _state.Units.Count; i++)
            {
                var u = _state.Units[i];
                if (u.Side == side && !u.IsAlive && !u.PermanentlyDown) return true;
            }
            return false;
        }
    }
}
