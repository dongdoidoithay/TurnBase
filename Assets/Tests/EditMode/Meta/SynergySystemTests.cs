using System.Collections.Generic;
using System.Linq;
using Game.Combat.Model;
using Game.Data;
using Game.Meta.Battle;
using NUnit.Framework;

namespace Game.Tests.Meta
{
    /// <summary>SynergySystem — task-formation-synergy.md, plan.md §5.7.</summary>
    public class SynergySystemTests
    {
        private static CombatUnit MakeUnit(HeroClass cls, Element element = Element.Neutral)
            => new CombatUnit
            {
                DefId = cls.ToString(),
                Side = TeamSide.Player,
                Row = Row.Front,
                Class = cls,
                Element = element,
                BasePrimary = new PrimaryStats(10, 10, 10, 10, 10, 10),
            };

        // ── Conditions 1 & 2: same-class pairs/triples ──────────────────────

        [Test]
        public void Condition1_TwoSameClass_GivesClassStat10Pct()
        {
            var units = new List<CombatUnit>
            {
                MakeUnit(HeroClass.Slayer),
                MakeUnit(HeroClass.Slayer),
                MakeUnit(HeroClass.Vanguard),
                MakeUnit(HeroClass.Warden),
            };
            var mods = SynergySystem.Compute(units);

            var atkMod = mods.FirstOrDefault(m => m.Stat == StatType.AtkPct && m.Source == "synergy");
            Assert.IsNotNull(atkMod, "Should have AtkPct synergy for 2 Slayers");
            Assert.AreEqual(10f, atkMod.Value, 0.001f);
        }

        [Test]
        public void Condition2_ThreeSameClass_Gives18Pct_Not10Pct()
        {
            var units = new List<CombatUnit>
            {
                MakeUnit(HeroClass.Slayer),
                MakeUnit(HeroClass.Slayer),
                MakeUnit(HeroClass.Slayer),
                MakeUnit(HeroClass.Warden),
            };
            var mods = SynergySystem.Compute(units);

            var atkMods = mods.Where(m => m.Stat == StatType.AtkPct && m.Source == "synergy").ToList();
            Assert.AreEqual(1, atkMods.Count, "Exactly one AtkPct mod for Slayer triple");
            Assert.AreEqual(18f, atkMods[0].Value, 0.001f);
        }

        [Test]
        public void Condition1_Vanguard_Pair_GivesDefPct()
        {
            var units = new List<CombatUnit>
            {
                MakeUnit(HeroClass.Vanguard),
                MakeUnit(HeroClass.Vanguard),
                MakeUnit(HeroClass.Slayer),
                MakeUnit(HeroClass.Slayer),
            };
            var mods = SynergySystem.Compute(units);

            var defMod = mods.FirstOrDefault(m => m.Stat == StatType.DefPct && m.Source == "synergy");
            Assert.IsNotNull(defMod);
            Assert.AreEqual(10f, defMod.Value, 0.001f);
        }

        // ── Condition 3: same element ────────────────────────────────────────

        [Test]
        public void Condition3_TwoSameElement_GivesDmgBonusPct()
        {
            var units = new List<CombatUnit>
            {
                MakeUnit(HeroClass.Slayer,   Element.Fire),
                MakeUnit(HeroClass.Arcanist, Element.Fire),
                MakeUnit(HeroClass.Vanguard, Element.Water),
                MakeUnit(HeroClass.Warden,   Element.Wind),
            };
            var mods = SynergySystem.Compute(units);

            var dmgMod = mods.FirstOrDefault(m => m.Stat == StatType.DmgBonusPct && m.Source == "synergy");
            Assert.IsNotNull(dmgMod, "Two Fire heroes should trigger element synergy");
            Assert.AreEqual(8f, dmgMod.Value, 0.001f);
        }

        [Test]
        public void Condition3_TwoElementPairs_OnlyOneBonus()
        {
            var units = new List<CombatUnit>
            {
                MakeUnit(HeroClass.Slayer,   Element.Fire),
                MakeUnit(HeroClass.Arcanist, Element.Fire),
                MakeUnit(HeroClass.Vanguard, Element.Water),
                MakeUnit(HeroClass.Warden,   Element.Water),
            };
            var mods = SynergySystem.Compute(units);

            int count = mods.Count(m => m.Stat == StatType.DmgBonusPct && m.Source == "synergy");
            Assert.AreEqual(1, count, "Multiple element pairs give only one DmgBonusPct bonus");
        }

