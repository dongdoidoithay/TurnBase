using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Core.UI;
using Game.Data;
using Game.Data.Dto;
using Game.Meta.Content;
using Game.Meta.Hero;
using Game.Services.Audio;
using Game.Services.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Meta.HeroList
{
    /// <summary>
    /// plan.md §10.1 "Hero List — Lọc/sắp xếp" — task-hero-list.md. Browse TOÀN BỘ hero SỞ HỮU,
    /// khác <see cref="Codex.CodexScreen"/> (liệt kê CẢ hero CHƯA mở khoá dạng "???", không lọc/
    /// sắp xếp được) và khác <see cref="TeamSelectScreen"/> (chỉ để CHỌN đội hình 4 hero, không có
    /// bộ lọc). Bấm 1 dòng mở <see cref="HeroDetailScreen"/> — tái dùng màn quản lý hero có sẵn,
    /// không tự vẽ chi tiết ở đây. Mở từ TopBar (nút "HeroListButton").
    /// </summary>
    public sealed class HeroListScreen : MonoBehaviour
    {
        private const string PrefabPath = "Prefabs/UI/Screens/UI_HeroList";
        private const int PAGE_SIZE = 6;

        private enum SortMode { Level, Rarity, Name }
        private static readonly SortMode[] SORT_CYCLE = { SortMode.Level, SortMode.Rarity, SortMode.Name };
        private static readonly string[] SORT_LABELS = { "SORT: LEVEL", "SORT: RARITY", "SORT: NAME" };

        // index 0 = ALL, 1..6 = HeroClass (Vanguard..Summoner theo đúng thứ tự enum).
        private static readonly string[] FILTER_LABELS =
        {
            "CLASS: ALL", "CLASS: VANGUARD", "CLASS: SLAYER", "CLASS: ARCANIST",
            "CLASS: WARDEN", "CLASS: TRICKSTER", "CLASS: SUMMONER",
        };

        private GameObject _root;
        private Text _statusLabel;
        private Text[] _nameLabels;
        private Text[] _statLabels;
        private Image[] _icons;
        private Button[] _rowButtons;
        private Button _sortButton, _filterButton, _prevButton, _nextButton, _closeButton;
        private Text _sortLabel, _filterLabel;

        private IAudioService _audio;
        private ILocalizationService _loc;
        private PlayerProfileDto _profile;
        private System.Action _onClosed;
        private HeroDetailScreen _detailScreen;

        private int _sortIndex;
        private int _filterIndex;
        private int _page;
        private List<HeroInstanceDto> _filteredSorted = new();

        private string HeroName(string defId) =>
            _loc != null ? _loc.GetName(defId, LocalizedNameKind.Hero) : HeroDisplayUtil.FormatName(defId);

        private static HeroDefinitionSO FindHeroDef(string defId) =>
            UnityEngine.AddressableAssets.Addressables
                .LoadAssetAsync<HeroDefinitionSO>($"Data/Heroes/{defId}").WaitForCompletion();

        public void Open(PlayerProfileDto profile, System.Action onClosed)
        {
            ServiceLocator.TryGet(out _audio);
            ServiceLocator.TryGet(out _loc);
            if (_root == null) BuildShell();

            _profile = profile;
            _onClosed = onClosed;
            _page = 0;
            _root.SetActive(true);
            Refresh();
        }

        private void BuildShell()
        {
            var prefab = Resources.Load<GameObject>(PrefabPath);
            _root = Instantiate(prefab, transform);

            var panel = _root.transform.Find("Panel");
            LayoutProfileSwitcher.ApplyStretchPanelLandscape(panel.gameObject, "HeroListPanel");
            _statusLabel = panel.Find("WalletLabel").GetComponent<Text>();

            var list = panel.Find("RowListContainer");
            _nameLabels = new Text[PAGE_SIZE];
            _statLabels = new Text[PAGE_SIZE];
            _icons = new Image[PAGE_SIZE];
            _rowButtons = new Button[PAGE_SIZE];
            for (int i = 0; i < PAGE_SIZE; i++)
            {
                var row = list.Find($"Row_{i}");
                _nameLabels[i] = row.Find("NameLabel").GetComponent<Text>();
                _statLabels[i] = row.Find("ProgressLabel").GetComponent<Text>();
                _icons[i] = row.Find("Icon").GetComponent<Image>();
                _rowButtons[i] = row.GetComponent<Button>();

                int slot = i; // capture đúng giá trị cho closure
                _rowButtons[i].onClick.AddListener(() => OpenDetail(slot));
            }

            _sortButton = panel.Find("SortButton").GetComponent<Button>();
            _sortLabel = panel.Find("SortButton/Label").GetComponent<Text>();
            _sortButton.onClick.AddListener(CycleSort);

            _filterButton = panel.Find("FilterButton").GetComponent<Button>();
            _filterLabel = panel.Find("FilterButton/Label").GetComponent<Text>();
            _filterButton.onClick.AddListener(CycleFilter);

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

        private void CycleSort()
        {
            _audio?.PlaySfx("ui/sfx_ui_tick");
            _sortIndex = (_sortIndex + 1) % SORT_CYCLE.Length;
            _page = 0;
            Refresh();
        }

        private void CycleFilter()
        {
            _audio?.PlaySfx("ui/sfx_ui_tick");
            _filterIndex = (_filterIndex + 1) % FILTER_LABELS.Length;
            _page = 0;
            Refresh();
        }

        private void ChangePage(int delta)
        {
            _audio?.PlaySfx("ui/sfx_ui_tick");
            _page += delta;
            Refresh();
        }

        private HeroDetailScreen EnsureDetailScreen()
        {
            if (_detailScreen == null)
            {
                _detailScreen = gameObject.GetComponent<HeroDetailScreen>() ?? gameObject.AddComponent<HeroDetailScreen>();
                // Ascend/nâng skill đổi Wallet+stat — refresh lại danh sách (sort theo Level/Rarity
                // có thể đổi thứ tự) khi đóng chi tiết, đúng mẫu TeamSelectScreen.EnsureDetailScreen.
                _detailScreen.OnProfileChanged += Refresh;
            }
            return _detailScreen;
        }

        private void OpenDetail(int slot)
        {
            int idx = _page * PAGE_SIZE + slot;
            if (idx >= _filteredSorted.Count) return;
            _audio?.PlaySfx("ui/sfx_ui_tick");
            EnsureDetailScreen().Open(_profile, _filteredSorted[idx]);
        }

        private void Refresh()
        {
            _sortLabel.text = SORT_LABELS[_sortIndex];
            _filterLabel.text = FILTER_LABELS[_filterIndex];

            // Tính 1 lần/refresh thay vì trong từng lambda Where/OrderBy — 24 hero tối đa nên
            // không đáng để cache lâu dài, chỉ tránh gọi Addressables.WaitForCompletion() lặp lại
            // nhiều lần cho CÙNG 1 hero trong 1 lượt sort/filter.
            var defCache = new Dictionary<string, HeroDefinitionSO>();
            HeroDefinitionSO DefOf(string defId)
            {
                if (!defCache.TryGetValue(defId, out var def))
                    defCache[defId] = def = FindHeroDef(defId);
                return def;
            }

            IEnumerable<HeroInstanceDto> query = _profile.Heroes;
            if (_filterIndex > 0)
            {
                var cls = (HeroClass)(_filterIndex - 1);
                query = query.Where(h => DefOf(h.DefId)?.Class == cls);
            }

            _filteredSorted = SORT_CYCLE[_sortIndex] switch
            {
                SortMode.Level => query.OrderByDescending(h => h.Level).ToList(),
                SortMode.Rarity => query.OrderByDescending(h => DefOf(h.DefId)?.Rarity ?? Rarity.Common)
                                         .ThenByDescending(h => h.Level).ToList(),
                _ => query.OrderBy(h => HeroName(h.DefId)).ToList(),
            };

            int totalPages = Mathf.Max(1, (_filteredSorted.Count + PAGE_SIZE - 1) / PAGE_SIZE);
            _page = Mathf.Clamp(_page, 0, totalPages - 1);
            _statusLabel.text = $"{_filteredSorted.Count} heroes  ·  Page {_page + 1}/{totalPages}";

            for (int i = 0; i < PAGE_SIZE; i++)
            {
                int idx = _page * PAGE_SIZE + i;
                bool active = idx < _filteredSorted.Count;
                _rowButtons[i].gameObject.SetActive(active);
                if (!active) continue;

                var hero = _filteredSorted[idx];
                var def = DefOf(hero.DefId);
                var rarityColor = def != null ? TeamSelectScreen.RarityColor(def.Rarity) : new Color(0.65f, 0.62f, 0.66f);

                _nameLabels[i].text = HeroName(hero.DefId);
                _statLabels[i].text = def != null
                    ? $"{def.Class} · {def.Element} · Lv{hero.Level} ★{hero.Star}"
                    : $"Lv{hero.Level} ★{hero.Star}";

                _icons[i].color = rarityColor;
                var sprite = Resources.Load<Sprite>($"Art/Characters/Heroes/{hero.DefId}/{hero.DefId}_v1_00");
                _icons[i].sprite = sprite;
                _icons[i].enabled = sprite != null;
            }

            _prevButton.interactable = _page > 0;
            _nextButton.interactable = _page < totalPages - 1;
        }
    }
}
