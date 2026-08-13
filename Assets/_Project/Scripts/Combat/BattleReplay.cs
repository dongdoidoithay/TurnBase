using System.Collections.Generic;

namespace Game.Combat
{
    /// <summary>
    /// Tái tạo 1 trận đang dở từ danh sách <see cref="ActionIntent"/> CỦA PHE PLAYER — edge case
    /// E17 (plan.md §4.14/§4.17). CHỈ cần intent của Player: enemy tự tái tạo GIỐNG HỆT qua AI +
    /// cùng seed (đã có bảo đảm determinism toàn hệ thống, xem <c>DeterminismTests</c>) — không
    /// cần lưu/replay intent enemy riêng, đúng tinh thần <c>ReplayData</c> ở plan.md §4.17.
    ///
    /// Gọi SAU KHI <see cref="CombatSimulation"/> đã <c>Start()</c> và dựng đủ unit (hero+enemy)
    /// GIỐNG HỆT trận gốc — cùng seed, cùng danh sách defId, cùng thứ tự <c>AddUnit</c>. Sai bất
    /// kỳ điều nào ở đây sẽ làm ATB tie-break / RNG lệch khỏi trận gốc, replay ra kết quả khác.
    /// </summary>
    public static class BattleReplay
    {
        /// <summary>Đưa <paramref name="sim"/> chạy qua đúng chuỗi <paramref name="playerIntents"/>
        /// đã ghi lại, dừng lại đúng chỗ người chơi rời trận (AwaitInput, sẵn sàng chơi tiếp).
        /// Trả về <c>false</c> nếu replay lệch khỏi kỳ vọng (actor đang chờ input không khớp
        /// intent tiếp theo trong log, hoặc trận kết thúc bất thường giữa chừng) — dấu hiệu
        /// snapshot không khớp trận hiện tại (VD đội hình đổi khác lúc lưu). Caller nên bỏ
        /// snapshot khi false, KHÔNG cố chơi tiếp từ trạng thái sai.</summary>
        public static bool ReplayPlayerIntents(CombatSimulation sim, IReadOnlyList<ActionIntent> playerIntents)
        {
            for (int i = 0; i < playerIntents.Count; i++)
            {
                bool waitingForInput = sim.Advance();
                if (!waitingForInput || sim.IsFinished) return false;

                var expected = playerIntents[i];
                if (sim.State.CurrentActorId != expected.ActorId) return false;

                sim.SubmitIntent(expected);
            }
            return true;
        }
    }
}
