using System;
using Game.Data;
using Game.Data.Dto;
using Game.Services.Economy;

namespace Game.Meta.Endgame
{
    /// <summary>
    /// Arena PvP — task-arena.md, plan.md v1.1. Mùa 14 ngày, đối thủ là snapshot hero do RNG sinh
    /// (dự án không có backend/server thật, xem task-arena.md §1 — KHÔNG giả vờ có multiplayer
    /// thật). Cùng khuôn <see cref="TrialBossSystem"/>: class này CHỈ giữ logic reset/nhận thưởng
    /// thuần (test được), việc sinh đối thủ ngẫu nhiên (cần đọc catalog HeroDefinitionSO) nằm ở
    /// <see cref="MetaSceneInstaller.PickArenaOpponents"/> — đúng tách bạch pure/impure đã có
    /// (DungeonSystem/TowerSystem/TrialBossSystem không hề random-pick nội dung, luôn ở Meta layer
    /// qua <c>UnityEngine.Random</c>).
    /// </summary>
    public static class ArenaSystem
    {
        public const int OPPONENT_COUNT = 5;

        /// <summary>Số kỳ 14 ngày kể từ epoch — cùng kỹ thuật <see cref="TrialBossSystem.WeekKey"/>.</summary>
        public static long SeasonKey(DateTime utcNow) => (long)(utcNow - DateTime.UnixEpoch).TotalDays / 14;

        /// <summary>true = vừa đổi mùa, caller (MetaSceneInstaller) cần gọi
        /// <see cref="PopulateOpponents"/> ngay sau đó để sinh đối thủ mới. Không tự sinh ở đây vì
        /// class này không được đọc catalog Resources (giữ pure/test được).</summary>
        public static bool EnsureSeasonReset(PlayerProfileDto profile, DateTime utcNow)
        {
            long key = SeasonKey(utcNow);
            if (profile.Arena.LastSeasonKey == key) return false;

            profile.Arena.LastSeasonKey = key;
            profile.Arena.Opponents.Clear();
            return true;
        }

        public static void PopulateOpponents(PlayerProfileDto profile,
            System.Collections.Generic.IReadOnlyList<ArenaOpponentDto> opponents)
        {
            profile.Arena.Opponents.Clear();
            profile.Arena.Opponents.AddRange(opponents);
        }

        /// <summary>Thắng bậc <paramref name="index"/> — cấp Honor NẾU chưa nhận, tăng Rating theo
        /// bậc (bậc cao thắng được cộng Rating nhiều hơn — index 0-based, +50/bậc). Trả false nếu
        /// index không hợp lệ hoặc đã Claimed từ trước (không cấp lặp).</summary>
        public static bool TryClaim(PlayerProfileDto profile, int index, IEconomyService economy)
        {
            if (index < 0 || index >= profile.Arena.Opponents.Count) return false;
            var opp = profile.Arena.Opponents[index];
            if (opp.Claimed) return false;

            economy.Grant(profile.Wallet, CurrencyType.Honor, opp.HonorReward);
            opp.Claimed = true;
            profile.Arena.Rating += 50 * (index + 1);
            return true;
        }
    }
}
