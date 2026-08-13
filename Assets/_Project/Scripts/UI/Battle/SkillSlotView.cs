using Game.Combat.Model;
using Game.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI.Battle
{
    /// <summary>Một ô trong Skill Grid — 8 trạng thái hiển thị theo plan.md §5.5.</summary>
    public sealed class SkillSlotView : MonoBehaviour, IPointerClickHandler
    {
        public enum SlotState
        {
            Available, OnCooldown, NotEnoughSp, Silenced,
            UltimateCharging, UltimateReady, Selected, Empty
        }

        // Màu theo references/palette.md
        private static readonly Color BORDER_NORMAL = new(0.357f, 0.290f, 0.365f);
        private static readonly Color BORDER_SELECTED = new(0.949f, 0.910f, 0.810f);
        private static readonly Color BORDER_ULTIMATE = new(1f, 0.820f, 0.400f);
        private static readonly Color FILL_NORMAL = new(0.169f, 0.106f, 0.180f, 0.95f);
        private static readonly Color FILL_DISABLED = new(0.05f, 0.03f, 0.05f, 0.95f);
        private static readonly Color TEXT_NORMAL = new(0.949f, 0.910f, 0.810f);
        private static readonly Color TEXT_DISABLED = new(0.36f, 0.33f, 0.38f);
        private static readonly Color SP_ENOUGH = new(0.271f, 0.482f, 0.616f);
        private static readonly Color SP_SHORT = new(0.902f, 0.224f, 0.275f);

        private Image _border;
        private Image _fill;
        private Image _icon;
        private TextMeshProUGUI _label;
        private TextMeshProUGUI _cost;
        private TextMeshProUGUI _cooldown;
        private Image _elementTint;

        public int SlotIndex { get; private set; }
        public SkillRuntime Skill { get; private set; }
        public SlotState State { get; private set; } = SlotState.Empty;
        public bool Interactable => State is SlotState.Available or SlotState.UltimateReady
                                              or SlotState.Selected;

        public System.Action<SkillSlotView> OnClicked;

        // =====================================================================

        public static SkillSlotView Create(Transform parent, int index, float size)
        {
            var go = new GameObject($"SkillSlot_{index}", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(size, size);

            var slot = go.AddComponent<SkillSlotView>();
            slot.SlotIndex = index;
            slot.Build(rt, size);
            return slot;
        }

        private void Build(RectTransform rt, float size)
        {
            _border = gameObject.AddComponent<Image>();
            _border.color = BORDER_NORMAL;

            _fill = NewImage("Fill", rt, FILL_NORMAL, 3f);
            _elementTint = NewImage("ElementTint", rt, new Color(1, 1, 1, 0f), 3f);

            // Icon hành động chiếm phần lớn ô — trước đây ô chỉ có 2 chữ cái viết tắt
            // (VD "BA","FC"), không đọc được skill là loại gì (đánh/heal/buff/AoE...).
            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(rt, false);
            var iconRt = (RectTransform)iconGo.transform;
            iconRt.anchorMin = new Vector2(0.14f, 0.22f);
            iconRt.anchorMax = new Vector2(0.86f, 0.94f);
            iconRt.offsetMin = iconRt.offsetMax = Vector2.zero;
            _icon = iconGo.AddComponent<Image>();
            _icon.preserveAspect = true;
            _icon.raycastTarget = false;
            _icon.enabled = false;

            // Chữ viết tắt lùi thành badge nhỏ góc trên — phụ trợ debug/phân biệt, icon mới
            // là yếu tố đọc chính.
            _label = NewText("Label", rt, size * 0.16f, TextAlignmentOptions.Top);
            _label.rectTransform.anchorMin = new Vector2(0, 0.84f);
            _label.rectTransform.anchorMax = new Vector2(1, 1f);
            _label.rectTransform.offsetMin = _label.rectTransform.offsetMax = Vector2.zero;

            // Badge chi phí SP ở góc dưới-trái
            _cost = NewText("Cost", rt, size * 0.2f, TextAlignmentOptions.BottomLeft);
            _cost.rectTransform.anchorMin = new Vector2(0.08f, 0.02f);
            _cost.rectTransform.anchorMax = new Vector2(0.6f, 0.24f);
            _cost.rectTransform.offsetMin = _cost.rectTransform.offsetMax = Vector2.zero;

            // Số cooldown to đè giữa ô
            _cooldown = NewText("Cooldown", rt, size * 0.5f, TextAlignmentOptions.Center);
            _cooldown.rectTransform.anchorMin = Vector2.zero;
            _cooldown.rectTransform.anchorMax = Vector2.one;
            _cooldown.rectTransform.offsetMin = _cooldown.rectTransform.offsetMax = Vector2.zero;
            _cooldown.color = TEXT_NORMAL;
            _cooldown.gameObject.SetActive(false);
        }

        private static Image NewImage(string name, RectTransform parent, Color color, float inset)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static TextMeshProUGUI NewText(string name, RectTransform parent,
                                               float fontSize, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = TEXT_NORMAL;
            t.raycastTarget = false;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        // =====================================================================

        public void Bind(SkillRuntime skill)
        {
            Skill = skill;

            if (skill == null)
            {
                _label.text = "";
                _cost.text = "";
                _icon.enabled = false;
                SetState(SlotState.Empty);
                return;
            }

            _label.text = ShortName(skill.Data);
            _cost.text = skill.Data.SpCost > 0 ? skill.Data.SpCost.ToString() : "";
            _elementTint.color = ElementColor(skill.Data.Element);

            var sprite = LoadIcon(IconKeyFor(skill.Data, SlotIndex));
            _icon.sprite = sprite;
            _icon.enabled = sprite != null;
        }

        // =====================================================================
        // Icon hành động — suy ra từ field skill sẵn có (không cần cột iconKey riêng
        // trong CSV). Ô Ultimate (slot 4) luôn dùng icon riêng cho dễ nhận ra ngay.
        // =====================================================================

        private static readonly System.Collections.Generic.Dictionary<string, Sprite> _iconCache = new();

        private static string IconKeyFor(SkillData d, int slotIndex)
        {
            if (slotIndex == 4) return "ultimate";
            if (d.CleanseCount > 0 || d.DispelCount > 0) return "cleanse";
            if (d.Type == SkillType.Heal || d.HealPower > 0f || d.RevivePercent > 0f) return "heal";
            if (d.ShieldPower > 0f) return "shield";
            if (HasStatus(d, StatusId.SpdUp)) return "haste";
            if (d.IsAoe) return "aoe_burst";
            if (d.Type == SkillType.Magical) return "magic_bolt";
            if (d.IsBreaker || d.PowerMultiplier >= 1.4f) return "power_strike";
            return "slash";
        }

        private static bool HasStatus(SkillData d, StatusId id)
        {
            for (int i = 0; i < d.Applies.Length; i++)
                if (d.Applies[i].Status == id) return true;
            return false;
        }

        private static Sprite LoadIcon(string key)
        {
            if (_iconCache.TryGetValue(key, out var cached)) return cached;
            var sprite = Resources.Load<Sprite>($"Art/UI/Icons/Skills/icon_skill_{key}");
            _iconCache[key] = sprite;
            return sprite;
        }

        /// <summary>Cập nhật trạng thái hiển thị theo tình hình hiện tại của unit.</summary>
        public void Refresh(CombatUnit actor, bool isSelected, bool ultimateReady)
        {
            if (Skill == null) { SetState(SlotState.Empty); return; }
            if (isSelected) { SetState(SlotState.Selected); return; }

            if (SlotIndex == 4)   // ô Ultimate
            {
                SetState(ultimateReady ? SlotState.UltimateReady : SlotState.UltimateCharging);
                return;
            }

            if (Skill.CooldownLeft > 0) { SetState(SlotState.OnCooldown); return; }
            if (actor != null && actor.IsSilenced() && SlotIndex != 0) { SetState(SlotState.Silenced); return; }
            if (actor != null && Skill.Data.SpCost > actor.Sp) { SetState(SlotState.NotEnoughSp); return; }

            SetState(SlotState.Available);
        }

        private void SetState(SlotState state)
        {
            State = state;

            bool showCooldown = state == SlotState.OnCooldown;
            _cooldown.gameObject.SetActive(showCooldown);
            if (showCooldown) _cooldown.text = Skill.CooldownLeft.ToString();

            switch (state)
            {
                case SlotState.Available:
                    _border.color = BORDER_NORMAL;
                    _fill.color = FILL_NORMAL;
                    _label.color = TEXT_NORMAL;
                    _icon.color = TEXT_NORMAL;
                    _cost.color = SP_ENOUGH;
                    transform.localScale = Vector3.one;
                    break;

                case SlotState.Selected:
                    _border.color = BORDER_SELECTED;
                    _fill.color = FILL_NORMAL;
                    _label.color = TEXT_NORMAL;
                    _icon.color = TEXT_NORMAL;
                    _cost.color = SP_ENOUGH;
                    transform.localScale = Vector3.one * 1.08f;
                    break;

                case SlotState.OnCooldown:
                    _border.color = BORDER_NORMAL;
                    _fill.color = FILL_DISABLED;
                    _label.color = TEXT_DISABLED;
                    _icon.color = TEXT_DISABLED;
                    _cost.color = TEXT_DISABLED;
                    transform.localScale = Vector3.one;
                    break;

                case SlotState.NotEnoughSp:
                    _border.color = BORDER_NORMAL;
                    _fill.color = FILL_DISABLED;
                    _label.color = TEXT_DISABLED;
                    _icon.color = TEXT_DISABLED;
                    _cost.color = SP_SHORT;      // badge SP đỏ = thiếu SP
                    transform.localScale = Vector3.one;
                    break;

                case SlotState.Silenced:
                    _border.color = new Color(0.6f, 0.2f, 0.25f);
                    _fill.color = FILL_DISABLED;
                    _label.color = TEXT_DISABLED;
                    _icon.color = TEXT_DISABLED;
                    transform.localScale = Vector3.one;
                    break;

                case SlotState.UltimateCharging:
                    _border.color = BORDER_NORMAL;
                    _fill.color = FILL_DISABLED;
                    // Không dùng TEXT_DISABLED ở đây — nền tối + tint nguyên tố (VD Dark, alpha
                    // 0.28) đã rất tối, chữ/icon mờ theo TEXT_DISABLED gần như biến mất (slot
                    // Ultimate trông trống trơn dù có skill). Đây là slot quan trọng nhất trên
                    // HUD nên phải luôn đọc được, kể cả khi chưa sẵn sàng.
                    _label.color = TEXT_NORMAL;
                    _icon.color = TEXT_NORMAL;
                    transform.localScale = Vector3.one;
                    break;

                case SlotState.UltimateReady:
                    // Nhấp nháy 1 Hz — dấu hiệu quan trọng nhất trên HUD
                    float pulse = Mathf.PingPong(Time.time, 0.5f) / 0.5f;
                    _border.color = Color.Lerp(BORDER_ULTIMATE, Color.white, pulse);
                    _fill.color = FILL_NORMAL;
                    _label.color = BORDER_ULTIMATE;
                    _icon.color = Color.Lerp(BORDER_ULTIMATE, Color.white, pulse);
                    transform.localScale = Vector3.one;
                    break;

                case SlotState.Empty:
                    _border.color = new Color(0.15f, 0.12f, 0.16f);
                    _fill.color = FILL_DISABLED;
                    _label.color = TEXT_DISABLED;
                    _icon.color = TEXT_DISABLED;
                    _elementTint.color = new Color(1, 1, 1, 0f);
                    transform.localScale = Vector3.one;
                    break;
            }
        }

        private void Update()
        {
            if (State == SlotState.UltimateReady) SetState(SlotState.UltimateReady);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!Interactable) return;
            OnClicked?.Invoke(this);
        }

        // =====================================================================

        private static string ShortName(SkillData d)
        {
            if (string.IsNullOrEmpty(d.Id)) return "?";
            // "skill_elemental_strike" → "EL"
            var parts = d.Id.Replace("skill_", "").Split('_');
            if (parts.Length >= 2)
                return (parts[0][..1] + parts[1][..1]).ToUpperInvariant();
            return parts[0].Length >= 2 ? parts[0][..2].ToUpperInvariant() : parts[0].ToUpperInvariant();
        }

        private static Color ElementColor(Element e) => e switch
        {
            Element.Fire  => new Color(0.902f, 0.224f, 0.275f, 0.28f),
            Element.Water => new Color(0.271f, 0.482f, 0.616f, 0.28f),
            Element.Earth => new Color(0.651f, 0.443f, 0.259f, 0.28f),
            Element.Wind  => new Color(0.482f, 0.788f, 0.314f, 0.28f),
            Element.Light => new Color(1.000f, 0.820f, 0.400f, 0.28f),
            Element.Dark  => new Color(0.608f, 0.365f, 0.898f, 0.28f),
            _ => new Color(1, 1, 1, 0f)
        };
    }
}
