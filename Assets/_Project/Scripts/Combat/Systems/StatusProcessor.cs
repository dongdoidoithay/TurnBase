using System.Collections.Generic;
using Game.Combat.Events;
using Game.Combat.Model;
using Game.Core.Maths;
using Game.Core.Random;
using Game.Data;

namespace Game.Combat.Systems
{
    /// <summary>
    /// Apply / tick / expire / stack / resist / dispel / cleanse — plan.md §4.11.
    /// 8 luật chung ở cuối §4.11 được cài trong file này; đổi luật thì chạy object-map.md §7.3.
    /// </summary>
    public sealed class StatusProcessor
    {
        private readonly BattleState _state;
        private readonly CombatEventQueue _events;
        private readonly List<StatusInstance> _removeBuffer = new(8);

        public StatusProcessor(BattleState state, CombatEventQueue events)
        {
            _state = state;
            _events = events;
        }

        // =====================================================================
        // ÁP DỤNG
        // =====================================================================

        /// <summary>
        /// Áp một status. Trả về true nếu áp thành công.
        /// Luật: Immunity chặn hoàn toàn debuff mới; tỉ lệ = chance + EffAcc(nguồn) − RES(đích), kẹp [0.05, 0.95].
        /// </summary>
        public bool Apply(CombatUnit source, CombatUnit target, in StatusApplication app, IRandomSource rng)
        {
            if (target == null || target.IsDead || app.Status == StatusId.None) return false;

            var info = StatusTable.Get(app.Status);
            bool isDebuff = info.Group != StatusGroup.Buff;

            // Luật: Immunity chặn debuff mới, KHÔNG tiêu hao stack
            if (isDebuff && target.HasStatus(StatusId.Immunity))
            {
                _events.Emit(CombatEventType.StatusResisted, source?.Id ?? -1, target.Id, status: app.Status);
                return false;
            }

            // Luật: tỉ lệ áp dụng chỉ tính cho debuff; buff lên đồng minh luôn thành công
            if (isDebuff && app.Chance < 1f)
            {
                float srcEffAcc = source?.Stats.EffAcc ?? 0f;
                float chance = GameMath.Clamp(app.Chance + srcEffAcc - target.Stats.Res, 0.05f, 0.95f);
                if (!rng.Chance(chance))
                {
                    _events.Emit(CombatEventType.StatusResisted, source?.Id ?? -1, target.Id, status: app.Status);
                    return false;
                }
            }

            int duration = app.Duration > 0 ? app.Duration : info.DefaultDuration;
            int stacks = GameMath.Max(1, app.Stacks);
            float value = ComputeStatusValue(app.Status, source, target);

            var existing = target.GetStatus(app.Status);
            if (existing != null)
            {
                // Luật refresh vs stack: chưa đầy stack → +stack VÀ reset duration; đầy → chỉ reset duration
                if (existing.Stacks < info.MaxStacks)
                    existing.Stacks = GameMath.Min(info.MaxStacks, existing.Stacks + stacks);

                existing.RemainingTurns = GameMath.Max(existing.RemainingTurns, duration);

                // Luật E21: 2 nguồn khác nhau → giữ giá trị mạnh hơn
                if (value > existing.Value) { existing.Value = value; existing.SourceUnitId = source?.Id ?? -1; }
                else if (app.Status == StatusId.Shield) existing.Value += value; // Shield cộng dồn giá trị
            }
            else
            {
                stacks = GameMath.Min(stacks, info.MaxStacks);
                target.Statuses.Add(new StatusInstance(app.Status, stacks, duration, source?.Id ?? -1, value));
            }

            target.MarkStatsDirty();

            var final = target.GetStatus(app.Status);
            _events.Emit(CombatEventType.StatusApplied, source?.Id ?? -1, target.Id,
                         intValue: final.Stacks, intValue2: final.RemainingTurns, status: app.Status);
            return true;
        }

        private static float ComputeStatusValue(StatusId id, CombatUnit source, CombatUnit target)
        {
            return id switch
            {
                // Bleed nhớ ATK của người gây để tick độc lập với buff sau này
                StatusId.Bleed => source?.Stats.AtkPhys ?? 0f,
                _ => 0f
            };
        }

