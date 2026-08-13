namespace Game.Tools.DataImport
{
    /// <summary>
    /// Tên cột CSV cho từng loại dữ liệu — object-map.md §7.1/§7.4, structure.md §4.
    /// Đổi cột thì sửa ở đây, KHÔNG sửa rải rác trong CsvToScriptableObject.
    /// </summary>
    public static class CsvSchema
    {
        public static class Skill
        {
            public const string Id = "id";
            public const string NameKey = "nameKey";
            public const string Type = "type";
            public const string DamageType = "damageType";
            public const string Element = "element";
            public const string Target = "target";
            public const string SpCost = "spCost";
            public const string Cooldown = "cooldown";
            public const string PowerMultiplier = "powerMultiplier";
            public const string HitCount = "hitCount";
            public const string PoiseDamage = "poiseDamage";
            public const string IsBreaker = "isBreaker";
            public const string IsAoe = "isAoe";
            public const string CommandType = "commandType";
            public const string ComboBeats = "comboBeats";
            public const string HealPower = "healPower";
            public const string ShieldPower = "shieldPower";
            public const string SpRestore = "spRestore";
            public const string CleanseCount = "cleanseCount";
            public const string AnimTrigger = "animTrigger";
            public const string StatusId = "statusId";
            public const string StatusChance = "statusChance";
            public const string StatusDuration = "statusDuration";
            public const string RevivePercent = "revivePercent";
            public const string DispelCount = "dispelCount";
        }

        public static class Hero
        {
            public const string Id = "id";
            public const string NameKey = "nameKey";
            public const string Class = "class";
            public const string Rarity = "rarity";
            public const string Element = "element";
            public const string PoiseMax = "poiseMax";
            public const string Str = "str";
            public const string Con = "con";
            public const string Int = "int";
            public const string Dex = "dex";
            public const string Aur = "aur";
            public const string Luk = "luk";
            public const string SkillIds = "skillIds";
            public const string SpriteFolder = "spriteFolder";
        }

        public static class Equipment
        {
            public const string Id = "id";
            public const string NameKey = "nameKey";
            public const string Slot = "slot";
            public const string Rarity = "rarity";
            public const string StatType = "statType";
            public const string StatValue = "statValue";
        }

        public static class Enemy
        {
            public const string Id = "id";
            public const string NameKey = "nameKey";
            public const string Element = "element";
            public const string Archetype = "archetype";
            public const string PoiseMax = "poiseMax";
            public const string Str = "str";
            public const string Con = "con";
            public const string Int = "int";
            public const string Dex = "dex";
            public const string Aur = "aur";
            public const string Luk = "luk";
            public const string SkillIds = "skillIds";
            public const string AiProfileId = "aiProfileId";
            public const string Chapter = "chapter";
            public const string SpriteFolder = "spriteFolder";
        }
    }
}
