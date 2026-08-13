using System;

namespace Game.Core.Random
{
    /// <summary>
    /// RNG deterministic (xorshift64*), không phụ thuộc nền tảng.
    /// Cùng seed → cùng chuỗi số trên mọi máy, mọi phiên bản Unity.
    /// </summary>
    public sealed class XorShiftRandom : IRandomSource
    {
        private const float INV_UINT_MAX = 1f / 4294967296f; // 1 / 2^32

        private ulong _state;

        public ulong Seed { get; }
        public long CallCount { get; private set; }

        public XorShiftRandom(ulong seed)
        {
            // state 0 làm xorshift chết cứng → thay bằng hằng số vàng
            Seed = seed;
            _state = seed == 0UL ? 0x9E3779B97F4A7C15UL : seed;
        }

        private XorShiftRandom(ulong seed, ulong state, long callCount)
        {
            Seed = seed;
            _state = state;
            CallCount = callCount;
        }

        private ulong NextRaw()
        {
            CallCount++;
            ulong x = _state;
            x ^= x >> 12;
            x ^= x << 25;
            x ^= x >> 27;
            _state = x;
            return x * 0x2545F4914F6CDD1DUL;
        }

        public float NextFloat()
        {
            // Lấy 32 bit cao — phân bố đều hơn bit thấp
            uint bits = (uint)(NextRaw() >> 32);
            return bits * INV_UINT_MAX;
        }

        public float NextFloat(float min, float max)
        {
            if (max <= min) return min;
            return min + NextFloat() * (max - min);
        }

        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0) return 0;
            return (int)(((ulong)(uint)(NextRaw() >> 32) * (ulong)maxExclusive) >> 32);
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            return minInclusive + NextInt(maxExclusive - minInclusive);
        }

        public bool Chance(float chance)
        {
            if (chance <= 0f) { CallCount++; _ = NextRaw(); return false; }
            if (chance >= 1f) { CallCount++; _ = NextRaw(); return true; }
            return NextFloat() < chance;
        }

        public IRandomSource Fork() => new XorShiftRandom(Seed, _state, CallCount);

        public override string ToString() => $"XorShift(seed={Seed}, calls={CallCount})";

        /// <summary>Sinh seed từ chuỗi — dùng để tạo seed ổn định từ id stage/run.</summary>
        public static ulong SeedFromString(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0x9E3779B97F4A7C15UL;
            ulong hash = 14695981039346656037UL; // FNV-1a 64
            for (int i = 0; i < s.Length; i++)
            {
                hash ^= s[i];
                hash *= 1099511628211UL;
            }
            return hash;
        }
    }
}
