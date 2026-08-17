using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Game.Services.Localization
{
    /// <summary>task-localization-pilot.md — đọc <c>Resources/Localization/strings.csv</c>
    /// (cột <c>key,vi,en</c>). Parser CSV nhỏ tự viết (không dùng lại <c>Game.Tools.CsvReader</c> —
    /// asmdef đó chỉ chạy Editor, <see cref="LocalizationService"/> phải chạy được ở runtime thật).
    /// Xử lý field trong dấu ngoặc kép (chứa dấu phẩy) như CsvReader, không xử lý xuống dòng trong
    /// field (không cần cho chuỗi UI ngắn của pilot này).</summary>
    public sealed class LocalizationService : ILocalizationService
    {
        private const string CsvResourcePath = "Localization/strings";
        private const string DefaultLanguage = "vi";

        // key -> (language -> value)
        private readonly Dictionary<string, Dictionary<string, string>> _table = new();
        private string _currentLanguage = DefaultLanguage;

        public string CurrentLanguage => _currentLanguage;
        public event Action OnLanguageChanged;

        public LocalizationService() => Load();

        public void SetLanguage(string language)
        {
            if (string.IsNullOrEmpty(language) || language == _currentLanguage) return;
            _currentLanguage = language;
            OnLanguageChanged?.Invoke();
        }

        public string Get(string key)
        {
            if (_table.TryGetValue(key, out var byLang)
                && byLang.TryGetValue(_currentLanguage, out var value))
                return value;
            return key;
        }

        public string Get(string key, params object[] args)
        {
            string template = Get(key);
            try { return string.Format(template, args); }
            catch (FormatException) { return template; } // thiếu placeholder → hiện nguyên bản thay vì crash
        }

        public string GetName(string defId, LocalizedNameKind kind)
        {
            if (string.IsNullOrEmpty(defId)) return defId;

            string idPrefix = kind switch
            {
                LocalizedNameKind.Hero => "hero_",
                LocalizedNameKind.Enemy => "enemy_",
                LocalizedNameKind.Skill => "skill_",
                LocalizedNameKind.Boss => "boss_",
                _ => "",
            };
            string keyPrefix = kind switch
            {
                LocalizedNameKind.Hero => "hero.",
                LocalizedNameKind.Enemy => "enemy.",
                LocalizedNameKind.Skill => "skill.",
                LocalizedNameKind.Boss => "boss.",
                _ => "",
            };

            string stripped = defId.StartsWith(idPrefix) ? defId[idPrefix.Length..] : defId;
            string key = keyPrefix + stripped + ".name";

            string value = Get(key);
            // task-phase-5-gaps.md Phần D — thiếu key nghĩa là chưa dịch (NameKey đã có sẵn 100%
            // trên data thật, chỉ strings.csv chưa đủ dòng) → tự format title-case thay vì hiện key
            // thô "hero.foo.name" trước mặt người chơi. Trùng lặp có chủ đích với HeroDisplayUtil
            // (không import được — Game.Services không tham chiếu Game.Meta).
            return value == key ? FormatTitleCase(stripped) : value;
        }

        private static string FormatTitleCase(string raw)
        {
            var parts = raw.Split('_', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
                parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i][1..].ToLowerInvariant();
            return string.Join(" ", parts);
        }

        private void Load()
        {
            var asset = Resources.Load<TextAsset>(CsvResourcePath);
            if (asset == null)
            {
                Debug.LogWarning($"[Localization] Không tìm thấy '{CsvResourcePath}' — mọi key sẽ hiện nguyên key.");
                return;
            }

            var lines = ParseLines(asset.text);
            if (lines.Count == 0) return;

            var header = lines[0]; // "key","vi","en"
            for (int c = 0; c < header.Count; c++) header[c] = header[c].Trim();
            int keyCol = header.IndexOf("key");
            var langCols = new Dictionary<int, string>();
            for (int c = 0; c < header.Count; c++)
                if (c != keyCol) langCols[c] = header[c];

            for (int r = 1; r < lines.Count; r++)
            {
                var line = lines[r];
                if (line.Count == 1 && line[0].Length == 0) continue; // dòng trống
                if (keyCol < 0 || keyCol >= line.Count) continue;

                string key = line[keyCol].Trim();
                if (string.IsNullOrEmpty(key)) continue;

                var byLang = new Dictionary<string, string>();
                foreach (var kv in langCols)
                    byLang[kv.Value] = kv.Key < line.Count ? line[kv.Key] : "";
                _table[key] = byLang;
            }
        }

        /// <summary>Tách CSV thành các dòng/cột — xử lý field trong dấu ngoặc kép (chứa dấu phẩy).</summary>
        private static List<List<string>> ParseLines(string text)
        {
            var lines = new List<List<string>>();
            var current = new List<string>();
            var field = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else field.Append(c);
                    continue;
                }

                switch (c)
                {
                    case '"': inQuotes = true; break;
                    case ',': current.Add(field.ToString()); field.Clear(); break;
                    case '\r': break;
                    case '\n':
                        current.Add(field.ToString()); field.Clear();
                        lines.Add(current);
                        current = new List<string>();
                        break;
                    default: field.Append(c); break;
                }
            }
            if (field.Length > 0 || current.Count > 0)
            {
                current.Add(field.ToString());
                lines.Add(current);
            }
            return lines;
        }
    }
}
