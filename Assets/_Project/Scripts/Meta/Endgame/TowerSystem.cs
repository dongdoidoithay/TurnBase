using System;
using System.Collections.Generic;
using Game.Core.Random;
using Game.Data;
using Game.Data.Dto;
using Game.Meta.Equipment;
using Game.Services.Economy;

namespace Game.Meta.Endgame
{
    /// <summary>
    /// Tháp Vô Tận — plan.md §8.3, task-endgame.md. 100 tầng, mỗi LƯỢT leo bắt đầu lại từ tầng 1
    /// với HP đầy (đúng cách mọi trận khác luôn full HP lúc bắt đầu) nhưng HP KHÔNG hồi giữa các
    /// tầng TRONG CÙNG 1 lượt (<see cref="Game.CombatView.BattleSceneInstaller"/> nối nhiều đợt
    /// địch vào 1 <c>CombatSimulation</c> duy nhất — xem <c>TryAdvanceTowerWave</c>). Xếp hạng
    /// CỤC BỘ theo tầng cao nhất đạt được trong tuần (offline v1, giống <see cref="TrialBossSystem"/>).
    /// </summary>
    public static class TowerSystem
    {
        public const int MAX_FLOOR = 100;

        public readonly struct RewardTier
        {
            public readonly int FloorThreshold;
            public readonly long Gem;
            public readonly long Core;
            /// <summary>Chỉ bậc cuối (tầng 100) mới có trang bị Mythic — true/false thay vì số
            /// lượng vì mỗi bậc chỉ phát tối đa 1 item.</summary>
            public readonly bool MythicEquipment;

            public RewardTier(int floorThreshold, long gem, long core, bool mythicEquipment = false)
            {
                FloorThreshold = floorThreshold; Gem = gem; Core = core; MythicEquipment = mythicEquipment;
            }
        }

        /// <summary>Số liệu placeholder (chưa qua Balance Harness) — cùng tinh thần các bảng
        /// thưởng khác trong dự án (task-ascend.md §9, TrialBossSystem.TIERS).</summary>
        private static readonly RewardTier[] TIERS =
        {
            new(10, 200, 0),
            new(25, 500, 2),
            new(50, 1_000, 5),
            new(75, 2_000, 10),
            new(MAX_FLOOR, 5_000, 20, mythicEquipment: true),
        };

        public static IReadOnlyList<RewardTier> Tiers => TIERS;

        public static long WeekKey(DateTime utcNow) => TrialBossSystem.WeekKey(utcNow);

        public static void EnsureWeeklyReset(PlayerProfileDto profile, DateTime utcNow)
        {
            long key = WeekKey(utcNow);
            if (profile.Tower.LastWeekKey == key) return;

            profile.Tower.LastWeekKey = key;
            profile.Tower.BestFloorThisWeek = 0;
            profile.Tower.ClaimedTier = 0;
        }

        /// <summary>Ghi nhận 1 lượt leo vừa kết thúc (Defeat/Escaped/Timeout/Victory-hết-100-tầng
        /// đều tính — xem task-endgame.md, giống Trial Boss không phân biệt thắng/thua). Cập nhật
        /// CẢ 2: <see cref="TowerProgressDto.BestFloorThisWeek"/> (reset hằng tuần, dùng tính bậc
        /// thưởng) VÀ <see cref="ProgressDto.TowerFloor"/> (mốc mọi thời đại, KHÔNG reset).</summary>
        public static void RecordClimb(PlayerProfileDto profile, int floorReached)
        {
            if (floorReached > profile.Tower.BestFloorThisWeek)
                profile.Tower.BestFloorThisWeek = floorReached;
            if (floorReached > profile.Progress.TowerFloor)
                profile.Progress.TowerFloor = floorReached;
        }

        /// <summary>Nhận thưởng TẤT CẢ bậc đã đủ điều kiện nhưng chưa nhận (cộng dồn, giống
        /// <see cref="TrialBossSystem.TryClaimRewards"/>). <paramref name="lootRng"/> chỉ dùng khi
        /// bậc cuối (Mythic equipment) được nhận — an toàn truyền RNG không xác định (Meta layer
        /// chưa theo kỷ luật RNG như Game.Combat, xem <c>MetaSceneInstaller._lootRng</c>).</summary>
        public static bool TryClaimRewards(PlayerProfileDto profile, IEconomyService economy, IRandomSource lootRng)
        {
            int highestEligible = 0;
            for (int i = 1; i <= TIERS.Length; i++)
                if (profile.Tower.BestFloorThisWeek >= TIERS[i - 1].FloorThreshold) highestEligible = i;

            if (highestEligible <= profile.Tower.ClaimedTier) return false;

            long totalGem = 0, totalCore = 0;
            bool grantMythic = false;
            for (int i = profile.Tower.ClaimedTier + 1; i <= highestEligible; i++)
            {
                totalGem += TIERS[i - 1].Gem;
                totalCore += TIERS[i - 1].Core;
                if (TIERS[i - 1].MythicEquipment) grantMythic = true;
            }

            economy.Grant(profile.Wallet, CurrencyType.Gem, totalGem);
            if (totalCore > 0) economy.Grant(profile.Wallet, CurrencyType.Core, totalCore);
            if (grantMythic)
            {
                var item = EquipmentGenerator.Roll(null, Rarity.Mythic, lootRng);
                if (item != null) profile.Equipment.Add(item);
            }

            profile.Tower.ClaimedTier = highestEligible;
            return true;
        }

        // ---------- Độ khó theo tầng ----------

        public static int EnemyCountForFloor(int floor) => floor <= 20 ? 3 : floor <= 60 ? 4 : 5;

        public static bool IsTougherFloor(int floor) => floor > 40;
    }
}
