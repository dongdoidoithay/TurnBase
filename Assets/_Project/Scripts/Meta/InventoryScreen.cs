using Game.Core;
using Game.Data;
using Game.Data.Dto;
using Game.Meta.Items;
using Game.Services.Audio;
using Game.Services.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Meta
{
    /// <summary>
    /// Modal Inventory — task-inventory-screen.md. Mở từ TopBar. Dựng từ UI_Inventory.prefab
    /// (clone UI_Codex.prefab, đã xoá PrevButton/NextButton — cả 2 tab đều ≤ 6 mục, không cần phân
    /// trang). Đọc thuần, không có hành động nào theo dòng (giống CodexScreen).
    ///
    /// 2 tab: ITEMS (6 vật phẩm tiêu hao, <see cref="ItemCatalog"/>) / MATERIALS (5 vật liệu Ascend
    /// — EssenceI/II/III/Core/EnhanceStone). KHÔNG hiện Gold/Gem (đã có TopBar) hay Energy/Ticket/
    /// Honor (currency chết, không consumer/producer nào — xem task-inventory-screen.md §0).
    ///
    /// `ItemCatalog.ItemDef.Description` KHÔNG hiện ở đây — đo thật bằng `TextGenerator` thấy chuỗi
    /// "×N — {description}" tràn `ProgressLabel` 90px nặng (144-193px cần) — cùng cách
    /// `ShopScreen`/`CodexScreen` cũng không hiện mô tả đầy đủ trong hàng danh sách, chỉ NameLabel +
    /// số lượng.
    /// </summary>
    public sealed class InventoryScreen : MonoBehaviour
    {
        private const string PrefabPath = "Prefabs/UI/Screens/UI_Inventory";
        private const int ROW_COUNT = 6;

        private static readonly CurrencyType[] MATERIALS =
        {
            CurrencyType.EssenceI, CurrencyType.EssenceII, CurrencyType.EssenceIII,
            CurrencyType.Core, CurrencyType.EnhanceStone,
        };

        private GameObject _root;
        private Text _statusLabel;
        private GameObject[] _rows;
        private Text[] _nameLabels;
        private Text[] _progressLabels;
        private Button _switchTabButton;
        private Text _switchTabLabel;
        private Button _closeButton;

        private IAudioService _audio;
        private IEconomyService _economy;
        private PlayerProfileDto _profile;
        private System.Action _onClosed;

        private bool _showingMaterials;

        public void Open(PlayerProfileDto profile, System.Action onClosed)
        {
            ServiceLocator.TryGet(out _audio);
            ServiceLocator.TryGet(out _economy);
            if (_root == null) BuildShell();

            _profile = profile;
            _onClosed = onClosed;
            _showingMaterials = false;
            _root.SetActive(true);
            Refresh();
        }

        private void BuildShell()
        {
            var prefab = Resources.Load<GameObject>(PrefabPath);
            _root = Instantiate(prefab, transform);

            var panel = _root.transform.Find("Panel");
            _statusLabel = panel.Find("WalletLabel").GetComponent<Text>();

            var list = panel.Find("RowListContainer");
            _rows = new GameObject[ROW_COUNT];
            _nameLabels = new Text[ROW_COUNT];
            _progressLabels = new Text[ROW_COUNT];
            for (int i = 0; i < ROW_COUNT; i++)
            {
                var row = list.Find($"Row_{i}");
                _rows[i] = row.gameObject;
                _nameLabels[i] = row.Find("NameLabel").GetComponent<Text>();
                _progressLabels[i] = row.Find("ProgressLabel").GetComponent<Text>();
                // Inventory không có hành động nào theo dòng (đọc thuần) — ẩn hẳn, giống Codex.
                row.Find("ClaimButton").gameObject.SetActive(false);
            }

            _switchTabButton = panel.Find("SwitchTabButton").GetComponent<Button>();
            _switchTabLabel = panel.Find("SwitchTabButton/Label").GetComponent<Text>();
            _switchTabButton.onClick.AddListener(SwitchTab);

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
            _showingMaterials = !_showingMaterials;
            Refresh();
        }

        private void Refresh()
        {
            _switchTabLabel.text = _showingMaterials ? "MATERIALS" : "ITEMS";

            if (_showingMaterials) RefreshMaterialsPage();
            else RefreshItemsPage();
        }

        private void RefreshItemsPage()
        {
            var all = ItemCatalog.ALL;
            _statusLabel.text = $"ITEMS · {all.Length} loại";

            for (int i = 0; i < ROW_COUNT; i++)
            {
                bool active = i < all.Length;
                _rows[i].SetActive(active);
                if (!active) continue;

                var def = all[i];
                long count = _economy?.GetItemCount(_profile.Inventory, def.Type) ?? 0;
                _nameLabels[i].text = def.Name;
                _progressLabels[i].text = $"×{count}";
            }
        }

        private void RefreshMaterialsPage()
        {
            _statusLabel.text = $"MATERIALS · {MATERIALS.Length} loại";

            for (int i = 0; i < ROW_COUNT; i++)
            {
                bool active = i < MATERIALS.Length;
                _rows[i].SetActive(active);
                if (!active) continue;

                var type = MATERIALS[i];
                long count = _economy?.Get(_profile.Wallet, type) ?? 0;
                _nameLabels[i].text = type.ToString();
                _progressLabels[i].text = $"×{count}";
            }
        }
    }
}
