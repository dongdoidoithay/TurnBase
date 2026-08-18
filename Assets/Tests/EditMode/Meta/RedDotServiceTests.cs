using System;
using Game.Core;
using Game.Data;
using Game.Data.Dto;
using Game.Meta.Endgame;
using Game.Meta.Notifications;
using Game.Services.Economy;
using NUnit.Framework;

namespace Game.Tests.Meta
{
    /// <summary>RedDotService — task-reddot.md, plan.md §10.6.</summary>
    public class RedDotServiceTests
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

        private static PlayerProfileDto Profile()
        {
            var p = new PlayerProfileDto();
            p.Heroes.Clear();
            p.Heroes.Add(new HeroInstanceDto { Uid = "u1", DefId = "hero_test", Star = 1 });
            return p;
        }

        // ---------- Hero ----------

        [Test]
        public void IsHeroDirty_EnoughGold_ReturnsTrue()
        {
            var p = Profile();
            Economy().Grant(p.Wallet, CurrencyType.Gold, 200);

            Assert.IsTrue(RedDotService.IsHeroDirty(p, Economy()));
        }

        [Test]
        public void IsHeroDirty_NotEnoughGold_ReturnsFalse()
        {
            var p = Profile();
            // Ví trống mặc định — không đủ 200 Gold cho UpgradeCost(1).

            Assert.IsFalse(RedDotService.IsHeroDirty(p, Economy()));
        }

        [Test]
        public void IsHeroDirty_NoHeroes_ReturnsFalse()
        {
            var p = Profile();
            p.Heroes.Clear();
            Economy().Grant(p.Wallet, CurrencyType.Gold, 999_999);

            Assert.IsFalse(RedDotService.IsHeroDirty(p, Economy()));
        }

        [Test]
        public void IsHeroDirty_AllSkillsMaxed_ReturnsFalse()
        {
            var p = Profile();
            Economy().Grant(p.Wallet, CurrencyType.Gold, 999_999);
            for (int i = 0; i < p.Heroes[0].SkillLevels.Length; i++)
                p.Heroes[0].SkillLevels[i] = 8; // MAX_SKILL_LEVEL

            Assert.IsFalse(RedDotService.IsHeroDirty(p, Economy()));
        }

        // ---------- Dungeon ----------

        [Test]
        public void IsDungeonDirty_FreshProfile_ReturnsTrue()
        {
            var p = Profile();
            var now = new DateTime(2026, 8, 18, 10, 0, 0, DateTimeKind.Utc);
            DungeonSystem.EnsureDailyReset(p, now);

            Assert.IsTrue(RedDotService.IsDungeonDirty(p, now));
        }

        [Test]
        public void IsDungeonDirty_AllFloorsClearedToday_ReturnsFalse()
        {
            var p = Profile();
            var now = new DateTime(2026, 8, 18, 10, 0, 0, DateTimeKind.Utc);
            DungeonSystem.EnsureDailyReset(p, now);
            foreach (var kind in new[] { DungeonKind.Gold, DungeonKind.Exp, DungeonKind.Material, DungeonKind.Stone })
                for (int f = 1; f <= DungeonSystem.MAX_FLOOR; f++)
                    DungeonSystem.MarkFloorCleared(p, kind, f);

            Assert.IsFalse(RedDotService.IsDungeonDirty(p, now));
        }

        // ---------- TrialBoss / Tower ----------

        [Test]
        public void IsTrialBossDirty_HasUnclaimedTier_ReturnsTrue()
        {
            var p = Profile();
            TrialBossSystem.EnsureWeeklyReset(p, DateTime.UtcNow);
            TrialBossSystem.RecordAttempt(p, TrialBossSystem.Tiers[0].DamageThreshold);

            Assert.IsTrue(RedDotService.IsTrialBossDirty(p));
        }

        [Test]
        public void IsTrialBossDirty_NothingEligibleYet_ReturnsFalse()
        {
            var p = Profile();
            TrialBossSystem.EnsureWeeklyReset(p, DateTime.UtcNow);

            Assert.IsFalse(RedDotService.IsTrialBossDirty(p));
        }

        [Test]
        public void IsTowerDirty_HasUnclaimedTier_ReturnsTrue()
        {
            var p = Profile();
            TowerSystem.EnsureWeeklyReset(p, DateTime.UtcNow);
            TowerSystem.RecordClimb(p, TowerSystem.Tiers[0].FloorThreshold);

            Assert.IsTrue(RedDotService.IsTowerDirty(p));
        }

        [Test]
        public void IsTowerDirty_NothingEligibleYet_ReturnsFalse()
        {
            var p = Profile();
            TowerSystem.EnsureWeeklyReset(p, DateTime.UtcNow);

            Assert.IsFalse(RedDotService.IsTowerDirty(p));
        }
    }
}
