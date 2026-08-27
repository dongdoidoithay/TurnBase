using System.Collections.Generic;
using Game.Data;
using Game.Meta.Items;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI.Battle
{
    /// <summary>task-consumable-items.md — 1 ô trong hàng item của Skill Grid (hàng 2, dưới hàng
    /// skill). Sibling ĐƠN GIẢN HOÁ của <see cref="SkillSlotView"/> — item không có SP cost/
    /// cooldown/element, chỉ cần tên + số lượng còn lại.</summary>
    public sealed class ItemSlotView : MonoBehaviour, IPointerClickHandler
    {
        private static readonly Color BORDER_NORMAL = Color.white;
        private static readonly Color BORDER_SELECTED = new(1.15f, 1.1f, 1f);
        private static readonly Color BORDER_DISABLED = new(0.45f, 0.45f, 0.45f);
        private static readonly Color TEXT_NORMAL = new(0.949f, 0.910f, 0.810f);
        private static readonly Color TEXT_DISABLED = new(0.36f, 0.33f, 0.38f);
        private static readonly Color COUNT_COLOR = new(1f, 0.820f, 0.400f);

        private static Sprite _slotSprite;

        private Image _border;
        private Image _icon;
        private TextMeshProUGUI _label;
        private TextMeshProUGUI _count;

        public int SlotIndex { get; private set; }
        public ItemType? Type { get; private set; }
        public bool Interactable { get; private set; }

        public System.Action<ItemSlotView> OnClicked;

        public static ItemSlotView Create(Transform parent, int index, float size)
        {
            var go = new GameObject($"ItemSlot_{index}", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(size, size);

            var slot = go.AddComponent<ItemSlotView>();
            slot.SlotIndex = index;
            slot.Build(rt);
            return slot;
        }

        private void Build(RectTransform rt)
        {
            if (_slotSprite == null) _slotSprite = Resources.Load<Sprite>("Art/UI/Chrome/icon_slot_brown");

            _border = gameObject.AddComponent<Image>();
            _border.sprite = _slotSprite;
            _border.type = Image.Type.Sliced;
            _border.color = BORDER_NORMAL;

            // task "thêm avatar thật cho item slot trong INV." — icon THẬT chiếm phần lớn ô, thay
            // tên đầy đủ ("Smoke Bomb"...) không đọc nổi ở ô 30px vuông. Tên rút gọn lùi thành
            // badge nhỏ dải trên, cùng cách SkillSlotView đã làm cho hàng skill.
            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(rt, false);
            var iconRt = (RectTransform)iconGo.transform;
            iconRt.anchorMin = new Vector2(0.14f, 0.16f);
            iconRt.anchorMax = new Vector2(0.86f, 0.82f);
            iconRt.offsetMin = iconRt.offsetMax = Vector2.zero;
            _icon = iconGo.AddComponent<Image>();
            _icon.preserveAspect = true;
            _icon.raycastTarget = false;
            _icon.enabled = false;

            _label = NewText("Label", rt, TextAlignmentOptions.Top);
            _label.rectTransform.anchorMin = new Vector2(0f, 0.82f);
            _label.rectTransform.anchorMax = new Vector2(1f, 1f);
            _label.rectTransform.offsetMin = _label.rectTransform.offsetMax = Vector2.zero;
            _label.fontSize = rt.sizeDelta.x * 0.16f;
            _label.enableWordWrapping = false;
            _label.overflowMode = TextOverflowModes.Overflow;

            _count = NewText("Count", rt, TextAlignmentOptions.BottomRight);
            _count.rectTransform.anchorMin = new Vector2(0.4f, 0.02f);
            _count.rectTransform.anchorMax = new Vector2(0.94f, 0.26f);
            _count.rectTransform.offsetMin = _count.rectTransform.offsetMax = Vector2.zero;
            _count.fontSize = rt.sizeDelta.x * 0.22f;
            _count.color = COUNT_COLOR;
        }

        private static TextMeshProUGUI NewText(string name, RectTransform parent, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.alignment = align;
            t.color = TEXT_NORMAL;
            t.raycastTarget = false;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        /// <summary>Trống (không mang loại này) hoặc <paramref name="remaining"/> &lt;= 0 → ô
        /// trống, không click được.</summary>
        public void Bind(ItemType? type, int remaining, bool isSelected)
        {
            Type = type;
            if (type == null)
            {
                _label.text = "";
                _count.text = "";
                _icon.enabled = false;
                SetState(false, false);
                return;
            }

            var def = ItemCatalog.Get(type.Value);
            _label.text = ShortName(def.Name);
            _count.text = remaining > 0 ? $"x{remaining}" : "";

            var sprite = LoadIcon(IconKeyFor(type.Value));
            _icon.sprite = sprite;
            _icon.enabled = sprite != null;

            SetState(remaining > 0, isSelected);
        }

        // =====================================================================
        // Icon thật cho từng loại item — task "thêm avatar thật cho item slot trong INV."
        // (trước đây chỉ có tên chữ, không icon nào). Art có sẵn từ trước ở
        // Assets/_Project/Art/UI/Icons/Items/prop_*/*_00.png (KHÔNG ở Resources/, không
        // Resources.Load được trực tiếp) — copy 1 bản sang Resources/ với path/tên mới
        // `icon_item_{key}`, viết lại `.meta` sạch (spriteMode Single đúng nghĩa — file gốc có
        // `spriteSheet` 2 sub-rect thừa từ pipeline cũ, có thể khiến Resources.Load trả về đúng 1
        // mảnh vỡ thay vì icon đầy đủ). Dùng frame `_00` của mỗi loại — `_01`/`_02` là frame
        // "vỡ/tan" phụ, không phải icon tĩnh mặc định.
        // =====================================================================

        private static readonly Dictionary<string, Sprite> _iconCache = new();

        private static string IconKeyFor(ItemType type) => type switch
        {
            ItemType.Potion        => "potion",
            ItemType.Ether         => "ether",
            ItemType.Antidote      => "antidote",
            ItemType.SmokeBomb     => "smoke_bomb",
            ItemType.ReviveFeather => "revive_feather",
            ItemType.ElementalBomb => "elemental_bomb",
            _ => "potion"
        };

        private static Sprite LoadIcon(string key)
        {
            if (_iconCache.TryGetValue(key, out var cached)) return cached;
            var sprite = Resources.Load<Sprite>($"Art/UI/Icons/Items/icon_item_{key}");
            _iconCache[key] = sprite;
            return sprite;
        }

        private static string ShortName(string name)
        {
            var parts = name.Split(' ');
            return parts.Length >= 2 ? (parts[0][..1] + parts[1][..1]).ToUpperInvariant()
                                      : (name.Length >= 2 ? name[..2].ToUpperInvariant() : name.ToUpperInvariant());
        }

        private void SetState(bool available, bool selected)
        {
            Interactable = available;
            _border.color = selected ? BORDER_SELECTED : available ? BORDER_NORMAL : BORDER_DISABLED;
            _label.color = available ? TEXT_NORMAL : TEXT_DISABLED;
            _icon.color = available ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.6f);
            transform.localScale = selected ? Vector3.one * 1.08f : Vector3.one;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!Interactable) return;
            OnClicked?.Invoke(this);
        }
    }
}
