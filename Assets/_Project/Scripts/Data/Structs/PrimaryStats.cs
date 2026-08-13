using System;
using Game.Core.Maths;

namespace Game.Data
{
    /// <summary>
    /// 6 chỉ số gốc — đúng bộ hiển thị trong image_UI.jpg (STR/CON/INT/DEX/AUR/LUK).
    /// Công thức dẫn xuất ở plan.md §4.5.
    /// </summary>
    [Serializable]
    public struct PrimaryStats
    {
        public float Str;
        public float Con;
        public float Int;
        public float Dex;
        public float Aur;
        public float Luk;

        public PrimaryStats(float str, float con, float @int, float dex, float aur, float luk)
        {
            Str = str; Con = con; Int = @int; Dex = dex; Aur = aur; Luk = luk;
        }

        public float Get(StatType t) => t switch
        {
            StatType.Str => Str,
            StatType.Con => Con,
            StatType.Int => Int,
            StatType.Dex => Dex,
            StatType.Aur => Aur,
            StatType.Luk => Luk,
            _ => 0f
        };

        public static PrimaryStats operator +(PrimaryStats a, PrimaryStats b) => new(
            a.Str + b.Str, a.Con + b.Con, a.Int + b.Int, a.Dex + b.Dex, a.Aur + b.Aur, a.Luk + b.Luk);

        public static PrimaryStats operator *(PrimaryStats a, float m) => new(
            a.Str * m, a.Con * m, a.Int * m, a.Dex * m, a.Aur * m, a.Luk * m);

        public override string ToString()
            => $"STR{Str:0} CON{Con:0} INT{Int:0} DEX{Dex:0} AUR{Aur:0} LUK{Luk:0}";
    }

    /// <summary>
    /// Chỉ số dẫn xuất đầy đủ (plan.md §4.5). Được tính lại mỗi khi stat gốc/buff/trang bị đổi.
    /// </summary>
    [Serializable]
    public struct DerivedStats
    {
        public int MaxHp;
        public int MaxSp;
        public float AtkPhys;
        public float AtkMag;
        public float Def;
        public float Spd;
        public float Acc;
        public float Eva;
        public float Crit;         // 0..0.95
        public float CritDmg;      // bonus, 0.5 = +50%
        public float Res;          // 0..0.85
        public float EffAcc;       // 0..1
        public float Lifesteal;    // 0..0.40
        public float DmgBonus;     // buff tấn công
        public float DmgReduct;    // 0..0.75
        public float SpRegen;
        public float PoiseDmgBonus;

        /// <summary>Tính từ 6 chỉ số gốc theo bảng công thức plan.md §4.5.</summary>
        public static DerivedStats FromPrimary(in PrimaryStats p) => new()
        {
            MaxHp     = GameMath.RoundToInt(50f + p.Con * 12f),
            MaxSp     = GameMath.RoundToInt(20f + p.Int * 3f),
            AtkPhys   = 5f + p.Str * 2.2f,
            AtkMag    = 5f + p.Int * 2.4f,
            Def       = 2f + p.Con * 1.4f,
            Spd       = 80f + p.Dex * 3f,
            Acc       = 90f + p.Dex * 0.5f,
            Eva       = 3f,
            Crit      = (5f + p.Luk * 0.6f) / 100f,
            CritDmg   = 0.5f,
            Res       = (p.Dex * 0.2f + p.Aur * 0.8f) / 100f,
            EffAcc    = 0f,
            Lifesteal = 0f,
            DmgBonus  = 0f,
            DmgReduct = 0f,
            SpRegen   = 5f + p.Aur * 0.3f,
            PoiseDmgBonus = 0f
        };

        /// <summary>Kẹp mọi chỉ số về trần đã quy định (plan.md §4.5 bảng cap).</summary>
        public void ClampToCaps()
        {
            Acc       = GameMath.Clamp(Acc, 0f, BalanceCaps.ACC_MAX);
            Eva       = GameMath.Clamp(Eva, 0f, BalanceCaps.EVA_MAX);
            Crit      = GameMath.Clamp(Crit, 0f, BalanceCaps.CRIT_MAX);
            CritDmg   = GameMath.Clamp(CritDmg, 0f, BalanceCaps.CRIT_DMG_MAX);
            Res       = GameMath.Clamp(Res, 0f, BalanceCaps.RES_MAX);
            EffAcc    = GameMath.Clamp(EffAcc, 0f, 1f);
            Lifesteal = GameMath.Clamp(Lifesteal, 0f, BalanceCaps.LIFESTEAL_MAX);
            DmgReduct = GameMath.Clamp(DmgReduct, 0f, BalanceCaps.DMG_REDUCT_MAX);
            Spd       = GameMath.Max(Spd, BalanceCaps.SPD_MIN); // chống deadlock ATB — edge case E22
            MaxHp     = GameMath.Max(MaxHp, 1);
            MaxSp     = GameMath.Max(MaxSp, 0);
        }
    }

    /// <summary>
    /// Trần chỉ số — plan.md §4.5. Đổi ở đây thì chạy checklist object-map.md §7.7.
    /// Lưu ý đơn vị: Acc/Eva/Spd tính theo ĐIỂM (0..150); Crit/Res/Lifesteal/DmgReduct tính theo TỈ LỆ (0..1).
    /// </summary>
    public static class BalanceCaps
    {
        public const float ACC_MAX        = 150f;
        public const float EVA_MAX        = 40f;
        public const float CRIT_MAX       = 0.95f;
        public const float CRIT_DMG_MAX   = 3.00f;
        public const float RES_MAX        = 0.85f;
        public const float LIFESTEAL_MAX  = 0.40f;
        public const float DMG_REDUCT_MAX = 0.75f;
        public const float SPD_MIN        = 10f;
    }
}
