using System;
using Game.Core;
using Game.Data;
using Game.Data.Dto;
using Game.Meta.Endgame;
using Game.Services.Economy;
using NUnit.Framework;

namespace Game.Tests.Meta
{
    /// <summary>TrialBossSystem — task-endgame.md, plan.md §8.3.</summary>
    public class TrialBossSystemTests
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

        // ---------- Reset hằng tuần ----------

        [Test]
        public void EnsureWeeklyReset_SameWeek_KeepsProgress()
        {
            var p = Profile();
            var now = new DateTime(2026, 8, 9, 10, 0, 0, DateTimeKind.Utc);
            TrialBossSystem.EnsureWeeklyReset(p, now);
            TrialBossSystem.RecordAttempt(p, 1500);

            TrialBossSystem.EnsureWeeklyReset(p, now.AddDays(2)); // vẫn cùng tuần

            Assert.AreEqual(1500, p.TrialBoss.BestDamageThisWeek);
        }

        [Test]
        public void EnsureWeeklyReset_NextWeek_ClearsBestDamageAndClaimedTier()
        {
            var p = Profile();
            var week1 = new DateTime(2026, 8, 9, 10, 0, 0, DateTimeKind.Utc);
            TrialBossSystem.EnsureWeeklyReset(p, week1);
            TrialBossSystem.RecordAttempt(p, 5000);
            TrialBossSystem.TryClaimRewards(p, Economy());
            Assert.Greater(p.TrialBoss.ClaimedTier, 0);

            TrialBossSystem.EnsureWeeklyReset(p, week1.AddDays(8)); // sang tuần khác

            Assert.AreEqual(0, p.TrialBoss.BestDamageThisWeek);
            Assert.AreEqual(0, p.TrialBoss.ClaimedTier);
        }

        // ---------- Ghi nhận lượt đánh ----------

        [Test]
        public void RecordAttempt_HigherDamage_UpdatesBest()
        {
            var p = Profile();
            TrialBossSystem.RecordAttempt(p, 1000);
            TrialBossSystem.RecordAttempt(p, 3000);

            Assert.AreEqual(3000, p.TrialBoss.BestDamageThisWeek);
        }

        [Test]
        public void RecordAttempt_LowerDamage_DoesNotOverwriteBest()
        {
            var p = Profile();
            TrialBossSystem.RecordAttempt(p, 3000);
            TrialBossSystem.RecordAttempt(p, 1000);

            Assert.AreEqual(3000, p.TrialBoss.BestDamageThisWeek);
        }

        // ---------- Nhận thưởng ----------

        [Test]
        public void TryClaimRewards_BelowTier1_ReturnsFalse_NoGrant()
        {
            var p = Profile();
            TrialBossSystem.RecordAttempt(p, 1000); // < 2000

            bool claimed = TrialBossSystem.TryClaimRewards(p, Economy());

            Assert.IsFalse(claimed);
            Assert.AreEqual(0, p.Wallet.Gem);
            Assert.AreEqual(0, p.TrialBoss.ClaimedTier);
        }

        [Test]
        public void TryClaimRewards_ReachesTier1Only_GrantsExactlyTier1Reward()
        {
            var p = Profile();
            TrialBossSystem.RecordAttempt(p, 2500);

            bool claimed = TrialBossSystem.TryClaimRewards(p, Economy());

            Assert.IsTrue(claimed);
            Assert.AreEqual(1, p.TrialBoss.ClaimedTier);
            Assert.AreEqual(TrialBossSystem.Tiers[0].Gem, p.Wallet.Gem);
            Assert.AreEqual(TrialBossSystem.Tiers[0].Shards,
                Economy().GetShards(p.Wallet, TrialBossSystem.FEATURED_HERO_DEF_ID));
        }

        [Test]
        public void TryClaimRewards_JumpsStraightToTier3_GrantsAllThreeTiersCumulatively()
        {
            var p = Profile();
            TrialBossSystem.RecordAttempt(p, 12000); // vượt cả 3 bậc cùng lúc

            TrialBossSystem.TryClaimRewards(p, Economy());

            Assert.AreEqual(3, p.TrialBoss.ClaimedTier);
            long expectedGem = TrialBossSystem.Tiers[0].Gem + TrialBossSystem.Tiers[1].Gem + TrialBossSystem.Tiers[2].Gem;
            long expectedShards = TrialBossSystem.Tiers[0].Shards + TrialBossSystem.Tiers[1].Shards + TrialBossSystem.Tiers[2].Shards;
            Assert.AreEqual(expectedGem, p.Wallet.Gem, "Nhảy thẳng bậc 3 vẫn phải nhận đủ cả bậc 1+2+3");
            Assert.AreEqual(expectedShards, Economy().GetShards(p.Wallet, TrialBossSystem.FEATURED_HERO_DEF_ID));
        }

        [Test]
        public void TryClaimRewards_AlreadyClaimedTier_SecondCallReturnsFalse_NoDoubleGrant()
        {
            var p = Profile();
            TrialBossSystem.RecordAttempt(p, 2500);
            Assert.IsTrue(TrialBossSystem.TryClaimRewards(p, Economy()));
            long gemAfterFirst = p.Wallet.Gem;

            bool secondClaim = TrialBossSystem.TryClaimRewards(p, Economy());

            Assert.IsFalse(secondClaim);
            Assert.AreEqual(gemAfterFirst, p.Wallet.Gem, "Không được cấp thêm Gem khi không có bậc mới");
        }

        [Test]
        public void TryClaimRewards_NewHigherDamageUnlocksNextTierOnly_GrantsOnlyTheDelta()
        {
            var p = Profile();
            TrialBossSystem.RecordAttempt(p, 2500);
            TrialBossSystem.TryClaimRewards(p, Economy()); // nhận Tier 1
            long gemAfterTier1 = p.Wallet.Gem;

            TrialBossSystem.RecordAttempt(p, 6000); // vượt luôn Tier 2
            bool claimed = TrialBossSystem.TryClaimRewards(p, Economy());

            Assert.IsTrue(claimed);
            Assert.AreEqual(2, p.TrialBoss.ClaimedTier);
            Assert.AreEqual(gemAfterTier1 + TrialBossSystem.Tiers[1].Gem, p.Wallet.Gem,
                "Chỉ cộng thêm đúng phần thưởng Tier 2, không cấp lại Tier 1");
        }

        [Test]
        public void WeekKey_ExactlySevenDaysLater_IsAlwaysOneKeyHigher()
        {
            // 7 ngày sau LUÔN lệch đúng +1 (floor((d+7)/7) = floor(d/7)+1), bất kể mốc bắt đầu
            // rơi vào đâu trong tuần — tránh test phụ thuộc biên tuần cụ thể như 8/9-8/10.
            var a = new DateTime(2026, 8, 9, 10, 0, 0, DateTimeKind.Utc);
            var b = a.AddDays(7);

            Assert.AreEqual(TrialBossSystem.WeekKey(a) + 1, TrialBossSystem.WeekKey(b));
        }
    }
}
