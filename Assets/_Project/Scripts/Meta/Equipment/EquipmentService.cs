using System.Collections.Generic;
using System.Linq;
using Game.Combat.Model;
using Game.Core;
using Game.Core.Random;
using Game.Data;
using Game.Data.Dto;
using Game.Meta.Content;
using Game.Services.Economy;
using UnityEngine;

namespace Game.Meta.Equipment
{
    /// <summary>
    /// Trang bị — MVP: mỗi món cộng 1 stat gốc cố định, nâng cấp (+0..+15, task-enhance-plus15.md)
    /// tăng % giá trị đó + mở thêm 1 sub-stat mới tại mốc +3/+6/+9/+12, có tỉ lệ thất bại từ +12
    /// (không mất đồ/tụt cấp, chỉ mất tài nguyên đã trả) và hệ số ×1.5 main stat ở +15. Set-bonus
    /// đã có riêng (task-setbonus.md) — chỉ còn reforge/re-roll sub-stat hiện có là chưa làm so với
    /// plan.md §7 đầy đủ.
    ///
    /// Không đăng ký qua ServiceLocator (Game.Meta chưa có DI cho tầng Content) — dùng như
    /// static helper, giống cách BattleSceneInstaller gọi Resources.Load trực tiếp.
    /// </summary>
    public static class EquipmentService
    {
        public const int MAX_ENHANCE_LEVEL = 15;
        private const float ENHANCE_STAT_BONUS_PER_LEVEL = 0.1f; // +10%/cấp
        private const float LEVEL15_BONUS_MULTIPLIER = 1.5f; // plan.md §7.3 — "+15: main stat ×1.5"

        /// <summary>task-enhance-plus15.md — đủ 4 mốc mở sub-stat mới đúng plan.md §7.3 (trước đó
        /// nén còn {3,6,9} vì trần cũ chỉ tới +9).</summary>
        private static readonly int[] SUBSTAT_UNLOCK_LEVELS = { 3, 6, 9, 12 };

        /// <summary>Đá cường hoá theo tier level — đúng 8 bracket bảng plan.md §7.3
        /// (1/2/3/5/8/10/14/20). Trước task-enhance-substat.md CurrencyType.EnhanceStone không
        /// được tiêu ở đâu cả dù DungeonSystem (Dungeon Đá) đã grant thật — currency chết, nay đã
        /// có chỗ dùng đủ hết dải +0..+15.</summary>
        public static long EnhanceStoneCost(int currentLevel) => currentLevel switch
        {
            < 3 => 1L,
            < 6 => 2L,
            < 9 => 3L,
            < 11 => 5L,
            11 => 8L,
            12 => 10L,
            13 => 14L,
            _ => 20L, // level 14 (attempt +15)
        };

        /// <summary>Tỉ lệ thành công 1 lần Enhance — 100% cho level 0-10 (chưa có khái niệm thất
        /// bại ở dải này, đúng plan.md §7.3), giảm dần 70/55/40/25% cho level 11-14 (attempt
        /// +12..+15). Thất bại KHÔNG mất đồ/tụt cấp — chỉ mất tài nguyên đã trả (xem
        /// <see cref="TryEnhance"/>).</summary>
        public static float SuccessChance(int currentLevel) => currentLevel switch
        {
            11 => 0.70f,
            12 => 0.55f,
            13 => 0.40f,
            14 => 0.25f,
            _ => 1f,
        };

        private static Dictionary<string, EquipmentDefinitionSO> _catalog;

        public static IReadOnlyDictionary<string, EquipmentDefinitionSO> Catalog
        {
            get
            {
                if (_catalog == null)
                {
                    _catalog = Resources.LoadAll<EquipmentDefinitionSO>("Data/Equipment")
                                         .ToDictionary(e => e.DefId, e => e);
                }
                return _catalog;
            }
        }

        /// <summary>Cấp 1 bộ mẫu (mỗi loại trong equipment.csv) nếu người chơi chưa có trang bị
        /// nào — chưa có shop/gacha trang bị nên đây là cách duy nhất để test hệ thống.</summary>
        public static void EnsureStarterEquipment(PlayerProfileDto profile)
        {
            if (profile.Equipment.Count > 0) return;

            int i = 0;
            foreach (var def in Catalog.Values.OrderBy(d => d.DefId))
            {
                profile.Equipment.Add(new EquipmentInstanceDto
                {
                    Uid = $"eqi_{i++:0000}_{def.DefId}",
                    DefId = def.DefId,
                    Rarity = def.Rarity,
                    Level = 0,
                    MainStatType = (int)def.StatType,
                    MainStatValue = def.StatValue,
                });
            }
        }

