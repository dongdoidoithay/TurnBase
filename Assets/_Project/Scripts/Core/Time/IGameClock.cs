using System;

namespace Game.Core.Times
{
    /// <summary>
    /// Nguồn thời gian duy nhất cho logic phụ thuộc ngày/giờ (Energy, daily reset, dungeon theo thứ).
    /// v1 dùng giờ hệ thống; v2 thay bằng server time để chống đổi giờ máy — plan.md §11.7.
    /// </summary>
    public interface IGameClock
    {
        DateTime UtcNow { get; }
        /// <summary>Giờ địa phương của game (UTC+7) — dùng cho reset hằng ngày.</summary>
        DateTime GameLocalNow { get; }
        /// <summary>Mốc reset gần nhất trước thời điểm hiện tại (00:00 giờ game).</summary>
        DateTime LastDailyResetUtc { get; }
        DayOfWeek GameDayOfWeek { get; }
    }

    public sealed class SystemGameClock : IGameClock
    {
        public const int GAME_TIMEZONE_OFFSET_HOURS = 7; // UTC+7
        public const int DAILY_RESET_HOUR = 0;

        public DateTime UtcNow => DateTime.UtcNow;

        public DateTime GameLocalNow => UtcNow.AddHours(GAME_TIMEZONE_OFFSET_HOURS);

        public DateTime LastDailyResetUtc
        {
            get
            {
                var local = GameLocalNow;
                var resetLocal = new DateTime(local.Year, local.Month, local.Day,
                                              DAILY_RESET_HOUR, 0, 0, DateTimeKind.Unspecified);
                if (local < resetLocal) resetLocal = resetLocal.AddDays(-1);
                return resetLocal.AddHours(-GAME_TIMEZONE_OFFSET_HOURS);
            }
        }

        public DayOfWeek GameDayOfWeek => GameLocalNow.DayOfWeek;
    }
}
