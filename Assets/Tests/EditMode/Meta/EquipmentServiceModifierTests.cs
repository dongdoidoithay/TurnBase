using System.Collections.Generic;
using Game.Data;
using Game.Data.Dto;
using Game.Meta.Equipment;
using NUnit.Framework;

namespace Game.Tests.Meta
{
    /// <summary>EquipmentService.GetEquipmentModifiers — task-equipment.md §3: sub-stat trang bị
    /// → CombatUnit.EquipmentModifiers. Việc EquipmentModifiers ảnh hưởng Stats đã được
    /// DamageCalculatorTests/SimulationTests test sẵn ở tầng Combat (unchanged) — test ở đây chỉ
    /// verify tầng Meta convert SubStats đúng, không lặp lại phần đó.</summary>
    public class EquipmentServiceModifierTests
    {
        [Test]
        public void GetEquipmentModifiers_ConvertsSubStats_FromAllEquippedSlots()
        {
            var profile = new PlayerProfileDto();
            var ring = new EquipmentInstanceDto
            {
                Uid = "eqi_ring",
                DefId = "eq_ring_luck",
                SubStats = new List<SubStatDto> { new((int)StatType.CritPct, 20f) },
            };
            var amulet = new EquipmentInstanceDto
            {
                Uid = "eqi_amulet",
                DefId = "eq_amulet_aura",
                SubStats = new List<SubStatDto> { new((int)StatType.Res, 8f), new((int)StatType.EffAcc, 5f) },
            };
            profile.Equipment.Add(ring);
            profile.Equipment.Add(amulet);

            var hero = new HeroInstanceDto { Uid = "h1", DefId = "hero_ember_knight" };
            hero.Equipped[(int)EquipSlot.Ring] = "eqi_ring";
            hero.Equipped[(int)EquipSlot.Amulet] = "eqi_amulet";

            var mods = EquipmentService.GetEquipmentModifiers(profile, hero);

            Assert.AreEqual(3, mods.Count, "2 sub-stat từ amulet + 1 từ ring = 3 modifier tổng");
            Assert.IsTrue(mods.Exists(m => m.Stat == StatType.CritPct && m.Value == 20f));
            Assert.IsTrue(mods.Exists(m => m.Stat == StatType.Res && m.Value == 8f));
            Assert.IsTrue(mods.Exists(m => m.Stat == StatType.EffAcc && m.Value == 5f));
        }

        [Test]
        public void GetEquipmentModifiers_NoItemsEquipped_ReturnsEmptyList()
        {
            var profile = new PlayerProfileDto();
            var hero = new HeroInstanceDto { Uid = "h2", DefId = "hero_dawn_cleric" };

            var mods = EquipmentService.GetEquipmentModifiers(profile, hero);

            Assert.AreEqual(0, mods.Count);
        }

        [Test]
        public void GetEquipmentModifiers_NullHero_ReturnsEmptyList_NoThrow()
        {
            var profile = new PlayerProfileDto();

            var mods = EquipmentService.GetEquipmentModifiers(profile, null);

            Assert.AreEqual(0, mods.Count);
        }

        [Test]
        public void GetEquipmentModifiers_IgnoresMainStat_OnlyConvertsSubStats()
        {
            var profile = new PlayerProfileDto();
            var sword = new EquipmentInstanceDto
            {
                Uid = "eqi_sword",
                DefId = "eq_sword_iron",
                MainStatType = (int)StatType.Str,
                MainStatValue = 6f,
                SubStats = new List<SubStatDto>(), // chưa roll qua EquipmentGenerator (VD starter kit)
            };
            profile.Equipment.Add(sword);

            var hero = new HeroInstanceDto { Uid = "h3", DefId = "hero_ember_knight" };
            hero.Equipped[(int)EquipSlot.Weapon] = "eqi_sword";

            var mods = EquipmentService.GetEquipmentModifiers(profile, hero);

            Assert.AreEqual(0, mods.Count, "MainStat vẫn đi qua GetBonusPrimary như cũ — không nhân đôi vào EquipmentModifiers");
        }
    }
}
