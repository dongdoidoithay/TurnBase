using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Game.Tools.ObjectMap
{
    /// <summary>
    /// object-map.md §11 quy tắc 3: quét mọi MonoBehaviour trong 3 scene + toàn bộ prefab,
    /// đối chiếu với bảng §3/§4 (cột "Script"/"Prefab"), báo 3 loại chênh lệch. Đọc thẳng
    /// object-map.md làm nguồn sự thật (không hardcode danh sách) — style DataValidator/
    /// BalanceHarness: chỉ log Console (+ file report tuỳ chọn), KHÔNG chặn merge, KHÔNG tự sửa docs.
    /// Quét bằng cách đọc text YAML thô (m_Script GUID) thay vì mở scene trong Editor — an toàn
    /// với phiên Editor đang chạy sống của người dùng (không đổi scene đang mở).
    /// Giới hạn đã biết: script gắn qua <c>AddComponent&lt;T&gt;()</c> lúc runtime (không serialize
    /// trong scene/prefab — đúng với phần lớn màn Meta code-dựng, xem object-map.md §12.1) sẽ
    /// KHÔNG bị quét thấy ở đây — chỉ bắt được bề mặt tĩnh của scene/prefab.
    /// </summary>
    public static class ObjectMapValidator
    {
        private static readonly Regex ScriptGuidRegex = new Regex(
            @"m_Script:\s*\{fileID:\s*-?\d+,\s*guid:\s*([0-9a-fA-F]{32}),\s*type:\s*\d+\}",
            RegexOptions.Compiled);

        private static readonly Regex BacktickToken = new Regex(
            @"`([A-Za-z_][A-Za-z0-9_]*)`", RegexOptions.Compiled);

        private static readonly Regex SeparatorRow = new Regex(@"^[\s|:-]+$", RegexOptions.Compiled);

        [MenuItem("Tools/Validate Object Map")]
        public static void Validate() => Run(writeReportFile: false);

        [MenuItem("Tools/Object Map/Generate Report")]
        public static void GenerateReport() => Run(writeReportFile: true);

        private static void Run(bool writeReportFile)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string docPath = Path.Combine(projectRoot, "object-map.md");
            if (!File.Exists(docPath))
            {
                Debug.LogError($"[ObjectMapValidator] Không tìm thấy {docPath}");
                return;
            }

            var (docScripts, docPrefabs) = ParseDocTables(File.ReadAllText(docPath));

            // Script thật đang gắn trong 3 scene + toàn bộ prefab, khoá bằng asset path (§A2: "so
            // sánh bằng Script asset path, không so tên class").
            var realScripts = new Dictionary<string, string>(); // className -> assetPath
            ScanScriptRefs(Path.Combine(projectRoot, "Assets/_Project/Scenes"), "*.unity", realScripts);
            ScanScriptRefs(Path.Combine(projectRoot, "Assets/_Project"), "*.prefab", realScripts);

            // (a) Script trong docs nhưng KHÔNG tồn tại dạng file .cs bất kỳ đâu trong Assets/.
            var allProjectScripts = CollectAllScriptFileNames();
            var missingInProject = docScripts.Keys
                .Where(n => !allProjectScripts.ContainsKey(n))
                .OrderBy(n => n).ToList();

            // (b) Script THẬT CỦA DỰ ÁN (loại built-in Unity/package như Button/Image/Volume —
            // object-map.md không có ý định tracking chúng) gắn trong scene/prefab nhưng CHƯA
            // đăng ký ở docs.
            var undocumented = realScripts.Keys
                .Where(n => realScripts[n].StartsWith("Assets/") && !docScripts.ContainsKey(n))
                .OrderBy(n => n)
                .Select(n => $"{n} ({realScripts[n]})")
                .ToList();

            // (c) Tên prefab trong docs nhưng KHÔNG có asset .prefab khớp tên trong project.
            var missingPrefabs = docPrefabs
                .Where(n => !PrefabExists(n))
                .OrderBy(n => n).ToList();

            int scannedScenes = Directory.Exists(Path.Combine(projectRoot, "Assets/_Project/Scenes"))
                ? Directory.GetFiles(Path.Combine(projectRoot, "Assets/_Project/Scenes"), "*.unity", SearchOption.AllDirectories).Length
                : 0;
            int scannedPrefabs = Directory.Exists(Path.Combine(projectRoot, "Assets/_Project"))
                ? Directory.GetFiles(Path.Combine(projectRoot, "Assets/_Project"), "*.prefab", SearchOption.AllDirectories).Length
                : 0;

            string summary =
                $"[ObjectMapValidator] Quét {scannedScenes} scene, {scannedPrefabs} prefab · " +
                $"docs khai báo {docScripts.Count} script/{docPrefabs.Count} prefab · " +
                $"(a) {missingInProject.Count} script trong docs không tồn tại file · " +
                $"(b) {undocumented.Count} script thật chưa đăng ký docs · " +
                $"(c) {missingPrefabs.Count} prefab trong docs không có asset.";

            if (missingInProject.Count + undocumented.Count + missingPrefabs.Count == 0)
                Debug.Log(summary + " — 0 chênh lệch.");
            else
                Debug.LogWarning(summary + " Chỉ báo cáo, KHÔNG tự sửa docs — xem chi tiết bên dưới" +
                                  (writeReportFile ? " hoặc file report." : " (chạy 'Tools/Object Map/Generate Report' để xuất file đầy đủ)."));

            LogList("(a) Script trong docs KHÔNG tồn tại (có thể là kiến trúc ĐÍCH chưa xây, xem object-map.md §12.1)", missingInProject);
            LogList("(b) Script thật CHƯA đăng ký trong object-map.md §3/§4", undocumented);
            LogList("(c) Prefab trong docs KHÔNG có asset khớp tên", missingPrefabs);

            if (writeReportFile)
                WriteReportFile(projectRoot, summary, missingInProject, undocumented, missingPrefabs);
        }

        private static void LogList(string title, List<string> items)
        {
            if (items.Count == 0) return;
            const int PREVIEW = 15;
            var preview = items.Take(PREVIEW);
            string more = items.Count > PREVIEW ? $" … (+{items.Count - PREVIEW}, xem file report)" : string.Empty;
            Debug.LogWarning($"[ObjectMapValidator] {title} ({items.Count}):\n- " + string.Join("\n- ", preview) + more);
        }

        private static void WriteReportFile(string projectRoot, string summary,
            List<string> missingInProject, List<string> undocumented, List<string> missingPrefabs)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# object-map-validation.md — Báo cáo Tools/Object Map/Generate Report");
            sb.AppendLine();
            sb.AppendLine($"> Sinh lúc {DateTime.Now:yyyy-MM-dd HH:mm:ss} · chỉ báo cáo, KHÔNG tự sửa " +
                           "object-map.md hay code — con người quyết định sửa bên nào.");
            sb.AppendLine();
            sb.AppendLine(summary.Replace("[ObjectMapValidator] ", ""));
            sb.AppendLine();
            AppendSection(sb, "(a) Script trong docs không tồn tại file .cs nào trong Assets/", missingInProject);
            AppendSection(sb, "(b) Script thật (gắn trong scene/prefab) chưa đăng ký ở object-map.md §3/§4", undocumented);
            AppendSection(sb, "(c) Prefab nêu trong docs không có asset khớp tên", missingPrefabs);

            string outPath = Path.Combine(projectRoot, "object-map-validation.md");
            File.WriteAllText(outPath, sb.ToString());
            AssetDatabase.Refresh();
            Debug.Log($"[ObjectMapValidator] Đã ghi report: {outPath}");
        }

        private static void AppendSection(StringBuilder sb, string title, List<string> items)
        {
            sb.AppendLine($"## {title} — {items.Count}");
            sb.AppendLine();
            if (items.Count == 0)
            {
                sb.AppendLine("_Không có._");
            }
            else
            {
                foreach (var item in items)
                    sb.AppendLine($"- {item}");
            }
            sb.AppendLine();
        }

        /// <summary>
        /// Đọc mọi bảng markdown có cột tiêu đề chứa "Script" (§3.1-3.3, §4.1-4.3, §6.1-6.3) —
        /// gom token trong dấu backtick ở đúng cột đó. Nếu bảng cũng có cột "Prefab" (§4.*), gom
        /// thêm token cột đó vào danh sách prefab. Không hardcode số cột/tên bảng — bám đúng cấu
        /// trúc markdown thật, để không tự ý tái cấu trúc docs (§A1 "ngoài phạm vi").
        /// </summary>
        private static (Dictionary<string, List<string>> scripts, HashSet<string> prefabs) ParseDocTables(string text)
        {
            var scripts = new Dictionary<string, List<string>>();
            var prefabs = new HashSet<string>();

            var lines = text.Replace("\r\n", "\n").Split('\n');
            int i = 0;
            while (i < lines.Length)
            {
                string line = lines[i];
                if (line.TrimStart().StartsWith("|"))
                {
                    var header = SplitRow(line);
                    int scriptCol = FindColumn(header, "Script");
                    if (scriptCol >= 0)
                    {
                        int prefabCol = FindColumn(header, "Prefab");
                        i++;
                        if (i < lines.Length && lines[i].TrimStart().StartsWith("|") && SeparatorRow.IsMatch(lines[i]))
                            i++;

                        while (i < lines.Length && lines[i].TrimStart().StartsWith("|"))
                        {
                            var cells = SplitRow(lines[i]);
                            if (scriptCol < cells.Count)
                                foreach (Match m in BacktickToken.Matches(cells[scriptCol]))
                                    AddScript(scripts, m.Groups[1].Value);
                            if (prefabCol >= 0 && prefabCol < cells.Count)
                                foreach (Match m in BacktickToken.Matches(cells[prefabCol]))
                                    prefabs.Add(m.Groups[1].Value);
                            i++;
                        }
                        continue;
                    }
                }
                i++;
            }

            return (scripts, prefabs);
        }

        private static void AddScript(Dictionary<string, List<string>> scripts, string name)
        {
            if (!scripts.TryGetValue(name, out var list))
                scripts[name] = list = new List<string>();
            list.Add(name);
        }

        private static List<string> SplitRow(string line)
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("|")) trimmed = trimmed.Substring(1);
            if (trimmed.EndsWith("|")) trimmed = trimmed.Substring(0, trimmed.Length - 1);
            return trimmed.Split('|').Select(c => c.Trim()).ToList();
        }

        private static int FindColumn(List<string> headerCells, string keyword)
        {
            for (int c = 0; c < headerCells.Count; c++)
                if (headerCells[c].Contains(keyword)) return c;
            return -1;
        }

        /// <summary>Quét mọi file khớp pattern dưới folder, gom GUID script → tên class + asset path.</summary>
        private static void ScanScriptRefs(string folder, string pattern, Dictionary<string, string> outMap)
        {
            if (!Directory.Exists(folder)) return;
            foreach (var file in Directory.GetFiles(folder, pattern, SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(file);
                foreach (Match m in ScriptGuidRegex.Matches(text))
                {
                    string guid = m.Groups[1].Value;
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(".cs")) continue;
                    outMap[Path.GetFileNameWithoutExtension(assetPath)] = assetPath;
                }
            }
        }

        private static Dictionary<string, string> CollectAllScriptFileNames()
        {
            var map = new Dictionary<string, string>();
            string assetsRoot = Application.dataPath;
            foreach (var file in Directory.GetFiles(assetsRoot, "*.cs", SearchOption.AllDirectories))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                string rel = "Assets" + file.Substring(assetsRoot.Length).Replace('\\', '/');
                map[name] = rel;
            }
            return map;
        }

        private static bool PrefabExists(string name)
        {
            foreach (var guid in AssetDatabase.FindAssets($"{name} t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == name) return true;
            }
            return false;
        }
    }
}
