using System.Linq;
using Game.Combat.Model;
using Game.Data;
using Game.Meta.Battle;
using NUnit.Framework;

namespace Game.Tests.Meta
{
    /// <summary>FormationSystem — task-formation-synergy.md, plan.md §5.7.</summary>
    public class FormationSystemTests
    {
        [Test]
        public void AllIds_Has8Presets()
        {
            Assert.AreEqual(8, FormationSystem.AllIds.Count);
        }

        [Test]
        public void Default_IsBalanced()
        {
            Assert.AreEqual("formation_balanced", FormationSystem.Default);
        }

        // ── FrontCount ──────────────────────────────────────────────────────

        [TestCase("formation_balanced",      2)]
        [TestCase("formation_phalanx",       3)]
        [TestCase("formation_arrowhead",     1)]
        [TestCase("formation_vanguard_line", 4)]
        [TestCase("formation_siege",         0)]
        [TestCase("formation_flanking",      2)]
        [TestCase("formation_turtle",        2)]
        [TestCase("formation_blitz",         2)]
        public void FrontCount_MatchesDesign(string id, int expected)
        {
            Assert.AreEqual(expected, FormationSystem.FrontCount(id));
        }

        // ── GetRow ──────────────────────────────────────────────────────────

        [Test]
        public void GetRow_Phalanx_Slots012Front_Slot3Back()
        {
            Assert.AreEqual(Row.Front, FormationSystem.GetRow("formation_phalanx", 0));
            Assert.AreEqual(Row.Front, FormationSystem.GetRow("formation_phalanx", 1));
            Assert.AreEqual(Row.Front, FormationSystem.GetRow("formation_phalanx", 2));
            Assert.AreEqual(Row.Back,  FormationSystem.GetRow("formation_phalanx", 3));
        }

        [Test]
        public void GetRow_Siege_AllBack()
        {
            for (int i = 0; i < 4; i++)
                Assert.AreEqual(Row.Back, FormationSystem.GetRow("formation_siege", i),
                                $"slot {i} should be Back in Siege (0 front units)");
        }

        [Test]
        public void GetRow_Arrowhead_Slot0Front_Rest_Back()
        {
            Assert.AreEqual(Row.Front, FormationSystem.GetRow("formation_arrowhead", 0));
            Assert.AreEqual(Row.Back,  FormationSystem.GetRow("formation_arrowhead", 1));
            Assert.AreEqual(Row.Back,  FormationSystem.GetRow("formation_arrowhead", 2));
        }

        [Test]
        public void GetRow_Balanced_Matches_Old_HardCode()
        {
            // Old behaviour: index < 2 = Front, rest = Back
            for (int i = 0; i < 4; i++)
            {
                Row expected = i < 2 ? Row.Front : Row.Back;
                Assert.AreEqual(expected, FormationSystem.GetRow("formation_balanced", i),
                                $"slot {i}");
            }
        }

        // ── GetModifiers ────────────────────────────────────────────────────

        [Test]
        public void GetModifiers_Balanced_Empty()
        {
            Assert.IsEmpty(FormationSystem.GetModifiers("formation_balanced", Row.Front));
            Assert.IsEmpty(FormationSystem.GetModifiers("formation_balanced", Row.Back));
        }

        [Test]
        public void GetModifiers_Phalanx_Front_DefBonus()
        {
            var mods = FormationSystem.GetModifiers("formation_phalanx", Row.Front).ToList();
            Assert.AreEqual(1, mods.Count);
            Assert.AreEqual(StatType.DefPct, mods[0].Stat);
            Assert.AreEqual(15f, mods[0].Value, 0.001f);
        }

        [Test]
        public void GetModifiers_Phalanx_Back_Empty()
        {
            Assert.IsEmpty(FormationSystem.GetModifiers("formation_phalanx", Row.Back));
        }

        [Test]
        public void GetModifiers_Arrowhead_Back_AtkBonus()
        {
            var mods = FormationSystem.GetModifiers("formation_arrowhead", Row.Back).ToList();
            Assert.AreEqual(1, mods.Count);
            Assert.AreEqual(StatType.AtkPct, mods[0].Stat);
            Assert.AreEqual(12f, mods[0].Value, 0.001f);
        }

        [Test]
        public void GetModifiers_VanguardLine_BothRows_AtkAndNegDef()
        {
            var mods = FormationSystem.GetModifiers("formation_vanguard_line", Row.Front).ToList();
            Assert.AreEqual(2, mods.Count);
            Assert.IsTrue(mods.Any(m => m.Stat == StatType.AtkPct  && m.Value > 0));
            Assert.IsTrue(mods.Any(m => m.Stat == StatType.DefPct  && m.Value < 0));
        }

        [Test]
        public void GetModifiers_Siege_DmgReductAndNegSpd()
        {
            var mods = FormationSystem.GetModifiers("formation_siege", Row.Back).ToList();
            Assert.AreEqual(2, mods.Count);
            Assert.IsTrue(mods.Any(m => m.Stat == StatType.DmgReductPct && m.Value > 0));
            Assert.IsTrue(mods.Any(m => m.Stat == StatType.SpdPct       && m.Value < 0));
        }

        [Test]
        public void GetModifiers_Flanking_CritBonus()
        {
            var mods = FormationSystem.GetModifiers("formation_flanking", Row.Front).ToList();
            Assert.AreEqual(1, mods.Count);
            Assert.AreEqual(StatType.CritPct, mods[0].Stat);
            Assert.AreEqual(8f, mods[0].Value, 0.001f);
        }

        [Test]
        public void GetModifiers_Turtle_DefAndNegAtk()
        {
            var mods = FormationSystem.GetModifiers("formation_turtle", Row.Front).ToList();
            Assert.AreEqual(2, mods.Count);
            Assert.IsTrue(mods.Any(m => m.Stat == StatType.DefPct && m.Value > 0));
            Assert.IsTrue(mods.Any(m => m.Stat == StatType.AtkPct && m.Value < 0));
        }

        [Test]
        public void GetModifiers_Blitz_SpdAndNegMaxHp()
        {
            var mods = FormationSystem.GetModifiers("formation_blitz", Row.Front).ToList();
            Assert.AreEqual(2, mods.Count);
            Assert.IsTrue(mods.Any(m => m.Stat == StatType.SpdPct    && m.Value > 0));
            Assert.IsTrue(mods.Any(m => m.Stat == StatType.MaxHpPct  && m.Value < 0));
        }

        // ── DisplayName / BonusSummary ──────────────────────────────────────

        [Test]
        public void DisplayName_AllIds_NonEmpty()
        {
            foreach (var id in FormationSystem.AllIds)
                Assert.IsNotEmpty(FormationSystem.DisplayName(id), $"DisplayName({id})");
        }

        [Test]
        public void BonusSummary_AllIds_NonEmpty()
        {
            foreach (var id in FormationSystem.AllIds)
                Assert.IsNotEmpty(FormationSystem.BonusSummary(id), $"BonusSummary({id})");
        }
    }
}
