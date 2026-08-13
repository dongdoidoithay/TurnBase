using System.Linq;
using Game.Core.Random;
using Game.Data;
using Game.Meta.Content;
using Game.Meta.Equipment;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Meta
{
    /// <summary>EquipmentGenerator — task-equipment.md. Tên class theo object-map.md §8
    /// (T-META-EQGEN). Dùng catalog GIẢ (ScriptableObject.CreateInstance tại chỗ, không phải
    /// Resources.LoadAll) qua <c>EquipmentGenerator.RollFrom</c> để test không phụ thuộc asset
    /// thật — khác <c>LootRollerTests</c> vốn phải dùng catalog thật vì Resources không mock
    /// được, nhưng ở đây generator tách sẵn 1 overload nhận catalog nên tận dụng được.</summary>
    public class EquipmentGeneratorTests
    {
        private static EquipmentDefinitionSO MakeDef(string defId, EquipSlot slot,
            StatType statType = StatType.Str, float statValue = 5f)
        {
            var def = ScriptableObject.CreateInstance<EquipmentDefinitionSO>();
            def.DefId = defId;
            def.Slot = slot;
            def.StatType = statType;
            def.StatValue = statValue;
            return def;
        }

        [Test]
        public void RollFrom_SubStatCount_MatchesRarityTable()
        {
            var catalog = new[] { MakeDef("eq_test_weapon", EquipSlot.Weapon) };
            var expected = new (Rarity rarity, int count)[]
            {
                (Rarity.Common, 1), (Rarity.Rare, 2), (Rarity.Epic, 2), (Rarity.Legendary, 3), (Rarity.Mythic, 4),
            };

            foreach (var (rarity, count) in expected)
            {
                var rng = new XorShiftRandom(100UL + (ulong)rarity);
                var inst = EquipmentGenerator.RollFrom(catalog, EquipSlot.Weapon, rarity, rng);

                Assert.IsNotNull(inst);
                Assert.AreEqual(count, inst.SubStats.Count, $"Rarity {rarity} phải có đúng {count} sub-stat (plan.md §7.2)");
            }
        }

        [Test]
        public void RollFrom_SubStats_NeverDuplicateType()
        {
            var catalog = new[] { MakeDef("eq_test_amulet", EquipSlot.Amulet) };
            var rng = new XorShiftRandom(200UL);

            for (int i = 0; i < 200; i++)
            {
                var inst = EquipmentGenerator.RollFrom(catalog, EquipSlot.Amulet, Rarity.Mythic, rng);
                int distinctTypes = inst.SubStats.Select(s => s.StatType).Distinct().Count();
                Assert.AreEqual(inst.SubStats.Count, distinctTypes, "Không sub-stat nào được trùng loại trong cùng 1 item");
            }
        }

        [Test]
        public void RollFrom_SubStatValue_WithinRangeForRarity()
        {
            // Cột Common plan.md §7.2: giá trị nhỏ nhất toàn bộ 8 dòng là 1 (SPD/CRIT%), lớn nhất là 5 (HP%/DEF%).
            var catalog = new[] { MakeDef("eq_test_ring", EquipSlot.Ring) };
            var rng = new XorShiftRandom(300UL);

            for (int i = 0; i < 500; i++)
            {
                var inst = EquipmentGenerator.RollFrom(catalog, EquipSlot.Ring, Rarity.Common, rng);
                foreach (var sub in inst.SubStats)
                {
                    Assert.GreaterOrEqual(sub.Value, 1f);
                    Assert.LessOrEqual(sub.Value, 5f);
                }
            }
        }

        [Test]
        public void RollFrom_RespectsSlotFilter()
        {
            var catalog = new[]
            {
                MakeDef("eq_test_weapon", EquipSlot.Weapon),
                MakeDef("eq_test_ring", EquipSlot.Ring),
            };
            var rng = new XorShiftRandom(400UL);

            for (int i = 0; i < 50; i++)
            {
                var inst = EquipmentGenerator.RollFrom(catalog, EquipSlot.Ring, Rarity.Common, rng);
                Assert.AreEqual("eq_test_ring", inst.DefId);
            }
        }

        [Test]
        public void RollFrom_ReturnsNull_WhenCatalogHasNoMatchingSlot()
        {
            var catalog = new[] { MakeDef("eq_test_weapon", EquipSlot.Weapon) };
            var rng = new XorShiftRandom(500UL);

            var inst = EquipmentGenerator.RollFrom(catalog, EquipSlot.Ring, Rarity.Common, rng);

            Assert.IsNull(inst);
        }

        [Test]
        public void RollFrom_MainStat_ComesFromChosenDef_NotRandomized()
        {
            var catalog = new[] { MakeDef("eq_test_helm", EquipSlot.Helm, StatType.Con, 12f) };
            var rng = new XorShiftRandom(600UL);

            var inst = EquipmentGenerator.RollFrom(catalog, EquipSlot.Helm, Rarity.Epic, rng);

            Assert.AreEqual((int)StatType.Con, inst.MainStatType);
            Assert.AreEqual(12f, inst.MainStatValue);
        }

        // ---------- TryRollAdditionalSubStat (task-enhance-substat.md) ----------

        [Test]
        public void TryRollAdditionalSubStat_ExcludesExistingTypes()
        {
            var existing = new System.Collections.Generic.List<Game.Data.Dto.SubStatDto>
            {
                new((int)StatType.CritPct, 5f), new((int)StatType.Res, 4f),
            };
            var rng = new XorShiftRandom(800UL);

            for (int i = 0; i < 100; i++)
            {
                bool ok = EquipmentGenerator.TryRollAdditionalSubStat(Rarity.Epic, existing, rng, out var result);
                Assert.IsTrue(ok);
                Assert.AreNotEqual((int)StatType.CritPct, result.StatType);
                Assert.AreNotEqual((int)StatType.Res, result.StatType);
            }
        }

        [Test]
        public void TryRollAdditionalSubStat_PoolFull_ReturnsFalse()
        {
            var full = new System.Collections.Generic.List<Game.Data.Dto.SubStatDto>
            {
                new((int)StatType.AtkPct, 1f), new((int)StatType.MaxHpPct, 1f), new((int)StatType.DefPct, 1f),
                new((int)StatType.Spd, 1f), new((int)StatType.CritPct, 1f), new((int)StatType.CritDmgPct, 1f),
                new((int)StatType.Res, 1f), new((int)StatType.EffAcc, 1f),
            };
            var rng = new XorShiftRandom(900UL);

            bool ok = EquipmentGenerator.TryRollAdditionalSubStat(Rarity.Mythic, full, rng, out var result);

            Assert.IsFalse(ok);
            Assert.IsNull(result);
        }

        [Test]
        public void TryRollAdditionalSubStat_ValueWithinRangeForRarity()
        {
            // Common: giá trị nhỏ nhất toàn bộ 8 dòng là 1, lớn nhất là 5 (khớp RollFrom_SubStatValue_WithinRangeForRarity).
            var existing = new System.Collections.Generic.List<Game.Data.Dto.SubStatDto>();
            var rng = new XorShiftRandom(1000UL);

            for (int i = 0; i < 200; i++)
            {
                var subs = new System.Collections.Generic.List<Game.Data.Dto.SubStatDto>();
                bool ok = EquipmentGenerator.TryRollAdditionalSubStat(Rarity.Common, subs, rng, out var result);
                Assert.IsTrue(ok);
                Assert.GreaterOrEqual(result.Value, 1f);
                Assert.LessOrEqual(result.Value, 5f);
            }
        }

        [Test]
        public void Roll_UsesRealCatalog_NeverThrows()
        {
            // EquipmentService.Catalog thật (Resources/Data/Equipment, 14 def) — smoke test cho
            // đường Roll() công khai (khác RollFrom dùng catalog giả ở các test trên).
            var rng = new XorShiftRandom(700UL);
            foreach (EquipSlot slot in System.Enum.GetValues(typeof(EquipSlot)))
            {
                var inst = EquipmentGenerator.Roll(slot, Rarity.Legendary, rng);
                Assert.IsNotNull(inst, $"Catalog thật phải có ít nhất 1 def cho slot {slot}");
            }
        }
    }
}
