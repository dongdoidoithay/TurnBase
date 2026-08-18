using System;
using System.Collections.Generic;
using Game.Data;
using Game.Data.Dto;
using Game.Services.Economy;

namespace Game.Meta.Endgame
{
    /// <summary>
    /// Trial Boss hằng tuần — plan.md §8.3, task-endgame.md. 1 boss HP cực cao
    /// (<see cref="BOSS_DEF_ID"/>, stat riêng trong enemies.csv — KHÔNG thay đổi mỗi tuần ở V1,
    /// ngoài phạm vi luân chuyển nội dung theo mùa), xếp hạng CỤC BỘ theo tổng damage tốt nhất
    /// trong tuần — offline v1, không leaderboard nhiều người chơi (plan.md §0).
    /// </summary>
    public static class TrialBossSystem
    {
        public const string BOSS_DEF_ID = "boss_trial_champion";

        /// <summary>Hero nhận mảnh thưởng — cố định 1 hero "featured" cho V1 (plan.md không có cơ
        /// chế chọn hero luân phiên theo mùa, đơn giản hoá có chủ đích).</summary>
        public const string FEATURED_HERO_DEF_ID = "hero_void_scholar";

        public readonly struct RewardTier
        {
            public readonly long DamageThreshold;
            public readonly long Gem;
            public readonly long Shards;
            public RewardTier(long damageThreshold, long gem, long shards)
            {
                DamageThreshold = damageThreshold; Gem = gem; Shards = shards;
            }
        }

        private static readonly RewardTier[] TIERS =
        {
            new(2_000, 100, 2),
            new(5_000, 250, 5),
            new(10_000, 500, 10),
        };

        public static IReadOnlyList<RewardTier> Tiers => TIERS;

        /// <summary>Số tuần kể từ epoch — dùng số nguyên thay vì chuỗi ISO week để không phụ
        /// thuộc API .NET có thể thiếu trên runtime Unity (khác <c>QuestProgressDto.
        /// LastDailyResetUtc</c> dạng chuỗi vì đó chỉ cần so ngày, không cần tính tuần).</summary>
        public static long WeekKey(DateTime utcNow) => (long)(utcNow - DateTime.UnixEpoch).TotalDays / 7;

        public static void EnsureWeeklyReset(PlayerProfileDto profile, DateTime utcNow)
        {
            long key = WeekKey(utcNow);
            if (profile.TrialBoss.LastWeekKey == key) return;

            profile.TrialBoss.LastWeekKey = key;
            profile.TrialBoss.BestDamageThisWeek = 0;
            profile.TrialBoss.ClaimedTier = 0;
        }

        /// <summary>Ghi nhận 1 lượt đánh — chỉ giữ lại nếu CAO HƠN kỷ lục tuần này. Gọi cho MỌI
        /// kết quả trận (Victory/Defeat/Timeout đều tính) — Trial Boss đo output damage, không
        /// đo thắng/thua (Timeout đặc biệt hữu ích vì boss HP quá cao để hạ gục thật).</summary>
        public static void RecordAttempt(PlayerProfileDto profile, long damageDealt)
        {
            if (damageDealt > profile.TrialBoss.BestDamageThisWeek)
                profile.TrialBoss.BestDamageThisWeek = damageDealt;
        }

        /// <summary>task-reddot.md — peek KHÔNG mutate, dùng cho RedDotService (chỉ hiện chấm đỏ khi
        /// có thưởng thật đang chờ, không phải "có thể đánh boss").</summary>
        public static bool HasClaimable(PlayerProfileDto profile)
        {
            int highestEligible = 0;
            for (int i = 1; i <= TIERS.Length; i++)
                if (profile.TrialBoss.BestDamageThisWeek >= TIERS[i - 1].DamageThreshold) highestEligible = i;
            return highestEligible > profile.TrialBoss.ClaimedTier;
        }

        /// <summary>Nhận thưởng TẤT CẢ bậc đã đủ điều kiện nhưng chưa nhận (không chỉ bậc cao
        /// nhất — nhảy thẳng lên bậc 3 vẫn phải được cả thưởng bậc 1+2, giống thang mốc
        /// (milestone) thông thường). Trả false nếu không có bậc mới nào để nhận.</summary>
        public static bool TryClaimRewards(PlayerProfileDto profile, IEconomyService economy)
        {
            int highestEligible = 0;
            for (int i = 1; i <= TIERS.Length; i++)
                if (profile.TrialBoss.BestDamageThisWeek >= TIERS[i - 1].DamageThreshold) highestEligible = i;

            if (highestEligible <= profile.TrialBoss.ClaimedTier) return false;

            long totalGem = 0, totalShards = 0;
            for (int i = profile.TrialBoss.ClaimedTier + 1; i <= highestEligible; i++)
            {
                totalGem += TIERS[i - 1].Gem;
                totalShards += TIERS[i - 1].Shards;
            }

            economy.Grant(profile.Wallet, CurrencyType.Gem, totalGem);
            economy.GrantShards(profile.Wallet, FEATURED_HERO_DEF_ID, totalShards);
            profile.TrialBoss.ClaimedTier = highestEligible;
            return true;
        }
    }
}