        public static EquipmentInstanceDto FindInstance(PlayerProfileDto profile, string equipUid)
            => profile.Equipment.Find(e => e.Uid == equipUid);

        public static EquipmentDefinitionSO DefOf(EquipmentInstanceDto inst)
            => inst != null && Catalog.TryGetValue(inst.DefId, out var def) ? def : null;

        /// <summary>Trang bị đã đeo bởi hero nào (nếu có) — 1 item chỉ được đeo bởi 1 hero
        /// tại 1 thời điểm, dò qua toàn bộ đội để đảm bảo không đeo trùng.</summary>
        public static HeroInstanceDto FindEquippingHero(PlayerProfileDto profile, string equipUid)
        {
            if (string.IsNullOrEmpty(equipUid)) return null;
            return profile.Heroes.Find(h => h.Equipped.Contains(equipUid));
        }

        public static bool Equip(PlayerProfileDto profile, string heroUid, string equipUid)
        {
            var hero = profile.Heroes.Find(h => h.Uid == heroUid);
            var inst = FindInstance(profile, equipUid);
            var def = DefOf(inst);
            if (hero == null || inst == null || def == null) return false;

            // Gỡ khỏi hero khác đang đeo (nếu có) trước khi gán cho hero này.
            var prevOwner = FindEquippingHero(profile, equipUid);
            if (prevOwner != null)
            {
                int prevSlot = System.Array.IndexOf(prevOwner.Equipped, equipUid);
                if (prevSlot >= 0) prevOwner.Equipped[prevSlot] = "";
            }

            hero.Equipped[(int)def.Slot] = equipUid;
            return true;
        }

        public static void Unequip(PlayerProfileDto profile, string heroUid, EquipSlot slot)
        {
            var hero = profile.Heroes.Find(h => h.Uid == heroUid);
            if (hero == null) return;
            hero.Equipped[(int)slot] = "";
        }

        /// <summary>Vàng để nâng 1 cấp — tăng dần theo cấp hiện tại, chặn ở MAX_ENHANCE_LEVEL.
        /// Giữ NGUYÊN công thức gốc (task-enhance-substat.md) xuyên suốt cả dải +0..+14, kể cả
        /// phần +10..+14 mới mở — KHÔNG nhảy sang số Vàng plan.md §7.3 (200-22.000, sẽ tạo 1 bậc
        /// thang giật cục ngay biên +9/+10 nếu chỉ đổi nửa sau). Gold vốn dĩ luôn RẺ/dồi dào trong
        /// nền kinh tế game này (`AscendSystem.COSTS` tốn tới 250.000 cho ★5→6) — nút thắt thật của
        /// dải +12-15 là Đá cường hoá (giới hạn tuần qua Dungeon) + tỉ lệ thất bại, cả 2 bám đúng số
        /// plan.md — xem task-enhance-plus15.md §0.</summary>
        public static long EnhanceCost(int currentLevel) => 80L * (currentLevel + 1);

        public static bool CanEnhance(EquipmentInstanceDto inst) => inst != null && inst.Level < MAX_ENHANCE_LEVEL;

        public enum EnhanceOutcome
        {
            /// <summary>Không đủ Gold/Đá, hoặc đã max level — KHÔNG trừ gì cả.</summary>
            Rejected,
            /// <summary>Đủ tài nguyên, ĐÃ TRỪ, nhưng roll trượt (chỉ có thể xảy ra từ level 11+) —
            /// level/sub-stat không đổi. plan.md §7.3: "không mất đồ, không tụt level, chỉ mất tài
            /// nguyên".</summary>
            Failed,
            /// <summary>Đủ tài nguyên, ĐÃ TRỪ, roll thành công — level+1, có thể mở sub-stat mới
            /// nếu trúng mốc.</summary>
            Succeeded,
        }

