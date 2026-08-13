using System.Collections.Generic;
using Game.Combat.Events;
using Game.Combat.Model;
using Game.Core.Maths;
using Game.Core.Random;
using Game.Data;

namespace Game.Combat.Systems
{
    /// <summary>
    /// Thực thi một hành động — bước 9→12 của pipeline lượt (plan.md §4.3).
    /// THỨ TỰ trong ResolveSkill LÀ BẮT BUỘC: damage áp dụng từng hit, status sau damage của hit đó.
    /// </summary>
    public sealed class ActionResolver
    {
        private const int MAX_DOWN_TURNS = 3;   // edge case E11
        private const int ULT_GAIN_ON_DAMAGE = 4;
        private const int ULT_GAIN_ON_TAKEN = 6;
        private const int ULT_GAIN_ON_PERFECT = 8;
        private const int SP_GAIN_ON_PERFECT = 5;
        private const int SP_GAIN_ON_GOOD = 2;

        private readonly BattleState _state;
        private readonly CombatEventQueue _events;
        private readonly StatusProcessor _status;
        private readonly PassiveProcessor _passive;
        private readonly PoiseSystem _poise;
        private readonly TargetSelector _targeting;

        private readonly List<CombatUnit> _targetsCopy = new(8);

        public ActionResolver(BattleState state, CombatEventQueue events, StatusProcessor status,
                              PassiveProcessor passive, PoiseSystem poise, TargetSelector targeting)
        {
            _state = state; _events = events; _status = status;
            _passive = passive; _poise = poise; _targeting = targeting;
        }

        // =====================================================================
        // ĐIỂM VÀO
        // =====================================================================

        public void Execute(CombatUnit actor, SkillRuntime skill, int preferredTargetId,
                            CommandGrade grade, IRandomSource rng)
        {
            var data = skill.Data;

            // --- Bước 9: trừ chi phí ---
            actor.AddSp(-data.SpCost);
            skill.CooldownLeft = data.Cooldown;
            _events.Emit(CombatEventType.SpChanged, actor.Id, actor.Id, intValue: actor.Sp);
            if (data.Cooldown > 0)
                _events.Emit(CombatEventType.CooldownChanged, actor.Id,
                             intValue: data.Cooldown, stringValue: data.Id);

            // Thưởng SP / Ultimate theo grade
            if (grade == CommandGrade.Perfect)
            {
                actor.AddSp(SP_GAIN_ON_PERFECT);
                _state.PerfectCount++;
                if (actor.Side == TeamSide.Player) _state.AddUltimate(ULT_GAIN_ON_PERFECT);
                _passive.TriggerOnPerfectCommand(actor, rng, data.SpCost);
            }
            else if (grade == CommandGrade.Good)
            {
                actor.AddSp(SP_GAIN_ON_GOOD);
            }

            // --- Bước 10 đã xảy ra ở tầng trên (Action Command) ---

            // Giải mục tiêu — sao chép vì buffer của TargetSelector bị tái sử dụng
            _targetsCopy.Clear();
            var resolved = _targeting.Resolve(actor, data, preferredTargetId, rng);
            _targetsCopy.AddRange(resolved);

            _events.Emit(CombatEventType.ActionDeclared, actor.Id,
                         _targetsCopy.Count > 0 ? _targetsCopy[0].Id : -1,
                         intValue: _targetsCopy.Count, grade: grade, stringValue: data.Id);

            // --- Bước 11: thực thi ---
            switch (data.Type)
            {
                case SkillType.Heal:    ResolveHeal(actor, skill, grade, rng); break;
                case SkillType.Support: ResolveSupport(actor, skill, rng); break;
                case SkillType.Summon:  ResolveSummon(actor, skill); break;
                default:                ResolveDamage(actor, skill, grade, rng); break;
            }

            // Hiệu ứng phụ áp dụng cho mọi loại skill
            ApplySelfEffects(actor, skill, rng);
        }

        // =====================================================================
        // SÁT THƯƠNG — từng hit một
        // =====================================================================

