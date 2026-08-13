using System;
using Game.Core;
using Game.Data;
using Game.Data.Dto;
using Game.Meta.Quest;
using Game.Services.Economy;
using NUnit.Framework;

namespace Game.Tests.Meta
{
    /// <summary>QuestSystem — task-quest.md, thay Gem faucet tạm (task-ascend.md §9).</summary>
    public class QuestSystemTests
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

        // ---------- Reset hằng ngày ----------

        [Test]
        public void EnsureDailyReset_SameDay_DoesNothing()
        {
            var p = Profile();
            var now = new DateTime(2026, 8, 9, 10, 0, 0, DateTimeKind.Utc);
            QuestSystem.EnsureDailyReset(p, now);
            QuestSystem.IncrementDailyProgress(p, QuestConditionType.BattlesWon, 2);

            QuestSystem.EnsureDailyReset(p, now.AddHours(5)); // vẫn cùng ngày UTC

            Assert.AreEqual(2, QuestSystem.GetDailyProgress(p, "daily_win_3_battles"));
        }

        [Test]
        public void EnsureDailyReset_NextDay_ClearsProgressAndClaimedDailyIds()
        {
            var p = Profile();
            var day1 = new DateTime(2026, 8, 9, 10, 0, 0, DateTimeKind.Utc);
            QuestSystem.EnsureDailyReset(p, day1);
            QuestSystem.IncrementDailyProgress(p, QuestConditionType.BattlesWon, 3);
            Assert.IsTrue(QuestSystem.TryClaimDaily(p, "daily_win_3_battles"));

            var day2 = day1.AddDays(1);
            QuestSystem.EnsureDailyReset(p, day2);

            Assert.AreEqual(0, QuestSystem.GetDailyProgress(p, "daily_win_3_battles"), "Tiến độ phải về 0 khi qua ngày mới");
            Assert.IsFalse(p.Quests.ClaimedQuestIds.Contains("daily_win_3_battles"), "Claimed id thuộc Daily phải bị xoá khi reset");
        }

        [Test]
        public void EnsureDailyReset_DoesNotTouchAchievements()
        {
            var p = Profile();
            var day1 = new DateTime(2026, 8, 9, 10, 0, 0, DateTimeKind.Utc);
            QuestSystem.EnsureDailyReset(p, day1);
            p.Quests.UnlockedAchievements.Add("ach_reach_chapter_3");

            QuestSystem.EnsureDailyReset(p, day1.AddDays(1));

            Assert.IsTrue(p.Quests.UnlockedAchievements.Contains("ach_reach_chapter_3"), "Achievement không reset theo ngày");
        }

        // ---------- Daily quest — atomic, chỉ claim khi đủ điều kiện ----------

        [Test]
        public void TryClaimDaily_BelowTarget_Fails_NoGemGranted()
        {
            var p = Profile();
            QuestSystem.IncrementDailyProgress(p, QuestConditionType.BattlesWon, 2); // cần 3

            bool ok = QuestSystem.TryClaimDaily(p, "daily_win_3_battles");

            Assert.IsFalse(ok);
            Assert.AreEqual(0, p.Wallet.Gem);
        }

        [Test]
        public void TryClaimDaily_MeetsTarget_GrantsGem_AndMarksClaimed()
        {
            var p = Profile();
            QuestSystem.IncrementDailyProgress(p, QuestConditionType.BattlesWon, 3);

            bool ok = QuestSystem.TryClaimDaily(p, "daily_win_3_battles");

            Assert.IsTrue(ok);
            Assert.Greater(p.Wallet.Gem, 0);
            Assert.IsTrue(p.Quests.ClaimedQuestIds.Contains("daily_win_3_battles"));
        }

