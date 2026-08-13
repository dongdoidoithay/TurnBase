using Game.Combat.Model;
using Game.Core.Maths;
using Game.Core.Random;
using Game.Data;

namespace Game.Combat.Systems
{
    public readonly struct DamageResult
    {
        public readonly int Amount;
        public readonly bool IsCrit;
        public readonly bool IsMiss;
        public readonly float ElementMultiplier;

        public DamageResult(int amount, bool isCrit, bool isMiss, float elementMult)
        {
            Amount = amount; IsCrit = isCrit; IsMiss = isMiss; ElementMultiplier = elementMult;
        }

        public static DamageResult Miss => new(0, false, true, 1f);
    }

    /// <summary>
    /// Pipeline sát thương 9 bước — plan.md §4.6. THỨ TỰ CÁC BƯỚC LÀ BẮT BUỘC.
    /// Đổi công thức ở đây → chạy checklist object-map.md §7.7.
    /// </summary>
    public static class DamageCalculator
    {
        public const float BACK_ROW_PHYS_MULT = 0.70f;
        public const float BROKEN_MULT = 1.50f;
        public const float VARIANCE_MIN = 0.95f;
        public const float VARIANCE_MAX = 1.05f;
        public const float ARMOR_CONSTANT = 100f;
        public const float MIN_HIT_CHANCE = 0.10f;

        public static float GradeMultiplier(CommandGrade grade) => grade switch
        {
            CommandGrade.Perfect => 1.30f,
            CommandGrade.Good => 1.10f,
            CommandGrade.Miss => 0.80f,
            _ => 1.00f
        };

        public static DamageResult Calculate(
            CombatUnit attacker, CombatUnit defender,
            SkillRuntime skill, CommandGrade grade, IRandomSource rng)
        {
            var data = skill.Data;
            var atkStats = attacker.Stats;
            var defStats = defender.Stats;

            // --- Bước 1: chọn ATK theo loại sát thương ---
            float atkStat = data.DamageType == DamageType.Magical ? atkStats.AtkMag : atkStats.AtkPhys;

            // --- Bước 2: sát thương cơ sở (dùng EffectivePower để tính cả skill level) ---
            float dmg = atkStat * skill.EffectivePower + data.FlatDamage;

            // --- Bước 3: kiểm tra né (chỉ vật lý; phép không né được) ---
            if (data.DamageType == DamageType.Physical)
            {
                float hitChance = GameMath.Clamp((atkStats.Acc - defStats.Eva) / 100f, MIN_HIT_CHANCE, 1f);
                if (!rng.Chance(hitChance)) return DamageResult.Miss;
            }

            // --- Bước 4: giảm theo giáp (bão hòa, không bao giờ về 0) ---
            if (data.DamageType != DamageType.True)
            {
                float defEff = defStats.Def * (1f - GameMath.Clamp01(data.DefIgnore));
                dmg *= GameMath.ArmorMitigation(defEff, ARMOR_CONSTANT);
            }

            // --- Bước 5: nguyên tố ---
            float elemMult = ElementTable.Multiplier(data.Element, defender.Element);
            dmg *= elemMult;

            // --- Bước 6: chí mạng ---
            // task-tactic-row.md — Focus đảm bảo chí mạng lượt kế (kiểm tra trước chance roll).
            bool crit = attacker.HasStatus(StatusId.Focus)
                     || rng.Chance(GameMath.Clamp(atkStats.Crit, 0.01f, BalanceCaps.CRIT_MAX));
            if (crit) dmg *= 1f + atkStats.CritDmg;

            // --- Bước 7: modifier tổng ---
            dmg *= 1f + atkStats.DmgBonus;
            // task-extra-damage.md — passive/Awakening có Trigger=OnHitDealt được cộng thêm %
            // ngay tại đây (không qua PassiveProcessor vì đó chạy SAU khi damage đã tính xong).
            // task-setbonus.md §1.2 — SetBonus (bộ Tempest) cũng đi qua đúng đường này.
            dmg *= 1f + ExtraDamageFrom(attacker.Passive, attacker)
                      + ExtraDamageFrom(attacker.Awakening, attacker)
                      + ExtraDamageFrom(attacker.SetBonus, attacker);
            dmg *= 1f - GameMath.Clamp(defStats.DmgReduct, 0f, BalanceCaps.DMG_REDUCT_MAX);
            dmg *= GradeMultiplier(grade);
            if (defender.IsBroken) dmg *= BROKEN_MULT;
            if (defender.HasStatus(StatusId.Freeze)) dmg *= 1f + StatusTable.FREEZE_DMG_TAKEN_BONUS;
            if (data.DamageType == DamageType.Physical && defender.Row == Row.Back && !data.IsAoe)
                dmg *= BACK_ROW_PHYS_MULT;

            // --- Bước 8: dao động deterministic ---
            dmg *= rng.NextFloat(VARIANCE_MIN, VARIANCE_MAX);

            // --- Bước 9: sàn 1 (edge case E24) ---
            int final = GameMath.Max(1, GameMath.FloorToInt(dmg));
            return new DamageResult(final, crit, false, elemMult);
        }

