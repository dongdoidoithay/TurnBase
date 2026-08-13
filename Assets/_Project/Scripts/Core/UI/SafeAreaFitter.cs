using UnityEngine;

namespace Game.Core.UI
{
    /// <summary>
    /// Co RectTransform (thường là gốc nội dung, con trực tiếp của Canvas) về đúng
    /// <see cref="Screen.safeArea"/> — tránh notch/tai thỏ/viền bo. task-phase-5-gaps.md Phần E.
    /// Cùng lý do đặt ở <c>Game.Core</c> như <see cref="LayoutProfileSwitcher"/> (xem doc-comment
    /// class đó) — không phải <c>Game.UI</c>.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rect;
        private Rect _lastSafeArea;
        private Vector2 _lastScreenSize;
        private bool _hasApplied;

        private void OnEnable()
        {
            _rect = GetComponent<RectTransform>();
            Apply(force: true);
        }

        private void Update() => Apply(force: false);

        private void Apply(bool force)
        {
            if (_rect == null) return;
            var safeArea = Screen.safeArea;
            var screenSize = new Vector2(Screen.width, Screen.height);
            if (!force && _hasApplied && safeArea == _lastSafeArea && screenSize == _lastScreenSize) return;
            _hasApplied = true;
            _lastSafeArea = safeArea;
            _lastScreenSize = screenSize;

            var normalized = GetSafeAreaRect(new Rect(Vector2.zero, screenSize), safeArea);
            _rect.anchorMin = normalized.position;
            _rect.anchorMax = normalized.position + normalized.size;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Hàm thuần — quy <paramref name="safeArea"/> (toạ độ pixel, gốc dưới-trái, cùng hệ
        /// <see cref="Screen.safeArea"/>) thành anchor chuẩn hoá 0..1 trong <paramref name="screen"/>.
        /// Trả về Rect mà <c>position</c> = anchorMin, <c>position+size</c> = anchorMax — test được
        /// với 5 tỉ lệ + notch giả lập mà không cần thiết bị thật (ResponsiveLayoutTests).
        /// </summary>
        public static Rect GetSafeAreaRect(Rect screen, Rect safeArea)
        {
            if (screen.width <= 0f || screen.height <= 0f) return new Rect(0, 0, 1, 1);

            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;
            anchorMin.x /= screen.width;
            anchorMin.y /= screen.height;
            anchorMax.x /= screen.width;
            anchorMax.y /= screen.height;
            return new Rect(anchorMin, anchorMax - anchorMin);
        }
    }
}