        /// <summary>Áp Shield với giá trị hấp thụ cụ thể.</summary>
        public void ApplyShield(CombatUnit source, CombatUnit target, float amount, int duration, IRandomSource rng)
        {
            if (target == null || target.IsDead || amount <= 0f) return;

            var existing = target.GetStatus(StatusId.Shield);
            if (existing != null)
            {
                existing.Value += amount;
                existing.RemainingTurns = GameMath.Max(existing.RemainingTurns, duration);
            }
            else
            {
                target.Statuses.Add(new StatusInstance(StatusId.Shield, 1, duration, source?.Id ?? -1, amount));
            }

            target.MarkStatsDirty();
            _events.Emit(CombatEventType.StatusApplied, source?.Id ?? -1, target.Id,
                         intValue: 1, intValue2: duration,
                         floatValue: target.GetStatus(StatusId.Shield).Value, status: StatusId.Shield);
        }

        // =====================================================================
        // TICK — chạy ở ĐẦU lượt của unit mang status (pipeline bước 3)
        // =====================================================================

        /// <summary>Trả về true nếu unit chết vì DoT.</summary>
        public bool TickTurnStart(CombatUnit unit)
        {
            if (unit.IsDead) return false;

            for (int i = 0; i < unit.Statuses.Count; i++)
            {
                var s = unit.Statuses[i];
                var info = StatusTable.Get(s.Id);
                if (info.Tick != StatusTickTiming.TurnStart) continue;

                if (info.Group == StatusGroup.Dot)
                {
                    var src = s.SourceUnitId >= 0 ? _state.GetUnit(s.SourceUnitId) : null;
                    int dmg = DamageCalculator.CalculateDot(unit, s, src);
                    if (dmg <= 0) continue;

                    unit.SetHp(unit.Hp - dmg);
                    _state.RecordDamage(s.SourceUnitId, dmg);
                    _events.Emit(CombatEventType.StatusTicked, s.SourceUnitId, unit.Id,
                                 intValue: dmg, status: s.Id);

                    if (unit.IsDead)
                    {
                        // Luật: chết do DoT vẫn tính cho người gây status (passive OnKill)
                        _events.Emit(CombatEventType.UnitDied, s.SourceUnitId, unit.Id);
                        return true;
                    }
                }
                else if (s.Id == StatusId.Regen)
                {
                    if (unit.HasStatus(StatusId.Curse)) continue;
                    int heal = GameMath.Max(1, GameMath.FloorToInt(
                        unit.MaxHp * StatusTable.REGEN_HP_PCT_PER_STACK * s.Stacks));
                    int before = unit.Hp;
                    unit.SetHp(unit.Hp + heal);
                    _events.Emit(CombatEventType.HealApplied, s.SourceUnitId, unit.Id,
                                 intValue: unit.Hp - before, status: StatusId.Regen);
                }
            }

            return false;
        }

        // =====================================================================
        // GIẢM THỜI LƯỢNG — chạy ở CUỐI lượt của unit (pipeline bước 13)
        // =====================================================================

        public void TickTurnEnd(CombatUnit unit)
        {
            if (unit.Statuses.Count == 0) return;

            _removeBuffer.Clear();
            for (int i = 0; i < unit.Statuses.Count; i++)
            {
                var s = unit.Statuses[i];
                s.RemainingTurns--;
                if (s.IsExpired) _removeBuffer.Add(s);
            }

            for (int i = 0; i < _removeBuffer.Count; i++)
                RemoveStatus(unit, _removeBuffer[i]);
        }

        public void RemoveStatus(CombatUnit unit, StatusInstance s)
        {
            unit.Statuses.Remove(s);
            unit.MarkStatsDirty();
            _events.Emit(CombatEventType.StatusExpired, target: unit.Id, status: s.Id);
        }

        public void RemoveStatus(CombatUnit unit, StatusId id)
        {
            var s = unit.GetStatus(id);
            if (s != null) RemoveStatus(unit, s);
        }

        // =====================================================================
        // SHIELD — hấp thụ trước khi trừ HP
        // =====================================================================

        /// <summary>Trả về sát thương còn lại sau khi Shield hấp thụ.</summary>
        public int AbsorbWithShield(CombatUnit target, int damage)
        {
            var shield = target.GetStatus(StatusId.Shield);
            if (shield == null || shield.Value <= 0f) return damage;

            int absorbed = GameMath.Min(damage, GameMath.FloorToInt(shield.Value));
            shield.Value -= absorbed;
            int remaining = damage - absorbed;

            _events.Emit(CombatEventType.ShieldAbsorbed, target: target.Id,
                         intValue: absorbed, floatValue: shield.Value);

            if (shield.Value <= 0f)
            {
                _events.Emit(CombatEventType.ShieldBroken, target: target.Id);
                RemoveStatus(target, shield);
            }

            return remaining;
        }

