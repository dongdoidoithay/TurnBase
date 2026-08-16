using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Core.Random;
using Game.Data;
using Game.Data.Dto;
using Game.Meta.Battle;
using Game.Meta.Content;
using Game.Meta.Equipment;
using Game.Meta.Hero;
using Game.Services.Audio;
using Game.Services.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Meta
{
    /// <summary>
    /// Chọn đội hình (tối đa <see cref="PartySize"/> hero) + trang bị/nâng cấp trước khi vào
    /// trận — plan.md "Pre-Battle: chọn 4 hero + đội hình" (roadmap Tuần 9).
    ///
    /// DỰNG BẰNG PREFAB (Resources/Prefabs/UI/...) — không còn tự tạo GameObject bằng code
    /// như bản đầu. Layout/màu/font giờ sửa trực tiếp trong prefab qua Inspector; script này
    /// CHỈ còn việc bind dữ liệu (text, sprite, sự kiện onClick) vào các component đã có sẵn.
    /// 3 prefab: UI_TeamSelect (khung màn hình), UI_HeroCard (1 dòng hero), UI_GearSlotRow
    /// (1 dòng trang bị) — 2 cái sau instantiate lặp lại theo dữ liệu.
    /// </summary>
    public sealed class TeamSelectScreen : MonoBehaviour
    {
        public const int PartySize = 4;

        private static readonly Color ACCENT = new(0.957f, 0.635f, 0.349f);
        private static readonly Color TEXT = new(0.949f, 0.910f, 0.810f);
        private static readonly Color TEXT_DIM = new(0.65f, 0.60f, 0.64f);
        private static readonly Color SELECTED = new(0.482f, 0.788f, 0.314f);
        private static readonly Color ROW_BG_VIEWED = new(0.22f, 0.16f, 0.10f, 0.95f);
        private static readonly Color GOLD = new(1f, 0.820f, 0.400f);
        private static readonly Color DISABLED = new(0.32f, 0.29f, 0.33f);
        private static readonly Color EQUIP_BLUE = new(0.271f, 0.482f, 0.616f, 0.95f);
        private static readonly Color ENHANCE_BROWN = new(0.42f, 0.32f, 0.06f, 0.95f);
        private static readonly Color FAIL_RED = new(0.906f, 0.322f, 0.322f);

        private static readonly EquipSlot[] SLOTS =
            { EquipSlot.Weapon, EquipSlot.Armor, EquipSlot.Helm, EquipSlot.Boots, EquipSlot.Ring, EquipSlot.Amulet };

        private const string ShellPrefabPath = "Prefabs/UI/Screens/UI_TeamSelect";
        private const string HeroCardPrefabPath = "Prefabs/UI/Widgets/UI_HeroCard";
        private const string GearSlotRowPrefabPath = "Prefabs/UI/Widgets/UI_GearSlotRow";

        private GameObject _root;
        private IAudioService _audio;
        private ILocalizationService _loc;
        private HeroDetailScreen _detailScreen;

        // task-phase-5-gaps.md Phần D — HeroDisplayUtil vẫn là fallback cuối khi service (hiếm khi)
        // chưa đăng ký, không đổi hành vi cũ trong trường hợp đó.
        private string HeroName(string defId) =>
            _loc != null ? _loc.GetName(defId, LocalizedNameKind.Hero) : HeroDisplayUtil.FormatName(defId);

        // Tham chiếu cache 1 lần khi Instantiate shell — khớp đường dẫn trong UI_TeamSelect.prefab.
        private RectTransform _heroListContainer, _gearSlotsContainer;
        private Text _gearTitleLabel, _selectedLabel, _startLabel;
        private Button _backButton, _startButton;
        private Image _gearPortraitImg;

        private PlayerProfileDto _profile;
        private MapNodeDto _node;
        private Action<List<string>> _onConfirm;
        private IRandomSource _rng;

        private readonly List<string> _selectedHeroUids = new();
        private string _viewedHeroUid;

        // Kết quả Enhance gần nhất — chỉ để hiện "FAILED" đúng 1 lần cho đúng dòng vừa bấm
        // (task-enhance-plus15.md §0, xem RefreshGearPanel). Không phải state game thật, chỉ UI.
        private string _lastEnhanceUid;
        private EquipmentService.EnhanceOutcome _lastEnhanceOutcome;

        // task-formation-synergy.md — preset đội hình chọn trước khi vào trận.
        private int _formationIndex;
        private Text _formationLabel;
        public string SelectedFormation => FormationSystem.AllIds[_formationIndex];

        public bool IsOpen => _root != null && _root.activeSelf;

        /// <summary>Gọi mỗi khi nội dung rebuild (chọn hero, equip, enhance) — MetaSceneInstaller
        /// nghe để cập nhật TopBar (Gold đổi ngay khi Enhance nhưng TopBar không tự refresh vì
        /// đây là 2 script độc lập, không dùng chung 1 event bus ở tầng Meta).</summary>
        public event Action OnProfileChanged;

        // =====================================================================

        public void Open(PlayerProfileDto profile, MapNodeDto node, Action<List<string>> onConfirm)
        {
            _profile = profile;
            _node = node;
            _onConfirm = onConfirm;
            ServiceLocator.TryGet(out _audio);
            ServiceLocator.TryGet(out _loc);
            _rng ??= new XorShiftRandom((ulong)System.DateTime.UtcNow.Ticks);

            _selectedHeroUids.Clear();
            foreach (var h in _profile.Heroes.Take(PartySize)) _selectedHeroUids.Add(h.Uid);
            _viewedHeroUid = _profile.Heroes.Count > 0 ? _profile.Heroes[0].Uid : null;
            _formationIndex = 0; // reset về Balanced mỗi lần mở

            if (_root == null) BuildShell();
            _root.SetActive(true);
            RefreshContent();
        }

        public void Close() => _root?.SetActive(false);

        // =====================================================================

        private void BuildShell()
        {
            var prefab = Resources.Load<GameObject>(ShellPrefabPath);
            _root = Instantiate(prefab, transform);

            var panel = _root.transform.Find("Panel");
            var content = panel.Find("Content");

            // HeroListContainer nay nằm trong HeroListViewport (RectMask2D + ScrollRect) —
            // roster 24 hero vượt quá vùng hiển thị cố định 380px, cần cuộn thay vì tràn ra
            // ngoài không bấm được (task_26720454).
            _heroListContainer = content.Find("HeroListViewport/HeroListContainer").GetComponent<RectTransform>();

            var gearPanel = content.Find("GearPanelContainer");
            _gearTitleLabel = gearPanel.Find("GearTitle").GetComponent<Text>();
            _gearSlotsContainer = gearPanel.Find("SlotsContainer").GetComponent<RectTransform>();
            // GearPortrait — chân dung lớn của hero đang xem trang bị, thêm cùng lượt sửa lỗi
            // "thiếu icon hero" người dùng báo (bảng Gear trước đây chỉ có chữ tiêu đề).
            _gearPortraitImg = gearPanel.Find("GearPortrait/PortraitMask/PortraitSprite").GetComponent<Image>();

            var footer = content.Find("Footer");
            _selectedLabel = footer.Find("SelectedLabel").GetComponent<Text>();
            _backButton = footer.Find("BackButton").GetComponent<Button>();
            _startButton = footer.Find("StartButton").GetComponent<Button>();
            _startLabel = _startButton.transform.Find("StartLabel").GetComponent<Text>();

            _backButton.onClick.AddListener(() =>
            {
                _audio?.PlaySfx("ui/sfx_ui_cancel");
                Close();
            });
            _startButton.onClick.AddListener(() =>
            {
                _audio?.PlaySfx("ui/sfx_ui_confirm");
                var chosenDefIds = _selectedHeroUids
                    .Select(uid => _profile.Heroes.Find(h => h.Uid == uid)?.DefId)
                    .Where(id => id != null)
                    .ToList();
                Close();
                _onConfirm?.Invoke(chosenDefIds);
            });

            // task-teamselect-start-button-fix.md — nút cycle Formation neo vào `content`, NGAY
            // TRÊN footer (không phải bên trong footer — footer chỉ cao 40px, đủ vừa khít
            // SelectedLabel/BackButton/StartButton neo giữa, chèn thêm dải 36px cao vào đó sẽ đè
            // lên StartButton và chặn luôn raycast, đúng lỗi người dùng báo).
            BuildFormationButton(content, (RectTransform)footer);
        }

        private void BuildFormationButton(Transform content, RectTransform footer)
        {
            var go = new GameObject("FormationRow", typeof(RectTransform));
            go.transform.SetParent(content, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            const float gap = 6f, rowH = 36f;
            rt.anchoredPosition = new Vector2(0f, footer.sizeDelta.y + gap);
            rt.sizeDelta = new Vector2(0f, rowH);

            // Nền
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.10f, 0.20f, 0.90f);

            // Nút bấm
            var btn = go.AddComponent<Button>();
            var btnColors = btn.colors;
            btnColors.highlightedColor = new Color(0.25f, 0.18f, 0.34f);
            btn.colors = btnColors;

            // Label — "FORMATION: Balanced  (+8% CRIT)"
            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero; labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(6f, 0f); labelRt.offsetMax = new Vector2(-6f, 0f);

            _formationLabel = labelGo.AddComponent<Text>();
            _formationLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _formationLabel.fontSize = 11;
            _formationLabel.alignment = TextAnchor.MiddleCenter;
            _formationLabel.color = new Color(0.85f, 0.75f, 1.00f);
            RefreshFormationLabel();

            btn.onClick.AddListener(() =>
            {
                _formationIndex = (_formationIndex + 1) % FormationSystem.AllIds.Count;
                _audio?.PlaySfx("ui/sfx_ui_click");
                RefreshFormationLabel();
            });
        }

        private void RefreshFormationLabel()
        {
            if (_formationLabel == null) return;
            string id = SelectedFormation;
            string bonus = FormationSystem.BonusSummary(id);
            _formationLabel.text = string.IsNullOrEmpty(bonus)
                ? $"FORMATION: {FormationSystem.DisplayName(id)}"
                : $"FORMATION: {FormationSystem.DisplayName(id)}  ({bonus})";
        }

        // =====================================================================
        // Rebuild phần nội dung động (hero card × N, gear slot row × 6) — phần khung
        // (Canvas/Panel/Footer) giữ nguyên từ prefab, chỉ 2 danh sách này lặp theo dữ liệu.
        // =====================================================================

        private void RefreshContent()
        {
            RefreshHeroList();
            RefreshGearPanel();

            _selectedLabel.text = $"Selected {_selectedHeroUids.Count}/{PartySize}";
            _selectedLabel.color = _selectedHeroUids.Count > 0 ? TEXT : TEXT_DIM;

            bool canStart = _selectedHeroUids.Count is > 0 and <= PartySize;
            _startButton.interactable = canStart;
            _startButton.GetComponent<Image>().color = canStart ? ENHANCE_BROWN : DISABLED;
            _startLabel.color = canStart ? GOLD : TEXT_DIM;

            OnProfileChanged?.Invoke();
        }

        private void RefreshHeroList()
        {
            foreach (Transform c in _heroListContainer) Destroy(c.gameObject);

            var cardPrefab = Resources.Load<GameObject>(HeroCardPrefabPath);
            const float rowH = 70f;

            // ScrollRect cần content cao đúng số hero thật để tính vùng cuộn — khi roster
            // ngắn hơn viewport (380px) thì Clamp về đúng chiều cao viewport, không co lại
            // nhỏ hơn khiến ScrollRect coi như "không có gì để cuộn" một cách sai lệch.
            float contentH = Mathf.Max(_heroListContainer.parent.GetComponent<RectTransform>().rect.height, _profile.Heroes.Count * rowH);
            _heroListContainer.sizeDelta = new Vector2(_heroListContainer.sizeDelta.x, contentH);

            for (int i = 0; i < _profile.Heroes.Count; i++)
            {
                var hero = _profile.Heroes[i];
                bool selected = _selectedHeroUids.Contains(hero.Uid);
                bool viewed = hero.Uid == _viewedHeroUid;
                var def = FindHeroDef(hero.DefId);
                Color rarityColor = def != null ? RarityColor(def.Rarity) : TEXT_DIM;

                var card = Instantiate(cardPrefab, _heroListContainer);
                var cardRt = (RectTransform)card.transform;
                cardRt.anchoredPosition = new Vector2(0, -i * rowH);

                card.GetComponent<Image>().color = viewed ? ACCENT : rarityColor;
                card.transform.Find("Fill").GetComponent<Image>().color = viewed ? ROW_BG_VIEWED : new Color(0.169f, 0.106f, 0.180f, 0.9f);

                card.GetComponent<Button>().onClick.AddListener(() =>
                {
                    _audio?.PlaySfx("ui/sfx_ui_tick");
                    _viewedHeroUid = hero.Uid;
                    RefreshContent();
                });

                var portraitRing = card.transform.Find("PortraitRing");
                portraitRing.GetComponent<Image>().color = rarityColor;
                var portraitImg = portraitRing.Find("PortraitMask/PortraitSprite").GetComponent<Image>();
                portraitImg.sprite = Resources.Load<Sprite>($"Art/Characters/Heroes/{hero.DefId}/{hero.DefId}_v1_00");
                portraitImg.enabled = portraitImg.sprite != null;

                // Bấm avatar để xem chi tiết hero — tách khỏi Button gốc của card (đổi hero
                // đang xem trang bị) nên không xung đột, raycast ăn PortraitRing trước.
                portraitRing.GetComponent<Button>().onClick.AddListener(() =>
                {
                    _audio?.PlaySfx("ui/sfx_ui_tick");
                    EnsureDetailScreen().Open(_profile, hero);
                });

                string name = HeroName(hero.DefId);
                var nameLabel = card.transform.Find("NameLabel").GetComponent<Text>();
                nameLabel.text = name;
                nameLabel.color = viewed ? ACCENT : TEXT;

                card.transform.Find("LevelLabel").GetComponent<Text>().text = $"Lv{hero.Level}";

                int gearCount = hero.Equipped.Count(e => !string.IsNullOrEmpty(e));
                card.transform.Find("GearLabel").GetComponent<Text>().text = $"Gear {gearCount}/6";

                var toggle = card.transform.Find("Toggle");
                toggle.GetComponent<Image>().color = selected ? SELECTED : DISABLED;
                toggle.Find("ToggleLabel").GetComponent<Text>().text = selected ? "IN TEAM" : "BENCH";
                toggle.GetComponent<Button>().onClick.AddListener(() =>
                {
                    _audio?.PlaySfx("ui/sfx_ui_confirm");
                    if (selected) _selectedHeroUids.Remove(hero.Uid);
                    else if (_selectedHeroUids.Count < PartySize) _selectedHeroUids.Add(hero.Uid);
                    RefreshContent();
                });
            }
        }

        private void RefreshGearPanel()
        {
            foreach (Transform c in _gearSlotsContainer) Destroy(c.gameObject);

            var hero = _profile.Heroes.Find(h => h.Uid == _viewedHeroUid);
            if (hero == null)
            {
                _gearTitleLabel.text = "No hero selected.";
                _gearPortraitImg.enabled = false;
                return;
            }

            string heroName = HeroName(hero.DefId);
            _gearTitleLabel.text = $"{heroName} — GEAR";
            _gearPortraitImg.sprite = Resources.Load<Sprite>($"Art/Characters/Heroes/{hero.DefId}/{hero.DefId}_v1_00");
            _gearPortraitImg.enabled = _gearPortraitImg.sprite != null;

            var rowPrefab = Resources.Load<GameObject>(GearSlotRowPrefabPath);
            // 52 — đo lại thật qua execute_code (task-ui-vfx-polish.md, người dùng báo "gear bị xô
            // lệch"): ở kích thước Content SỐNG hiện tại (856×456, đã đổi so với lúc chốt 60 trước
            // đây), 6 dòng × 60 đã ĐÈ vào FormationRow tạo runtime 18px (content-top 392 > 374) —
            // lỗi thật, không phải cảm tính, xác nhận bằng GetWorldCorners() sau khi dựng màn thật.
            // 52 (khớp chiều cao mới của prefab UI_GearSlotRow, nút Equip/Enhance/Reforge co 26→20)
            // đẩy đáy dòng cuối lên content-top 368, cách FormationRow(374) 6px — xác nhận lại bằng
            // execute_code sau khi sửa. Cũng là chiều cao dải header GearTitle+GearPortrait mới.
            const float rowH = 52f;

            for (int i = 0; i < SLOTS.Length; i++)
            {
                var slot = SLOTS[i];
                string equippedUid = hero.Equipped[(int)slot];
                var inst = string.IsNullOrEmpty(equippedUid) ? null : EquipmentService.FindInstance(_profile, equippedUid);
                var instDef = EquipmentService.DefOf(inst);

                var row = Instantiate(rowPrefab, _gearSlotsContainer);
                ((RectTransform)row.transform).anchoredPosition = new Vector2(0, -i * rowH);

                row.transform.Find("SlotNameLabel").GetComponent<Text>().text = slot.ToString().ToUpperInvariant();

                var itemLabel = row.transform.Find("ItemLabel").GetComponent<Text>();
                itemLabel.text = instDef != null ? FormatItemText(instDef, inst) : "-- empty --";
                itemLabel.color = instDef != null ? TEXT : TEXT_DIM;

                // Nút EQUIP: bấm để đổi sang món kế tiếp còn trống trong túi (đúng slot),
                // vòng qua cả lựa chọn "tháo ra" — đơn giản hoá thay vì mở danh sách riêng.
                var equipBtn = row.transform.Find("EquipButton").GetComponent<Button>();
                row.transform.Find("EquipButton/EquipLabel").GetComponent<Text>().text = instDef != null ? "SWAP" : "EQUIP";
                var capturedSlot = slot;
                var capturedUid = equippedUid;
                equipBtn.onClick.AddListener(() =>
                {
                    _audio?.PlaySfx("ui/sfx_ui_tick");
                    CycleEquip(hero, capturedSlot, capturedUid);
                    RefreshContent();
                });

                var enhanceBtn = row.transform.Find("EnhanceButton").GetComponent<Button>();
                var enhanceImg = enhanceBtn.GetComponent<Image>();
                var enhanceLabel = row.transform.Find("EnhanceButton/EnhanceLabel").GetComponent<Text>();
                bool canEnhance = inst != null && EquipmentService.CanEnhance(inst);
                enhanceBtn.interactable = canEnhance;
                enhanceImg.color = canEnhance ? ENHANCE_BROWN : DISABLED;
                if (canEnhance)
                {
                    // FAILED chỉ hiện ĐÚNG 1 lần cho dòng vừa bấm (tự "tiêu" field ngay sau khi
                    // đọc) — TeamSelectScreen không có Toast/canvas riêng (khác MetaSceneInstaller),
                    // tái dùng EnhanceLabel làm nơi báo kết quả thay vì xây UI mới (task-enhance-
                    // plus15.md §0). Không báo gì thì Gold+Đá bị trừ trong im lặng đọc như bug.
                    if (capturedUid == _lastEnhanceUid && _lastEnhanceOutcome == EquipmentService.EnhanceOutcome.Failed)
                    {
                        // "FAILED! (retry)" — đo thật qua execute_code (Text.cachedTextGenerator)
                        // trước khi chọn, tránh lặp lỗi tràn dòng đã biết ở label hàng danh sách
                        // (feedback_unity_mcp_ui_gotchas.md) mà không phải chờ Play mode mới biết.
                        enhanceLabel.text = "FAILED! (retry)";
                        enhanceLabel.color = FAIL_RED;
                        _lastEnhanceUid = null;
                    }
                    else
                    {
                        long goldCost = EquipmentService.EnhanceCost(inst.Level);
                        long stoneCost = EquipmentService.EnhanceStoneCost(inst.Level);
                        float chance = EquipmentService.SuccessChance(inst.Level);
                        // Đã đo thật cả 4 chuỗi level 11-14 (goldCost dùng công thức RẺ cũ nên vẫn
                        // đủ ngắn để vừa, xem task-enhance-plus15.md §0) — không tràn dòng.
                        enhanceLabel.text = chance >= 1f
                            ? $"+1 ({goldCost}g · {stoneCost}◆)"
                            : $"{goldCost}g·{stoneCost}◆·{chance:P0}";
                        enhanceLabel.color = GOLD;
                    }
                    enhanceBtn.onClick.AddListener(() =>
                    {
                        var outcome = EquipmentService.TryEnhance(_profile, capturedUid, _rng);
                        _lastEnhanceUid = capturedUid;
                        _lastEnhanceOutcome = outcome;
                        _audio?.PlaySfx(outcome == EquipmentService.EnhanceOutcome.Succeeded
                            ? "ui/sfx_ui_confirm" : "ui/sfx_ui_cancel");
                        RefreshContent();
                    });
                }
                else
                {
                    enhanceLabel.text = inst != null ? "MAX" : "—";
                    enhanceLabel.color = TEXT_DIM;
                }

                // task-phase-5-gaps.md Phần C — Reforge: reroll toàn bộ sub-stat bằng Vàng, không
                // có tỉ lệ thất bại (khác Enhance) nên không cần cơ chế báo FAILED riêng.
                var reforgeBtn = row.transform.Find("ReforgeButton").GetComponent<Button>();
                var reforgeImg = reforgeBtn.GetComponent<Image>();
                var reforgeLabel = row.transform.Find("ReforgeButton/ReforgeLabel").GetComponent<Text>();
                bool canReforge = EquipmentService.CanReforge(inst);
                reforgeBtn.interactable = canReforge;
                reforgeImg.color = canReforge ? EQUIP_BLUE : DISABLED;
                if (canReforge)
                {
                    long reforgeCost = EquipmentService.ReforgeCost(inst.Level, (Rarity)inst.Rarity);
                    reforgeLabel.text = $"REFORGE {reforgeCost}g";
                    reforgeLabel.color = TEXT;
                    reforgeBtn.onClick.AddListener(() =>
                    {
                        var outcome = EquipmentService.TryReforge(_profile, capturedUid, _rng);
                        _audio?.PlaySfx(outcome == EquipmentService.ReforgeOutcome.Succeeded
                            ? "ui/sfx_ui_confirm" : "ui/sfx_ui_cancel");
                        RefreshContent();
                    });
                }
                else
                {
                    reforgeLabel.text = inst != null ? "NO SUB" : "—";
                    reforgeLabel.color = TEXT_DIM;
                }
            }
        }

        /// <summary>Đổi trang bị ở 1 slot: tháo ra → món trống kế tiếp trong túi → ... → quay
        /// lại tháo ra. Vòng lặp đơn giản, không cần màn danh sách/modal riêng.</summary>
        private void CycleEquip(HeroInstanceDto hero, EquipSlot slot, string currentUid)
        {
            var options = EquipmentService.UnequippedBySlot(_profile, slot);
            if (string.IsNullOrEmpty(currentUid))
            {
                if (options.Count > 0) EquipmentService.Equip(_profile, hero.Uid, options[0].Uid);
                return;
            }

            int idx = options.FindIndex(o => o.Uid == currentUid);
            if (idx < 0 && options.Count > 0)
            {
                EquipmentService.Equip(_profile, hero.Uid, options[0].Uid);
            }
            else if (idx >= 0 && idx + 1 < options.Count)
            {
                EquipmentService.Equip(_profile, hero.Uid, options[idx + 1].Uid);
            }
            else
            {
                EquipmentService.Unequip(_profile, hero.Uid, slot);
            }
        }

        // =====================================================================

        /// <summary>task-addressables-pilot.md — pilot Addressables cho HeroDefinitionSO (đọc
        /// đơn theo defId). `WaitForCompletion()` giữ hành vi ĐỒNG BỘ y hệt `Resources.Load` cũ
        /// (caller không cần đổi gì) — codebase này chưa dùng async/await ở tầng Meta, refactor
        /// bất đồng bộ thật là việc lớn hơn hẳn, ngoài phạm vi pilot. Address = đúng path cũ
        /// (`Data/Heroes/{defId}`), gán khi đánh dấu 24 asset Addressable — không đổi key lookup.</summary>
        private static HeroDefinitionSO FindHeroDef(string defId)
            => UnityEngine.AddressableAssets.Addressables
                .LoadAssetAsync<HeroDefinitionSO>($"Data/Heroes/{defId}").WaitForCompletion();

        private HeroDetailScreen EnsureDetailScreen()
        {
            if (_detailScreen == null)
            {
                _detailScreen = gameObject.GetComponent<HeroDetailScreen>() ?? gameObject.AddComponent<HeroDetailScreen>();
                // Ascend/nâng skill đổi Wallet — dùng lại đúng dây chuyền refresh TopBar mà
                // Enhance trang bị đã dùng, không cần thêm event bus riêng.
                _detailScreen.OnProfileChanged += RefreshContent;
            }
            return _detailScreen;
        }

        /// <summary>Dòng 1: rarity (màu, chỉ item qua Generator) + tên + cấp nâng. Dòng 2: main
        /// stat + sub-stat — task-equipment.md follow-up: EquipmentGenerator giờ roll rarity/
        /// sub-stat thật, ItemLabel trước đây bỏ qua hoàn toàn (chỉ hiện main stat), người chơi
        /// không thấy được thứ khiến món đồ đặc biệt.
        /// KHÔNG hiện rarity tag cho item chưa qua Generator (VD starter kit từ
        /// EnsureStarterEquipment, SubStats rỗng) — inst.Rarity của nhóm này chỉ copy nguyên
        /// def.Rarity (nhãn tier 1/2 KHÔNG cùng quy ước enum Rarity, xem task-equipment.md §0),
        /// hiện nó dưới dạng "[Rare]"/"[Epic]" sẽ SAI và gây hiểu lầm.</summary>
        private static string FormatItemText(EquipmentDefinitionSO def, EquipmentInstanceDto inst)
        {
            string name = def.DefId.Replace("eq_", "").Replace('_', ' ');
            string mainStat = $"{FormatStat((StatType)inst.MainStatType)}+{EquipmentService.EffectiveStatValue(inst):0}";

            if (inst.SubStats.Count == 0)
                return $"{name} +{inst.Level}\n{mainStat}";

            var rarity = (Rarity)inst.Rarity;
            string rarityTag = $"<color=#{ColorUtility.ToHtmlStringRGB(RarityColor(rarity))}>[{rarity}]</color>";
            string subs = "  " + string.Join(" ", inst.SubStats.ConvertAll(s => $"{FormatStat((StatType)s.StatType)}+{s.Value:0.#}"));
            return $"{rarityTag} {name} +{inst.Level}\n{mainStat}{subs}";
        }

        private static string FormatStat(StatType t) => t switch
        {
            StatType.AtkPct => "ATK%",
            StatType.MaxHpPct => "HP%",
            StatType.DefPct => "DEF%",
            StatType.SpdPct => "SPD%",
            StatType.CritPct => "CRIT%",
            StatType.CritDmgPct => "CDMG%",
            StatType.Res => "RES",
            StatType.EffAcc => "EACC",
            _ => t.ToString().ToUpperInvariant(),
        };

        // internal (không private) — SummonScreen tái dùng đúng bảng màu này cho kết quả gacha,
        // tránh bịa 1 bảng màu rarity thứ 2 (cùng assembly Game.Meta, không cần lớp chia sẻ riêng).
        internal static Color RarityColor(Rarity r) => r switch
        {
            Rarity.Common => new Color(0.65f, 0.62f, 0.66f),
            Rarity.Rare => new Color(0.322f, 0.573f, 0.906f),
            Rarity.Epic => new Color(0.647f, 0.365f, 0.898f),
            Rarity.Legendary => new Color(1f, 0.702f, 0.204f),
            Rarity.Mythic => new Color(0.902f, 0.271f, 0.271f),
            _ => TEXT_DIM
        };
    }
}
