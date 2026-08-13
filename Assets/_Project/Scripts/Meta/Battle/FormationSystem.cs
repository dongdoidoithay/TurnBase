using System.Collections.Generic;
using Game.Combat.Model;
using Game.Data;

namespace Game.Meta.Battle
{
    /// <summary>
    /// 8 preset đội hình từ plan.md §5.7.
    /// Xác định hàng Trước/Sau cho từng slot và bonus stat đi kèm.
    /// Pure static — không phụ thuộc Unity, test được headless.
    /// </summary>
    public static class FormationSystem
    {
        public const string Default = "formation_balanced";

        public static readonly IReadOnlyList<string> AllIds = new[]
        {
            "formation_balanced",
            "formation_phalanx",
            "formation_arrowhead",
            "formation_vanguard_line",
            "formation_siege",
            "formation_flanking",
            "formation_turtle",
            "formation_blitz",
        };

        // ── hàng trước/sau ──────────────────────────────────────────────────

        /// <summary>Số hero ở hàng Trước (slot 0..frontCount-1 = Front, còn lại = Back).</summary>
        public static int FrontCount(string id) => id switch
        {
            "formation_phalanx"       => 3,
            "formation_arrowhead"     => 1,
            "formation_vanguard_line" => 4,
            "formation_siege"         => 0,
            _                         => 2,  // balanced / flanking / turtle / blitz
        };

        public static Row GetRow(string formationId, int slotIndex)
            => slotIndex < FrontCount(formationId) ? Row.Front : Row.Back;

        // ── bonus stat ──────────────────────────────────────────────────────

        public static IEnumerable<StatModifier> GetModifiers(string formationId, Row row)
        {
            switch (formationId)
            {
                case "formation_phalanx":
                    if (row == Row.Front)
                        yield return new StatModifier(StatType.DefPct, 15f, "formation");
                    break;

                case "formation_arrowhead":
                    if (row == Row.Back)
                        yield return new StatModifier(StatType.AtkPct, 12f, "formation");
                    break;

                case "formation_vanguard_line":
                    yield return new StatModifier(StatType.AtkPct,  10f, "formation");
                    yield return new StatModifier(StatType.DefPct, -10f, "formation");
                    break;

                case "formation_siege":
                    yield return new StatModifier(StatType.DmgReductPct, 20f, "formation");
                    yield return new StatModifier(StatType.SpdPct,       -15f, "formation");
                    break;

                case "formation_flanking":
                    yield return new StatModifier(StatType.CritPct, 8f, "formation");
                    break;

                case "formation_turtle":
                    yield return new StatModifier(StatType.DefPct,  20f, "formation");
                    yield return new StatModifier(StatType.AtkPct, -15f, "formation");
                    break;

                case "formation_blitz":
                    yield return new StatModifier(StatType.SpdPct,    12f, "formation");
                    yield return new StatModifier(StatType.MaxHpPct,  -8f, "formation");
                    break;

                // formation_balanced: no modifiers
            }
        }

        /// <summary>Tên hiển thị ngắn cho UI.</summary>
        public static string DisplayName(string id) => id switch
        {
            "formation_balanced"      => "Balanced",
            "formation_phalanx"       => "Phalanx",
            "formation_arrowhead"     => "Arrowhead",
            "formation_vanguard_line" => "Vanguard Line",
            "formation_siege"         => "Siege",
            "formation_flanking"      => "Flanking",
            "formation_turtle"        => "Turtle",
            "formation_blitz"         => "Blitz",
            _                         => id,
        };

        /// <summary>Mô tả bonus ngắn cho UI tooltip.</summary>
        public static string BonusSummary(string id) => id switch
        {
            "formation_balanced"      => "No bonus",
            "formation_phalanx"       => "Front: +15% DEF",
            "formation_arrowhead"     => "Back: +12% ATK",
            "formation_vanguard_line" => "+10% ATK / -10% DEF",
            "formation_siege"         => "-20% Damage Taken / -15% SPD",
            "formation_flanking"      => "+8% CRIT",
            "formation_turtle"        => "+20% DEF / -15% ATK",
            "formation_blitz"         => "+12% SPD / -8% Max HP",
            _                         => "",
        };
    }
}
