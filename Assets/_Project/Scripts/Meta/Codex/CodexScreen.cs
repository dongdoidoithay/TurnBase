using Game.Core;
using Game.Core.UI;
using Game.Data.Dto;
using Game.Meta.Hero;
using Game.Services.Audio;
using Game.Services.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Meta.Codex
{
    /// <summary>
    /// Modal Codex/Collection — task-codex.md. Mở từ TopBar. Dựng từ UI_Codex.prefab (clone
    /// UI_Quest.prefab + thêm 3 nút SwitchTabButton/PrevButton/NextButton). Khác mọi screen khác
    /// trong dự án (Quest/Mail dùng đúng 6 mục cố định) — Codex là màn ĐẦU TIÊN cần phân trang
    /// thật (24 hero / 66 enemy đều vượt quá 6 mục 1 trang, xem task-codex.md §0).
    /// </summary>
    public sealed class CodexScreen : MonoBehaviour
    {
        private const string PrefabPath = "Prefabs/UI/Screens/UI_Codex";
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

        private bool _showingEnemies;
        private int _page;

        // task-phase-5-gaps.md Phần D — HeroDisplayUtil vẫn là fallback cuối khi service (hiếm khi)
        // chưa đăng ký, không đổi hành vi cũ trong trường hợp đó.
        private string HeroName(string defId) =>
            _loc != null ? _loc.GetName(defId, LocalizedNameKind.Hero) : HeroDisplayUtil.FormatName(defId);
        private string EnemyName(string defId) =>
            _loc != null ? _loc.GetName(defId, LocalizedNameKind.Enemy) : HeroDisplayUtil.FormatEnemyName(defId);

        /// <summary>Portrait tĩnh dùng chung quy ước với HeroDetailScreen — "{defId}/{defId}_v1_00"
        /// (hero VÀ enemy đều có, xác nhận qua Resources thật, khác bộ animation frame trong
        /// Animations/ chỉ dùng lúc chiến đấu). Ẩn icon khi CHƯA mở khoá — tránh lộ hình trước khi
        /// người chơi thật sự gặp/sở hữu (khớp tinh thần "???" của tên).</summary>
        private void SetIcon(Image icon, string folder, string defId, bool unlocked)
        {
            icon.enabled = unlocked;
            if (!unlocked) return;
            icon.sprite = Resources.Load<Sprite>($"Art/Characters/{folder}/{defId}/{defId}_v1_00");
            icon.enabled = icon.sprite != null;
        }

        public void Open(PlayerProfileDto profile, System.Action onClosed)
        {
            ServiceLocator.TryGet(out _audio);
            ServiceLocator.TryGet(out _loc);
            if (_root == null) BuildShell();

            _profile = profile;
            _onClosed = onClosed;
            _showingEnemies = false;
            _page = 0;
            _root.SetActive(true);
            Refresh();
        }

        private void BuildShell()
        {
            var prefab = Resources.Load<GameObject>(PrefabPath);
            _root = Instantiate(prefab, transform);

            var panel = _root.transform.Find("Panel");
            LayoutProfileSwitcher.ApplyStretchPanelLandscape(panel.gameObject, "CodexPanel");
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
                // Codex không có hành động nào theo dòng (khác Quest/Mail có Claim) — ẩn hẳn.
                row.Find("ClaimButton").gameObject.SetActive(false);
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
            _showingEnemies = !_showingEnemies;
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
            _switchTabLabel.text = _showingEnemies ? "ENEMIES" : "HEROES";

            if (_showingEnemies) RefreshEnemyPage();
            else RefreshHeroPage();
        }

        private void RefreshHeroPage()
        {
            var all = CodexSystem.AllHeroes;
            int totalPages = System.Math.Max(1, (all.Count + PAGE_SIZE - 1) / PAGE_SIZE);
            _page = Mathf.Clamp(_page, 0, totalPages - 1);
            _statusLabel.text = $"HEROES · Page {_page + 1}/{totalPages}";

            for (int i = 0; i < PAGE_SIZE; i++)
            {
                int idx = _page * PAGE_SIZE + i;
                bool active = idx < all.Count;
                _rows[i].SetActive(active);
                if (!active) continue;

                var def = all[idx];
                bool unlocked = CodexSystem.IsHeroUnlocked(_profile, def);
                _nameLabels[i].text = unlocked ? HeroName(def.DefId) : "???";
                _progressLabels[i].text = unlocked
                    ? $"{def.Class} · {def.Element} · {def.Rarity}"
                    : "LOCKED";
                SetIcon(_icons[i], "Heroes", def.DefId, unlocked);
            }

            _prevButton.interactable = _page > 0;
            _nextButton.interactable = _page < totalPages - 1;
        }

        private void RefreshEnemyPage()
        {
            var all = CodexSystem.AllEnemies;
            int totalPages = System.Math.Max(1, (all.Count + PAGE_SIZE - 1) / PAGE_SIZE);
            _page = Mathf.Clamp(_page, 0, totalPages - 1);
            _statusLabel.text = $"ENEMIES · Page {_page + 1}/{totalPages}";

            for (int i = 0; i < PAGE_SIZE; i++)
            {
                int idx = _page * PAGE_SIZE + i;
                bool active = idx < all.Count;
                _rows[i].SetActive(active);
                if (!active) continue;

                var def = all[idx];
                bool unlocked = CodexSystem.IsEnemyUnlocked(_profile, def);
                _nameLabels[i].text = unlocked ? EnemyName(def.DefId) : "???";
                _progressLabels[i].text = unlocked
                    ? $"{def.Archetype} · {def.Element} · Ch.{def.Chapter}"
                    : $"LOCKED — Ch.{def.Chapter}";
                SetIcon(_icons[i], "Enemies", def.DefId, unlocked);
            }

            _prevButton.interactable = _page > 0;
            _nextButton.interactable = _page < totalPages - 1;
        }
    }
}