        /// <summary>Nâng 1 cấp trang bị — tốn Gold + Đá cường hoá (kiểm tra đủ CẢ HAI trước khi
        /// trừ bất kỳ currency nào, atomic). Từ level 11 trở lên có tỉ lệ thất bại
        /// (<see cref="SuccessChance"/>) — tài nguyên vẫn bị trừ dù trượt, đúng plan.md §7.3. Tại
        /// mốc level 3/6/9/12 (chỉ khi THÀNH CÔNG) mở thêm 1 sub-stat mới (nếu item chưa có đủ 8/8
        /// loại) — task-enhance-substat.md/task-enhance-plus15.md. <paramref name="rng"/> dùng cho
        /// cả roll thành/bại lẫn milestone sub-stat.</summary>
        public static EnhanceOutcome TryEnhance(PlayerProfileDto profile, string equipUid, IRandomSource rng)
        {
            var inst = FindInstance(profile, equipUid);
            if (!CanEnhance(inst)) return EnhanceOutcome.Rejected;

            long goldCost = EnhanceCost(inst.Level);
            long stoneCost = EnhanceStoneCost(inst.Level);
            if (!ServiceLocator.TryGet<IEconomyService>(out var economy)) return EnhanceOutcome.Rejected;
            if (economy.Get(profile.Wallet, CurrencyType.Gold) < goldCost) return EnhanceOutcome.Rejected;
            if (economy.Get(profile.Wallet, CurrencyType.EnhanceStone) < stoneCost) return EnhanceOutcome.Rejected;

            economy.TryConsume(profile.Wallet, CurrencyType.Gold, goldCost);
            economy.TryConsume(profile.Wallet, CurrencyType.EnhanceStone, stoneCost);

            if (!rng.Chance(SuccessChance(inst.Level))) return EnhanceOutcome.Failed;

            inst.Level++;
            if (System.Array.IndexOf(SUBSTAT_UNLOCK_LEVELS, inst.Level) >= 0
                && EquipmentGenerator.TryRollAdditionalSubStat((Rarity)inst.Rarity, inst.SubStats, rng, out var newSub))
            {
                inst.SubStats.Add(newSub);
            }
            return EnhanceOutcome.Succeeded;
        }

        /// <summary>task-phase-5-gaps.md Phần C — Vàng để reroll toàn bộ sub-stat 1 lần. Tự thiết
        /// kế (plan.md không cho bảng số cho Reforge) — bám mốc <see cref="EnhanceCost"/> nhân
        /// thêm theo rarity (item hiếm hơn có nhiều sub-stat hơn/giá trị cao hơn → reroll đắt hơn).</summary>
        public static long ReforgeCost(int level, Rarity rarity) => (80L * (level + 1)) * (2 + (int)rarity);

        /// <summary>Chỉ reroll được khi item có ít nhất 1 sub-stat (item +0 rarity Common có thể
        /// chưa có sub-stat nào — xem <see cref="EquipmentGenerator.RollSubStats"/>).</summary>
        public static bool CanReforge(EquipmentInstanceDto inst) => inst != null && inst.SubStats.Count > 0;

        public enum ReforgeOutcome
        {
            /// <summary>Không đủ Vàng, hoặc item chưa có sub-stat nào — KHÔNG trừ gì cả.</summary>
            Rejected,
            /// <summary>Đủ Vàng, đã trừ, toàn bộ sub-stat được reroll giá trị MỚI (giữ nguyên số
            /// lượng theo rarity — không đổi Level/main stat).</summary>
            Succeeded,
        }

        /// <summary>Reroll toàn bộ sub-stat của 1 trang bị — tốn Vàng theo <see cref="ReforgeCost"/>.
        /// Khác <see cref="TryEnhance"/>: không có tỉ lệ thất bại (luôn thành công nếu đủ Vàng +
        /// có sub-stat để reroll), không đổi Level/main stat, chỉ đổi <c>SubStats</c>. Dùng lại
        /// đúng <see cref="EquipmentGenerator.RollSubStats"/> — giữ ĐÚNG số lượng sub-stat theo
        /// rarity như lúc sinh item, chỉ đổi loại/giá trị.</summary>
        public static ReforgeOutcome TryReforge(PlayerProfileDto profile, string equipUid, IRandomSource rng)
        {
            var inst = FindInstance(profile, equipUid);
            if (!CanReforge(inst)) return ReforgeOutcome.Rejected;

            long goldCost = ReforgeCost(inst.Level, (Rarity)inst.Rarity);
            if (!ServiceLocator.TryGet<IEconomyService>(out var economy)) return ReforgeOutcome.Rejected;
            if (economy.Get(profile.Wallet, CurrencyType.Gold) < goldCost) return ReforgeOutcome.Rejected;

            economy.TryConsume(profile.Wallet, CurrencyType.Gold, goldCost);
            inst.SubStats = EquipmentGenerator.RollSubStats((Rarity)inst.Rarity, rng);
            return ReforgeOutcome.Succeeded;
        }

