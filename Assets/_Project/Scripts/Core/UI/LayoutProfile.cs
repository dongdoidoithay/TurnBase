using System;
using UnityEngine;

namespace Game.Core.UI
{
    /// <summary>
    /// Preset RectTransform theo hướng màn hình — P6 responsive (roadmap.md Tuần 24,
    /// task-phase-5-gaps.md Phần E). Đủ field để tái tạo mọi vị trí/kích thước hand-tuned hiện có
    /// trong project (mọi UI trong game này đều dựng bằng anchor+anchoredPosition+sizeDelta, không
    /// dùng LayoutGroup — xem object-map.md §12.1).
    /// </summary>
    [Serializable]
    public struct LayoutProfile
    {
        public string Name;
        public Vector2 AnchorMin;
        public Vector2 AnchorMax;
        public Vector2 Pivot;
        public Vector2 AnchoredPosition;
        public Vector2 SizeDelta;
        public Vector3 Scale;

        /// <summary>Chụp lại preset từ 1 RectTransform sống — dùng để lấy giá trị Portrait "giữ
        /// nguyên số liệu hiện tại" mà không phải chép tay từng con số (rủi ro gõ sai).</summary>
        public static LayoutProfile CaptureFrom(RectTransform rt, string name)
        {
            return new LayoutProfile
            {
                Name = name,
                AnchorMin = rt.anchorMin,
                AnchorMax = rt.anchorMax,
                Pivot = rt.pivot,
                AnchoredPosition = rt.anchoredPosition,
                SizeDelta = rt.sizeDelta,
                Scale = rt.localScale,
            };
        }

        public readonly void ApplyTo(RectTransform rt)
        {
            rt.anchorMin = AnchorMin;
            rt.anchorMax = AnchorMax;
            rt.pivot = Pivot;
            rt.anchoredPosition = AnchoredPosition;
            rt.sizeDelta = SizeDelta;
            rt.localScale = Scale;
        }
    }
}
