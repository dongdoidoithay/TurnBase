using System.Collections.Generic;
using Game.Core;
using Game.Core.Random;
using Game.Data;
using Game.Data.Dto;
using Game.Meta.Equipment;
using Game.Services.Economy;
using NUnit.Framework;

namespace Game.Tests.Meta
{
    /// <summary>EquipmentService.TryEnhance — task-enhance-substat.md. Trước task này hàm
    /// TryEnhance/EnhanceCost hoàn toàn chưa có test nào (grep xác nhận), nên bộ test này bao
    /// luôn cả phần Gold-only cũ, không chỉ phần Đá/sub-stat mới thêm.</summary>
    public class EquipmentServiceEnhanceTests
    {
        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            ServiceLocator.Register<IEconomyService>(new EconomyService());
        }

        [TearDown]
        public void TearDown() => ServiceLocator.Clear();

        private static IEconomyService Economy() => ServiceLocator.Get<IEconomyService>();

        private static PlayerProfileDto ProfileWithItem(out EquipmentInstanceDto inst,
            int level = 0, Rarity rarity = Rarity.Rare, List<SubStatDto> subStats = null)
        {
            var p = new PlayerProfileDto();
            inst = new EquipmentInstanceDto
            {
                Uid = "eqi_test",
                DefId = "eq_sword_iron",
                Rarity = (int)rarity,
                Level = level,
                MainStatType = (int)StatType.Str,
                MainStatValue = 6f,
                SubStats = subStats ?? new List<SubStatDto>(),
            };
            p.Equipment.Add(inst);
            return p;
        }

        private static void GrantEnough(PlayerProfileDto p, int level)
        {
            Economy().Grant(p.Wallet, CurrencyType.Gold, EquipmentService.EnhanceCost(level));
            Economy().Grant(p.Wallet, CurrencyType.EnhanceStone, EquipmentService.EnhanceStoneCost(level));
        }

        // ---------- Cơ bản (trước đây chưa có test) ----------

        [Test]
        public void TryEnhance_EnoughCurrency_IncrementsLevel_ConsumesBoth()
        {
            var p = ProfileWithItem(out var inst);
            GrantEnough(p, 0);
            long goldBefore = Economy().Get(p.Wallet, CurrencyType.Gold);
            long stoneBefore = Economy().Get(p.Wallet, CurrencyType.EnhanceStone);

            var outcome = EquipmentService.TryEnhance(p, inst.Uid, new XorShiftRandom(1UL));

            Assert.AreEqual(EquipmentService.EnhanceOutcome.Succeeded, outcome);
            Assert.AreEqual(1, inst.Level);
            Assert.AreEqual(goldBefore - EquipmentService.EnhanceCost(0), Economy().Get(p.Wallet, CurrencyType.Gold));
            Assert.AreEqual(stoneBefore - EquipmentService.EnhanceStoneCost(0), Economy().Get(p.Wallet, CurrencyType.EnhanceStone));
        }

        [Test]
        public void TryEnhance_AtMaxLevel_Rejected()
        {
            var p = ProfileWithItem(out var inst, level: EquipmentService.MAX_ENHANCE_LEVEL);
            GrantEnough(p, EquipmentService.MAX_ENHANCE_LEVEL);

            var outcome = EquipmentService.TryEnhance(p, inst.Uid, new XorShiftRandom(1UL));

            Assert.AreEqual(EquipmentService.EnhanceOutcome.Rejected, outcome);
            Assert.AreEqual(EquipmentService.MAX_ENHANCE_LEVEL, inst.Level);
        }

        [Test]
        public void TryEnhance_MissingGold_Rejected_DoesNotConsumeStone()
        {
            var p = ProfileWithItem(out var inst);
            Economy().Grant(p.Wallet, CurrencyType.EnhanceStone, EquipmentService.EnhanceStoneCost(0));
            // Không grant Gold — thiếu.

            var outcome = EquipmentService.TryEnhance(p, inst.Uid, new XorShiftRandom(1UL));

            Assert.AreEqual(EquipmentService.EnhanceOutcome.Rejected, outcome);
            Assert.AreEqual(0, inst.Level);
            Assert.AreEqual(EquipmentService.EnhanceStoneCost(0), Economy().Get(p.Wallet, CurrencyType.EnhanceStone),
                "Thiếu Gold thì không được đụng tới Đá — atomic, không trừ 1 nửa.");
        }

        [Test]
        public void TryEnhance_MissingStone_Rejected_DoesNotConsumeGold()
        {
            var p = ProfileWithItem(out var inst);
            Economy().Grant(p.Wallet, CurrencyType.Gold, EquipmentService.EnhanceCost(0));
            // Không grant Đá — thiếu.

            var outcome = EquipmentService.TryEnhance(p, inst.Uid, new XorShiftRandom(1UL));

            Assert.AreEqual(EquipmentService.EnhanceOutcome.Rejected, outcome);
            Assert.AreEqual(0, inst.Level);
            Assert.AreEqual(EquipmentService.EnhanceCost(0), Economy().Get(p.Wallet, CurrencyType.Gold),
                "Thiếu Đá thì không được đụng tới Gold — atomic, không trừ 1 nửa.");
        }