        /// <summary>Giá trị stat thực tế của 1 item (đã cộng % theo cấp nâng, cộng thêm hệ số ×1.5
        /// khi đạt +15 — plan.md §7.3 "+15: main stat ×1.5" đọc là 1 hệ số CỘNG THÊM lên công thức
        /// tuyến tính nền, cùng cách các mốc mở sub-stat là sự kiện cộng thêm chứ không thay thế —
        /// xem task-enhance-plus15.md §0 để biết đây là 1 diễn giải, plan.md không nói rõ).</summary>
        public static float EffectiveStatValue(EquipmentInstanceDto inst)
        {
            float value = inst.MainStatValue * (1f + inst.Level * ENHANCE_STAT_BONUS_PER_LEVEL);
            if (inst.Level >= MAX_ENHANCE_LEVEL) value *= LEVEL15_BONUS_MULTIPLIER;
            return value;
        }

        /// <summary>Tổng cộng dồn 6 slot trang bị của 1 hero thành PrimaryStats để cộng vào
        /// BasePrimary lúc dựng CombatUnit — chỉ hỗ trợ StatType Str/Con/Int/Dex/Aur/Luk (MVP).</summary>
        public static PrimaryStats GetBonusPrimary(PlayerProfileDto profile, HeroInstanceDto hero)
        {
            var bonus = new PrimaryStats();
            if (hero == null) return bonus;

            foreach (var equipUid in hero.Equipped)
            {
                if (string.IsNullOrEmpty(equipUid)) continue;
                var inst = FindInstance(profile, equipUid);
                if (inst == null) continue;

                float value = EffectiveStatValue(inst);
                bonus = AddToPrimary(bonus, (StatType)inst.MainStatType, value);
            }
            return bonus;
        }

        /// <summary>Sub-stat của trang bị đang đeo → <see cref="StatModifier"/> cho
        /// <c>CombatUnit.EquipmentModifiers</c> — task-equipment.md. Đường RIÊNG với
        /// <see cref="GetBonusPrimary"/>: main stat vẫn đi qua Primary như cũ (không đổi), chỉ
        /// sub-stat (%, RES, EFF_ACC...) đi qua đường mới này vì chúng không map được vào
        /// <see cref="PrimaryStats"/>.</summary>
        public static List<StatModifier> GetEquipmentModifiers(PlayerProfileDto profile, HeroInstanceDto hero)
        {
            var mods = new List<StatModifier>();
            if (hero == null) return mods;

            foreach (var equipUid in hero.Equipped)
            {
                if (string.IsNullOrEmpty(equipUid)) continue;
                var inst = FindInstance(profile, equipUid);
                if (inst == null) continue;

                foreach (var sub in inst.SubStats)
                    mods.Add(new StatModifier((StatType)sub.StatType, sub.Value, inst.Uid));
            }
            return mods;
        }

        private static PrimaryStats AddToPrimary(PrimaryStats p, StatType type, float value) => type switch
        {
            StatType.Str => new PrimaryStats(p.Str + value, p.Con, p.Int, p.Dex, p.Aur, p.Luk),
            StatType.Con => new PrimaryStats(p.Str, p.Con + value, p.Int, p.Dex, p.Aur, p.Luk),
            StatType.Int => new PrimaryStats(p.Str, p.Con, p.Int + value, p.Dex, p.Aur, p.Luk),
            StatType.Dex => new PrimaryStats(p.Str, p.Con, p.Int, p.Dex + value, p.Aur, p.Luk),
            StatType.Aur => new PrimaryStats(p.Str, p.Con, p.Int, p.Dex, p.Aur + value, p.Luk),
            StatType.Luk => new PrimaryStats(p.Str, p.Con, p.Int, p.Dex, p.Aur, p.Luk + value),
            _ => p
        };

        /// <summary>Danh sách item trong túi đang KHÔNG được hero nào đeo, đúng slot yêu cầu —
        /// dùng cho UI chọn trang bị.</summary>
        public static List<EquipmentInstanceDto> UnequippedBySlot(PlayerProfileDto profile, EquipSlot slot)
        {
            var equippedUids = new HashSet<string>();
            foreach (var h in profile.Heroes)
                foreach (var e in h.Equipped)
                    if (!string.IsNullOrEmpty(e)) equippedUids.Add(e);

            return profile.Equipment
                .Where(e => !equippedUids.Contains(e.Uid) && DefOf(e)?.Slot == slot)
                .ToList();
        }
    }
}
