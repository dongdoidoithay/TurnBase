using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Core.Random;
using Game.Data;
using Game.Data.Dto;
using Game.Meta.Equipment;
using Game.Services.Economy;
using NUnit.Framework;

namespace Game.Tests.Meta
{
    /// <summary>EquipmentService.TryReforge — task-phase-5-gaps.md Phần C. Trước task này reroll
    /// sub-stat hoàn toàn chưa có (chỉ enum MetaEnums.Reforge tồn tại, không có logic).</summary>
    public class EquipmentServiceReforgeTests
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
            int level = 0, Rarity rarity = Rarity.Epic, List<SubStatDto> subStats = null)
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
                SubStats = subStats ?? new List<SubStatDto>
                {
                    new((int)StatType.CritPct, 5f), new((int)StatType.Res, 4f),
                },
            };
            p.Equipment.Add(inst);
            return p;
        }

        [Test]
        public void ReforgeCost_ScalesWithLevelAndRarity()
        {
            Assert.AreEqual(80L * 1 * 2, EquipmentService.ReforgeCost(0, Rarity.Common));
            Assert.AreEqual(80L * 1 * 6, EquipmentService.ReforgeCost(0, Rarity.Mythic));
            Assert.AreEqual(80L * 15 * 6, EquipmentService.ReforgeCost(14, Rarity.Mythic),
                "Level cao + rarity cao nhất phải là mốc chi phí lớn nhất");
        }

        [Test]
        public void CanReforge_RequiresAtLeastOneSubStat()
        {
            var withSubs = new EquipmentInstanceDto { SubStats = new List<SubStatDto> { new(1, 1f) } };
            var noSubs = new EquipmentInstanceDto { SubStats = new List<SubStatDto>() };

            Assert.IsTrue(EquipmentService.CanReforge(withSubs));
            Assert.IsFalse(EquipmentService.CanReforge(noSubs));
            Assert.IsFalse(EquipmentService.CanReforge(null));
        }

        [Test]
        public void TryReforge_EnoughGold_Succeeds_KeepsSubStatCount_ConsumesGold()
        {
            var p = ProfileWithItem(out var inst, level: 3, rarity: Rarity.Epic);
            int originalCount = inst.SubStats.Count;
            long cost = EquipmentService.ReforgeCost(inst.Level, (Rarity)inst.Rarity);
            Economy().Grant(p.Wallet, CurrencyType.Gold, cost);

            var outcome = EquipmentService.TryReforge(p, inst.Uid, new XorShiftRandom(7UL));

            Assert.AreEqual(EquipmentService.ReforgeOutcome.Succeeded, outcome);
            Assert.AreEqual(originalCount, inst.SubStats.Count, "Reforge giữ nguyên SỐ LƯỢNG sub-stat theo rarity");
            Assert.AreEqual(3, inst.Level, "Reforge không đổi Level");
            Assert.AreEqual(0, Economy().Get(p.Wallet, CurrencyType.Gold), "Đã trừ đúng hết Gold vừa grant");
        }

        [Test]
        public void TryReforge_AcrossManySeeds_ActuallyChangesSubStats()
        {
            // Xác nhận Reforge THẬT SỰ reroll (không phải no-op) — quét nhiều seed, chỉ cần 1 lần
            // khác bản gốc là đủ chứng minh logic reroll có hoạt động (đúng kỹ thuật scan-seed đã
            // dùng ở EquipmentServiceEnhanceTests thay vì đoán 1 seed cụ thể).
            var original = new List<SubStatDto> { new((int)StatType.CritPct, 5f), new((int)StatType.Res, 4f) };
            bool everChanged = false;

            for (ulong seed = 1; seed <= 30; seed++)
            {
                var p = ProfileWithItem(out var inst, subStats: new List<SubStatDto>(original));
                Economy().Grant(p.Wallet, CurrencyType.Gold, EquipmentService.ReforgeCost(inst.Level, (Rarity)inst.Rarity));

                EquipmentService.TryReforge(p, inst.Uid, new XorShiftRandom(seed));

                bool sameTypesAndValues = inst.SubStats.Count == original.Count &&
                    inst.SubStats.Zip(original, (a, b) => a.StatType == b.StatType && Approximately(a.Value, b.Value))
                                 .All(same => same);
                if (!sameTypesAndValues) { everChanged = true; break; }
            }

            Assert.IsTrue(everChanged, "Reforge phải đổi được sub-stat (type hoặc value) ở ít nhất 1/30 seed");
        }

        private static bool Approximately(float a, float b) => System.Math.Abs(a - b) < 0.0001f;

        [Test]
        public void TryReforge_NoSubStats_Rejected_DoesNotConsumeGold()
        {
            var p = ProfileWithItem(out var inst, subStats: new List<SubStatDto>());
            Economy().Grant(p.Wallet, CurrencyType.Gold, 999999L);
            long goldBefore = Economy().Get(p.Wallet, CurrencyType.Gold);

            var outcome = EquipmentService.TryReforge(p, inst.Uid, new XorShiftRandom(1UL));

            Assert.AreEqual(EquipmentService.ReforgeOutcome.Rejected, outcome);
            Assert.AreEqual(goldBefore, Economy().Get(p.Wallet, CurrencyType.Gold), "Rejected không được trừ Gold");
        }

        [Test]
        public void TryReforge_MissingGold_Rejected_SubStatsUnchanged()
        {
            var p = ProfileWithItem(out var inst);
            var beforeCount = inst.SubStats.Count;
            // Không grant Gold — thiếu.

            var outcome = EquipmentService.TryReforge(p, inst.Uid, new XorShiftRandom(1UL));

            Assert.AreEqual(EquipmentService.ReforgeOutcome.Rejected, outcome);
            Assert.AreEqual(beforeCount, inst.SubStats.Count);
            Assert.AreEqual(0, Economy().Get(p.Wallet, CurrencyType.Gold));
        }
    }
}
