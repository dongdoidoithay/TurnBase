using Game.Combat.Systems;
using Game.Data;
using NUnit.Framework;

namespace Game.Tests.Combat
{
    /// <summary>
    /// Action Command — plan.md §4.8. Đây là USP số 1 và cũng là rủi ro R1,
    /// nên cửa sổ nhịp phải có test khoá số liệu.
    /// </summary>
    public class ActionCommandTests
    {
        // ---------- SingleTap ----------

        [Test]
        public void SingleTap_ExactTiming_IsPerfect()
        {
            var w = ActionCommandEvaluator.BuildWindow(ActionCommandType.SingleTap);
            Assert.AreEqual(CommandGrade.Perfect,
                ActionCommandEvaluator.Evaluate(w, ActionCommandEvaluator.SINGLE_TAP_TARGET_MS));
        }

        [Test]
        public void SingleTap_WithinPerfectWindow_IsPerfect()
        {
            var w = ActionCommandEvaluator.BuildWindow(ActionCommandType.SingleTap);
            int target = ActionCommandEvaluator.SINGLE_TAP_TARGET_MS;

            Assert.AreEqual(CommandGrade.Perfect, ActionCommandEvaluator.Evaluate(w, target - 79));
            Assert.AreEqual(CommandGrade.Perfect, ActionCommandEvaluator.Evaluate(w, target + 79));
        }

        [Test]
        public void SingleTap_JustOutsidePerfect_IsGood()
        {
            var w = ActionCommandEvaluator.BuildWindow(ActionCommandType.SingleTap);
            int target = ActionCommandEvaluator.SINGLE_TAP_TARGET_MS;

            Assert.AreEqual(CommandGrade.Good, ActionCommandEvaluator.Evaluate(w, target - 120));
            Assert.AreEqual(CommandGrade.Good, ActionCommandEvaluator.Evaluate(w, target + 120));
        }

        [Test]
        public void SingleTap_FarOff_IsMiss()
        {
            var w = ActionCommandEvaluator.BuildWindow(ActionCommandType.SingleTap);
            int target = ActionCommandEvaluator.SINGLE_TAP_TARGET_MS;

            Assert.AreEqual(CommandGrade.Miss, ActionCommandEvaluator.Evaluate(w, target - 400));
            Assert.AreEqual(CommandGrade.Miss, ActionCommandEvaluator.Evaluate(w, target + 400));
        }

        [Test]
        public void InputBuffer_AcceptsSlightlyEarlyPress()
        {
            var w = ActionCommandEvaluator.BuildWindow(ActionCommandType.Guard);
            // Guard target = 0, bấm sớm 50ms vẫn trong buffer 100ms → không bị Miss vì "quá sớm"
            Assert.AreNotEqual(CommandGrade.Miss, ActionCommandEvaluator.Evaluate(w, -50));
        }

        [Test]
        public void PressBeforeBuffer_IsMiss()
        {
            var w = ActionCommandEvaluator.BuildWindow(ActionCommandType.SingleTap);
            Assert.AreEqual(CommandGrade.Miss, ActionCommandEvaluator.Evaluate(w, -300));
        }

        // ---------- Mobile ----------

        [Test]
        public void MobileWindow_IsWiderThanDesktop()
        {
            var pc = ActionCommandEvaluator.BuildWindow(ActionCommandType.SingleTap, isMobile: false);
            var mobile = ActionCommandEvaluator.BuildWindow(ActionCommandType.SingleTap, isMobile: true);

            Assert.Greater(mobile.PerfectToleranceMs, pc.PerfectToleranceMs,
                           "Mobile phải được nới cửa sổ để bù độ trễ chạm (R1)");
            Assert.AreEqual(ActionCommandEvaluator.MOBILE_TOLERANCE_BONUS_MS,
                            mobile.PerfectToleranceMs - pc.PerfectToleranceMs);
        }

