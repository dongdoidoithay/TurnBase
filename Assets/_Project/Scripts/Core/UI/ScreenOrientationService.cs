using System;
using UnityEngine;

namespace Game.Core.UI
{
    /// <summary>
    /// object-map.md §3 mô tả kiến trúc đích có <c>ScreenOrientationService</c>/
    /// <c>E-ORIENTATION_CHANGED</c> — trước đây chưa xây, mỗi <see cref="LayoutProfileSwitcher"/>
    /// tự poll <c>Screen.width/height</c> trong <c>Update()</c> riêng (N panel = N lần đọc
    /// Screen mỗi frame, hầu như luôn không đổi). Service này đọc ĐÚNG 1 LẦN/frame, chỉ bắn sự
    /// kiện khi hướng màn hình thực sự lật — mọi <see cref="LayoutProfileSwitcher"/> đăng ký nghe
    /// thay vì tự poll khi đang chạy thật (Play mode/thiết bị).
    /// </summary>
    public interface IScreenOrientationService
    {
        bool IsLandscape { get; }
        event Action<bool> OnOrientationChanged;
    }

    public sealed class ScreenOrientationService : MonoBehaviour, IScreenOrientationService
    {
        public bool IsLandscape { get; private set; }
        public event Action<bool> OnOrientationChanged;

        /// <summary>Cùng mẫu <see cref="Game.Services.Audio.AudioService.Create"/> — 1 GameObject
        /// con của serviceRoot, không tồn tại rời rạc.</summary>
        public static ScreenOrientationService Create(Transform parent)
        {
            var go = new GameObject("ScreenOrientationService");
            go.transform.SetParent(parent, false);
            return go.AddComponent<ScreenOrientationService>();
        }

        private void Awake()
        {
            IsLandscape = LayoutProfileSwitcher.IsLandscape(Screen.width, Screen.height);
        }

        private void Update()
        {
            bool now = LayoutProfileSwitcher.IsLandscape(Screen.width, Screen.height);
            if (now == IsLandscape) return;
            IsLandscape = now;
            OnOrientationChanged?.Invoke(now);
        }
    }
}