        /// <summary>Sát thương DoT — không né, không crit, không grade.</summary>
        public static int CalculateDot(CombatUnit target, StatusInstance status, CombatUnit source)
        {
            var info = StatusTable.Get(status.Id);
            if (info.Group != StatusGroup.Dot) return 0;

            float dmg = status.Id switch
            {
                StatusId.Burn   => target.MaxHp * StatusTable.BURN_HP_PCT_PER_STACK * status.Stacks,
                StatusId.Poison => target.Hp * StatusTable.POISON_HP_PCT_PER_STACK * status.Stacks,
                StatusId.Bleed  => status.Value * StatusTable.BLEED_ATK_RATIO * status.Stacks,
                _ => 0f
            };

            return GameMath.Max(1, GameMath.FloorToInt(dmg));
        }

        /// <summary>Hồi máu = HealPower × ATK. Bị chặn hoàn toàn bởi Curse.</summary>
        public static int CalculateHeal(CombatUnit healer, CombatUnit target, SkillRuntime skill)
        {
            if (target.HasStatus(StatusId.Curse)) return 0;
            var stats = healer.Stats;
            float atk = skill.Data.DamageType == DamageType.Physical ? stats.AtkPhys : stats.AtkMag;
            float heal = atk * skill.Data.HealPower * (1f + 0.06f * (skill.Level - 1));
            return GameMath.Max(1, GameMath.FloorToInt(heal));
        }

        /// <summary>Poise damage của một hit — plan.md §4.9.</summary>
        public static int CalculatePoiseDamage(
            CombatUnit attacker, CombatUnit defender, SkillRuntime skill, CommandGrade grade)
        {
            var data = skill.Data;
            int poise = data.PoiseDamage;

            if (ElementTable.IsStrong(data.Element, defender.Element)) poise += 15;
            if (grade == CommandGrade.Perfect) poise += 8;
            if (data.IsBreaker) poise += 25;
            if (data.PowerMultiplier >= 2.0f) poise += 12;

            poise = GameMath.FloorToInt(poise * (1f + attacker.Stats.PoiseDmgBonus));
            return GameMath.Max(0, poise);
        }

        /// <summary>task-extra-damage.md — CHỈ áp dụng cho passive có Trigger=OnHitDealt (ý
        /// nghĩa "mỗi cú đánh trúng đều cộng thêm X% sát thương"), không phải mọi trigger.
        /// task-setbonus.md §1.2 — bộ Tempest thêm điều kiện RequiresFirstActionOfRound, đọc trực
        /// tiếp <c>attacker.IsFirstActorThisRound</c> (CombatSimulation set trước khi Calculate
        /// chạy) — không cần đổi chữ ký Calculate để nhận BattleState.</summary>
        private static float ExtraDamageFrom(PassiveData p, CombatUnit attacker)
        {
            if (p == null || p.Trigger != PassiveTrigger.OnHitDealt) return 0f;
            if (p.RequiresFirstActionOfRound && !attacker.IsFirstActorThisRound) return 0f;
            return p.ExtraDamagePercent / 100f;
        }
    }
}
