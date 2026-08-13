namespace Game.Core.Random
{
    /// <summary>
    /// Nguồn ngẫu nhiên duy nhất được phép dùng trong simulation.
    /// CẤM dùng UnityEngine.Random / System.Random ở Game.Combat — xem plan.md §4.17.
    /// </summary>
    public interface IRandomSource
    {
        ulong Seed { get; }

        /// <summary>Số lần đã roll — dùng để so sánh determinism.</summary>
        long CallCount { get; }

        /// <summary>[0, 1)</summary>
        float NextFloat();

        /// <summary>[min, max)</summary>
        float NextFloat(float min, float max);

        /// <summary>[0, maxExclusive)</summary>
        int NextInt(int maxExclusive);

        /// <summary>[minInclusive, maxExclusive)</summary>
        int NextInt(int minInclusive, int maxExclusive);

        /// <summary>True với xác suất <paramref name="chance"/> (0..1).</summary>
        bool Chance(float chance);

        /// <summary>Bản sao độc lập ở đúng trạng thái hiện tại — dùng cho IntentPredictor (peek không tiêu seed).</summary>
        IRandomSource Fork();
    }
}
