using System.Collections.Generic;
using Game.Core;
using Game.Core.UI;
using Game.Data;
using Game.Data.Dto;
using Game.Meta.Hero;
using Game.Meta.Items;
using Game.Services.Audio;
using Game.Services.Economy;
using Game.Services.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Meta
{
    /// <summary>
    /// Modal Inventory — task-inventory-screen.md. Mở từ TopBar. Dựng từ UI_Inventory.prefab —
    /// người dùng tự thiết kế lại tay theo phong cách UI_02 (CharacterBox/InventoryGridBg 24 ô
    /// lưới/StatsBg dùng TextMeshPro), KHÁC HẲN bản list 6-dòng+tab cũ. Class này viết lại để khớp
    /// cấu trúc mới, không còn ITEMS/MATERIALS tách tab — gộp chung 1 danh sách (6 vật phẩm tiêu
    /// hao + 5 vật liệu Ascend = 11 mục) hiển thị đồng thời.
    ///
    /// GHI CHÚ GIỚI HẠN THẬT (prefab đang xây dở, không phải lỗi code):
    /// - Cả 24/24 ô lưới nay có con "Icon" (Image) — 11 ô đầu (Slot_0..10) gán SẴN sprite thật
    ///   trong prefab, đúng khớp thứ tự cố định lúc biên dịch của <see cref="ItemCatalog.ALL"/> +
    ///   <see cref="MATERIALS"/> (không cần tra cứu runtime — cả 2 mảng là hằng số, gán tĩnh 1 lần
    ///   là đủ, giống mọi TopBar icon khác trong dự án). 13 ô còn lại (11-23) không có sprite, code
    ///   chỉ ẩn/hiện theo <see cref="Refresh"/>, dự phòng khi danh sách item/material dài thêm.
    /// - <see cref="Close"/> nay có CloseButton thật trong `ActionBg` (trước đó trống — người chơi
    ///   không có cách nào tự đóng màn này, phát hiện qua audit "action không gắn chức năng nào").
    /// - `CharacterBox` nay hiện chân dung/tên/level hero đầu roster ("leader", cùng quy ước
    ///   <c>profile.Heroes[0]</c> đã dùng ở TopBar) thay vì chỉ chữ "INVENTORY" phủ kín — xem
    ///   <see cref="RefreshLeader"/>.
    /// </summary>
    public sealed class InventoryScreen : MonoBehaviour
    {
        private const string PrefabPath = "Prefabs/UI/Screens/UI_Inventory";

        private static readonly CurrencyType[] MATERIALS =
        {
            CurrencyType.EssenceI, CurrencyType.EssenceII, CurrencyType.EssenceIII,
            CurrencyType.Core, CurrencyType.EnhanceStone,
        };

        private GameObject _root;
        private TextMeshProUGUI _titleLabel;
        private TextMeshProUGUI _statsText;
        private Button _closeButton;
        private Text _closeLabel;
        private Image _leaderPortrait;
        private Text _leaderNameLabel;
        private Text _leaderLevelLabel;
        private readonly List<Image> _slotIcons = new(); // phần tử null = ô đó chưa có "Icon" con

        private IAudioService _audio;
        private IEconomyService _economy;
        private ILocalizationService _loc;
        private PlayerProfileDto _profile;
        private System.Action _onClosed;

        public void Open(PlayerProfileDto profile, System.Action onClosed)
        {
            ServiceLocator.TryGet(out _audio);
            ServiceLocator.TryGet(out _economy);
            ServiceLocator.TryGet(out _loc);
            if (_root == null) BuildShell();

            _profile = profile;
            _onClosed = onClosed;
            _root.SetActive(true);
            Refresh();
        }

        private void BuildShell()
        {
            var prefab = Resources.Load<GameObject>(PrefabPath);
            _root = Instantiate(prefab, transform);

            ApplyLandscapePilot();

            _titleLabel = _root.transform.Find("CharacterBox/InnerBlue/PlaceholderText")
                ?.GetComponent<TextMeshProUGUI>();

            // Leader showcase — CharacterBox trước đây chỉ có chữ "INVENTORY" phủ kín, không có
            // chân dung/thông tin nào dù chiếm gần nửa màn hình. Dùng đúng hero đầu roster
            // (profile.Heroes[0]) — cùng quy ước "leader" đã dùng ở TopBar (RefreshLeaderPortrait).
            var characterBox = _root.transform.Find("CharacterBox/InnerBlue");
            _leaderPortrait = characterBox.Find("PortraitRing/PortraitMask/PortraitSprite").GetComponent<Image>();
            _leaderNameLabel = characterBox.Find("LeaderNameLabel").GetComponent<Text>();
            _leaderLevelLabel = characterBox.Find("LeaderLevelLabel").GetComponent<Text>();

            _statsText = _root.transform.Find("StatsBg/InnerGreen/StatsText").GetComponent<TextMeshProUGUI>();

            var grid = _root.transform.Find("InventoryGridBg/Inner/Grid");
            _slotIcons.Clear();
            foreach (Transform slot in grid)
            {
                if (!slot.name.StartsWith("Slot_")) continue;
                var icon = slot.Find("Icon");
                _slotIcons.Add(icon != null ? icon.GetComponent<Image>() : null);
            }

            _closeButton = _root.transform.Find("InventoryGridBg/Inner/ActionBg/CloseButton").GetComponent<Button>();
            _closeLabel = _closeButton.transform.Find("Label").GetComponent<Text>();
            _closeButton.onClick.AddListener(Close);
        }

        /// <summary>
        /// task-ui-vfx-polish.md §7 — Landscape cho màn duy nhất KHÔNG dùng 1 "Panel" chung
        /// (§5 từng để dành vì rủi ro này): <c>CharacterBox</c>/<c>InventoryGridBg</c>/<c>StatsBg</c>
        /// là 3 fraction ĐỘC LẬP của root, trong đó <c>InventoryGridBg</c> (đáy 0.35) và
        /// <c>StatsBg</c> (đỉnh 0.32) chỉ cách nhau 0.03 — không thể áp công thức nới-biên-đều
        /// (<see cref="LayoutProfileSwitcher.ApplyStretchPanelLandscape"/>) cho từng khối riêng lẻ
        /// vì sẽ ăn hết/vượt quá khoảng cách 0.03 đó, gây chồng lấn. Thay vào đó tính tay: nới biên
        /// trên/dưới TOÀN CỤM (0.05→0.02 mỗi bên, +6% chiều cao — cùng lý do CanvasScaler đã tính ở
        /// §5) rồi CHIA LẠI khoảng trống dư đó theo ĐÚNG TỈ LỆ cũ giữa 3 phần (Grid 66.7% · gap
        /// 3.3% · Stats 30% trên tổng biên dọc 0.90) thay vì giữ nguyên số tuyệt đối — giữ được
        /// khoảng cách Grid/Stats tỉ lệ thuận với biên mới (0.03 → 0.03×0.96/0.90≈0.032, làm tròn
        /// 0.03) thay vì cố định cứng có thể lệch tỉ lệ khi biên đổi.
        /// </summary>
        private void ApplyLandscapePilot()
        {
            var characterBox = (RectTransform)_root.transform.Find("CharacterBox");
            var gridBg = (RectTransform)_root.transform.Find("InventoryGridBg");
            var statsBg = (RectTransform)_root.transform.Find("StatsBg");

            var cbPortrait = LayoutProfile.CaptureFrom(characterBox, "InvCharacterBox_Portrait");
            var cbLandscape = cbPortrait;
            cbLandscape.Name = "InvCharacterBox_Landscape";
            cbLandscape.AnchorMin = new Vector2(cbPortrait.AnchorMin.x, 0.02f);
            cbLandscape.AnchorMax = new Vector2(cbPortrait.AnchorMax.x, 0.98f);
            characterBox.gameObject.AddComponent<LayoutProfileSwitcher>()
                .SetProfiles(characterBox, cbPortrait, cbLandscape);

            var gridPortrait = LayoutProfile.CaptureFrom(gridBg, "InvGridBg_Portrait");
            var gridLandscape = gridPortrait;
            gridLandscape.Name = "InvGridBg_Landscape";
            gridLandscape.AnchorMin = new Vector2(gridPortrait.AnchorMin.x, 0.34f);
            gridLandscape.AnchorMax = new Vector2(gridPortrait.AnchorMax.x, 0.98f);
            gridBg.gameObject.AddComponent<LayoutProfileSwitcher>()
                .SetProfiles(gridBg, gridPortrait, gridLandscape);

            var statsPortrait = LayoutProfile.CaptureFrom(statsBg, "InvStatsBg_Portrait");
            var statsLandscape = statsPortrait;
            statsLandscape.Name = "InvStatsBg_Landscape";
            statsLandscape.AnchorMin = new Vector2(statsPortrait.AnchorMin.x, 0.02f);
            statsLandscape.AnchorMax = new Vector2(statsPortrait.AnchorMax.x, 0.31f);
            statsBg.gameObject.AddComponent<LayoutProfileSwitcher>()
                .SetProfiles(statsBg, statsPortrait, statsLandscape);
        }

        private void Close()
        {
            _audio?.PlaySfx("ui/sfx_ui_tick");
            _root.SetActive(false);
            _onClosed?.Invoke();
        }

        private void RefreshLeader()
        {
            if (_profile.Heroes.Count == 0)
            {
                _leaderPortrait.enabled = false;
                _leaderNameLabel.text = "";
                _leaderLevelLabel.text = "";
                return;
            }

            var leader = _profile.Heroes[0];
            var sprite = Resources.Load<Sprite>($"Art/Characters/Heroes/{leader.DefId}/{leader.DefId}_v1_00");
            _leaderPortrait.sprite = sprite;
            _leaderPortrait.enabled = sprite != null;
            _leaderNameLabel.text = _loc != null
                ? _loc.GetName(leader.DefId, LocalizedNameKind.Hero)
                : HeroDisplayUtil.FormatName(leader.DefId);
            _leaderLevelLabel.text = $"Lv.{leader.Level}";
        }

        private void Refresh()
        {
            if (_titleLabel != null)
                _titleLabel.text = _loc != null ? _loc.Get("inventory.label.title") : "INVENTORY";
            if (_closeLabel != null)
                _closeLabel.text = _loc != null ? _loc.Get("inventory.button.close") : "CLOSE";

            RefreshLeader();

            var entries = new List<(string name, long count)>();
            foreach (var def in ItemCatalog.ALL)
            {
                long count = _economy?.GetItemCount(_profile.Inventory, def.Type) ?? 0;
                entries.Add((def.Name, count));
            }
            foreach (var type in MATERIALS)
            {
                long count = _economy?.Get(_profile.Wallet, type) ?? 0;
                entries.Add((type.ToString(), count));
            }

            // Danh sách đầy đủ tên+số lượng luôn hiện ở đây — lưới icon hiện chưa đủ ô có Icon
            // con để hiện riêng từng nhãn, StatsText là nguồn thông tin đầy đủ duy nhất lúc này.
            var sb = new System.Text.StringBuilder();
            foreach (var e in entries) sb.AppendLine($"{e.name}  ×{e.count}");
            _statsText.text = sb.ToString();

            for (int i = 0; i < _slotIcons.Count; i++)
            {
                var icon = _slotIcons[i];
                if (icon == null) continue; // ô chưa có Icon con — bỏ qua an toàn

                // 11 ô đầu đã gán sẵn sprite thật trong prefab (đúng thứ tự entries) — chỉ cần
                // ẩn/hiện theo còn dữ liệu hay không, không tô màu đè lên sprite thật nữa.
                icon.enabled = i < entries.Count && icon.sprite != null;
            }
        }
    }
}
