using System;
using System.Collections.Generic;
using Game.Data;
using Game.Data.Dto;
using Game.Meta.Hero;
using Game.Services.Economy;

namespace Game.Meta.Endgame
{
    /// <summary>
    /// Dungeon hằng ngày — plan.md §8.3, task-endgame.md. 4 loại (<see cref="DungeonKind"/>
    /// Gold/Exp/Material/Stone — Tower/TrialBoss cùng enum nhưng KHÔNG xử lý ở đây, xem
    /// <see cref="TrialBossSystem"/>/ngoài phạm vi cho Tower). Mỗi loại chỉ mở đúng vài ngày/tuần
    /// (plan.md bảng §8.3), 10 tầng/loại, reset mỗi ngày UTC — cùng kỷ luật với
    /// <c>QuestSystem.EnsureDailyReset</c> (chuỗi "yyyy-MM-dd", không phải Dictionary vì
    /// JsonUtility không serialize được).
    /// </summary>
    public static class DungeonSystem
    {
        public const int MAX_FLOOR = 10;

        private static readonly Dictionary<DungeonKind, DayOfWeek[]> ACTIVE_DAYS = new()
        {
            [DungeonKind.Gold] = new[] { DayOfWeek.Monday, DayOfWeek.Thursday, DayOfWeek.Sunday },
            [DungeonKind.Exp] = new[] { DayOfWeek.Tuesday, DayOfWeek.Friday, DayOfWeek.Sunday },
            [DungeonKind.Material] = new[] { DayOfWeek.Wednesday, DayOfWeek.Saturday, DayOfWeek.Sunday },
            [DungeonKind.Stone] = new[] { DayOfWeek.Monday, DayOfWeek.Thursday, DayOfWeek.Saturday },
        };

        public static bool IsAvailableToday(DungeonKind kind, DateTime utcNow)
            => ACTIVE_DAYS.TryGetValue(kind, out var days) && Array.IndexOf(days, utcNow.DayOfWeek) >= 0;

        /// <summary>So <c>LastDailyResetUtc</c> (ISO ngày UTC) — khác ngày thì xoá hết tiến độ
        /// tầng hôm nay của cả 4 loại (đúng tinh thần "mỗi ngày làm lại từ tầng 1").</summary>
        public static void EnsureDailyReset(PlayerProfileDto profile, DateTime utcNow)
        {
            string today = utcNow.ToString("yyyy-MM-dd");
            if (profile.Dungeon.LastDailyResetUtc == today) return;

            profile.Dungeon.FloorClearedToday.Clear();
            profile.Dungeon.LastDailyResetUtc = today;
        }

        public static int FloorCleared(PlayerProfileDto profile, DungeonKind kind)
        {
            foreach (var e in profile.Dungeon.FloorClearedToday)
                if (e.Key == kind.ToString()) return (int)e.Value;
            return 0;
        }

        public static int NextFloor(PlayerProfileDto profile, DungeonKind kind)
            => FloorCleared(profile, kind) + 1;

        public static bool CanEnter(PlayerProfileDto profile, DungeonKind kind, DateTime utcNow)
            => IsAvailableToday(kind, utcNow) && FloorCleared(profile, kind) < MAX_FLOOR;

        /// <summary>Số địch + độ khó theo tầng — tái dùng logic Elite ("tougher") đã có ở
        /// <c>MetaSceneInstaller.PickEnemies</c>, không phát minh hệ scale mới.</summary>
        public static int EnemyCountForFloor(int floor) => floor <= 3 ? 3 : floor <= 7 ? 4 : 5;
        public static bool IsTougherFloor(int floor) => floor > 5;

        /// <summary>Cập nhật tầng đã qua hôm nay (chỉ tăng, không giảm) — gọi SAU KHI xác nhận
        /// thắng, trước <see cref="GrantFloorReward"/>.</summary>
        public static void MarkFloorCleared(PlayerProfileDto profile, DungeonKind kind, int floor)
        {
            var entry = FindOrAdd(profile.Dungeon.FloorClearedToday, kind.ToString());
            if (floor > entry.Value) entry.Value = floor;
        }

        /// <summary>Thưởng đúng 1 tầng vừa thắng. Exp: hồi trực tiếp cho TOÀN BỘ hero sở hữu —
        /// đúng cách <c>MetaSceneInstaller.ApplyPendingBattleResult</c> đã làm cho thưởng trận
        /// thường (không chỉ đội hình ra trận), giữ nhất quán hành vi giữa 2 nguồn EXP.
        /// Material: mở khoá dần Essence II (tầng ≥4)/III (tầng ≥8) — cùng tinh thần bậc thang
        /// task-ascend.md §8 đã dùng cho Boss reward.</summary>
        public static void GrantFloorReward(PlayerProfileDto profile, DungeonKind kind, int floor,
                                             IEconomyService economy)
        {
            switch (kind)
            {
                case DungeonKind.Gold:
                    economy.Grant(profile.Wallet, CurrencyType.Gold, 200L * floor);
                    break;
                case DungeonKind.Stone:
                    economy.Grant(profile.Wallet, CurrencyType.EnhanceStone, floor);
                    break;
                case DungeonKind.Material:
                    economy.Grant(profile.Wallet, CurrencyType.EssenceI, 2);
                    if (floor >= 4) economy.Grant(profile.Wallet, CurrencyType.EssenceII, 1);
                    if (floor >= 8) economy.Grant(profile.Wallet, CurrencyType.EssenceIII, 1);
                    break;
                case DungeonKind.Exp:
                    foreach (var h in profile.Heroes) HeroLevelSystem.AddExp(h, 150L * floor);
                    break;
            }
        }

        private static CurrencyEntryDto FindOrAdd(List<CurrencyEntryDto> list, string key)
        {
            foreach (var e in list)
                if (e.Key == key) return e;
            var fresh = new CurrencyEntryDto(key, 0);
            list.Add(fresh);
            return fresh;
        }
    }
}