        // ---------- Mốc mở sub-stat (3/6/9/12) ----------

        [Test]
        public void TryEnhance_ReachesLevel3_UnlocksOneNewSubStat()
        {
            var existing = new List<SubStatDto> { new((int)StatType.CritPct, 5f), new((int)StatType.Res, 4f) };
            var p = ProfileWithItem(out var inst, level: 2, subStats: existing);
            GrantEnough(p, 2);

            var outcome = EquipmentService.TryEnhance(p, inst.Uid, new XorShiftRandom(42UL));

            Assert.AreEqual(EquipmentService.EnhanceOutcome.Succeeded, outcome);
            Assert.AreEqual(3, inst.Level);
            Assert.AreEqual(3, inst.SubStats.Count, "Level 3 phải mở thêm đúng 1 sub-stat mới (2 cũ + 1 mới)");
        }

        // (mốc level 12 với tỉ lệ thành công 70% — xem
        // TryEnhance_AtLevel11_CanSucceed_IncrementsLevel_UnlocksSubStatAt12 bên dưới, dùng seed
        // quét thay vì đoán 1 seed cụ thể có thành công hay không)

        [Test]
        public void TryEnhance_ReachesLevel3_NewSubStat_NeverDuplicatesExistingType()
        {
            var existing = new List<SubStatDto> { new((int)StatType.CritPct, 5f) };
            for (ulong seed = 1; seed <= 30; seed++)
            {
                var p = ProfileWithItem(out var inst, level: 2, subStats: new List<SubStatDto>(existing));
                GrantEnough(p, 2);

                EquipmentService.TryEnhance(p, inst.Uid, new XorShiftRandom(seed));

                int distinctTypes = new HashSet<int>(inst.SubStats.ConvertAll(s => s.StatType)).Count;
                Assert.AreEqual(inst.SubStats.Count, distinctTypes, $"seed={seed}: không được trùng loại sub-stat");
            }
        }

        [Test]
        public void TryEnhance_NonMilestoneLevel_DoesNotAddSubStat()
        {
            var existing = new List<SubStatDto> { new((int)StatType.CritPct, 5f) };
            var p = ProfileWithItem(out var inst, level: 1, subStats: new List<SubStatDto>(existing));
            GrantEnough(p, 1);

            var outcome = EquipmentService.TryEnhance(p, inst.Uid, new XorShiftRandom(1UL));

            Assert.AreEqual(EquipmentService.EnhanceOutcome.Succeeded, outcome);
            Assert.AreEqual(2, inst.Level);
            Assert.AreEqual(1, inst.SubStats.Count, "Level 2 không phải mốc — không thêm sub-stat");
        }

        [Test]
        public void TryEnhance_ReachesLevel3_PoolAlreadyFull_NoOp_StillSucceeds()
        {
            // Đủ 8/8 loại sẵn — milestone phải no-op, không throw, Enhance vẫn thành công.
            var full = new List<SubStatDto>
            {
                new((int)StatType.AtkPct, 1f), new((int)StatType.MaxHpPct, 1f), new((int)StatType.DefPct, 1f),
                new((int)StatType.Spd, 1f), new((int)StatType.CritPct, 1f), new((int)StatType.CritDmgPct, 1f),
                new((int)StatType.Res, 1f), new((int)StatType.EffAcc, 1f),
            };
            var p = ProfileWithItem(out var inst, level: 2, subStats: new List<SubStatDto>(full));
            GrantEnough(p, 2);

            var outcome = EquipmentService.TryEnhance(p, inst.Uid, new XorShiftRandom(1UL));

            Assert.AreEqual(EquipmentService.EnhanceOutcome.Succeeded, outcome);
            Assert.AreEqual(3, inst.Level);
            Assert.AreEqual(8, inst.SubStats.Count);
        }

        // ---------- Chi phí Đá cường hoá + tỉ lệ thành công (task-enhance-plus15.md) ----------

        [Test]
        public void EnhanceStoneCost_FollowsEightTierBrackets()
        {
            Assert.AreEqual(1L, EquipmentService.EnhanceStoneCost(0));
            Assert.AreEqual(1L, EquipmentService.EnhanceStoneCost(2));
            Assert.AreEqual(2L, EquipmentService.EnhanceStoneCost(3));
            Assert.AreEqual(2L, EquipmentService.EnhanceStoneCost(5));
            Assert.AreEqual(3L, EquipmentService.EnhanceStoneCost(6));
            Assert.AreEqual(3L, EquipmentService.EnhanceStoneCost(8));
            Assert.AreEqual(5L, EquipmentService.EnhanceStoneCost(9));
            Assert.AreEqual(5L, EquipmentService.EnhanceStoneCost(10));
            Assert.AreEqual(8L, EquipmentService.EnhanceStoneCost(11));
            Assert.AreEqual(10L, EquipmentService.EnhanceStoneCost(12));
            Assert.AreEqual(14L, EquipmentService.EnhanceStoneCost(13));
            Assert.AreEqual(20L, EquipmentService.EnhanceStoneCost(14));
        }

