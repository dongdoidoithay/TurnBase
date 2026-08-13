using System;

namespace Game.Core.Maths
{
    /// <summary>
    /// Toán học dùng trong simulation — KHÔNG phụ thuộc UnityEngine.Mathf
    /// để Game.Combat chạy được headless và deterministic (plan.md §11.1).
    /// </summary>
    public static class GameMath
    {
        public static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
        public static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);
        public static float Clamp01(float v) => Clamp(v, 0f, 1f);

        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float InverseLerp(float a, float b, float v)
            => Math.Abs(b - a) < 1e-6f ? 0f : Clamp01((v - a) / (b - a));

        public static int Max(int a, int b) => a > b ? a : b;
        public static int Min(int a, int b) => a < b ? a : b;
        public static float Max(float a, float b) => a > b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;

        public static int FloorToInt(float v) => (int)Math.Floor(v);
        public static int RoundToInt(float v) => (int)Math.Round(v, MidpointRounding.AwayFromZero);
        public static int CeilToInt(float v) => (int)Math.Ceiling(v);

        public static float Abs(float v) => v < 0f ? -v : v;
        public static int Abs(int v) => v < 0 ? -v : v;

        /// <summary>Phần trăm an toàn: trả về 0 khi mẫu = 0.</summary>
        public static float SafeRatio(float numerator, float denominator)
            => Math.Abs(denominator) < 1e-6f ? 0f : numerator / denominator;

        /// <summary>Đường cong bão hòa dùng cho giảm sát thương theo giáp (plan.md §4.6 bước 4).</summary>
        public static float ArmorMitigation(float defense, float constant = 100f)
            => constant / (constant + Max(0f, defense));
    }
}