        private void ResolveDamage(CombatUnit actor, SkillRuntime skill, CommandGrade grade, IRandomSource rng)
        {
            var data = skill.Data;
            int hits = GameMath.Max(1, data.HitCount);

            for (int h = 0; h < hits; h++)
            {
                for (int t = 0; t < _targetsCopy.Count; t++)
                {
                    var target = _targetsCopy[t];

                    // Edge case E01: target chết giữa combo → chuyển sang target khác
                    if (target.IsDead)
                    {
                        if (data.Target != TargetMode.SingleEnemy) continue;
                        var replacement = _targeting.ReplacementTarget(actor, target);
                        if (replacement == null) return;  // hết mục tiêu → dừng, không phí thêm
                        target = replacement;
                        _targetsCopy[t] = replacement;
                    }

                    ApplyOneHit(actor, target, skill, grade, rng);

                    if (actor.IsDead) return; // edge case E03: chết do Counter/Reflect
                }
            }
        }

        private void ApplyOneHit(CombatUnit actor, CombatUnit target, SkillRuntime skill,
                                 CommandGrade grade, IRandomSource rng)
        {
            var data = skill.Data;
            var result = DamageCalculator.Calculate(actor, target, skill, grade, rng);

            if (result.IsMiss)
            {
                _events.Emit(CombatEventType.DamageDealt, actor.Id, target.Id,
                             intValue: -1, element: data.Element, grade: grade, stringValue: data.Id);
                return;
            }

            // Shield hấp thụ TRƯỚC khi trừ HP
            int afterShield = _status.AbsorbWithShield(target, result.Amount);

            if (afterShield > 0)
            {
                target.SetHp(target.Hp - afterShield);
                _state.RecordDamage(actor.Id, afterShield);
            }

            _events.Emit(CombatEventType.DamageDealt, actor.Id, target.Id,
                         intValue: afterShield, intValue2: target.Hp,
                         floatValue: result.ElementMultiplier, flag: result.IsCrit,
                         element: data.Element, grade: grade, stringValue: data.Id);

            // Nạp Ultimate
            if (actor.Side == TeamSide.Player) _state.AddUltimate(ULT_GAIN_ON_DAMAGE);
            else if (target.Side == TeamSide.Player) _state.AddUltimate(ULT_GAIN_ON_TAKEN);

            // Passive/Awakening — sau khi HP đã trừ, TRƯỚC khi kiểm tra chết (task-ascend.md §7 mục A.3)
            if (afterShield > 0)
            {
                _passive.TriggerOnHitDealt(actor, target, rng, result.IsCrit, grade == CommandGrade.Perfect);
                if (target.IsAlive)
                {
                    _passive.TriggerOnDamageTaken(target, actor, rng);
                    _passive.CheckHpThreshold(target, rng);
                }
            }

            // Hút máu
            float lifesteal = actor.Stats.Lifesteal + data.LifestealBonus;
            if (lifesteal > 0f && afterShield > 0 && actor.IsAlive)
            {
                int heal = GameMath.Max(1, GameMath.FloorToInt(afterShield * lifesteal));
                int before = actor.Hp;
                actor.SetHp(actor.Hp + heal);
                if (actor.Hp > before)
                    _events.Emit(CombatEventType.HealApplied, actor.Id, actor.Id,
                                 intValue: actor.Hp - before);
            }

            // Freeze tan khi trúng Fire (E05) — TRƯỚC khi kiểm tra chết
            _status.OnHitByElement(target, data.Element);
            // Sleep tỉnh sau khi damage đã áp dụng (E06)
            _status.OnDamaged(target);

            // Poise
            if (target.IsAlive)
            {
                int poiseDmg = DamageCalculator.CalculatePoiseDamage(actor, target, skill, grade);
                _poise.DamagePoise(actor, target, poiseDmg, rng);
            }

            // Status của skill — SAU khi damage đã trừ HP
            if (target.IsAlive) ApplyStatuses(actor, target, data, rng);

            if (target.IsDead) { HandleDeath(actor, target, rng); return; }

            // --- Bước 12: phản ứng ---
            ResolveReactions(actor, target, skill, result.Amount, rng);
        }

