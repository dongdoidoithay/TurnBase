using UnityEngine;

namespace Game.Core.UI
{
    /// <summary>
    /// Áp <see cref="LayoutProfile"/> Portrait/Landscape lên 1 RectTransform theo tỉ lệ màn hình
    /// thật — nền tảng responsive P6 (roadmap.md Tuần 24, task-phase-5-gaps.md Phần E). Đặt ở
    /// <c>Game.Core</c> (không phải <c>Game.UI</c> như bản nháp task file ban đầu) vì
    /// <c>Game.Meta</c> KHÔNG được phép tham chiếu <c>Game.UI</c> (structure.md §6,
    /// AssemblyRuleTests) trong khi 1 trong 3 màn pilot (SettingsScreen) nằm ở Game.Meta — đúng
    /// precedent <see cref="IUiRootHost"/> đã dùng cho lý do y hệt. Cố ý KHÔNG đổi
    /// <c>CanvasScaler.referenceResolution</c> (mọi Canvas trong game vẫn cố định 960×540) — chỉ
    /// thêm 1 lớp preset cho TỪNG RectTransform con, theo đúng phạm vi pilot (§E1 "ngoài phạm vi:
    /// áp toàn bộ 23 màn").
    /// </summary>
    [ExecuteAlways]
    public sealed class LayoutProfileSwitcher : MonoBehaviour
    {
        [SerializeField] private RectTransform _target;
        [SerializeField] private LayoutProfile _portrait;
        [SerializeField] private LayoutProfile _landscape;

        private bool _hasApplied;
        private bool _lastIsLandscape;

        private void Reset()
        {
            _target = GetComponent<RectTransform>();
        }

        private void OnEnable() => Apply(force: true);

        // [ExecuteAlways] để nhà thiết kế thấy kết quả ngay khi đổi kích thước Game View trong
        // Editor, không chỉ lúc Play — đúng scope E2 "xem trước trong Editor khi xoay".
        private void Update() => Apply(force: false);

        /// <summary>Gán target + 2 profile thật từ code (màn code-dựng như BattleHudScreen/
        /// SettingsScreen). Với màn tĩnh (TitleCanvas), gán trực tiếp qua Inspector.</summary>
        public void SetProfiles(RectTransform target, LayoutProfile portrait, LayoutProfile landscape)
        {
            _target = target;
            _portrait = portrait;
            _landscape = landscape;
            Apply(force: true);
        }

        private void Apply(bool force)
        {
            if (_target == null) return;
            bool isLandscape = IsLandscape(Screen.width, Screen.height);
            if (!force && _hasApplied && isLandscape == _lastIsLandscape) return;
            _hasApplied = true;
            _lastIsLandscape = isLandscape;
            PickProfile(Screen.width, Screen.height, _portrait, _landscape).ApplyTo(_target);
        }

        /// <summary>Hàm thuần — không phụ thuộc Unity runtime, test được với bất kỳ tỉ lệ nào
        /// (ResponsiveLayoutTests: 9:16, 3:4, 16:9, 20:9, 21:9, 1:1).</summary>
        public static bool IsLandscape(int width, int height) => width > height;

        /// <summary>Hàm thuần theo đúng chữ ký task-phase-5-gaps.md §E1.</summary>
        public static LayoutProfile PickProfile(int width, int height, LayoutProfile portrait, LayoutProfile landscape)
            => IsLandscape(width, height) ? landscape : portrait;
    }
}
