using System;
using Game.Core;
using Game.Data;
using Game.Data.Dto;
using Game.Meta.Endgame;
using Game.Services.Economy;
using NUnit.Framework;

namespace Game.Tests.Meta
{
    /// <summary>DungeonSystem — task-endgame.md, plan.md §8.3.</summary>
    public class DungeonSystemTests
    {
        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            ServiceLocator.Register<IEconomyService>(new EconomyService());
        }

        [TearDown]
        public void TearDown() => ServiceLocator.Clear();

        private static PlayerProfileDto Profile()
        {
            var p = new PlayerProfileDto();
            p.Heroes.Clear();
            return p;
        }

        private static IEconomyService Economy() => ServiceLocator.Get<IEconomyService>();

        // ---------- Ngày mở ----------

        [Test]
        public void IsAvailableToday_MatchesActiveDaysTable()
        {
            // 2026-08-09 là Chủ Nhật — Gold/Exp/Material đều mở Chủ Nhật, Stone thì không.
            var sunday = new DateTime(2026, 8, 9, 10, 0, 0, DateTimeKind.Utc);
            Assert.IsTrue(DungeonSystem.IsAvailableToday(DungeonKind.Gold, sunday));
            Assert.IsTrue(DungeonSystem.IsAvailableToday(DungeonKind.Exp, sunday));
            Assert.IsTrue(DungeonSystem.IsAvailableToday(DungeonKind.Material, sunday));
            Assert.IsFalse(DungeonSystem.IsAvailableToday(DungeonKind.Stone, sunday));
        }

        [Test]
        public void CanEnter_UnavailableToday_ReturnsFalseEvenWithFloorsLeft()
        {
            var p = Profile();
            var sunday = new DateTime(2026, 8, 9, 10, 0, 0, DateTimeKind.Utc);

            Assert.IsFalse(DungeonSystem.CanEnter(p, DungeonKind.Stone, sunday));
        }

        // ---------- Reset hằng ngày ----------

        [Test]
        public void EnsureDailyReset_SameDay_KeepsProgress()
        {
            var p = Profile();
            var now = new DateTime(2026, 8, 9, 10, 0, 0, DateTimeKind.Utc);
            DungeonSystem.EnsureDailyReset(p, now);
            DungeonSystem.MarkFloorCleared(p, DungeonKind.Gold, 3);

            DungeonSystem.EnsureDailyReset(p, now.AddHours(6));

            Assert.AreEqual(3, DungeonSystem.FloorCleared(p, DungeonKind.Gold));
        }

        [Test]
        public void EnsureDailyReset_NextDay_ClearsAllKindsFloorProgress()
        {
            var p = Profile();
            var day1 = new DateTime(2026, 8, 9, 10, 0, 0, DateTimeKind.Utc);
            DungeonSystem.EnsureDailyReset(p, day1);
            DungeonSystem.MarkFloorCleared(p, DungeonKind.Gold, 5);
            DungeonSystem.MarkFloorCleared(p, DungeonKind.Stone, 2);

            DungeonSystem.EnsureDailyReset(p, day1.AddDays(1));

            Assert.AreEqual(0, DungeonSystem.FloorCleared(p, DungeonKind.Gold));
            Assert.AreEqual(0, DungeonSystem.FloorCleared(p, DungeonKind.Stone));
        }

        // ---------- Tầng ----------

        [Test]
        public void NextFloor_FreshProfile_IsOne()
        {
            var p = Profile();
            Assert.AreEqual(1, DungeonSystem.NextFloor(p, DungeonKind.Gold));
        }

        [Test]
        public void MarkFloorCleared_OnlyIncreases_NeverGoesBackward()
        {
            var p = Profile();
            DungeonSystem.MarkFloorCleared(p, DungeonKind.Gold, 5);
            DungeonSystem.MarkFloorCleared(p, DungeonKind.Gold, 3); // thấp hơn — bỏ qua

            Assert.AreEqual(5, DungeonSystem.FloorCleared(p, DungeonKind.Gold));
        }