        private void ResolveReactions(CombatUnit actor, CombatUnit target, SkillRuntime skill,
                                      int rawDamage, IRandomSource rng)
        {
            if (!actor.IsAlive) return;

            // Counter — chỉ phản đòn vật lý
            if (target.HasStatus(StatusId.Counter) && skill.Data.DamageType == DamageType.Physical)
            {
                int dmg = GameMath.Max(1, GameMath.FloorToInt(
                    target.Stats.AtkPhys * StatusTable.COUNTER_ATK_RATIO));
                dmg = _status.AbsorbWithShield(actor, dmg);
                if (dmg > 0)
                {
                    actor.SetHp(actor.Hp - dmg);
                    _state.RecordDamage(target.Id, dmg);
                }
                _events.Emit(CombatEventType.DamageDealt, target.Id, actor.Id,
                             intValue: dmg, intValue2: actor.Hp, status: StatusId.Counter,
                             stringValue: skill.Data.Id);
                if (actor.IsDead) { HandleDeath(target, actor, rng); return; }
            }

            // Reflect — chỉ dội sát thương phép
            if (target.HasStatus(StatusId.Reflect) && skill.Data.DamageType == DamageType.Magical)
            {
                int dmg = GameMath.Max(1, GameMath.FloorToInt(rawDamage * StatusTable.REFLECT_RATIO));
                dmg = _status.AbsorbWithShield(actor, dmg);
                if (dmg > 0)
                {
                    actor.SetHp(actor.Hp - dmg);
                    _state.RecordDamage(target.Id, dmg);
                }
                _events.Emit(CombatEventType.DamageDealt, target.Id, actor.Id,
                             intValue: dmg, intValue2: actor.Hp, status: StatusId.Reflect,
                             stringValue: skill.Data.Id);
                if (actor.IsDead) HandleDeath(target, actor, rng);
            }
        }

        // =====================================================================
        // HỒI MÁU / HỖ TRỢ / TRIỆU HỒI
        // =====================================================================

        private void ResolveHeal(CombatUnit actor, SkillRuntime skill, CommandGrade grade, IRandomSource rng)
        {
            var data = skill.Data;

            for (int i = 0; i < _targetsCopy.Count; i++)
            {
                var target = _targetsCopy[i];

                // Hồi sinh (edge case E10)
                if (data.Target == TargetMode.DeadAlly && data.RevivePercent > 0f)
                {
                    if (target.IsAlive || target.PermanentlyDown) continue;
                    _status.ClearAll(target);                       // xoá toàn bộ status
                    target.MarkStatsDirty();
                    target.SetHp(GameMath.Max(1, GameMath.FloorToInt(target.MaxHp * data.RevivePercent)));
                    target.TurnsDown = 0;
                    target.Poise = target.PoiseMax;
                    _events.Emit(CombatEventType.UnitRevived, actor.Id, target.Id, intValue: target.Hp);
                    ApplyStatuses(actor, target, data, rng);
                    continue;
                }

                if (target.IsDead) continue;

                int heal = DamageCalculator.CalculateHeal(actor, target, skill);
                heal = GameMath.FloorToInt(heal * DamageCalculator.GradeMultiplier(grade));

                int before = target.Hp;
                target.SetHp(target.Hp + heal);   // cắt tại MaxHp, phần dư KHÔNG thành shield (E09)
                int actual = target.Hp - before;

                _events.Emit(CombatEventType.HealApplied, actor.Id, target.Id,
                             intValue: actual, intValue2: target.Hp,
                             flag: target.HasStatus(StatusId.Curse));

                if (actual > 0) _passive.TriggerOnHealDone(actor, rng);

                ApplyStatuses(actor, target, data, rng);
            }
        }

