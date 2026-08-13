using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core.Random;
using Game.Data;
using Game.Data.Dto;
using Game.Meta.Content;

namespace Game.Meta.Equipment
{
    /// <summary>
    /// Sinh 1 <see cref="EquipmentInstanceDto"/> ngẫu nhiên từ catalog <see cref="EquipmentService"/>
    /// — task-equipment.md. Chọn def theo <see cref="EquipSlot"/> (bỏ qua <c>def.Rarity</c>, xem
    /// task-equipment.md §0), rồi tự roll Rarity + sub-stat của INSTANCE độc lập với def.
    ///
    /// Dùng <see cref="IRandomSource"/> (không phải UnityEngine.Random) — đúng kỷ luật
    /// GachaSystem/LootRoller, để test/harness gọi lại được với seed cố định.
    /// </summary>
    public static class EquipmentGenerator
    {
        /// <summary>Khoảng giá trị 1 sub-stat theo rarity — plan.md §7.2, cột theo thứ tự
        /// Common/Rare/Epic/Legendary/Mythic (index khớp <see cref="Rarity"/>).</summary>
        private readonly struct SubStatRange
        {
            public readonly StatType Stat;
            public readonly float[] Min;
            public readonly float[] Max;
            public SubStatRange(StatType stat, float[] min, float[] max) { Stat = stat; Min = min; Max = max; }
        }

        private static readonly SubStatRange[] POOL =
        {
            new(StatType.AtkPct,     new[] { 2f, 4f, 6f, 9f, 12f },  new[] { 4f, 7f, 10f, 14f, 18f }),
            new(StatType.MaxHpPct,   new[] { 3f, 5f, 7f, 11f, 14f }, new[] { 5f, 8f, 12f, 16f, 20f }),
            new(StatType.DefPct,     new[] { 3f, 5f, 7f, 11f, 14f }, new[] { 5f, 8f, 12f, 16f, 20f }),
            new(StatType.Spd,        new[] { 1f, 2f, 3f, 5f, 7f },   new[] { 2f, 4f, 6f, 8f, 11f }),
            new(StatType.CritPct,    new[] { 1f, 2f, 3f, 5f, 7f },   new[] { 2f, 4f, 6f, 8f, 10f }),
            new(StatType.CritDmgPct, new[] { 2f, 4f, 7f, 10f, 15f }, new[] { 4f, 8f, 12f, 18f, 25f }),
            new(StatType.Res,        new[] { 2f, 4f, 6f, 9f, 12f },  new[] { 4f, 7f, 10f, 14f, 18f }),
            new(StatType.EffAcc,     new[] { 2f, 4f, 6f, 9f, 12f },  new[] { 4f, 7f, 10f, 14f, 18f }),
        };

        /// <summary>Số sub-stat khởi điểm theo rarity — plan.md §7.2: Common 1 · Rare 2 · Epic 2 ·
        /// Legendary 3 · Mythic 4 (index khớp <see cref="Rarity"/>).</summary>
        private static readonly int[] SUBSTAT_COUNT = { 1, 2, 2, 3, 4 };

        /// <summary>Roll 1 item mới từ catalog thật (<see cref="EquipmentService.Catalog"/>).
        /// <paramref name="slot"/> null = mọi slot. Trả null nếu catalog không có def nào khớp
        /// slot (chưa author asset) — caller tự fallback, không throw.</summary>
        public static EquipmentInstanceDto Roll(EquipSlot? slot, Rarity rarity, IRandomSource rng)
            => RollFrom(EquipmentService.Catalog.Values, slot, rarity, rng);

        /// <summary>Như <see cref="Roll"/> nhưng nhận catalog tuỳ ý — tách riêng để test được mà
        /// không phụ thuộc <c>Resources.LoadAll</c> (VD test case "slot không có def nào").</summary>
        public static EquipmentInstanceDto RollFrom(IEnumerable<EquipmentDefinitionSO> catalog,
            EquipSlot? slot, Rarity rarity, IRandomSource rng)
        {
            var candidates = slot.HasValue
                ? catalog.Where(d => d.Slot == slot.Value).ToList()
                : catalog.ToList();
            if (candidates.Count == 0) return null;

            var def = candidates[rng.NextInt(candidates.Count)];

            return new EquipmentInstanceDto
            {
                Uid = Guid.NewGuid().ToString("N"),
                DefId = def.DefId,
                Rarity = (int)rarity,
                Level = 0,
                MainStatType = (int)def.StatType,
                MainStatValue = def.StatValue,
                SubStats = RollSubStats(rarity, rng),
                // task-setbonus.md — SetId roll ở tầng INSTANCE, độc lập với def (cùng nguyên tắc
                // Rarity/sub-stat đã dùng), đều 1/8 mỗi bộ.
                SetId = SetBonusCatalog.RollRandomSetId(rng),
            };
        }

        /// <summary>Roll N sub-stat không trùng loại — loại trừ dần khỏi pool sau mỗi lần chọn
        /// (không phải reject-and-retry). Public để test độc lập với việc chọn def.</summary>
        public static List<SubStatDto> RollSubStats(Rarity rarity, IRandomSource rng)
        {
            int rarityIdx = (int)rarity;
            int count = Math.Min(SUBSTAT_COUNT[rarityIdx], POOL.Length);

            var remaining = new List<int>(POOL.Length);
            for (int i = 0; i < POOL.Length; i++) remaining.Add(i);

            var result = new List<SubStatDto>(count);
            for (int i = 0; i < count; i++)
            {
                int pick = rng.NextInt(remaining.Count);
                int poolIdx = remaining[pick];
                remaining.RemoveAt(pick);

                var range = POOL[poolIdx];
                float value = rng.NextFloat(range.Min[rarityIdx], range.Max[rarityIdx]);
                result.Add(new SubStatDto((int)range.Stat, value));
            }
            return result;
        }

        /// <summary>task-enhance-substat.md — Enhance mốc mở sub-stat mới: roll 1 loại CHƯA có
        /// trong <paramref name="existing"/>, cùng khoảng giá trị rarity với lúc sinh item. Trả
        /// false (không throw) nếu item đã có đủ toàn bộ <see cref="POOL"/> — milestone thành
        /// no-op thay vì lỗi.</summary>
        public static bool TryRollAdditionalSubStat(Rarity rarity, List<SubStatDto> existing,
            IRandomSource rng, out SubStatDto result)
        {
            int rarityIdx = (int)rarity;
            var candidates = new List<int>(POOL.Length);
            for (int i = 0; i < POOL.Length; i++)
            {
                if (!existing.Exists(s => s.StatType == (int)POOL[i].Stat))
                    candidates.Add(i);
            }

            if (candidates.Count == 0)
            {
                result = null;
                return false;
            }

            var range = POOL[candidates[rng.NextInt(candidates.Count)]];
            float value = rng.NextFloat(range.Min[rarityIdx], range.Max[rarityIdx]);
            result = new SubStatDto((int)range.Stat, value);
            return true;
        }
    }
}
