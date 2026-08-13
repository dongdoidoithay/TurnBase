using System;
using Game.Combat.Events;
using Game.Combat.Model;
using Game.Data;

namespace Game.CombatView.Tutorial
{
    /// <summary>5 bước dạy trong trận thật lần đầu chơi (plan.md Tuần 11, task-phase-5-gaps.md
    /// Phần B): chọn skill → Action Command → hệ khắc chế → Break → Ultimate.</summary>
    public enum TutorialStep
    {
        ChooseSkill,
        ActionCommand,
        Counter,
        Break,
        Ultimate,
        Done,
    }

    /// <summary>
    /// State machine thuần (không MonoBehaviour) — chỉ QUAN SÁT sự kiện/trạng thái trận đã có sẵn,
    /// không tự tính lại ngưỡng Break/Poise/element (đã có trong <c>Game.Combat</c> + test riêng).
    /// Đọc <see cref="CombatEventQueue.All"/> (không <c>TryDequeue</c>) để KHÔNG tranh event với
    /// <see cref="Game.CombatView.CombatPresenter"/> đang là bên tiêu thụ chính thức duy nhất của
    /// hàng đợi đó (object-map.md §3.3 "không MonoBehaviour nào khác gọi thẳng CombatSimulation" —
    /// cùng tinh thần, chỉ ĐỌC không MUTATE trạng thái đọc của presenter).
    /// </summary>
    public sealed class TutorialController
    {
        public TutorialStep Step { get; private set; } = TutorialStep.ChooseSkill;
        public bool IsDone => Step == TutorialStep.Done;

        public event Action<TutorialStep> OnStepChanged;

        private int _scannedEventCount;
        private bool _wasUltimateReady;

        /// <summary>Hook từ <c>BattleHudScreen.OnSkillChosen</c> — lần chọn skill đầu tiên đưa
        /// người chơi sang màn hình Action Command (do chính luồng game hiện có mở ra, tutorial chỉ
        /// quan sát để đổi bước chỉ dẫn).</summary>
        public void NotifySkillChosen()
        {
            if (Step == TutorialStep.ChooseSkill) Advance(TutorialStep.ActionCommand);
        }

        /// <summary>Hook từ <c>BattleSceneInstaller.OnCommandResolved</c> — cửa sổ Action Command
        /// vừa trả về 1 grade (Miss/Good/Perfect).</summary>
        public void NotifyCommandResolved(CommandGrade grade)
        {
            if (Step == TutorialStep.ActionCommand) Advance(TutorialStep.Counter);
        }

        /// <summary>Gọi mỗi frame (hoặc mỗi lần <c>CombatEventQueue</c> có sự kiện mới) từ
        /// <c>BattleSceneInstaller.Update()</c>. Không throw khi <paramref name="events"/>/
        /// <paramref name="state"/> null (an toàn gọi trước khi trận khởi tạo xong).</summary>
        public void Tick(CombatEventQueue events, BattleState state)
        {
            if (events != null) ScanEvents(events, state);
            if (state != null) CheckUltimateConsumed(state);
        }

        private void ScanEvents(CombatEventQueue events, BattleState state)
        {
            var all = events.All;
            for (int i = _scannedEventCount; i < all.Count; i++)
            {
                var e = all[i];

                if (Step == TutorialStep.Counter && e.Type == CombatEventType.DamageDealt &&
                    e.FloatValue > 1f && IsSide(state, e.SourceUnitId, TeamSide.Player))
                {
                    Advance(TutorialStep.Break);
                }
                else if (Step == TutorialStep.Break && e.Type == CombatEventType.PoiseBroken &&
                         IsSide(state, e.TargetUnitId, TeamSide.Enemy))
                {
                    Advance(TutorialStep.Ultimate);
                }
            }
            _scannedEventCount = all.Count;
        }

        /// <summary>Ultimate gauge dùng CHUNG cả đội Player (BattleState.UltimateGauge, plan.md
        /// §4.10) — không cần lọc theo unit. Phát hiện "vừa dùng" bằng cạnh xuống: đầy → 0 giữa 2
        /// lần Tick, thay vì thêm event mới (gauge đã đủ thông tin, tránh trùng lặp nguồn sự thật).</summary>
        private void CheckUltimateConsumed(BattleState state)
        {
            bool ready = state.IsUltimateReady;
            if (Step == TutorialStep.Ultimate && _wasUltimateReady && !ready)
                Advance(TutorialStep.Done);
            _wasUltimateReady = ready;
        }

        private static bool IsSide(BattleState state, int unitId, TeamSide side)
        {
            var unit = state?.GetUnit(unitId);
            return unit != null && unit.Side == side;
        }

        /// <summary>Nút "Bỏ qua" — nhảy thẳng Done, không set flag hoàn tất theo cách khác Done tự
        /// nhiên (caller đối xử Skip/Done giống hệt nhau khi lưu <c>TutorialCompleted</c>).</summary>
        public void Skip()
        {
            if (Step != TutorialStep.Done) Advance(TutorialStep.Done);
        }

        private void Advance(TutorialStep next)
        {
            Step = next;
            OnStepChanged?.Invoke(next);
        }
    }
}