        private void ResolveSupport(CombatUnit actor, SkillRuntime skill, IRandomSource rng)
        {
            var data = skill.Data;

            for (int i = 0; i < _targetsCopy.Count; i++)
            {
                var target = _targetsCopy[i];
                if (target.IsDead) continue;

                if (data.ShieldPower > 0f)
                {
                    float amount = actor.Stats.Def * data.ShieldPower;
                    _status.ApplyShield(actor, target, amount, 3, rng);
                }

                if (data.CleanseCount > 0) _status.Cleanse(target, data.CleanseCount);

                if (data.DispelCount > 0)
                    _status.Dispel(target, data.DispelCount, data.StealsDispelled ? actor : null, rng);

                if (data.SpRestore > 0)
                {
                    target.AddSp(data.SpRestore);
                    _events.Emit(CombatEventType.SpRestored, actor.Id, target.Id,
                                 intValue: data.SpRestore, intValue2: target.Sp);
                }

                ApplyStatuses(actor, target, data, rng);
            }
        }

        private void ResolveSummon(CombatUnit actor, SkillRuntime skill)
        {
            var data = skill.Data;
            for (int i = 0; i < GameMath.Max(1, data.SummonCount); i++)
            {
                var minion = new CombatUnit
                {
                    DefId = data.SummonId,
                    DisplayNameKey = data.SummonId,
                    Side = actor.Side,
                    Row = Row.Front,
                    SlotIndex = 90 + i,
                    IsMinion = true,
                    OwnerId = actor.Id,
                    MinionTurnsLeft = data.SummonDuration,
                    Element = actor.Element,
                    Level = actor.Level,
                    PoiseMax = 20,
                    // Minion kế thừa 45% chỉ số của chủ
                    BasePrimary = actor.BasePrimary * 0.45f
                };
                minion.Skills.Add(new SkillRuntime(BasicAttackFor(minion), 0));
                _state.AddUnit(minion);
                minion.FillResources();

                _events.Emit(CombatEventType.MinionSummoned, actor.Id, minion.Id,
                             intValue: data.SummonDuration, stringValue: data.SummonId);
            }
        }

        private static SkillData BasicAttackFor(CombatUnit u) => new()
        {
            Id = "skill_minion_strike",
            NameKey = "skill.minion_strike.name",
            Type = SkillType.Physical,
            DamageType = DamageType.Physical,
            Element = u.Element,
            Target = TargetMode.SingleEnemy,
            PowerMultiplier = 0.9f,
            PoiseDamage = 3,
            CommandType = ActionCommandType.SingleTap
        };

        // =====================================================================
        // HỖ TRỢ
        // =====================================================================

        private void ApplyStatuses(CombatUnit actor, CombatUnit target, SkillData data, IRandomSource rng)
        {
            var applies = data.Applies;
            if (applies == null) return;

            for (int i = 0; i < applies.Length; i++)
            {
                var app = applies[i];
                if (app.TargetSelf) continue;    // xử lý ở ApplySelfEffects
                _status.Apply(actor, target, app, rng);
            }
        }

        private void ApplySelfEffects(CombatUnit actor, SkillRuntime skill, IRandomSource rng)
        {
            var applies = skill.Data.Applies;
            if (applies == null || !actor.IsAlive) return;

            for (int i = 0; i < applies.Length; i++)
            {
                var app = applies[i];
                if (!app.TargetSelf) continue;
                _status.Apply(actor, actor, app, rng);
            }
        }

        private void HandleDeath(CombatUnit killer, CombatUnit victim, IRandomSource rng)
        {
            victim.Hp = 0;
            victim.Atb = 0;
            victim.BrokenTurnsLeft = 0;
            _events.Emit(CombatEventType.UnitDied, killer?.Id ?? -1, victim.Id);
            _status.ClearTauntFromDead();

            _passive.TriggerOnKill(killer, rng);
            _passive.TriggerOnAllyDeath(victim, rng);

            // Edge case E20: minion biến mất sau khi chủ chết được xử lý ở CombatSimulation
        }

        /// <summary>Đánh dấu unit gục thêm 1 lượt; quá 3 lượt → không hồi sinh được (E11).</summary>
        public static void TickDownedUnits(BattleState state)
        {
            for (int i = 0; i < state.Units.Count; i++)
            {
                var u = state.Units[i];
                if (u.IsAlive || u.PermanentlyDown) continue;
                u.TurnsDown++;
                if (u.TurnsDown > MAX_DOWN_TURNS) u.PermanentlyDown = true;
            }
        }
    }
}
