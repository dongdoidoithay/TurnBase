using Game.Core.Maths;
using Game.Data;

namespace Game.Combat.Systems
{
    /// <summary>Cấu hình cửa sổ nhịp cho một lần bấm — plan.md §4.8.1.</summary>
    public readonly struct CommandWindow
    {
        public readonly ActionCommandType Type;
        /// <summary>Thời điểm lý tưởng tính từ lúc mở cửa sổ (ms).</summary>
        public readonly int TargetMs;
        public readonly int PerfectToleranceMs;
        public readonly int GoodToleranceMs;
        /// <summary>Tổng thời lượng cửa sổ (ms) — quá hạn tính là Miss.</summary>
        public readonly int DurationMs;
        public readonly int Beats;

        public CommandWindow(ActionCommandType type, int targetMs, int perfectMs,
                             int goodMs, int durationMs, int beats = 1)
        {
            Type = type; TargetMs = targetMs;
            PerfectToleranceMs = perfectMs; GoodToleranceMs = goodMs;
            DurationMs = durationMs; Beats = beats;
        }
    }

    /// <summary>
    /// Chấm điểm Action Command — plan.md §4.8. THUẦN LOGIC, không Unity API,
    /// nên test được và chạy được headless (quan trọng vì đây là tính năng rủi ro nhất, R1).
    /// UI chỉ việc gọi Evaluate() với thời điểm bấm.
    /// </summary>
    public static class ActionCommandEvaluator
    {
        // Thông số theo bảng plan.md §4.8.1
        public const int SINGLE_TAP_TARGET_MS = 900;
        public const int SINGLE_TAP_PERFECT_MS = 80;
        public const int SINGLE_TAP_GOOD_MS = 180;

        public const int COMBO_BEAT_INTERVAL_MS = 500;
        public const int COMBO_PERFECT_MS = 90;
        public const int COMBO_GOOD_MS = 200;

        public const int CHARGE_DURATION_MS = 1400;
        public const float CHARGE_PERFECT_MIN = 0.92f;
        public const float CHARGE_GOOD_MIN = 0.80f;

        public const int GUARD_PERFECT_MS = 100;
        public const int GUARD_GOOD_MS = 220;

        /// <summary>Nới cửa sổ trên mobile để bù độ trễ chạm (plan.md §4.8.3).</summary>
        public const int MOBILE_TOLERANCE_BONUS_MS = 20;

        /// <summary>Nhấn sớm trước khi cửa sổ mở vẫn được ghi nhận.</summary>
        public const int INPUT_BUFFER_MS = 100;

        /// <summary>Bonus khi đủ n nhịp Perfect liên tiếp trong combo.</summary>
        public const float ALL_PERFECT_BONUS = 1.15f;

        public static CommandWindow BuildWindow(ActionCommandType type, int beats = 1, bool isMobile = false)
        {
            int bonus = isMobile ? MOBILE_TOLERANCE_BONUS_MS : 0;

            return type switch
            {
                ActionCommandType.Combo => new CommandWindow(
                    type, COMBO_BEAT_INTERVAL_MS,
                    COMBO_PERFECT_MS + bonus, COMBO_GOOD_MS + bonus,
                    COMBO_BEAT_INTERVAL_MS * GameMath.Max(1, beats) + 300, beats),

                ActionCommandType.Charge => new CommandWindow(
                    type, CHARGE_DURATION_MS, 0, 0, CHARGE_DURATION_MS + 400),

                ActionCommandType.Guard => new CommandWindow(
                    type, 0,
                    GUARD_PERFECT_MS + bonus, GUARD_GOOD_MS + bonus,
                    GUARD_GOOD_MS * 2 + bonus),

                ActionCommandType.SingleTap => new CommandWindow(
                    type, SINGLE_TAP_TARGET_MS,
                    SINGLE_TAP_PERFECT_MS + bonus, SINGLE_TAP_GOOD_MS + bonus,
                    SINGLE_TAP_TARGET_MS + SINGLE_TAP_GOOD_MS + 200),

                _ => new CommandWindow(ActionCommandType.None, 0, 0, 0, 0)
            };
        }

        /// <summary>
        /// Chấm một lần bấm. <paramref name="pressMs"/> tính từ lúc mở cửa sổ,
        /// ĐÃ trừ độ trễ nền tảng và offset hiệu chỉnh của người chơi.
        /// </summary>
        public static CommandGrade Evaluate(in CommandWindow window, int pressMs)
        {
            if (window.Type == ActionCommandType.None) return CommandGrade.Good;

            // Nhấn quá sớm ngoài buffer → Miss
            if (pressMs < -INPUT_BUFFER_MS) return CommandGrade.Miss;

            int delta = GameMath.Abs(pressMs - window.TargetMs);

            if (delta <= window.PerfectToleranceMs) return CommandGrade.Perfect;
            if (delta <= window.GoodToleranceMs) return CommandGrade.Good;
            return CommandGrade.Miss;
        }

        /// <summary>
        /// Chấm kiểu Charge: người chơi giữ rồi nhả, <paramref name="chargeRatio"/> = 0..1+.
        /// Nhả quá tay (&gt;1) bị phạt như nhả quá sớm.
        /// </summary>
        public static CommandGrade EvaluateCharge(float chargeRatio)
        {
            if (chargeRatio > 1.0f) return CommandGrade.Miss;     // giữ quá lâu, cháy
            if (chargeRatio >= CHARGE_PERFECT_MIN) return CommandGrade.Perfect;
            if (chargeRatio >= CHARGE_GOOD_MIN) return CommandGrade.Good;
            return CommandGrade.Miss;
        }

        /// <summary>
        /// Gộp kết quả nhiều nhịp của combo thành một grade + hệ số bonus.
        /// Đủ n Perfect liên tiếp → thêm ×1.15 lên tổng sát thương.
        /// </summary>
        public static (CommandGrade grade, float bonusMultiplier) EvaluateCombo(CommandGrade[] beats)
        {
            if (beats == null || beats.Length == 0) return (CommandGrade.Good, 1f);

            int perfect = 0, miss = 0;
            for (int i = 0; i < beats.Length; i++)
            {
                if (beats[i] == CommandGrade.Perfect) perfect++;
                else if (beats[i] == CommandGrade.Miss) miss++;
            }

            if (perfect == beats.Length) return (CommandGrade.Perfect, ALL_PERFECT_BONUS);
            if (perfect > beats.Length / 2) return (CommandGrade.Perfect, 1f);
            if (miss > beats.Length / 2) return (CommandGrade.Miss, 1f);
            return (CommandGrade.Good, 1f);
        }

        /// <summary>
        /// Thời điểm bấm hiệu dụng sau khi bù độ trễ — plan.md §4.8.3.
        /// effectiveInputTime = rawInputTime − platformLatency − userCalibration
        /// </summary>
        public static int ApplyLatencyCompensation(int rawPressMs, int platformLatencyMs, int userOffsetMs)
            => rawPressMs - platformLatencyMs - userOffsetMs;

        /// <summary>Auto-battle và chế độ tắt Action Command đều tính là Good — không bị phạt.</summary>
        public static CommandGrade AutoGrade => CommandGrade.Good;
    }
}
