using Game.Data;
using Game.Data.Dto;
using Game.Meta.Hero;
using NUnit.Framework;

namespace Game.Tests.Meta
{
    public class HeroLevelSystemTests
    {
        private static HeroInstanceDto Hero(int level = 1, long exp = 0, int star = 1)
            => new() { Uid = "u", DefId = "hero_test", Level = level, Exp = exp, Star = star };

        [Test]
        public void ExpToReachLevel_MatchesPlanFormula()
        {
            // plan.md §5.3: EXP(n) = round(40 × n^1.85)
            Assert.AreEqual(40, HeroLevelSystem.ExpToReachLevel(1));
            Assert.AreEqual((long)System.Math.Round(40d * System.Math.Pow(20, 1.85)),
                            HeroLevelSystem.ExpToReachLevel(20));
        }

        [Test]
        public void ExpToReachLevel_IsStrictlyIncreasing()
        {
            long prev = HeroLevelSystem.ExpToReachLevel(1);
            for (int lvl = 2; lvl <= 60; lvl++)
            {
                long cur = HeroLevelSystem.ExpToReachLevel(lvl);
                Assert.Greater(cur, prev, $"EXP(level {lvl}) phải lớn hơn EXP(level {lvl - 1})");
                prev = cur;
            }
        }

        [TestCase(1, 40)] [TestCase(2, 40)] [TestCase(3, 40)]
        [TestCase(4, 50)] [TestCase(5, 55)] [TestCase(6, 60)] [TestCase(9, 60)]
        public void LevelCap_MatchesPlanTable(int star, int expectedCap)
        {
            Assert.AreEqual(expectedCap, HeroLevelSystem.LevelCap(star));
        }

        [Test]
        public void AddExp_NotEnoughForNextLevel_NoLevelUp()
        {
            var hero = Hero(level: 1, exp: 0);
            int gained = HeroLevelSystem.AddExp(hero, 5);

            Assert.AreEqual(0, gained);
            Assert.AreEqual(1, hero.Level);
            Assert.AreEqual(5, hero.Exp);
        }

        [Test]
        public void AddExp_EnoughForOneLevel_LevelsUpOnce()
        {
            var hero = Hero(level: 1, exp: 0);
            long need = HeroLevelSystem.ExpToReachLevel(2);

            int gained = HeroLevelSystem.AddExp(hero, need);

            Assert.AreEqual(1, gained);
            Assert.AreEqual(2, hero.Level);
        }

        [Test]
        public void AddExp_HugeAmount_LevelsUpMultipleTimesInOneCall()
        {
            var hero = Hero(level: 1, exp: 0);
            int gained = HeroLevelSystem.AddExp(hero, HeroLevelSystem.ExpToReachLevel(10) + 1);

            Assert.GreaterOrEqual(hero.Level, 10);
            Assert.AreEqual(hero.Level - 1, gained);
        }

        [Test]
        public void AddExp_StopsAtStarCap_EvenWithExcessExp()
        {
            var hero = Hero(level: 1, exp: 0, star: 1); // cap = 40
            HeroLevelSystem.AddExp(hero, HeroLevelSystem.ExpToReachLevel(60));

            Assert.AreEqual(40, hero.Level, "★1 phải kẹp ở cấp 40, muốn lên tiếp phải Ascend");
        }

        [Test]
        public void AddExp_ZeroOrNegative_NoOp()
        {
            var hero = Hero(level: 5, exp: 1000);
            int gained = HeroLevelSystem.AddExp(hero, 0);
            Assert.AreEqual(0, gained);
            Assert.AreEqual(1000, hero.Exp);

            HeroLevelSystem.AddExp(hero, -50);
            Assert.AreEqual(1000, hero.Exp, "EXP âm không được trừ ngược lại");
        }

        [Test]
        public void EffectivePrimary_Level1_EqualsBase()
        {
            var basePrimary = new PrimaryStats(10, 10, 10, 10, 10, 10);
            var effective = HeroLevelSystem.EffectivePrimary(basePrimary, 1);

            Assert.AreEqual(basePrimary.Str, effective.Str, 0.001f);
            Assert.AreEqual(basePrimary.Con, effective.Con, 0.001f);
        }

        [Test]
        public void EffectivePrimary_ScalesUpWithLevel()
        {
            var basePrimary = new PrimaryStats(10, 10, 10, 10, 10, 10);
            var lvl1 = HeroLevelSystem.EffectivePrimary(basePrimary, 1);
            var lvl10 = HeroLevelSystem.EffectivePrimary(basePrimary, 10);

            Assert.Greater(lvl10.Str, lvl1.Str);
            // +10%/level → level 10 = base * (1 + 9*0.10) = base * 1.9
            Assert.AreEqual(19f, lvl10.Str, 0.01f);
        }
    }
}
