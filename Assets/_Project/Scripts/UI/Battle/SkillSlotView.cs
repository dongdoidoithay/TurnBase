using Game.Combat.Model;
using Game.Core;
using Game.Data;
using Game.Services.Settings;
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
        private static readonly Color BORDER_NORMAL = Color.white;
        private static readonly Color BORDER_ULTIMATE = new(1f, 0.820f, 0.400f);
        private static readonly Color TEXT_NORMAL = new(0.949f, 0.910f, 0.810f);
        private static readonly Color TEXT_DISABLED = new(0.36f, 0.33f, 0.38f);
        private static readonly Color SP_ENOUGH = new(0.271f, 0.482f, 0.616f);
        private static readonly Color SP_SHORT = new(0.902f, 0.224f, 0.275f);

        // Thẻ bài kiểu Art_Sample (task-ui-chrome-popups.md) — 3 sprite thay cho khối màu phẳng
        // trước đây, tự chứa cả border+fill nên không cần lớp "_fill" riêng nữa.
        private static Sprite _cardNormal, _cardSelected, _cardDisabled, _cooldownBadgeSprite;
        private static void LoadCardSprites()
        {
            if (_cardNormal != null) return;
            _cardNormal = Resources.Load<Sprite>("Art/UI/Chrome/card_gold");
            _cardSelected = Resources.Load<Sprite>("Art/UI/Chrome/card_gold_selected");
            _cardDisabled = Resources.Load<Sprite>("Art/UI/Chrome/card_gold_disabled");
            _cooldownBadgeSprite = Resources.Load<Sprite>("Art/UI/Chrome/cooldown_badge");
        }

        private Image _border;
        private Image _icon;
        private TextMeshProUGUI _label;
        private TextMeshProUGUI _cost;
        private TextMeshProUGUI _cooldown;
        private GameObject _cooldownBadge;
        private Image _elementTint;
        /// <summary>task-accessibility-part2.md, plan.md §10.7 — "icon nguyên tố có hình dạng khác
        /// nhau, không chỉ khác màu" cho chế độ mù màu. KHÔNG có art sprite riêng theo nguyên tố
        /// trong dự án (thư mục Art/UI/Icons/Elements rỗng) — dùng glyph ký tự hình khối phân biệt
        /// rõ (● ▲ ▼ ■ ◆ ★ ✚) thay vì màu, chỉ hiện khi bật ColorblindMode.</summary>
        private TextMeshProUGUI _elementGlyph;
        private ISettingsService _settings;

        public int SlotIndex { get; private set; }
        public SkillRuntime Skill { get; private set; }
        public SlotState State { get; private set; } = SlotState.Empty;
        public bool Interactable => State is SlotState.Available or SlotState.UltimateReady
                                              or SlotState.Selected;

        public System.Action<SkillSlotView> OnClicked;

        // =====================================================================

        public static SkillSlotView Create(Transform parent, int index, float width, float height)
        {
            var go = new GameObject($"SkillSlot_{index}", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(width, height);

            var slot = go.AddComponent<SkillSlotView>();
            slot.SlotIndex = index;
            slot.Build(rt, Mathf.Min(width, height));
            return slot;
        }

        private void Build(RectTransform rt, float size)
        {
            ServiceLocator.TryGet(out _settings);
            LoadCardSprites();

            _border = gameObject.AddComponent<Image>();
            _border.sprite = _cardNormal;
            _border.type = Image.Type.Sliced;
            _border.color = BORDER_NORMAL;

            _elementTint = NewImage("ElementTint", rt, new Color(1, 1, 1, 0f), 6f);

            // Icon hành động chiếm phần lớn ô — trước đây ô chỉ có 2 chữ cái viết tắt
            // (VD "BA","FC"), không đọc được skill là loại gì (đánh/heal/buff/AoE...).
            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(rt, false);
            var iconRt = (RectTransform)iconGo.transform;
            iconRt.anchorMin = new Vector2(0.12f, 0.30f);
            iconRt.anchorMax = new Vector2(0.88f, 0.92f);
            iconRt.offsetMin = iconRt.offsetMax = Vector2.zero;
            _icon = iconGo.AddComponent<Image>();
            _icon.preserveAspect = true;
            _icon.raycastTarget = false;
            _icon.enabled = false;

            // Chữ viết tắt lùi thành badge nhỏ góc trên — phụ trợ debug/phân biệt, icon mới
            // là yếu tố đọc chính.
            _label = NewText("Label", rt, size * 0.16f, TextAlignmentOptions.Top);
            _label.rectTransform.anchorMin = new Vector2(0, 0.90f);
            _label.rectTransform.anchorMax = new Vector2(1, 1f);
            _label.rectTransform.offsetMin = _label.rectTransform.offsetMax = Vector2.zero;

            // Badge chi phí SP — góc trên-trái, đối xứng Label (trên-phải-ish/center). Trước đây
            // đặt ở góc dưới-trái (anchor y 0.02-0.18) đè lên đúng dải tua rua (fringe) cong ở đáy
            // card_gold, số bị cắt/khó đọc — task "UI Screen Battle chưa giống sample" phát hiện.
            // Đưa lên góc trên, dưới vùng Label, tránh hẳn vùng fringe.
            _cost = NewText("Cost", rt, size * 0.18f, TextAlignmentOptions.TopLeft);
            _cost.rectTransform.anchorMin = new Vector2(0.06f, 0.74f);
            _cost.rectTransform.anchorMax = new Vector2(0.5f, 0.90f);
            _cost.rectTransform.offsetMin = _cost.rectTransform.offsetMax = Vector2.zero;

            // Số cooldown — badge tròn nhỏ giữa ô (trước đây chữ to phủ HẾT ô, đè lên icon trông
            // rất thô — task-ui-chrome-popups.md phản hồi "siêu xấu"). Badge tròn tối + số vừa
            // phải giữ được độ dễ đọc mà không che icon.
            var badgeGo = new GameObject("CooldownBadge", typeof(RectTransform));
            badgeGo.transform.SetParent(rt, false);
            var badgeRt = (RectTransform)badgeGo.transform;
            badgeRt.anchorMin = new Vector2(0.5f, 0.5f);
            badgeRt.anchorMax = new Vector2(0.5f, 0.5f);
            badgeRt.sizeDelta = new Vector2(size * 0.5f, size * 0.5f);
            var badgeImg = badgeGo.AddComponent<Image>();
            badgeImg.sprite = _cooldownBadgeSprite;
            badgeImg.type = Image.Type.Sliced;
            badgeImg.color = Color.white;
            badgeImg.raycastTarget = false;
            _cooldownBadge = badgeGo;

            _cooldown = NewText("Cooldown", badgeRt, size * 0.28f, TextAlignmentOptions.Center);
            _cooldown.rectTransform.anchorMin = Vector2.zero;
            _cooldown.rectTransform.anchorMax = Vector2.one;
            _cooldown.rectTransform.offsetMin = _cooldown.rectTransform.offsetMax = Vector2.zero;
            _cooldown.color = TEXT_NORMAL;
            badgeGo.SetActive(false);

            // Glyph nguyên tố cho chế độ mù màu — góc trên-phải, đối xứng Cost (trên-trái). Cùng
            // sửa như Cost ở trên: vùng dưới (anchor y 0.02-0.18) đè lên tua rua fringe của
            // card_gold, đưa lên góc trên để tránh hẳn.
            _elementGlyph = NewText("ElementGlyph", rt, size * 0.2f, TextAlignmentOptions.TopRight);
            _elementGlyph.rectTransform.anchorMin = new Vector2(0.5f, 0.74f);
            _elementGlyph.rectTransform.anchorMax = new Vector2(0.94f, 0.90f);
            _elementGlyph.rectTransform.offsetMin = _elementGlyph.rectTransform.offsetMax = Vector2.zero;
            _elementGlyph.gameObject.SetActive(false);
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
                _elementGlyph.gameObject.SetActive(false);
                SetState(SlotState.Empty);
                return;
            }

            _label.text = ShortName(skill.Data);
            _cost.text = skill.Data.SpCost > 0 ? skill.Data.SpCost.ToString() : "";
            _elementTint.color = ElementColor(skill.Data.Element);

            bool colorblind = _settings != null && _settings.Current.ColorblindMode;
            _elementGlyph.gameObject.SetActive(colorblind);
            if (colorblind) _elementGlyph.text = ElementGlyph(skill.Data.Element);

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
            _cooldownBadge.SetActive(showCooldown);
            if (showCooldown) _cooldown.text = Skill.CooldownLeft.ToString();

            switch (state)
            {
                case SlotState.Available:
                    _border.sprite = _cardNormal;
                    _border.color = BORDER_NORMAL;
                    _label.color = TEXT_NORMAL;
                    _icon.color = TEXT_NORMAL;
                    _cost.color = SP_ENOUGH;
                    transform.localScale = Vector3.one;
                    break;

                case SlotState.Selected:
                    _border.sprite = _cardSelected;
                    _border.color = BORDER_NORMAL;
                    _label.color = TEXT_NORMAL;
                    _icon.color = TEXT_NORMAL;
                    _cost.color = SP_ENOUGH;
                    transform.localScale = Vector3.one * 1.08f;
                    break;

                case SlotState.OnCooldown:
                    _border.sprite = _cardDisabled;
                    _border.color = BORDER_NORMAL;
                    _label.color = TEXT_DISABLED;
                    _icon.color = TEXT_DISABLED;
                    _cost.color = TEXT_DISABLED;
                    transform.localScale = Vector3.one;
                    break;

                case SlotState.NotEnoughSp:
                    _border.sprite = _cardDisabled;
                    _border.color = BORDER_NORMAL;
                    _label.color = TEXT_DISABLED;
                    _icon.color = TEXT_DISABLED;
                    _cost.color = SP_SHORT;      // badge SP đỏ = thiếu SP
                    transform.localScale = Vector3.one;
                    break;

                case SlotState.Silenced:
                    _border.sprite = _cardDisabled;
                    _border.color = new Color(1f, 0.65f, 0.7f);
                    _label.color = TEXT_DISABLED;
                    _icon.color = TEXT_DISABLED;
                    transform.localScale = Vector3.one;
                    break;

                case SlotState.UltimateCharging:
                    _border.sprite = _cardNormal;
                    // Không dùng TEXT_DISABLED ở đây — nền tối + tint nguyên tố (VD Dark, alpha
                    // 0.28) đã rất tối, chữ/icon mờ theo TEXT_DISABLED gần như biến mất (slot
                    // Ultimate trông trống trơn dù có skill). Đây là slot quan trọng nhất trên
                    // HUD nên phải luôn đọc được, kể cả khi chưa sẵn sàng.
                    _border.color = BORDER_NORMAL;
                    _label.color = TEXT_NORMAL;
                    _icon.color = TEXT_NORMAL;
                    transform.localScale = Vector3.one;
                    break;

                case SlotState.UltimateReady:
                    // Nhấp nháy 1 Hz — dấu hiệu quan trọng nhất trên HUD
                    float pulse = Mathf.PingPong(Time.time, 0.5f) / 0.5f;
                    _border.sprite = _cardSelected;
                    _border.color = Color.Lerp(BORDER_ULTIMATE, Color.white, pulse);
                    _label.color = BORDER_ULTIMATE;
                    _icon.color = Color.Lerp(BORDER_ULTIMATE, Color.white, pulse);
                    transform.localScale = Vector3.one;
                    break;

                case SlotState.Empty:
                    _border.sprite = _cardDisabled;
                    _border.color = new Color(0.6f, 0.6f, 0.6f);
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

        // task-ui-chrome-popups.md — hạ alpha (0.28→0.16) so với bản cũ: nền thẻ giờ có texture
        // thật (viền răng cưa) thay vì màu phẳng, tint đậm che mất chi tiết + tạo cảm giác "vá lỗi"
        // khi 1 thẻ Fire đỏ chóe cạnh 1 thẻ tím bình thường. Glyph mù màu (§accessibility) vẫn còn
        // nguyên nên KHÔNG mất khả năng phân biệt nguyên tố khi tắt tint mạnh.
        private static Color ElementColor(Element e) => e switch
        {
            Element.Fire  => new Color(0.902f, 0.224f, 0.275f, 0.16f),
            Element.Water => new Color(0.271f, 0.482f, 0.616f, 0.16f),
            Element.Earth => new Color(0.651f, 0.443f, 0.259f, 0.16f),
            Element.Wind  => new Color(0.482f, 0.788f, 0.314f, 0.16f),
            Element.Light => new Color(1.000f, 0.820f, 0.400f, 0.16f),
            Element.Dark  => new Color(0.608f, 0.365f, 0.898f, 0.16f),
            _ => new Color(1, 1, 1, 0f)
        };

        /// <summary>task-accessibility-part2.md — 7 hình khối phân biệt tối đa, không dựa vào màu.</summary>
        private static string ElementGlyph(Element e) => e switch
        {
            Element.Fire  => "▲", // ▲
            Element.Water => "▼", // ▼
            Element.Earth => "■", // ■
            Element.Wind  => "◆", // ◆
            Element.Light => "★", // ★
            Element.Dark  => "✚", // ✚
            _ => "●"              // ● Neutral
        };
    }
}
