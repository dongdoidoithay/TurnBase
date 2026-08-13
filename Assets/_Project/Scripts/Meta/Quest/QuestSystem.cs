using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Data;
using Game.Data.Dto;
using Game.Meta.Hero;
using Game.Services.Economy;
using UnityEngine;

namespace Game.Meta.Quest
{
    /// <summary>
    /// Quest hằng ngày + Achievement một-lần — task-quest.md. Thay Gem faucet tạm
    /// (task-ascend.md §9, xoá ở mục 5). V1 CHỈ Daily (không Weekly/Chain/Battle Pass dù field đã
    /// có trong <see cref="QuestProgressDto"/> — giữ tối giản).
    /// </summary>
    public static class QuestSystem
    {
        public readonly struct DailyQuestDef
        {
            public readonly string Id;
            public readonly QuestConditionType Condition;
            public readonly int Target;
            public readonly long GemReward;

            public DailyQuestDef(string id, QuestConditionType condition, int target, long gemReward)
            {
                Id = id; Condition = condition; Target = target; GemReward = gemReward;
            }
        }

        public readonly struct AchievementDef
        {
            public readonly string Id;
            public readonly string NameKey;
            public readonly Func<PlayerProfileDto, bool> IsUnlocked;
            public readonly long GemReward;

            public AchievementDef(string id, string nameKey, Func<PlayerProfileDto, bool> isUnlocked, long gemReward)
            {
                Id = id; NameKey = nameKey; IsUnlocked = isUnlocked; GemReward = gemReward;
            }
        }

        /// <summary>Chỉ 3 điều kiện rẻ, không cần plumbing mới (task-quest.md §1) — điều kiện
        /// khác trong QuestConditionType (PerfectHits, BreaksTriggered, GoldSpent,
        /// DungeonCleared, LoginDays, StagesCleared, EquipEnhanced) cần hạ tầng chưa có, ngoài
        /// phạm vi V1.</summary>
        private static readonly DailyQuestDef[] DAILY_QUESTS =
        {
            new("daily_win_3_battles", QuestConditionType.BattlesWon, 3, 50),
            new("daily_level_up_5", QuestConditionType.HeroLevelUps, 5, 50),
            new("daily_summon_1", QuestConditionType.SummonsPerformed, 1, 100),
        };

        private static readonly AchievementDef[] ACHIEVEMENTS =
        {
            new("ach_collect_6_heroes", "achievement.collect_6_heroes",
                p => p.Heroes.Count >= 6, 100),
            new("ach_ascend_to_max", "achievement.ascend_to_max",
                p => p.Heroes.Any(h => h.Star >= AscendSystem.MAX_STAR), 200),
            new("ach_reach_chapter_3", "achievement.reach_chapter_3",
                p => p.Progress.ChapterUnlocked >= 3, 150),
        };

        public static IReadOnlyList<DailyQuestDef> DailyQuests => DAILY_QUESTS;
        public static IReadOnlyList<AchievementDef> Achievements => ACHIEVEMENTS;

        // =====================================================================
        // Reset hằng ngày
        // =====================================================================

        /// <summary>So <c>LastDailyResetUtc</c> (ISO ngày, "yyyy-MM-dd") với ngày UTC hiện tại —
        /// khác ngày thì xoá tiến độ Daily + Claimed id thuộc Daily, KHÔNG đụng Achievement.</summary>
        public static void EnsureDailyReset(PlayerProfileDto profile, DateTime utcNow)
        {
            string today = utcNow.ToString("yyyy-MM-dd");
            if (profile.Quests.LastDailyResetUtc == today) return;

            profile.Quests.Daily.Clear();
            var dailyIds = new HashSet<string>(DAILY_QUESTS.Select(q => q.Id));
            profile.Quests.ClaimedQuestIds.RemoveAll(id => dailyIds.Contains(id));
            profile.Quests.LastDailyResetUtc = today;
        }

        // =====================================================================
        // Tăng tiến độ — gọi TỪ nơi hành vi xảy ra (KHÔNG đọc lại LifetimeStatsDto vì đó là
        // counter TRỌN ĐỜI, không reset theo ngày — đọc thẳng sẽ khiến quest daily "tự động đủ
        // điều kiện" mãi mãi sau lần đầu, không cần lặp lại hành vi mỗi ngày).
        // =====================================================================

        public static void IncrementDailyProgress(PlayerProfileDto profile, QuestConditionType condition, int amount)
        {
            if (amount <= 0) return;
            foreach (var quest in DAILY_QUESTS)
            {
                if (quest.Condition != condition) continue;
                var entry = FindOrAddDaily(profile, quest.Id);
                entry.Value += amount;
            }
        }

        public static int GetDailyProgress(PlayerProfileDto profile, string questId)
        {
            foreach (var e in profile.Quests.Daily)
                if (e.Key == questId) return (int)e.Value;
            return 0;
        }

        private static CurrencyEntryDto FindOrAddDaily(PlayerProfileDto profile, string questId)
        {
            foreach (var e in profile.Quests.Daily)
                if (e.Key == questId) return e;
            var fresh = new CurrencyEntryDto(questId, 0);
            profile.Quests.Daily.Add(fresh);
            return fresh;
        }

        // =====================================================================
        // Claim
        // =====================================================================

        public static bool TryClaimDaily(PlayerProfileDto profile, string questId)
        {
            if (profile.Quests.ClaimedQuestIds.Contains(questId)) return false;

            DailyQuestDef? found = null;
            foreach (var q in DAILY_QUESTS)
                if (q.Id == questId) { found = q; break; }
            if (found == null) return false;

            var quest = found.Value;
            if (GetDailyProgress(profile, questId) < quest.Target) return false;
            if (!ServiceLocator.TryGet<IEconomyService>(out var economy)) return false;

            economy.Grant(profile.Wallet, CurrencyType.Gem, quest.GemReward);
            profile.Quests.ClaimedQuestIds.Add(questId);
            return true;
        }

        public static bool TryClaimAchievement(PlayerProfileDto profile, string achievementId)
        {
            if (profile.Quests.UnlockedAchievements.Contains(achievementId)) return false;

            AchievementDef? found = null;
            foreach (var a in ACHIEVEMENTS)
                if (a.Id == achievementId) { found = a; break; }
            if (found == null) return false;

            var achievement = found.Value;
            if (!achievement.IsUnlocked(profile)) return false;
            if (!ServiceLocator.TryGet<IEconomyService>(out var economy)) return false;

            economy.Grant(profile.Wallet, CurrencyType.Gem, achievement.GemReward);
            profile.Quests.UnlockedAchievements.Add(achievementId);
            return true;
        }
    }
}
