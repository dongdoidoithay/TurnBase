using System;
using Game.Core;
using Game.Core.Random;
using Game.Data;
using Game.Data.Dto;
using Game.Meta.Endgame;
using Game.Services.Economy;
using NUnit.Framework;

namespace Game.Tests.Meta
{
    /// <summary>TowerSystem — task-endgame.md, plan.md §8.3.</summary>
    public class TowerSystemTests
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
        private static IRandomSource Rng() => new XorShiftRandom(900UL);

        // ---------- Reset hằng tuần ----------

        [Test]
        public void EnsureWeeklyReset_SameWeek_KeepsProgress()
        {
            var p = Profile();
            var now = new DateTime(2026, 8, 9, 10, 0, 0, DateTimeKind.Utc);
            TowerSystem.EnsureWeeklyReset(p, now);
            TowerSystem.RecordClimb(p, 15);

            TowerSystem.EnsureWeeklyReset(p, now.AddDays(2));

            Assert.AreEqual(15, p.Tower.BestFloorThisWeek);
        }

        [Test]
        public void EnsureWeeklyReset_NextWeek_ClearsBestFloorAndClaimedTier_ButNotPermanentMark()
        {
            var p = Profile();
            var week1 = new DateTime(2026, 8, 9, 10, 0, 0, DateTimeKind.Utc);
            TowerSystem.EnsureWeeklyReset(p, week1);
            TowerSystem.RecordClimb(p, 30);
            TowerSystem.TryClaimRewards(p, Economy(), Rng());
            Assert.Greater(p.Tower.ClaimedTier, 0);

            TowerSystem.EnsureWeeklyReset(p, week1.AddDays(8));

            Assert.AreEqual(0, p.Tower.BestFloorThisWeek);
            Assert.AreEqual(0, p.Tower.ClaimedTier);
            Assert.AreEqual(30, p.Progress.TowerFloor, "Mốc mọi thời đại KHÔNG được reset theo tuần");
        }

        // ---------- Ghi nhận lượt leo ----------

        [Test]
        public void RecordClimb_HigherFloor_UpdatesBothWeeklyAndPermanentMark()
        {
            var p = Profile();
            TowerSystem.RecordClimb(p, 10);
            TowerSystem.RecordClimb(p, 40);

            Assert.AreEqual(40, p.Tower.BestFloorThisWeek);
            Assert.AreEqual(40, p.Progress.TowerFloor);
        }

        [Test]
        public void RecordClimb_LowerFloor_DoesNotOverwriteEitherMark()
        {
            var p = Profile();
            TowerSystem.RecordClimb(p, 40);
            TowerSystem.RecordClimb(p, 10);

            Assert.AreEqual(40, p.Tower.BestFloorThisWeek);
            Assert.AreEqual(40, p.Progress.TowerFloor);
        }

        [Test]
        public void RecordClimb_NewWeekLowerThanPermanentMark_StillUpdatesPermanentMarkCorrectly()
        {
            // Permanent mark không reset hằng tuần — leo yếu hơn ở tuần mới không được hạ nó xuống.
            var p = Profile();
            p.Progress.TowerFloor = 50;

            TowerSystem.RecordClimb(p, 20);

            Assert.AreEqual(50, p.Progress.TowerFloor);
            Assert.AreEqual(20, p.Tower.BestFloorThisWeek);
        }

        // ---------- Nhận thưởng ----------

        [Test]
        public void TryClaimRewards_BelowTier1_ReturnsFalse_NoGrant()
        {
            var p = Profile();
            TowerSystem.RecordClimb(p, 5); // < 10

            bool claimed = TowerSystem.TryClaimRewards(p, Economy(), Rng());

            Assert.IsFalse(claimed);
            Assert.AreEqual(0, p.Wallet.Gem);
            Assert.AreEqual(0, p.Tower.ClaimedTier);
        }

