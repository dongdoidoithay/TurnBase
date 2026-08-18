using System.Runtime.CompilerServices;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Meta.Accessibility
{
    /// <summary>
    /// task-accessibility.md, plan.md §10.7 — <c>SettingsDto.TextScale</c> đã tồn tại từ đầu dự án
    /// (100-150%, clamp sẵn ở <see cref="Game.Services.Settings.SettingsService.Apply"/>) nhưng
    /// chưa từng có nơi ĐỌC giá trị này — đúng mẫu "hạ tầng có sẵn, chưa dùng" lặp lại nhiều lần
    /// trong dự án. Class này đọc/áp thật: quét ĐỆ QUY mọi <see cref="Text"/>/
    /// <see cref="TextMeshProUGUI"/> dưới 1 root, nhân fontSize gốc theo scale hiện tại — nhớ SIZE
    /// GỐC qua <see cref="ConditionalWeakTable{TKey,TValue}"/> (không cần thêm component nào, không
    /// leak vì key tự GC khi component bị huỷ) để gọi lặp lại không CỘNG DỒN nhân nhiều lần.
    /// Đặt ở <c>Game.Meta</c> (không phải <c>Game.Core.UI</c>) vì cần <c>Unity.TextMeshPro</c> —
    /// <c>Game.Core</c> không tham chiếu package này; <c>Game.UI</c> (BattleHudScreen) được phép
    /// tham chiếu ngược <c>Game.Meta</c> nên vẫn dùng được từ đó.
    /// </summary>
    public static class TextScaleApplier
    {
        private static readonly ConditionalWeakTable<Text, object> _baseSizeLegacy = new();
        private static readonly ConditionalWeakTable<TextMeshProUGUI, object> _baseSizeTmp = new();

        public static void Apply(Transform root, float scale)
        {
            if (root == null) return;

            var legacyTexts = root.GetComponentsInChildren<Text>(true);
            foreach (var t in legacyTexts)
            {
                if (!_baseSizeLegacy.TryGetValue(t, out var boxed))
                {
                    boxed = (float)t.fontSize;
                    _baseSizeLegacy.Add(t, boxed);
                }
                t.fontSize = Mathf.Max(1, Mathf.RoundToInt((float)boxed * scale));
            }

            var tmpTexts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in tmpTexts)
            {
                if (!_baseSizeTmp.TryGetValue(t, out var boxed))
                {
                    boxed = t.fontSize;
                    _baseSizeTmp.Add(t, boxed);
                }
                t.fontSize = Mathf.Max(1f, (float)boxed * scale);
            }
        }
    }
}