        [Test]
        public void CanEnter_AllFloorsCleared_ReturnsFalse()
        {
            var p = Profile();
            var sunday = new DateTime(2026, 8, 9, 10, 0, 0, DateTimeKind.Utc);
            DungeonSystem.MarkFloorCleared(p, DungeonKind.Gold, DungeonSystem.MAX_FLOOR);

            Assert.IsFalse(DungeonSystem.CanEnter(p, DungeonKind.Gold, sunday));
        }

        [Test]
        public void EnemyCountForFloor_ScalesByThreshold()
        {
            Assert.AreEqual(3, DungeonSystem.EnemyCountForFloor(1));
            Assert.AreEqual(3, DungeonSystem.EnemyCountForFloor(3));
            Assert.AreEqual(4, DungeonSystem.EnemyCountForFloor(4));
            Assert.AreEqual(4, DungeonSystem.EnemyCountForFloor(7));
            Assert.AreEqual(5, DungeonSystem.EnemyCountForFloor(8));
        }

        [Test]
        public void IsTougherFloor_OnlyAboveFive()
        {
            Assert.IsFalse(DungeonSystem.IsTougherFloor(5));
            Assert.IsTrue(DungeonSystem.IsTougherFloor(6));
        }

        // ---------- Thưởng ----------

        [Test]
        public void GrantFloorReward_Gold_ScalesWithFloor()
        {
            var p = Profile();
            DungeonSystem.GrantFloorReward(p, DungeonKind.Gold, 3, Economy());

            Assert.AreEqual(600, p.Wallet.Gold); // 200 * floor
        }

        [Test]
        public void GrantFloorReward_Stone_GrantsEnhanceStoneEqualToFloor()
        {
            var p = Profile();
            DungeonSystem.GrantFloorReward(p, DungeonKind.Stone, 4, Economy());

            Assert.AreEqual(4, Economy().Get(p.Wallet, CurrencyType.EnhanceStone));
        }

        [Test]
        public void GrantFloorReward_Material_LowFloor_OnlyGrantsEssenceI()
        {
            var p = Profile();
            DungeonSystem.GrantFloorReward(p, DungeonKind.Material, 1, Economy());

            Assert.AreEqual(2, Economy().Get(p.Wallet, CurrencyType.EssenceI));
            Assert.AreEqual(0, Economy().Get(p.Wallet, CurrencyType.EssenceII));
            Assert.AreEqual(0, Economy().Get(p.Wallet, CurrencyType.EssenceIII));
        }

        [Test]
        public void GrantFloorReward_Material_Floor4_UnlocksEssenceII()
        {
            var p = Profile();
            DungeonSystem.GrantFloorReward(p, DungeonKind.Material, 4, Economy());

            Assert.AreEqual(1, Economy().Get(p.Wallet, CurrencyType.EssenceII));
            Assert.AreEqual(0, Economy().Get(p.Wallet, CurrencyType.EssenceIII));
        }

        [Test]
        public void GrantFloorReward_Material_Floor8_UnlocksEssenceIII()
        {
            var p = Profile();
            DungeonSystem.GrantFloorReward(p, DungeonKind.Material, 8, Economy());

            Assert.AreEqual(1, Economy().Get(p.Wallet, CurrencyType.EssenceII));
            Assert.AreEqual(1, Economy().Get(p.Wallet, CurrencyType.EssenceIII));
        }

        [Test]
        public void GrantFloorReward_Exp_GrantsToAllOwnedHeroes()
        {
            var p = Profile();
            p.Heroes.Add(new HeroInstanceDto { DefId = "hero_a", Uid = "u1", Level = 1, Exp = 0 });
            p.Heroes.Add(new HeroInstanceDto { DefId = "hero_b", Uid = "u2", Level = 1, Exp = 0 });

            DungeonSystem.GrantFloorReward(p, DungeonKind.Exp, 2, Economy());

            Assert.AreEqual(300, p.Heroes[0].Exp, "150 * floor cho mỗi hero sở hữu");
            Assert.AreEqual(300, p.Heroes[1].Exp);
        }
    }
}
