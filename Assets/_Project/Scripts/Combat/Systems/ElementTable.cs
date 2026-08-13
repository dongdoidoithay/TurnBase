using Game.Data;

namespace Game.Combat.Systems
{
    /// <summary>
    /// Bảng khắc chế nguyên tố 6×6 — plan.md §4.7.
    /// Fire → Wind → Earth → Water → Fire (×1.3) · Light ⇄ Dark (×1.4).
    /// </summary>
    public static class ElementTable
    {
        public const float STRONG = 1.30f;
        public const float WEAK = 0.70f;
        public const float NEUTRAL = 1.00f;
        public const float OPPOSED = 1.40f; // Light ⇄ Dark

        /// <summary>Hệ số sát thương khi <paramref name="attacker"/> đánh vào <paramref name="defender"/>.</summary>
        public static float Multiplier(Element attacker, Element defender)
        {
            if (attacker == Element.Neutral || defender == Element.Neutral) return NEUTRAL;

            // Cặp đối lập Light ⇄ Dark
            if (attacker == Element.Light && defender == Element.Dark) return OPPOSED;
            if (attacker == Element.Dark && defender == Element.Light) return OPPOSED;
            if (attacker == Element.Light || attacker == Element.Dark ||
                defender == Element.Light || defender == Element.Dark) return NEUTRAL;

            // Vòng 4 hệ
            if (StrongAgainst(attacker) == defender) return STRONG;
            if (StrongAgainst(defender) == attacker) return WEAK;
            return NEUTRAL;
        }

        /// <summary>Hệ mà <paramref name="e"/> khắc chế.</summary>
        public static Element StrongAgainst(Element e) => e switch
        {
            Element.Fire  => Element.Wind,
            Element.Wind  => Element.Earth,
            Element.Earth => Element.Water,
            Element.Water => Element.Fire,
            Element.Light => Element.Dark,
            Element.Dark  => Element.Light,
            _ => Element.Neutral
        };

        public static bool IsStrong(Element attacker, Element defender)
            => Multiplier(attacker, defender) > NEUTRAL;

        public static bool IsWeak(Element attacker, Element defender)
            => Multiplier(attacker, defender) < NEUTRAL;
    }
}
