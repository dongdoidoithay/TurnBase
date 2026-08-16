using System.Collections.Generic;
using Game.Core;
using Game.Data;
using Game.Data.Dto;
using Game.Meta.Items;
using Game.Services.Audio;
using Game.Services.Economy;
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
    /// - CHỈ vài ô lưới đầu có sẵn con "Icon" (Image) — ô nào chưa có bị bỏ qua an toàn (không
    ///   crash), tự động hiện thêm khi prefab có thêm Icon.
    /// - Chưa có icon THẬT theo từng loại vật phẩm (chưa có asset) — tô màu phẳng phân biệt
    ///   Item/Material tạm thời thay icon thật.
    /// - Prefab hiện KHÔNG có CloseButton nào — <see cref="Close"/> vẫn gọi được qua code
    ///   (MetaSceneInstaller có thể wire nút khác gọi vào) nhưng chưa có nút bấm thật trên UI để
    ///   người chơi tự đóng màn này.
    /// </summary>
    public sealed class InventoryScreen : MonoBehaviour
    {
        private const string PrefabPath = "Prefabs/UI/Screens/UI_Inventory";

        private static readonly CurrencyType[] MATERIALS =
        {
            CurrencyType.EssenceI, CurrencyType.EssenceII, CurrencyType.EssenceIII,
            CurrencyType.Core, CurrencyType.EnhanceStone,
        };

        private static readonly Color ITEM_TINT = new(0.271f, 0.482f, 0.616f, 1f);
        private static readonly Color MATERIAL_TINT = new(0.647f, 0.365f, 0.898f, 1f);
        private static readonly Color EMPTY_TINT = new(0.2f, 0.2f, 0.2f, 0.35f);

        private GameObject _root;
        private TextMeshProUGUI _statsText;
        private readonly List<Image> _slotIcons = new(); // phần tử null = ô đó chưa có "Icon" con

        private IAudioService _audio;
        private IEconomyService _economy;
        private PlayerProfileDto _profile;
        private System.Action _onClosed;

        public void Open(PlayerProfileDto profile, System.Action onClosed)
        {
            ServiceLocator.TryGet(out _audio);
            ServiceLocator.TryGet(out _economy);
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

            var titleLabel = _root.transform.Find("CharacterBox/InnerBlue/PlaceholderText")
                ?.GetComponent<TextMeshProUGUI>();
            if (titleLabel != null) titleLabel.text = "INVENTORY";

            _statsText = _root.transform.Find("StatsBg/InnerGreen/StatsText").GetComponent<TextMeshProUGUI>();

            var grid = _root.transform.Find("InventoryGridBg/Inner/Grid");
            _slotIcons.Clear();
            foreach (Transform slot in grid)
            {
                if (!slot.name.StartsWith("Slot_")) continue;
                var icon = slot.Find("Icon");
                _slotIcons.Add(icon != null ? icon.GetComponent<Image>() : null);
            }
        }

        private void Close()
        {
            _audio?.PlaySfx("ui/sfx_ui_tick");
            _root.SetActive(false);
            _onClosed?.Invoke();
        }

        private void Refresh()
        {
            var entries = new List<(string name, long count, Color tint)>();
            foreach (var def in ItemCatalog.ALL)
            {
                long count = _economy?.GetItemCount(_profile.Inventory, def.Type) ?? 0;
                entries.Add((def.Name, count, ITEM_TINT));
            }
            foreach (var type in MATERIALS)
            {
                long count = _economy?.Get(_profile.Wallet, type) ?? 0;
                entries.Add((type.ToString(), count, MATERIAL_TINT));
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

                icon.enabled = true;
                icon.color = i < entries.Count ? entries[i].tint : EMPTY_TINT;
            }
        }
    }
}