        // ── Condition 4: 4 unique classes ───────────────────────────────────

        [Test]
        public void Condition4_FourDifferentClasses_GivesAllFourStats()
        {
            var units = new List<CombatUnit>
            {
                MakeUnit(HeroClass.Vanguard),
                MakeUnit(HeroClass.Slayer),
                MakeUnit(HeroClass.Warden),
                MakeUnit(HeroClass.Arcanist),
            };
            var mods = SynergySystem.Compute(units);

            Assert.IsTrue(mods.Any(m => m.Stat == StatType.AtkPct   && m.Value == 10f), "ATK");
            Assert.IsTrue(mods.Any(m => m.Stat == StatType.DefPct   && m.Value == 10f), "DEF");
            Assert.IsTrue(mods.Any(m => m.Stat == StatType.MaxHpPct && m.Value == 10f), "MaxHP");
            Assert.IsTrue(mods.Any(m => m.Stat == StatType.SpdPct   && m.Value == 10f), "SPD");
        }

        [Test]
        public void Condition4_ThreeClasses_NoAllStatBonus()
        {
            var units = new List<CombatUnit>
            {
                MakeUnit(HeroClass.Vanguard),
                MakeUnit(HeroClass.Slayer),
                MakeUnit(HeroClass.Slayer),
                MakeUnit(HeroClass.Warden),
            };
            var mods = SynergySystem.Compute(units);

            // Condition 4 needs exactly 4 different classes — should NOT fire here
            Assert.IsFalse(mods.Any(m => m.Stat == StatType.SpdPct && m.Value == 10f),
                           "Condition 4 should not fire with only 3 unique classes");
        }

        // ── Condition 5: Vanguard + Warden + DPS ────────────────────────────

        [Test]
        public void Condition5_TankHealDps_GivesMaxHpBonus()
        {
            var units = new List<CombatUnit>
            {
                MakeUnit(HeroClass.Vanguard),
                MakeUnit(HeroClass.Warden),
                MakeUnit(HeroClass.Slayer),
                MakeUnit(HeroClass.Arcanist),
            };
            var mods = SynergySystem.Compute(units);

            var maxHpMods = mods.Where(m => m.Stat == StatType.MaxHpPct && m.Source == "synergy").ToList();
            Assert.IsTrue(maxHpMods.Any(m => m.Value == 5f),
                          "Vanguard + Warden + DPS should give +5% MaxHP synergy");
        }

        [Test]
        public void Condition5_MissingWarden_DoesNotFire()
        {
            var units = new List<CombatUnit>
            {
                MakeUnit(HeroClass.Vanguard),
                MakeUnit(HeroClass.Slayer),
                MakeUnit(HeroClass.Arcanist),
                MakeUnit(HeroClass.Trickster),
            };
            var mods = SynergySystem.Compute(units);

            Assert.IsFalse(mods.Any(m => m.Stat == StatType.MaxHpPct && m.Value == 5f),
                           "Condition 5 requires Warden");
        }

        // ── Edge cases ───────────────────────────────────────────────────────

        [Test]
        public void Compute_EmptyList_ReturnsEmpty()
        {
            Assert.IsEmpty(SynergySystem.Compute(new List<CombatUnit>()));
        }

        [Test]
        public void Compute_NullList_ReturnsEmpty()
        {
            Assert.IsEmpty(SynergySystem.Compute(null));
        }

        [Test]
        public void Apply_SetsEquipmentModifiers_AndDirtiesStats()
        {
            var units = new List<CombatUnit>
            {
                MakeUnit(HeroClass.Slayer),
                MakeUnit(HeroClass.Slayer),
            };
            // Baseline: no mods
            foreach (var u in units)
                Assert.IsEmpty(u.EquipmentModifiers);

            SynergySystem.Apply(units);

            foreach (var u in units)
                Assert.IsNotEmpty(u.EquipmentModifiers, "EquipmentModifiers should contain synergy mods");
        }
    }
}