        [Test]
        public void TryClaimDaily_AlreadyClaimed_FailsSecondTime_NoDoubleGrant()
        {
            var p = Profile();
            QuestSystem.IncrementDailyProgress(p, QuestConditionType.BattlesWon, 3);
            Assert.IsTrue(QuestSystem.TryClaimDaily(p, "daily_win_3_battles"));
            long gemAfterFirst = p.Wallet.Gem;

            bool secondClaim = QuestSystem.TryClaimDaily(p, "daily_win_3_battles");

            Assert.IsFalse(secondClaim);
            Assert.AreEqual(gemAfterFirst, p.Wallet.Gem, "Claim lần 2 không được cấp thêm Gem");
        }

        [Test]
        public void IncrementDailyProgress_UnrelatedCondition_DoesNotAffectOtherQuests()
        {
            var p = Profile();
            QuestSystem.IncrementDailyProgress(p, QuestConditionType.HeroLevelUps, 999);

            Assert.AreEqual(0, QuestSystem.GetDailyProgress(p, "daily_win_3_battles"));
        }

        // ---------- Achievement — một lần, kiểm tra điều kiện trực tiếp trên profile ----------

        [Test]
        public void TryClaimAchievement_ConditionNotMet_Fails()
        {
            var p = Profile();
            Assert.IsFalse(QuestSystem.TryClaimAchievement(p, "ach_collect_6_heroes"));
        }

        [Test]
        public void TryClaimAchievement_CollectSixHeroes_UnlocksAndGrants()
        {
            var p = Profile();
            for (int i = 0; i < 6; i++)
                p.Heroes.Add(new HeroInstanceDto { DefId = $"hero_{i}", Uid = $"u{i}" });

            bool ok = QuestSystem.TryClaimAchievement(p, "ach_collect_6_heroes");

            Assert.IsTrue(ok);
            Assert.Greater(p.Wallet.Gem, 0);
            Assert.IsTrue(p.Quests.UnlockedAchievements.Contains("ach_collect_6_heroes"));
        }

        [Test]
        public void TryClaimAchievement_AscendToMaxStar_UnlocksWhenAnyHeroReachesMaxStar()
        {
            var p = Profile();
            p.Heroes.Add(new HeroInstanceDto { DefId = "hero_x", Uid = "u1", Star = Game.Meta.Hero.AscendSystem.MAX_STAR });

            Assert.IsTrue(QuestSystem.TryClaimAchievement(p, "ach_ascend_to_max"));
        }

        [Test]
        public void TryClaimAchievement_ReachChapter3_UnlocksWhenChapterUnlockedHighEnough()
        {
            var p = Profile();
            p.Progress.ChapterUnlocked = 3;

            Assert.IsTrue(QuestSystem.TryClaimAchievement(p, "ach_reach_chapter_3"));
        }

        [Test]
        public void TryClaimAchievement_AlreadyClaimed_FailsSecondTime()
        {
            var p = Profile();
            p.Progress.ChapterUnlocked = 3;
            Assert.IsTrue(QuestSystem.TryClaimAchievement(p, "ach_reach_chapter_3"));

            Assert.IsFalse(QuestSystem.TryClaimAchievement(p, "ach_reach_chapter_3"));
        }

        /// <summary>Regression thay thế PlaceholderLootTableTests.BossReward_GrantsGem_... đã xoá
        /// (task-ascend.md §9 → task-quest.md mục 5): Gem giờ đến từ Quest/Achievement, phải có
        /// ÍT NHẤT 1 nguồn > 0 — nếu không Shop/Gacha vĩnh viễn bế tắc sau 300 Gem khởi điểm.</summary>
        [Test]
        public void AtLeastOneQuestOrAchievement_GrantsGem_SoShopAndGachaStayUsable()
        {
            bool anyPositive = false;
            foreach (var q in QuestSystem.DailyQuests)
                if (q.GemReward > 0) anyPositive = true;
            foreach (var a in QuestSystem.Achievements)
                if (a.GemReward > 0) anyPositive = true;

            Assert.IsTrue(anyPositive, "Phải có ít nhất 1 Quest/Achievement cấp Gem > 0");
        }
    }
}