        [Test]
        public void MobilePress_ThatWouldMissOnPc_CanStillBePerfect()
        {
            int target = ActionCommandEvaluator.SINGLE_TAP_TARGET_MS;
            int press = target + 90;   // ngoài 80ms của PC, trong 100ms của mobile

            var pc = ActionCommandEvaluator.BuildWindow(ActionCommandType.SingleTap, isMobile: false);
            var mobile = ActionCommandEvaluator.BuildWindow(ActionCommandType.SingleTap, isMobile: true);

            Assert.AreEqual(CommandGrade.Good, ActionCommandEvaluator.Evaluate(pc, press));
            Assert.AreEqual(CommandGrade.Perfect, ActionCommandEvaluator.Evaluate(mobile, press));
        }

        // ---------- Bù độ trễ ----------

        [Test]
        public void LatencyCompensation_SubtractsPlatformAndUserOffset()
        {
            // Người chơi bấm ở 1000ms, máy trễ 60ms, người chơi quen bấm sớm 35ms
            int effective = ActionCommandEvaluator.ApplyLatencyCompensation(1000, 60, -35);
            Assert.AreEqual(975, effective);
        }

        [Test]
        public void LatencyCompensation_TurnsLateMissIntoPerfect()
        {
            var w = ActionCommandEvaluator.BuildWindow(ActionCommandType.SingleTap);
            int target = ActionCommandEvaluator.SINGLE_TAP_TARGET_MS;

            int raw = target + 200;   // trên máy trễ 200ms thì đây là Miss
            Assert.AreEqual(CommandGrade.Miss, ActionCommandEvaluator.Evaluate(w, raw));

            int compensated = ActionCommandEvaluator.ApplyLatencyCompensation(raw, 200, 0);
            Assert.AreEqual(CommandGrade.Perfect, ActionCommandEvaluator.Evaluate(w, compensated));
        }

        // ---------- Charge ----------

        [Test]
        public void Charge_InPerfectZone_IsPerfect()
        {
            Assert.AreEqual(CommandGrade.Perfect, ActionCommandEvaluator.EvaluateCharge(0.95f));
            Assert.AreEqual(CommandGrade.Perfect, ActionCommandEvaluator.EvaluateCharge(1.00f));
        }

        [Test]
        public void Charge_InGoodZone_IsGood()
        {
            Assert.AreEqual(CommandGrade.Good, ActionCommandEvaluator.EvaluateCharge(0.85f));
        }

        [Test]
        public void Charge_ReleasedTooEarly_IsMiss()
        {
            Assert.AreEqual(CommandGrade.Miss, ActionCommandEvaluator.EvaluateCharge(0.4f));
        }

        [Test]
        public void Charge_HeldTooLong_IsMiss()
        {
            Assert.AreEqual(CommandGrade.Miss, ActionCommandEvaluator.EvaluateCharge(1.2f),
                            "Giữ quá tay phải bị phạt, nếu không ai cũng giữ mãi");
        }

        // ---------- Combo ----------

        [Test]
        public void Combo_AllPerfect_GivesBonusMultiplier()
        {
            var beats = new[] { CommandGrade.Perfect, CommandGrade.Perfect,
                                CommandGrade.Perfect, CommandGrade.Perfect };
            var (grade, bonus) = ActionCommandEvaluator.EvaluateCombo(beats);

            Assert.AreEqual(CommandGrade.Perfect, grade);
            Assert.AreEqual(ActionCommandEvaluator.ALL_PERFECT_BONUS, bonus, 0.001f);
        }

        [Test]
        public void Combo_MostlyPerfect_IsPerfectWithoutBonus()
        {
            var beats = new[] { CommandGrade.Perfect, CommandGrade.Perfect,
                                CommandGrade.Perfect, CommandGrade.Good };
            var (grade, bonus) = ActionCommandEvaluator.EvaluateCombo(beats);

            Assert.AreEqual(CommandGrade.Perfect, grade);
            Assert.AreEqual(1f, bonus, 0.001f, "Chỉ ALL Perfect mới được bonus");
        }

        [Test]
        public void Combo_MostlyMiss_IsMiss()
        {
            var beats = new[] { CommandGrade.Miss, CommandGrade.Miss,
                                CommandGrade.Miss, CommandGrade.Good };
            var (grade, _) = ActionCommandEvaluator.EvaluateCombo(beats);
            Assert.AreEqual(CommandGrade.Miss, grade);
        }