        // =====================================================================
        // CLEANSE / DISPEL
        // =====================================================================

        /// <summary>Xoá tối đa <paramref name="count"/> debuff. Trả về số đã xoá.</summary>
        public int Cleanse(CombatUnit target, int count)
        {
            int removed = 0;
            for (int i = target.Statuses.Count - 1; i >= 0 && removed < count; i--)
            {
                var s = target.Statuses[i];
                if (StatusTable.Get(s.Id).Dispel != DispelType.Cleanse) continue;
                RemoveStatus(target, s);
                removed++;
            }
            return removed;
        }

        /// <summary>Xoá tối đa <paramref name="count"/> buff của địch; nếu <paramref name="thief"/> khác null thì chuyển sang thief.</summary>
        public int Dispel(CombatUnit target, int count, CombatUnit thief, IRandomSource rng)
        {
            int removed = 0;
            for (int i = target.Statuses.Count - 1; i >= 0 && removed < count; i--)
            {
                var s = target.Statuses[i];
                if (StatusTable.Get(s.Id).Dispel != DispelType.Dispel) continue;

                var stolen = s.Clone();
                RemoveStatus(target, s);
                removed++;

                if (thief != null && thief.IsAlive)
                {
                    thief.Statuses.Add(stolen);
                    thief.MarkStatsDirty();
                    _events.Emit(CombatEventType.StatusApplied, target.Id, thief.Id,
                                 intValue: stolen.Stacks, intValue2: stolen.RemainingTurns, status: stolen.Id);
                }
            }
            return removed;
        }

        /// <summary>Break kéo dài mọi debuff thêm 1 lượt (plan.md §4.9) — chỉ áp dụng 1 lần.</summary>
        public void ExtendDebuffsOnBreak(CombatUnit unit)
        {
            for (int i = 0; i < unit.Statuses.Count; i++)
            {
                var s = unit.Statuses[i];
                if (StatusTable.IsDebuff(s.Id)) s.RemainingTurns++;
            }
        }

        /// <summary>Freeze tan ngay khi trúng đòn Fire (edge case E05).</summary>
        public void OnHitByElement(CombatUnit target, Element element)
        {
            if (element == Element.Fire && target.HasStatus(StatusId.Freeze))
                RemoveStatus(target, StatusId.Freeze);
        }

        /// <summary>Sleep tỉnh sau khi damage đã áp dụng (edge case E06).</summary>
        public void OnDamaged(CombatUnit target)
        {
            if (target.HasStatus(StatusId.Sleep))
                RemoveStatus(target, StatusId.Sleep);
        }

        /// <summary>Gỡ Taunt khi unit taunt đã chết (edge case E07).</summary>
        public void ClearTauntFromDead()
        {
            for (int i = 0; i < _state.Units.Count; i++)
            {
                var u = _state.Units[i];
                if (u.IsAlive) continue;
                if (u.HasStatus(StatusId.Taunt)) RemoveStatus(u, StatusId.Taunt);
            }
        }

        /// <summary>task-consumable-items.md — xoá TOÀN BỘ status thuộc đúng 1
        /// <see cref="StatusGroup"/> (VD Antidote "cleanse mọi DoT"). Khác <see cref="Cleanse"/>
        /// (xoá theo <see cref="DispelType.Cleanse"/> — bao trùm cả Control/Debuff, quá rộng cho
        /// nhu cầu "chỉ DoT"). Trả về số đã xoá.</summary>
        public int CleanseGroup(CombatUnit target, StatusGroup group)
        {
            int removed = 0;
            for (int i = target.Statuses.Count - 1; i >= 0; i--)
            {
                var s = target.Statuses[i];
                if (StatusTable.Get(s.Id).Group != group) continue;
                RemoveStatus(target, s);
                removed++;
            }
            return removed;
        }

        /// <summary>Xoá toàn bộ status — dùng khi hồi sinh (edge case E10).</summary>
        public void ClearAll(CombatUnit unit)
        {
            for (int i = unit.Statuses.Count - 1; i >= 0; i--)
                RemoveStatus(unit, unit.Statuses[i]);
        }
    }
}
