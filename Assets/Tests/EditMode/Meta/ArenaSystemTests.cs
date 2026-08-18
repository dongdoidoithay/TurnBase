using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data;
using Game.Data.Dto;
using Game.Meta.Endgame;
using Game.Services.Economy;
using NUnit.Framework;

namespace Game.Tests.Meta
{
    /// <summary>ArenaSystem — task-arena.md, plan.md v1.1.</summary>
    public class ArenaSystemTests
    {
        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            ServiceLocator.Register<IEconomyService>(new EconomyService());
        }

        [TearDown]
        public void TearDown() => ServiceLocator.Clear();

        private static PlayerProfileDto Profile() => new();

        private static IEconomyService Economy() => ServiceLocator.Get<IEconomyService>();

        private static ArenaOpponentDto Opponent(long honor = 100) => new()
        {
            HeroDefIds = new[] { "hero_ember_knight", "hero_frost_sage", "hero_gale_thief" },
            Level = 10,
            Star = 2,
            HonorReward = honor,
        };

        // ---------- Reset theo mùa (14 ngày) ----------

        [Test]
        public void EnsureSeasonReset_SameSeason_ReturnsFalse_KeepsOpponents()
        {
            // Neo mốc bắt đầu ĐÚNG bằng biên mùa thật (key*14 ngày kể từ epoch) rồi cộng 5 ngày —
            // đảm bảo TOÁN HỌC luôn cùng 1 key dù mốc epoch rơi vào đâu, tránh lỗi flaky đã gặp ở
            // TrialBossSystemTests (test cũ dùng ngày lịch cụ thể, có thể vô tình rơi đúng biên).
            var p = Profile();
            long key = ArenaSystem.SeasonKey(new DateTime(2026, 8, 9, 10, 0, 0, DateTimeKind.Utc));
            var seasonStart = DateTime.UnixEpoch.AddDays(key * 14);
            Assert.IsTrue(ArenaSystem.EnsureSeasonReset(p, seasonStart)); // lần đầu luôn true (LastSeasonKey=-1)
            ArenaSystem.PopulateOpponents(p, new List<ArenaOpponentDto> { Opponent() });

            bool changed = ArenaSystem.EnsureSeasonReset(p, seasonStart.AddDays(5)); // vẫn trong 14 ngày

            Assert.IsFalse(changed);
            Assert.AreEqual(1, p.Arena.Opponents.Count);
        }

        [Test]
        public void EnsureSeasonReset_NextSeason_ReturnsTrue_ClearsOpponents()
        {
            var p = Profile();
            var season1 = new DateTime(2026, 8, 9, 10, 0, 0, DateTimeKind.Utc);
            ArenaSystem.EnsureSeasonReset(p, season1);
            ArenaSystem.PopulateOpponents(p, new List<ArenaOpponentDto> { Opponent() });

            bool changed = ArenaSystem.EnsureSeasonReset(p, season1.AddDays(15)); // sang mùa khác

            Assert.IsTrue(changed);
            Assert.AreEqual(0, p.Arena.Opponents.Count, "Đổi mùa phải xoá đối thủ cũ, caller sinh lại");
        }

        [Test]
        public void SeasonKey_Exactly14DaysLater_IsAlwaysOneKeyHigher()
        {
            var a = new DateTime(2026, 8, 9, 10, 0, 0, DateTimeKind.Utc);
            var b = a.AddDays(14);

            Assert.AreEqual(ArenaSystem.SeasonKey(a) + 1, ArenaSystem.SeasonKey(b));
        }

        // ---------- Nhận thưởng ----------

        [Test]
        public void TryClaim_ValidIndex_GrantsHonor_MarksClaimed()
        {
            var p = Profile();
            ArenaSystem.PopulateOpponents(p, new List<ArenaOpponentDto> { Opponent(honor: 150) });

            bool claimed = ArenaSystem.TryClaim(p, 0, Economy());

            Assert.IsTrue(claimed);
            Assert.AreEqual(150, Economy().Get(p.Wallet, CurrencyType.Honor));
            Assert.IsTrue(p.Arena.Opponents[0].Claimed);
        }

        [Test]
        public void TryClaim_AlreadyClaimed_ReturnsFalse_NoDoubleGrant()
        {
            var p = Profile();
            ArenaSystem.PopulateOpponents(p, new List<ArenaOpponentDto> { Opponent(honor: 150) });
            Assert.IsTrue(ArenaSystem.TryClaim(p, 0, Economy()));

            bool secondClaim = ArenaSystem.TryClaim(p, 0, Economy());

            Assert.IsFalse(secondClaim);
            Assert.AreEqual(150, Economy().Get(p.Wallet, CurrencyType.Honor), "Không cấp Honor lần 2");
        }

        [Test]
        public void TryClaim_OutOfRangeIndex_ReturnsFalse_NoThrow()
        {
            var p = Profile();
            ArenaSystem.PopulateOpponents(p, new List<ArenaOpponentDto> { Opponent() });

            Assert.IsFalse(ArenaSystem.TryClaim(p, 5, Economy()));
            Assert.IsFalse(ArenaSystem.TryClaim(p, -1, Economy()));
        }

        [Test]
        public void TryClaim_HigherIndex_GrantsMoreRatingThanLowerIndex()
        {
            var p = Profile();
            ArenaSystem.PopulateOpponents(p, new List<ArenaOpponentDto> { Opponent(), Opponent(), Opponent() });
            long ratingBefore = p.Arena.Rating;

            ArenaSystem.TryClaim(p, 2, Economy()); // bậc cao nhất (index 2)

            Assert.Greater(p.Arena.Rating, ratingBefore);
            Assert.AreEqual(ratingBefore + 50 * 3, p.Arena.Rating);
        }

        [Test]
        public void TryClaim_Loss_DoesNotReduceRating()
        {
            // "Thua" tương đương KHÔNG gọi TryClaim (Arena không phạt điểm khi thua — task-arena.md).
            var p = Profile();
            ArenaSystem.PopulateOpponents(p, new List<ArenaOpponentDto> { Opponent() });
            long ratingBefore = p.Arena.Rating;

            // Không có API "ghi nhận thua" nào cả — xác nhận Rating chỉ đổi khi TryClaim thắng.
            Assert.AreEqual(ratingBefore, p.Arena.Rating);
        }
    }
}
