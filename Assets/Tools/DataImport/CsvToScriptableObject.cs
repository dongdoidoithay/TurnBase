using System;
using System.Collections.Generic;
using Game.Data;
using Game.Meta.Content;
using UnityEditor;
using UnityEngine;

namespace Game.Tools.DataImport
{
    /// <summary>
    /// CSV → ScriptableObject — plan.md §11.4, structure.md §4, object-map.md §7.1/§7.4.
    ///
    /// Nguồn:  Assets/_Project/Data/CSV/{skills,heroes,enemies}.csv
    /// Đích:   Assets/_Project/Data/{Skills,Heroes,Enemies}/{id}.asset
    ///
    /// Import LẶP LẠI ĐƯỢC AN TOÀN: nếu asset đã tồn tại (cùng tên file = id) thì
    /// ghi đè field, không tạo trùng. Xoá dòng khỏi CSV KHÔNG tự xoá asset cũ
    /// (tránh mất asset đang được prefab/scene tham chiếu) — DataValidator sẽ cảnh báo asset mồ côi.
    /// </summary>
    public static class CsvToScriptableObject
    {
        private const string CsvFolder = "Assets/_Project/Data/CSV";

        // Nằm dưới Resources vì chưa cài Addressables (bị gỡ do lỗi CS0619 trên Unity 6.5 — xem plan.md).
        // Runtime nạp qua Resources.Load, giống AudioService. Sẽ đổi sang Addressables ở P2/P3.
        private const string SkillOutFolder = "Assets/_Project/Resources/Data/Skills";
        private const string HeroOutFolder = "Assets/_Project/Resources/Data/Heroes";
        private const string EnemyOutFolder = "Assets/_Project/Resources/Data/Enemies";
        private const string EquipmentOutFolder = "Assets/_Project/Resources/Data/Equipment";

        [MenuItem("Tools/Import Game Data")]
        public static void ImportAll()
        {
            int skills = ImportSkills();
            int heroes = ImportHeroes();
            int enemies = ImportEnemies();
            int equipment = ImportEquipment();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CsvImport] Xong — {skills} skill, {heroes} hero, {enemies} enemy, {equipment} equipment.");

            DataValidator.ValidateAll();
        }

        // =====================================================================

        private static int ImportSkills()
        {
            var rows = CsvReader.ReadFile($"{CsvFolder}/skills.csv");
            EnsureFolder(SkillOutFolder);

            foreach (var row in rows)
            {
                string id = row.Get(CsvSchema.Skill.Id);
                if (string.IsNullOrWhiteSpace(id)) continue;

                var so = LoadOrCreate<SkillDefinitionSO>($"{SkillOutFolder}/{id}.asset");
                var d = so.Data ??= new SkillData();

                d.Id = id;
                d.NameKey = row.Get(CsvSchema.Skill.NameKey);
                d.Type = ParseEnum(row.Get(CsvSchema.Skill.Type), SkillType.Physical);
                d.DamageType = ParseEnum(row.Get(CsvSchema.Skill.DamageType), DamageType.Physical);
                d.Element = ParseEnum(row.Get(CsvSchema.Skill.Element), Element.Neutral);
                d.Target = ParseEnum(row.Get(CsvSchema.Skill.Target), TargetMode.SingleEnemy);
                d.SpCost = row.GetInt(CsvSchema.Skill.SpCost);
                d.Cooldown = row.GetInt(CsvSchema.Skill.Cooldown);
                d.PowerMultiplier = row.GetFloat(CsvSchema.Skill.PowerMultiplier, 1f);
                d.HitCount = row.GetInt(CsvSchema.Skill.HitCount, 1);
                d.PoiseDamage = row.GetInt(CsvSchema.Skill.PoiseDamage, 3);
                d.IsBreaker = row.GetBool(CsvSchema.Skill.IsBreaker);
                d.IsAoe = row.GetBool(CsvSchema.Skill.IsAoe);
                d.CommandType = ParseEnum(row.Get(CsvSchema.Skill.CommandType), ActionCommandType.SingleTap);
                d.ComboBeats = row.GetInt(CsvSchema.Skill.ComboBeats, 1);
                d.HealPower = row.GetFloat(CsvSchema.Skill.HealPower);
                d.ShieldPower = row.GetFloat(CsvSchema.Skill.ShieldPower);
                d.SpRestore = row.GetInt(CsvSchema.Skill.SpRestore);
                d.CleanseCount = row.GetInt(CsvSchema.Skill.CleanseCount);
                d.AnimTrigger = string.IsNullOrEmpty(row.Get(CsvSchema.Skill.AnimTrigger))
                    ? "attack" : row.Get(CsvSchema.Skill.AnimTrigger);

                string statusRaw = row.Get(CsvSchema.Skill.StatusId);
                d.Applies = string.IsNullOrWhiteSpace(statusRaw)
                    ? Array.Empty<StatusApplication>()
                    : new[]
                    {
                        new StatusApplication(
                            ParseEnum(statusRaw, StatusId.None),
                            row.GetFloat(CsvSchema.Skill.StatusChance, 1f),
                            row.GetInt(CsvSchema.Skill.StatusDuration))
                    };

                d.RevivePercent = row.GetFloat(CsvSchema.Skill.RevivePercent);
                d.DispelCount = row.GetInt(CsvSchema.Skill.DispelCount);

                EditorUtility.SetDirty(so);
            }

            return rows.Count;
        }

