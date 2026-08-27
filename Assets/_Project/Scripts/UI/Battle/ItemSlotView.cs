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

            _label = NewText("Label", rt, TextAlignmentOptions.Center);
            _label.rectTransform.anchorMin = new Vector2(0.06f, 0.28f);
            _label.rectTransform.anchorMax = new Vector2(0.94f, 0.92f);
            _label.rectTransform.offsetMin = _label.rectTransform.offsetMax = Vector2.zero;
            _label.fontSize = rt.sizeDelta.x * 0.18f;
            _label.enableWordWrapping = true;

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
                SetState(false, false);
                return;
            }

            var def = ItemCatalog.Get(type.Value);
            _label.text = def.Name;
            _count.text = remaining > 0 ? $"x{remaining}" : "";
            SetState(remaining > 0, isSelected);
        }

        private void SetState(bool available, bool selected)
        {
            Interactable = available;
            _border.color = selected ? BORDER_SELECTED : available ? BORDER_NORMAL : BORDER_DISABLED;
            _label.color = available ? TEXT_NORMAL : TEXT_DISABLED;
            transform.localScale = selected ? Vector3.one * 1.08f : Vector3.one;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!Interactable) return;
            OnClicked?.Invoke(this);
        }
    }
}
