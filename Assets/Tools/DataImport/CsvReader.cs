using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Game.Tools.DataImport
{
    /// <summary>Đọc CSV tối giản — hỗ trợ field trong dấu ngoặc kép chứa dấu phẩy/xuống dòng.</summary>
    public static class CsvReader
    {
        public sealed class Row
        {
            private readonly Dictionary<string, string> _values;
            public Row(Dictionary<string, string> values) => _values = values;

            public string Get(string col) => _values.TryGetValue(col, out var v) ? v : "";
            public bool GetBool(string col) => bool.TryParse(Get(col), out var b) && b;
            public int GetInt(string col, int fallback = 0)
                => int.TryParse(Get(col), out var i) ? i : fallback;
            public float GetFloat(string col, float fallback = 0f)
                => float.TryParse(Get(col), System.Globalization.NumberStyles.Float,
                                   System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : fallback;
            public string[] GetList(string col, char sep = ';')
            {
                var raw = Get(col);
                if (string.IsNullOrWhiteSpace(raw)) return System.Array.Empty<string>();
                var parts = raw.Split(sep);
                for (int i = 0; i < parts.Length; i++) parts[i] = parts[i].Trim();
                return parts;
            }
        }

        public static List<Row> ReadFile(string path)
        {
            var rows = new List<Row>();
            if (!File.Exists(path)) return rows;

            var lines = ParseLines(File.ReadAllText(path));
            if (lines.Count == 0) return rows;

            var header = lines[0];
            for (int r = 1; r < lines.Count; r++)
            {
                var line = lines[r];
                if (line.Count == 1 && line[0].Length == 0) continue; // dòng trống

                var dict = new Dictionary<string, string>(header.Count);
                for (int c = 0; c < header.Count; c++)
                    dict[header[c].Trim()] = c < line.Count ? line[c].Trim() : "";
                rows.Add(new Row(dict));
            }
            return rows;
        }

        /// <summary>Tách text CSV thành các dòng, mỗi dòng là list cột — xử lý ngoặc kép cơ bản.</summary>
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