        [Test]
        public void SuccessChance_Is100Percent_BelowLevel11()
        {
            for (int level = 0; level <= 10; level++)
                Assert.AreEqual(1f, EquipmentService.SuccessChance(level), $"level={level}");
        }

        [Test]
        public void SuccessChance_FollowsPlanTable_FromLevel11()
        {
            Assert.AreEqual(0.70f, EquipmentService.SuccessChance(11));
            Assert.AreEqual(0.55f, EquipmentService.SuccessChance(12));
            Assert.AreEqual(0.40f, EquipmentService.SuccessChance(13));
            Assert.AreEqual(0.25f, EquipmentService.SuccessChance(14));
        }

        // ---------- Thất bại từ level 11 (task-enhance-plus15.md) ----------

        [Test]
        public void TryEnhance_AtLevel11_CanFail_StillConsumesBothCurrencies_LevelUnchanged()
        {
            // Tìm 1 seed thật sự trượt 70% thành công (không đoán) — quét tới khi gặp Failed.
            for (ulong seed = 1; seed <= 200; seed++)
            {
                var p = ProfileWithItem(out var inst, level: 11);
                GrantEnough(p, 11);
                long goldBefore = Economy().Get(p.Wallet, CurrencyType.Gold);
                long stoneBefore = Economy().Get(p.Wallet, CurrencyType.EnhanceStone);

                var outcome = EquipmentService.TryEnhance(p, inst.Uid, new XorShiftRandom(seed));
                if (outcome != EquipmentService.EnhanceOutcome.Failed) continue;

                Assert.AreEqual(11, inst.Level, "Failed không được tụt/tăng level");
                Assert.AreEqual(goldBefore - EquipmentService.EnhanceCost(11), Economy().Get(p.Wallet, CurrencyType.Gold),
                    "Failed vẫn phải trừ Gold — plan.md §7.3 'chỉ mất tài nguyên'");
                Assert.AreEqual(stoneBefore - EquipmentService.EnhanceStoneCost(11), Economy().Get(p.Wallet, CurrencyType.EnhanceStone),
                    "Failed vẫn phải trừ Đá — plan.md §7.3 'chỉ mất tài nguyên'");
                return;
            }
            Assert.Fail("Không tìm được seed nào trượt trong 200 lần thử ở tỉ lệ 70% — nghi ngờ SuccessChance/rng.Chance sai.");
        }

        [Test]
        public void TryEnhance_AtLevel11_CanSucceed_IncrementsLevel_UnlocksSubStatAt12()
        {
            var existing = new List<SubStatDto>
            {
                new((int)StatType.CritPct, 5f), new((int)StatType.Res, 4f), new((int)StatType.AtkPct, 3f),
            };
            for (ulong seed = 1; seed <= 200; seed++)
            {
                var p = ProfileWithItem(out var inst, level: 11, subStats: new List<SubStatDto>(existing));
                GrantEnough(p, 11);

                var outcome = EquipmentService.TryEnhance(p, inst.Uid, new XorShiftRandom(seed));
                if (outcome != EquipmentService.EnhanceOutcome.Succeeded) continue;

                Assert.AreEqual(12, inst.Level);
                Assert.AreEqual(4, inst.SubStats.Count, "Level 12 phải mở thêm đúng 1 sub-stat mới (3 cũ + 1 mới)");
                return;
            }
            Assert.Fail("Không tìm được seed nào thành công trong 200 lần thử ở tỉ lệ 70% — nghi ngờ SuccessChance/rng.Chance sai.");
        }

        // ---------- Hệ số ×1.5 ở +15 (plan.md §7.3) ----------

        [Test]
        public void EffectiveStatValue_At15_AppliesExtra1Point5Multiplier()
        {
            var p = ProfileWithItem(out var inst, level: 15);
            // 1 + 15*0.1 = 2.5 (công thức tuyến tính nền) × 1.5 (hệ số mốc +15) = 3.75.
            Assert.AreEqual(inst.MainStatValue * 3.75f, EquipmentService.EffectiveStatValue(inst), 0.001f);
        }

        [Test]
        public void EffectiveStatValue_At14_DoesNotApplyMultiplier()
        {
            var p = ProfileWithItem(out var inst, level: 14);
            // 1 + 14*0.1 = 2.4, KHÔNG nhân ×1.5 (chưa đạt +15).
            Assert.AreEqual(inst.MainStatValue * 2.4f, EquipmentService.EffectiveStatValue(inst), 0.001f);
        }
    }
}