        [Test]
        public void TryClaimRewards_ReachesTier1Only_GrantsExactlyTier1Reward_NoCoreNoMythic()
        {
            var p = Profile();
            TowerSystem.RecordClimb(p, 12);

            bool claimed = TowerSystem.TryClaimRewards(p, Economy(), Rng());

            Assert.IsTrue(claimed);
            Assert.AreEqual(1, p.Tower.ClaimedTier);
            Assert.AreEqual(TowerSystem.Tiers[0].Gem, p.Wallet.Gem);
            Assert.AreEqual(0, Economy().Get(p.Wallet, CurrencyType.Core));
            Assert.AreEqual(0, p.Equipment.Count, "Tier 1 chưa có trang bị Mythic");
        }

        [Test]
        public void TryClaimRewards_JumpsStraightToMaxFloor_GrantsAllTiersCumulatively_IncludingMythic()
        {
            var p = Profile();
            TowerSystem.RecordClimb(p, TowerSystem.MAX_FLOOR); // nhảy thẳng tầng 100

            TowerSystem.TryClaimRewards(p, Economy(), Rng());

            Assert.AreEqual(TowerSystem.Tiers.Count, p.Tower.ClaimedTier);

            long expectedGem = 0, expectedCore = 0;
            foreach (var t in TowerSystem.Tiers) { expectedGem += t.Gem; expectedCore += t.Core; }
            Assert.AreEqual(expectedGem, p.Wallet.Gem);
            Assert.AreEqual(expectedCore, Economy().Get(p.Wallet, CurrencyType.Core));

            Assert.AreEqual(1, p.Equipment.Count, "Bậc cuối (tầng 100) phải cấp đúng 1 trang bị Mythic");
            Assert.AreEqual((int)Rarity.Mythic, p.Equipment[0].Rarity);
        }

        [Test]
        public void TryClaimRewards_AlreadyClaimedTier_SecondCallReturnsFalse_NoDoubleGrant()
        {
            var p = Profile();
            TowerSystem.RecordClimb(p, 12);
            Assert.IsTrue(TowerSystem.TryClaimRewards(p, Economy(), Rng()));
            long gemAfterFirst = p.Wallet.Gem;

            bool secondClaim = TowerSystem.TryClaimRewards(p, Economy(), Rng());

            Assert.IsFalse(secondClaim);
            Assert.AreEqual(gemAfterFirst, p.Wallet.Gem, "Không được cấp thêm Gem khi không có bậc mới");
        }

        [Test]
        public void TryClaimRewards_NewHigherFloorUnlocksNextTierOnly_GrantsOnlyTheDelta()
        {
            var p = Profile();
            TowerSystem.RecordClimb(p, 12);
            TowerSystem.TryClaimRewards(p, Economy(), Rng()); // nhận Tier 1
            long gemAfterTier1 = p.Wallet.Gem;

            TowerSystem.RecordClimb(p, 26); // vượt luôn Tier 2 (ngưỡng 25)
            bool claimed = TowerSystem.TryClaimRewards(p, Economy(), Rng());

            Assert.IsTrue(claimed);
            Assert.AreEqual(2, p.Tower.ClaimedTier);
            Assert.AreEqual(gemAfterTier1 + TowerSystem.Tiers[1].Gem, p.Wallet.Gem,
                "Chỉ cộng thêm đúng phần thưởng Tier 2, không cấp lại Tier 1");
        }

        // ---------- Độ khó theo tầng ----------

        [Test]
        public void EnemyCountForFloor_ScalesByThreshold()
        {
            Assert.AreEqual(3, TowerSystem.EnemyCountForFloor(1));
            Assert.AreEqual(3, TowerSystem.EnemyCountForFloor(20));
            Assert.AreEqual(4, TowerSystem.EnemyCountForFloor(21));
            Assert.AreEqual(4, TowerSystem.EnemyCountForFloor(60));
            Assert.AreEqual(5, TowerSystem.EnemyCountForFloor(61));
            Assert.AreEqual(5, TowerSystem.EnemyCountForFloor(TowerSystem.MAX_FLOOR));
        }

        [Test]
        public void IsTougherFloor_OnlyAboveForty()
        {
            Assert.IsFalse(TowerSystem.IsTougherFloor(40));
            Assert.IsTrue(TowerSystem.IsTougherFloor(41));
        }
    }
}
