using System.Collections.Generic;
using Game.Meta.Content;
using UnityEditor;
using UnityEngine;

namespace Game.Tools.DataImport
{
    /// <summary>
    /// Kiểm tra dữ liệu sau import — object-map.md §4 "DataValidator":
    /// id trùng, tham chiếu chết (skillIds trỏ tới skill không tồn tại), thiếu NameKey.
    /// Chỉ log cảnh báo — KHÔNG chặn import, để không cản người làm content.
    /// </summary>
    public static class DataValidator
    {
        [MenuItem("Tools/Validate Game Data")]
        public static void ValidateAll()
        {
            var skillIds = new HashSet<string>();
            int errors = 0, warnings = 0;

            foreach (var skill in LoadAll<SkillDefinitionSO>("Assets/_Project/Resources/Data/Skills"))
            {
                if (!skillIds.Add(skill.Id))
                    Err(ref errors, $"Skill id trùng: '{skill.Id}' ({AssetDatabase.GetAssetPath(skill)})");
                if (string.IsNullOrWhiteSpace(skill.Data.NameKey))
                    Warn(ref warnings, $"Skill '{skill.Id}' thiếu nameKey");
            }

            var heroIds = new HashSet<string>();
            foreach (var hero in LoadAll<HeroDefinitionSO>("Assets/_Project/Resources/Data/Heroes"))
            {
                if (!heroIds.Add(hero.DefId))
                    Err(ref errors, $"Hero id trùng: '{hero.DefId}' ({AssetDatabase.GetAssetPath(hero)})");
                if (string.IsNullOrWhiteSpace(hero.NameKey))
                    Warn(ref warnings, $"Hero '{hero.DefId}' thiếu nameKey");
                if (hero.SkillIds.Length == 0)
                    Warn(ref warnings, $"Hero '{hero.DefId}' không có skill nào");

                foreach (var sid in hero.SkillIds)
                    if (!skillIds.Contains(sid))
                        Err(ref errors, $"Hero '{hero.DefId}' tham chiếu skill không tồn tại: '{sid}'");
            }

            var enemyIds = new HashSet<string>();
            foreach (var enemy in LoadAll<EnemyDefinitionSO>("Assets/_Project/Resources/Data/Enemies"))
            {
                if (!enemyIds.Add(enemy.DefId))
                    Err(ref errors, $"Enemy id trùng: '{enemy.DefId}' ({AssetDatabase.GetAssetPath(enemy)})");
                if (string.IsNullOrWhiteSpace(enemy.NameKey))
                    Warn(ref warnings, $"Enemy '{enemy.DefId}' thiếu nameKey");

                foreach (var sid in enemy.SkillIds)
                    if (!skillIds.Contains(sid))
                        Err(ref errors, $"Enemy '{enemy.DefId}' tham chiếu skill không tồn tại: '{sid}'");
            }

            string summary = $"[DataValidator] {skillIds.Count} skill · {heroIds.Count} hero · " +
                              $"{enemyIds.Count} enemy — {errors} lỗi, {warnings} cảnh báo.";
            if (errors > 0) Debug.LogError(summary);
            else if (warnings > 0) Debug.LogWarning(summary);
            else Debug.Log(summary);
        }

        private static IEnumerable<T> LoadAll<T>(string folder) where T : Object
        {
            if (!AssetDatabase.IsValidFolder(folder)) yield break;
            foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder }))
                yield return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
        }

        private static void Err(ref int count, string msg) { count++; Debug.LogError("[DataValidator] " + msg); }
        private static void Warn(ref int count, string msg) { count++; Debug.LogWarning("[DataValidator] " + msg); }
    }
}
