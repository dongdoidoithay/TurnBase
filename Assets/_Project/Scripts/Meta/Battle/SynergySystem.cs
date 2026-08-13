using System.Collections.Generic;
using Game.Combat.Model;
using Game.Data;

namespace Game.Meta.Battle
{
    /// <summary>
    /// 5 điều kiện Team Synergy từ plan.md §5.7.
    /// Tính bonus và bơm trực tiếp vào <see cref="CombatUnit.EquipmentModifiers"/> của mỗi hero.
    /// Gọi sau khi toàn bộ hero đã được spawn — cần thấy đủ đội.
    /// Pure static — test được headless.
    /// </summary>
    public static class SynergySystem
    {
        // Stat chính đại diện cho từng class (dùng cho synergy 2/3 hero cùng class).
        private static StatType ClassStat(HeroClass c) => c switch
        {
            HeroClass.Vanguard  => StatType.DefPct,
            HeroClass.Trickster => StatType.SpdPct,
            HeroClass.Warden    => StatType.MaxHpPct,
            _                   => StatType.AtkPct,   // Slayer, Arcanist, Summoner
        };

        private static bool IsDps(HeroClass c)
            => c == HeroClass.Slayer || c == HeroClass.Arcanist
            || c == HeroClass.Trickster || c == HeroClass.Summoner;

        // ── điểm vào chính ─────────────────────────────────────────────────

        /// <summary>Tính toàn bộ synergy bonus cho <paramref name="playerUnits"/> và bơm vào
        /// <c>EquipmentModifiers</c> của từng unit. Gọi MarkStatsDirty để stat cache refresh.</summary>
        public static void Apply(IReadOnlyList<CombatUnit> playerUnits)
        {
            if (playerUnits == null || playerUnits.Count == 0) return;

            var mods = Compute(playerUnits);
            foreach (var unit in playerUnits)
            {
                unit.EquipmentModifiers.AddRange(mods);
                unit.MarkStatsDirty();
            }
        }

        /// <summary>Trả về danh sách modifier CHUNG áp cho mọi hero trong đội (không phải per-hero).
        /// Tách khỏi Apply để test được không cần CombatUnit thật.</summary>
        public static List<StatModifier> Compute(IReadOnlyList<CombatUnit> playerUnits)
        {
            if (playerUnits == null || playerUnits.Count == 0) return new List<StatModifier>(0);
            var result = new List<StatModifier>(8);

            // ── đếm class ──────────────────────────────────────────────────
            var classCounts = new Dictionary<HeroClass, int>();
            foreach (var u in playerUnits)
            {
                if (!classCounts.TryGetValue(u.Class, out int cnt)) cnt = 0;
                classCounts[u.Class] = cnt + 1;
            }

            // ── đếm element ────────────────────────────────────────────────
            var elementCounts = new Dictionary<Element, int>();
            foreach (var u in playerUnits)
            {
                if (!elementCounts.TryGetValue(u.Element, out int cnt)) cnt = 0;
                elementCounts[u.Element] = cnt + 1;
            }

            // ── điều kiện 1 & 2: ≥2 / ≥3 hero cùng class ─────────────────
            foreach (var kv in classCounts)
            {
                if (kv.Value >= 3)
                {
                    result.Add(new StatModifier(ClassStat(kv.Key), 18f, "synergy"));
                }
                else if (kv.Value == 2)
                {
                    result.Add(new StatModifier(ClassStat(kv.Key), 10f, "synergy"));
                }
            }

            // ── điều kiện 3: ≥2 hero cùng element → +8% DmgBonusPct ───────
            foreach (var kv in elementCounts)
            {
                if (kv.Value >= 2)
                {
                    result.Add(new StatModifier(StatType.DmgBonusPct, 8f, "synergy"));
                    break; // chỉ tính 1 lần dù nhiều cặp element
                }
            }

            // ── điều kiện 4: 4 hero khác class hoàn toàn → +10% all stat ──
            if (playerUnits.Count == 4 && classCounts.Count == 4)
            {
                result.Add(new StatModifier(StatType.AtkPct,   10f, "synergy"));
                result.Add(new StatModifier(StatType.DefPct,   10f, "synergy"));
                result.Add(new StatModifier(StatType.MaxHpPct, 10f, "synergy"));
                result.Add(new StatModifier(StatType.SpdPct,   10f, "synergy"));
            }

            // ── điều kiện 5: Vanguard + Warden + ≥1 DPS → +5% MaxHP ───────
            bool hasTank  = classCounts.ContainsKey(HeroClass.Vanguard);
            bool hasHeal  = classCounts.ContainsKey(HeroClass.Warden);
            bool hasDps   = false;
            foreach (var u in playerUnits)
            {
                if (IsDps(u.Class)) { hasDps = true; break; }
            }
            if (hasTank && hasHeal && hasDps)
                result.Add(new StatModifier(StatType.MaxHpPct, 5f, "synergy"));

            return result;
        }
    }
}