        [Test]
        public void Combo_EmptyOrNull_DefaultsToGood()
        {
            Assert.AreEqual(CommandGrade.Good, ActionCommandEvaluator.EvaluateCombo(null).grade);
            Assert.AreEqual(CommandGrade.Good,
                            ActionCommandEvaluator.EvaluateCombo(new CommandGrade[0]).grade);
        }

        [Test]
        public void ComboWindow_ScalesDurationWithBeats()
        {
            var w2 = ActionCommandEvaluator.BuildWindow(ActionCommandType.Combo, beats: 2);
            var w5 = ActionCommandEvaluator.BuildWindow(ActionCommandType.Combo, beats: 5);

            Assert.Greater(w5.DurationMs, w2.DurationMs);
            Assert.AreEqual(5, w5.Beats);
        }

        // ---------- Accessibility ----------

        [Test]
        public void AutoGrade_IsGood_SoAutoBattleIsNotUseless()
        {
            Assert.AreEqual(CommandGrade.Good, ActionCommandEvaluator.AutoGrade);
        }

        [Test]
        public void NoneType_AlwaysGood_ForAccessibilityToggle()
        {
            var w = ActionCommandEvaluator.BuildWindow(ActionCommandType.None);
            Assert.AreEqual(CommandGrade.Good, ActionCommandEvaluator.Evaluate(w, 0));
            Assert.AreEqual(CommandGrade.Good, ActionCommandEvaluator.Evaluate(w, 99999),
                            "Tắt Action Command thì KHÔNG bị phạt (plan.md §4.8.4)");
        }

        // ---------- Kết nối với công thức sát thương ----------

        [Test]
        public void GradeMultipliers_MatchDamageCalculator()
        {
            Assert.AreEqual(1.30f, DamageCalculator.GradeMultiplier(CommandGrade.Perfect), 0.001f);
            Assert.AreEqual(1.10f, DamageCalculator.GradeMultiplier(CommandGrade.Good), 0.001f);
            Assert.AreEqual(0.80f, DamageCalculator.GradeMultiplier(CommandGrade.Miss), 0.001f);
        }

        [Test]
        public void PlayingByHand_BeatsAutoBattle()
        {
            // Lý do tồn tại của cả tính năng: chơi tay phải hơn auto rõ rệt
            float auto = DamageCalculator.GradeMultiplier(ActionCommandEvaluator.AutoGrade);
            float perfect = DamageCalculator.GradeMultiplier(CommandGrade.Perfect);

            Assert.Greater(perfect / auto, 1.15f,
                           "Perfect phải hơn Auto ít nhất 15% thì người chơi mới có động lực");
        }

        // ---------- Edge case E23 (plan.md §4.14) ----------

        /// <summary>Edge case E23: đổi tốc độ game (×1/×2/×3) giữa Action Command không được ảnh
        /// hưởng cửa sổ nhịp. `ActionCommandEvaluator` không nhận tham số tốc độ nào (đúng luật
        /// plan.md §4.17 cấm Time.*/tốc độ trong Game.Combat) — cửa sổ chỉ phụ thuộc mốc ms thô
        /// (`pressMs`) nên bất biến "do thiết kế", không phải luật được enforce chủ động. Test này
        /// khẳng định lại: cùng input ms → cùng kết quả, gọi lặp lại nhiều lần không trôi.</summary>
        [Test]
        public void E23_Evaluate_SameInputMs_AlwaysProducesSameGrade_RegardlessOfCallCount()
        {
            var w = ActionCommandEvaluator.BuildWindow(ActionCommandType.SingleTap);
            int pressMs = ActionCommandEvaluator.SINGLE_TAP_TARGET_MS - 50;

            var first = ActionCommandEvaluator.Evaluate(w, pressMs);
            for (int i = 0; i < 50; i++)
            {
                var repeat = ActionCommandEvaluator.Evaluate(w, pressMs);
                Assert.AreEqual(first, repeat,
                    "Evaluate không có state ẩn/phụ thuộc thời gian thực — cùng input phải luôn ra cùng kết quả");
            }
        }
    }
}
