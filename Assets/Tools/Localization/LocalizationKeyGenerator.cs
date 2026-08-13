using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Game.Meta.Content;
using UnityEditor;
using UnityEngine;

namespace Game.Tools.Localization
{
    /// <summary>
    /// task-phase-5-gaps.md Phần D — quét 24 hero/66 enemy/65 skill (đã có <c>NameKey</c> gán sẵn
    /// 100% đúng pattern <c>{hero|enemy|skill}.{id không tiền tố}.name</c>, xem <c>DataValidator</c>)
    /// và bổ sung dòng thiếu vào <c>Resources/Localization/strings.csv</c> — chỉ ghi những key CHƯA
    /// có (idempotent, không đè key đã tồn tại/đã dịch tay). Tên riêng (fantasy proper noun) —
    /// VI và EN dùng CHUNG 1 giá trị title-case từ id (đúng cách "AETHER LEGION" đã làm trong 10
    /// key pilot), KHÔNG bịa dịch tiếng Việt cho danh từ riêng. Chạy TAY qua menu (không tự chạy
    /// khi import) — style <c>DataValidator</c>/<c>BalanceHarness</c>/<c>ObjectMapValidator</c>.
    /// </summary>
    public static class LocalizationKeyGenerator
    {
        private const string CsvPath = "Assets/_Project/Resources/Localization/strings.csv";

        [MenuItem("Tools/Localization/Generate Name Keys")]
        public static void Generate()
        {
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), CsvPath);
            if (!File.Exists(fullPath))
            {
                Debug.LogError($"[LocalizationKeyGenerator] Không tìm thấy {CsvPath}");
                return;
            }

            string original = File.ReadAllText(fullPath);
            var existingKeys = ParseExistingKeys(original);

            var newRows = new List<(string key, string name)>();
            CollectHeroes(existingKeys, newRows);
            CollectEnemies(existingKeys, newRows);
            CollectSkills(existingKeys, newRows);

            if (newRows.Count == 0)
            {
                Debug.Log("[LocalizationKeyGenerator] Không có key mới — strings.csv đã đủ.");
                return;
            }

            var sb = new StringBuilder(original);
            if (!original.EndsWith("\n")) sb.Append('\n');
            foreach (var (key, name) in newRows)
                sb.Append(CsvEscape(key)).Append(',').Append(CsvEscape(name)).Append(',')
                  .Append(CsvEscape(name)).Append('\n');

            File.WriteAllText(fullPath, sb.ToString());
            AssetDatabase.Refresh();

            Debug.Log($"[LocalizationKeyGenerator] Thêm {newRows.Count} key mới vào strings.csv " +
                      $"(tổng {existingKeys.Count + newRows.Count} key).");
        }

        private static void CollectHeroes(HashSet<string> existingKeys, List<(string, string)> rows)
        {
            foreach (var def in LoadAll<HeroDefinitionSO>("Assets/_Project/Resources/Data/Heroes"))
                AddIfMissing(def.NameKey, def.DefId, "hero_", "hero.", existingKeys, rows);
        }

        private static void CollectEnemies(HashSet<string> existingKeys, List<(string, string)> rows)
        {
            foreach (var def in LoadAll<EnemyDefinitionSO>("Assets/_Project/Resources/Data/Enemies"))
                AddIfMissing(def.NameKey, def.DefId, "enemy_", "enemy.", existingKeys, rows);
        }

        private static void CollectSkills(HashSet<string> existingKeys, List<(string, string)> rows)
        {
            foreach (var def in LoadAll<SkillDefinitionSO>("Assets/_Project/Resources/Data/Skills"))
                AddIfMissing(def.Data.NameKey, def.Data.Id, "skill_", "skill.", existingKeys, rows);
        }

        private static void AddIfMissing(string nameKey, string defId, string idPrefix, string keyPrefix,
            HashSet<string> existingKeys, List<(string key, string name)> rows)
        {
            string stripped = defId.StartsWith(idPrefix) ? defId[idPrefix.Length..] : defId;
            // NameKey đã có sẵn 100% trên data thật (xem doc-comment class) — vẫn tính key độc lập
            // từ DefId phòng trường hợp nội dung mới sau này thiếu NameKey, thay vì phụ thuộc hoàn
            // toàn vào field có thể trống.
            string key = string.IsNullOrEmpty(nameKey) ? $"{keyPrefix}{stripped}.name" : nameKey;
            if (existingKeys.Contains(key)) return;

            existingKeys.Add(key);
            rows.Add((key, FormatTitleCase(stripped)));
        }

        private static string FormatTitleCase(string raw)
        {
            var parts = raw.Split('_', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
                parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i][1..].ToLowerInvariant();
            return string.Join(" ", parts);
        }

        private static HashSet<string> ParseExistingKeys(string csvText)
        {
            var keys = new HashSet<string>();
            var lines = csvText.Replace("\r\n", "\n").Split('\n');
            for (int i = 1; i < lines.Length; i++) // bỏ header
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;
                int comma = line.IndexOf(',');
                keys.Add(comma < 0 ? line.Trim() : line[..comma].Trim());
            }
            return keys;
        }

        private static string CsvEscape(string value)
        {
            if (value != null && (value.Contains(',') || value.Contains('"')))
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value ?? "";
        }

        private static IEnumerable<T> LoadAll<T>(string folder) where T : UnityEngine.Object
        {
            if (!AssetDatabase.IsValidFolder(folder)) yield break;
            foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder }))
                yield return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
        }
    }
}
