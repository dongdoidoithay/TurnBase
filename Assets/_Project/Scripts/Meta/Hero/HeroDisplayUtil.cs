namespace Game.Meta.Hero
{
    /// <summary>Chưa có ILocalizationService để tra NameKey ra chuỗi hiển thị thật (P5) —
    /// dùng chung giữa TeamSelectScreen/HeroDetailScreen để không lặp code.</summary>
    public static class HeroDisplayUtil
    {
        /// <summary>"hero_ember_knight" → "Ember Knight".</summary>
        public static string FormatName(string defId) => FormatId(defId, "hero_");

        /// <summary>"skill_power_strike" → "Power Strike".</summary>
        public static string FormatSkillName(string skillId) => FormatId(skillId, "skill_");

        /// <summary>"enemy_goblin_grunt" → "Goblin Grunt" — task-codex.md.</summary>
        public static string FormatEnemyName(string defId) => FormatId(defId, "enemy_");

        private static string FormatId(string id, string prefix)
        {
            if (string.IsNullOrEmpty(id)) return id;
            string raw = id.StartsWith(prefix) ? id[prefix.Length..] : id;
            var parts = raw.Split('_', System.StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
                parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i][1..].ToLowerInvariant();
            return string.Join(" ", parts);
        }
    }
}
