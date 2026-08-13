using Game.Core.Random;
using Game.Data;
using Game.Data.Dto;
using Game.Meta.Gacha;
using NUnit.Framework;

namespace Game.Tests.Meta
{
    /// <summary>
    /// GachaSystem.RollRarity — plan.md §9.3 bắt buộc test tỉ lệ khớp ±0.05% trên 1 triệu roll.
    /// task-ascend.md §7 mục B.2.
    /// </summary>
    public class GachaPityTests
    {
        // plan.md §9.3 yêu cầu ±0.05% trên "1 triệu roll" — nhưng ở đúng 1M mẫu, sai số chuẩn
        // của bucket Epic (12%) đã ~0.033%, đủ để 1 seed xui rơi ngoài dung sai dù code đúng.
        // Tăng mẫu lên 5M để sai số chuẩn nhỏ hơn nhiều so với ±0.05%, seed cố định nên vẫn
        // 100% tái lập (không flaky giữa các lần chạy).
        private const int SAMPLE_SIZE = 5_000_000;
        private const float TOLERANCE = 0.0005f; // ±0.05%

        /// <summary>
        /// Mỗi roll dùng 1 GachaStateDto MỚI (PullsSinceLegendary/Epic luôn = 0 trước khi tăng) —
        /// cô lập tỉ lệ GỐC (chưa có soft/hard pity can thiệp) để so trực tiếp với bảng §9.3. Nếu
        /// dùng chung 1 state xuyên suốt 1 triệu roll thì soft/hard pity sẽ kéo tỉ lệ Legendary
        /// thực tế lên cao hơn hẳn 1.5% base (đúng thiết kế — pity NGHĨA LÀ tăng tỉ lệ theo thời
        /// gian) nên so sánh trực tiếp với bảng gốc sẽ luôn fail dù code đúng.
        /// </summary>
        [Test]
        public void RollRarity_OrganicRates_MatchTargetTable_Over1MRolls()
        {
            var rng = new XorShiftRandom(123456789UL);
            int legendary = 0, epic = 0, rare = 0, common = 0;

            for (int i = 0; i < SAMPLE_SIZE; i++)
            {
                var state = new GachaStateDto();
                switch (GachaSystem.RollRarity(state, rng))
                {
                    case Rarity.Legendary: legendary++; break;
                    case Rarity.Epic: epic++; break;
                    case Rarity.Rare: rare++; break;
                    default: common++; break;
                }
            }

            AssertRate("Legendary", legendary, 0.015f);
            AssertRate("Epic", epic, 0.120f);
            AssertRate("Rare", rare, 0.365f);
            AssertRate("Common", common, 0.500f);
        }

        private static void AssertRate(string label, int count, float target)
        {
            float actual = (float)count / SAMPLE_SIZE;
            Assert.That(actual, Is.EqualTo(target).Within(TOLERANCE),
                $"{label}: actual {actual:P3} vs target {target:P1} (±{TOLERANCE:P2})");
        }

        [Test]
        public void RollRarity_HardPity_ForcesLegendaryAtPull60_AndEpicEveryTenPullsBeforeThat()
        {
            // rng "xui xẻo" cố định — r luôn > mọi ngưỡng tổ chức (legRate tối đa 0.315 ở lần 59)
            // nên Legendary/Epic/Rare không bao giờ trúng tự nhiên, CHỈ trúng khi bị ép bởi pity.
            var rng = new AlwaysHighRandom();
            var state = new GachaStateDto();
            Rarity last = Rarity.Common;

            for (int i = 1; i <= 60; i++)
            {
                last = GachaSystem.RollRarity(state, rng);
                if (i % 10 == 0 && i != 60)
                    Assert.AreEqual(Rarity.Epic, last, $"Lần {i}: hard pity Epic (PullsSinceEpic=10) phải ép ra Epic");
                else if (i != 60)
                    Assert.AreEqual(Rarity.Common, last, $"Lần {i}: rng luôn cao, không pity thì phải ra Common");
            }

            Assert.AreEqual(Rarity.Legendary, last, "Lần 60: hard pity Legendary (PullsSinceLegendary=60) phải ép ra Legendary");
            Assert.AreEqual(0, state.PullsSinceLegendary, "Reset sau khi trúng Legendary");
            Assert.AreEqual(0, state.PullsSinceEpic, "Reset sau khi trúng Legendary (Legendary reset cả 2 counter)");
        }

        [Test]
        public void RollRarity_SoftPity_IncreasesLegendaryRate_From45thPull()
        {
            // Ngay dưới ngưỡng soft pity (lần 44): legRate vẫn = 1.5%. Từ lần 45: +2%/lần.
            var state = new GachaStateDto { PullsSinceLegendary = 44, PullsSinceEpic = 0 };
            // r kẹp ngay trên 1.5% nhưng dưới 3.5% (legRate ở lần 45) → chỉ trúng Legendary NẾU có soft pity.
            var rng = new FixedRandom(0.02f);

            var result = GachaSystem.RollRarity(state, rng);

            Assert.AreEqual(Rarity.Legendary, result,
                "Lần 45 (PullsSinceLegendary 44→45): legRate = 1.5%+2% = 3.5%, r=0.02 phải trúng nhờ soft pity");
        }

        /// <summary>IRandomSource giả — NextFloat() luôn trả về giá trị cố định, dùng để ép nhánh pity.</summary>
        private sealed class FixedRandom : IRandomSource
        {
            private readonly float _value;
            public FixedRandom(float value) { _value = value; }
            public ulong Seed => 0;
            public long CallCount { get; private set; }
            public float NextFloat() { CallCount++; return _value; }
            public float NextFloat(float min, float max) => min + _value * (max - min);
            public int NextInt(int maxExclusive) => 0;
            public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
            public bool Chance(float chance) => _value < chance;
            public IRandomSource Fork() => new FixedRandom(_value);
        }

        private sealed class AlwaysHighRandom : IRandomSource
        {
            public ulong Seed => 0;
            public long CallCount { get; private set; }
            public float NextFloat() { CallCount++; return 0.999f; }
            public float NextFloat(float min, float max) => max;
            public int NextInt(int maxExclusive) => 0;
            public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
            public bool Chance(float chance) => false;
            public IRandomSource Fork() => new AlwaysHighRandom();
        }
    }
}