        private static int ImportHeroes()
        {
            var rows = CsvReader.ReadFile($"{CsvFolder}/heroes.csv");
            EnsureFolder(HeroOutFolder);

            foreach (var row in rows)
            {
                string id = row.Get(CsvSchema.Hero.Id);
                if (string.IsNullOrWhiteSpace(id)) continue;

                var so = LoadOrCreate<HeroDefinitionSO>($"{HeroOutFolder}/{id}.asset");
                so.DefId = id;
                so.NameKey = row.Get(CsvSchema.Hero.NameKey);
                so.Class = ParseEnum(row.Get(CsvSchema.Hero.Class), HeroClass.Vanguard);
                so.Rarity = ParseEnum(row.Get(CsvSchema.Hero.Rarity), Rarity.Common);
                so.Element = ParseEnum(row.Get(CsvSchema.Hero.Element), Element.Neutral);
                so.PoiseMax = row.GetInt(CsvSchema.Hero.PoiseMax, 60);
                so.BasePrimary = new PrimaryStats(
                    row.GetFloat(CsvSchema.Hero.Str), row.GetFloat(CsvSchema.Hero.Con),
                    row.GetFloat(CsvSchema.Hero.Int), row.GetFloat(CsvSchema.Hero.Dex),
                    row.GetFloat(CsvSchema.Hero.Aur), row.GetFloat(CsvSchema.Hero.Luk));
                so.SkillIds = row.GetList(CsvSchema.Hero.SkillIds);
                so.SpriteFolder = row.Get(CsvSchema.Hero.SpriteFolder);

                EditorUtility.SetDirty(so);
            }

            return rows.Count;
        }

        private static int ImportEnemies()
        {
            var rows = CsvReader.ReadFile($"{CsvFolder}/enemies.csv");
            EnsureFolder(EnemyOutFolder);

            foreach (var row in rows)
            {
                string id = row.Get(CsvSchema.Enemy.Id);
                if (string.IsNullOrWhiteSpace(id)) continue;

                var so = LoadOrCreate<EnemyDefinitionSO>($"{EnemyOutFolder}/{id}.asset");
                so.DefId = id;
                so.NameKey = row.Get(CsvSchema.Enemy.NameKey);
                so.Element = ParseEnum(row.Get(CsvSchema.Enemy.Element), Element.Neutral);
                so.Archetype = ParseEnum(row.Get(CsvSchema.Enemy.Archetype), ArchetypeId.Grunt);
                so.PoiseMax = row.GetInt(CsvSchema.Enemy.PoiseMax, 30);
                so.BasePrimary = new PrimaryStats(
                    row.GetFloat(CsvSchema.Enemy.Str), row.GetFloat(CsvSchema.Enemy.Con),
                    row.GetFloat(CsvSchema.Enemy.Int), row.GetFloat(CsvSchema.Enemy.Dex),
                    row.GetFloat(CsvSchema.Enemy.Aur), row.GetFloat(CsvSchema.Enemy.Luk));
                so.SkillIds = row.GetList(CsvSchema.Enemy.SkillIds);
                so.AiProfileId = string.IsNullOrEmpty(row.Get(CsvSchema.Enemy.AiProfileId))
                    ? "ai_basic" : row.Get(CsvSchema.Enemy.AiProfileId);
                so.Chapter = row.GetInt(CsvSchema.Enemy.Chapter, 1);
                so.SpriteFolder = row.Get(CsvSchema.Enemy.SpriteFolder);

                EditorUtility.SetDirty(so);
            }

            return rows.Count;
        }

        private static int ImportEquipment()
        {
            var rows = CsvReader.ReadFile($"{CsvFolder}/equipment.csv");
            EnsureFolder(EquipmentOutFolder);

            foreach (var row in rows)
            {
                string id = row.Get(CsvSchema.Equipment.Id);
                if (string.IsNullOrWhiteSpace(id)) continue;

                var so = LoadOrCreate<EquipmentDefinitionSO>($"{EquipmentOutFolder}/{id}.asset");
                so.DefId = id;
                so.NameKey = row.Get(CsvSchema.Equipment.NameKey);
                so.Slot = ParseEnum(row.Get(CsvSchema.Equipment.Slot), EquipSlot.Weapon);
                so.Rarity = row.GetInt(CsvSchema.Equipment.Rarity, 1);
                so.StatType = ParseEnum(row.Get(CsvSchema.Equipment.StatType), StatType.Str);
                so.StatValue = row.GetFloat(CsvSchema.Equipment.StatValue, 5f);

                EditorUtility.SetDirty(so);
            }

            return rows.Count;
        }

        // =====================================================================

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var so = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(so, path);
            return so;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int lastSlash = path.LastIndexOf('/');
            string parent = path[..lastSlash];
            string leaf = path[(lastSlash + 1)..];
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static TEnum ParseEnum<TEnum>(string raw, TEnum fallback) where TEnum : struct
            => !string.IsNullOrWhiteSpace(raw) && Enum.TryParse<TEnum>(raw, true, out var v) ? v : fallback;
    }
}
