using System.Collections.Generic;
using Game.Core;
using Game.Core.UI;
using Game.Data;
using Game.Data.Dto;
using Game.Meta.Hero;
using Game.Services.Audio;
using Game.Services.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Meta.Gacha
{
    /// <summary>
    /// Modal RATES/HISTORY — task-gacha-disclosure.md, plan.md §9.3 "Bắt buộc: hiển thị tỉ lệ trong
    /// game, lưu lịch sử 100 lần gần nhất". Lưu trữ lịch sử ĐÃ có sẵn từ trước
    /// (<see cref="GachaStateDto.History"/>, capped <see cref="GachaSystem.HISTORY_CAP"/>) — phần
    /// còn thiếu THẬT chỉ là hiển thị, màn này chỉ đọc.
    ///
    /// Dựng từ UI_GachaInfo.prefab (clone UI_Codex.prefab — ĐÃ có sẵn đúng khuôn 2-tab +
    /// pagination cần dùng, không phải xây mới). Mở từ nút InfoButton mới trên UI_Summon.prefab.
    /// </summary>
    public sealed class GachaInfoScreen : MonoBehaviour
    {
        private const string PrefabPath = "Prefabs/UI/Screens/UI_GachaInfo";
        private const int PAGE_SIZE = 6;

        private GameObject _root;
        private Text _statusLabel;
        private GameObject[] _rows;
        private Text[] _nameLabels;
        private Text[] _progressLabels;
        private Image[] _icons;
        private Button _switchTabButton;
        private Text _switchTabLabel;
        private Button _prevButton;
        private Button _nextButton;
        private Button _closeButton;

        private IAudioService _audio;
        private ILocalizationService _loc;
        private PlayerProfileDto _profile;
        private System.Action _onClosed;

        private bool _showingHistory;
        private int _page;

        private string HeroName(string defId) =>
            _loc != null ? _loc.GetName(defId, LocalizedNameKind.Hero) : HeroDisplayUtil.FormatName(defId);

        public void Open(PlayerProfileDto profile, System.Action onClosed)
        {
            ServiceLocator.TryGet(out _audio);
            ServiceLocator.TryGet(out _loc);
            if (_root == null) BuildShell();

            _profile = profile;
            _onClosed = onClosed;
            _showingHistory = false;
            _page = 0;
            _root.SetActive(true);
            Refresh();
        }

        private void BuildShell()
        {
            var prefab = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>(PrefabPath).WaitForCompletion();
            _root = Instantiate(prefab, transform);

            var panel = _root.transform.Find("Panel");
            LayoutProfileSwitcher.ApplyStretchPanelLandscape(panel.gameObject, "GachaInfoPanel");
            _statusLabel = panel.Find("WalletLabel").GetComponent<Text>();

            var list = panel.Find("RowListContainer");
            _rows = new GameObject[PAGE_SIZE];
            _nameLabels = new Text[PAGE_SIZE];
            _progressLabels = new Text[PAGE_SIZE];
            _icons = new Image[PAGE_SIZE];
            for (int i = 0; i < PAGE_SIZE; i++)
            {
                var row = list.Find($"Row_{i}");
                _rows[i] = row.gameObject;
                _nameLabels[i] = row.Find("NameLabel").GetComponent<Text>();
                _progressLabels[i] = row.Find("ProgressLabel").GetComponent<Text>();
                _icons[i] = row.Find("Icon").GetComponent<Image>();
                row.Find("ClaimButton").gameObject.SetActive(false); // màn đọc thuần, không có hành động theo dòng
            }

            _switchTabButton = panel.Find("SwitchTabButton").GetComponent<Button>();
            _switchTabLabel = panel.Find("SwitchTabButton/Label").GetComponent<Text>();
            _switchTabButton.onClick.AddListener(SwitchTab);

            _prevButton = panel.Find("PrevButton").GetComponent<Button>();
            _prevButton.onClick.AddListener(() => ChangePage(-1));

            _nextButton = panel.Find("NextButton").GetComponent<Button>();
            _nextButton.onClick.AddListener(() => ChangePage(1));

            _closeButton = panel.Find("CloseButton").GetComponent<Button>();
            _closeButton.onClick.AddListener(Close);
        }

        private void Close()
        {
            _audio?.PlaySfx("ui/sfx_ui_tick");
            _root.SetActive(false);
            _onClosed?.Invoke();
        }

        private void SwitchTab()
        {
            _audio?.PlaySfx("ui/sfx_ui_tick");
            _showingHistory = !_showingHistory;
            _page = 0;
            Refresh();
        }

        private void ChangePage(int delta)
        {
            _audio?.PlaySfx("ui/sfx_ui_tick");
            _page += delta;
            Refresh();
        }

        private void Refresh()
        {
            _switchTabLabel.text = _showingHistory
                ? (_loc != null ? _loc.Get("gachainfo.tab.history") : "HISTORY")
                : (_loc != null ? _loc.Get("gachainfo.tab.rates") : "RATES");

            if (_showingHistory) RefreshHistoryPage();
            else RefreshRatesPage();
        }

        /// <summary>plan.md §9.3 — bảng tỉ lệ TĨNH, đúng 4 dòng, không cần phân trang. Đọc thẳng
        /// hằng số <see cref="GachaSystem"/> — cùng nguồn số liệu với logic roll thật, không thể
        /// lệch nhau.</summary>
        private void RefreshRatesPage()
        {
            _page = 0;
            _statusLabel.text = _loc != null ? _loc.Get("gachainfo.label.rates_title") : "DROP RATES";

            string legFlavor = _loc != null
                ? _loc.Get("gachainfo.rates.legendary_flavor", GachaSystem.LEGENDARY_SOFT_PITY_START, GachaSystem.LEGENDARY_HARD_PITY)
                : $"Soft pity từ #{GachaSystem.LEGENDARY_SOFT_PITY_START} · đảm bảo #{GachaSystem.LEGENDARY_HARD_PITY}";
            string epicFlavor = _loc != null
                ? _loc.Get("gachainfo.rates.epic_flavor", GachaSystem.EPIC_HARD_PITY)
                : $"Đảm bảo mỗi #{GachaSystem.EPIC_HARD_PITY} lần";

            SetRateRow(0, Rarity.Legendary, GachaSystem.LEGENDARY_BASE_RATE, legFlavor);
            SetRateRow(1, Rarity.Epic, GachaSystem.EPIC_BASE_RATE, epicFlavor);
            SetRateRow(2, Rarity.Rare, GachaSystem.RARE_BASE_RATE, "");
            float commonRate = 1f - GachaSystem.LEGENDARY_BASE_RATE - GachaSystem.EPIC_BASE_RATE - GachaSystem.RARE_BASE_RATE;
            SetRateRow(3, Rarity.Common, commonRate, "");

            for (int i = 4; i < PAGE_SIZE; i++) _rows[i].SetActive(false);

            _prevButton.interactable = false;
            _nextButton.interactable = false;
        }

        private void SetRateRow(int i, Rarity rarity, float rate, string flavor)
        {
            _rows[i].SetActive(true);
            _nameLabels[i].text = rarity.ToString();
            _nameLabels[i].color = TeamSelectScreen.RarityColor(rarity);
            _progressLabels[i].text = $"{rate * 100f:0.0}%" + (string.IsNullOrEmpty(flavor) ? "" : $" — {flavor}");
            _icons[i].enabled = true;
            _icons[i].sprite = null;
            _icons[i].color = TeamSelectScreen.RarityColor(rarity);
        }

        /// <summary>plan.md §9.3 — "lưu lịch sử 100 lần gần nhất" (lưu trữ đã có sẵn từ trước qua
        /// <see cref="GachaStateDto.History"/>). Hiện MỚI NHẤT TRƯỚC (đảo ngược thứ tự Add), phân
        /// trang <see cref="PAGE_SIZE"/>/trang.</summary>
        private void RefreshHistoryPage()
        {
            List<string> history = _profile.Gacha.History;
            int total = history.Count;
            int totalPages = System.Math.Max(1, (total + PAGE_SIZE - 1) / PAGE_SIZE);
            _page = Mathf.Clamp(_page, 0, totalPages - 1);
            _statusLabel.text = (_loc != null ? _loc.Get("gachainfo.label.history_title") : "HISTORY")
                                 + $" · {_page + 1}/{totalPages} ({total})";

            for (int i = 0; i < PAGE_SIZE; i++)
            {
                int idxFromNewest = _page * PAGE_SIZE + i;
                bool active = idxFromNewest < total;
                _rows[i].SetActive(active);
                if (!active) continue;

                string defId = history[total - 1 - idxFromNewest];
                int pullNumber = total - idxFromNewest;

                if (defId == "none")
                {
                    _nameLabels[i].text = $"#{pullNumber} — —";
                    _nameLabels[i].color = Color.gray;
                    _progressLabels[i].text = "";
                    _icons[i].enabled = false;
                    continue;
                }

                var rarity = LookupRarity(defId);
                _nameLabels[i].text = $"#{pullNumber} — {HeroName(defId)}";
                _nameLabels[i].color = TeamSelectScreen.RarityColor(rarity);
                _progressLabels[i].text = rarity.ToString();
                _icons[i].enabled = true;
                _icons[i].sprite = null;
                _icons[i].color = TeamSelectScreen.RarityColor(rarity);
            }

            _prevButton.interactable = _page > 0;
            _nextButton.interactable = _page < totalPages - 1;
        }

        private static Rarity LookupRarity(string defId)
        {
            var def = UnityEngine.AddressableAssets.Addressables
                .LoadAssetAsync<Game.Meta.Content.HeroDefinitionSO>($"Data/Heroes/{defId}").WaitForCompletion();
            return def != null ? def.Rarity : Rarity.Common;
        }
    }
}
