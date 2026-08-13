using Game.Data;
using Game.Data.Dto;

namespace Game.Meta.Hero
{
    /// <summary>Cộng EXP, lên cấp, cap theo sao — plan.md §5.3/§5.4.</summary>
    public static class HeroLevelSystem
    {
        /// <summary>EXP tích luỹ cần có để ĐANG ở cấp n — plan.md §5.3: EXP(n) = round(40·n^1.85).</summary>
        public static long ExpToReachLevel(int level)
            => (long)System.Math.Round(40d * System.Math.Pow(level, 1.85));

        /// <summary>Trần cấp theo sao hiện tại — plan.md §5.4 (muốn lên tiếp phải Ascend).</summary>
        public static int LevelCap(int star) => star switch
        {
            <= 3 => 40,
            4 => 50,
            5 => 55,
            _ => 60
        };

        /// <summary>Cộng EXP và lên cấp tới khi hết EXP hoặc chạm trần sao. Trả về số cấp lên được
        /// (0 nếu không đủ EXP hoặc đã chạm trần) — dùng để hiện thông báo lên cấp.</summary>
        public static int AddExp(HeroInstanceDto hero, long exp)
        {
            if (exp <= 0) return 0;
            hero.Exp += exp;

            int cap = LevelCap(hero.Star);
            int gained = 0;
            while (hero.Level < cap && hero.Exp >= ExpToReachLevel(hero.Level + 1))
            {
                hero.Level++;
                gained++;
            }
            return gained;
        }

        /// <summary>+10%/cấp lên chỉ số gốc — xấp xỉ dải 8–12%/level ở plan.md §5.2. Chưa có field
        /// GrowthPerLevel riêng theo hero trong CSV nên dùng tỉ lệ cố định trên BasePrimary.</summary>
        public static PrimaryStats EffectivePrimary(PrimaryStats basePrimary, int level)
            => basePrimary * (1f + (level - 1) * 0.10f);
    }
}
