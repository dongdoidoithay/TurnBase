using Game.Core;
using Game.Core.UI;
using Game.Data;
using Game.Data.Dto;
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
    /// Modal Shop mở từ node <see cref="NodeType.Shop"/> trên map — task-ascend.md §7 mục C.
    /// Bán vật liệu Ascend (Gem) + task-consumable-items.md: 6 vật phẩm tiêu hao (Vàng), catalog
    /// cố định (không ScriptableObject — 10 dòng hàng không đáng để thêm 1 lớp data-driven).
    /// Dựng từ UI_Shop.prefab (Instantiate + Find), cùng khuôn <see cref="HeroDetailScreen"/>.
    /// </summary>
    public sealed class ShopScreen : MonoBehaviour
    {
        private const string PrefabPath = "Prefabs/UI/Screens/UI_Shop";

        private readonly struct CatalogItem
        {
            public readonly string Label;
            public readonly CurrencyType PriceCurrency;
            public readonly long Price;
            /// <summary>Vật liệu Ascend — null nếu dòng này bán vật phẩm tiêu hao (xem
            /// <see cref="ItemToGrant"/>, đúng 1 trong 2 luôn có giá trị).</summary>
            public readonly CurrencyType? MaterialType;
            public readonly long MaterialAmount;
            /// <summary>task-consumable-items.md — vật phẩm tiêu hao, cấp vào
            /// <c>profile.Inventory.Items</c> (khác Wallet của vật liệu Ascend).</summary>
            public readonly ItemType? ItemToGrant;

            public CatalogItem(string label, CurrencyType priceCurrency, long price,
                CurrencyType materialType, long materialAmount)
            {
                Label = label; PriceCurrency = priceCurrency; Price = price;
                MaterialType = materialType; MaterialAmount = materialAmount; ItemToGrant = null;
            }

            public CatalogItem(string label, CurrencyType priceCurrency, long price, ItemType itemToGrant)
            {
                Label = label; PriceCurrency = priceCurrency; Price = price;
                MaterialType = null; MaterialAmount = 0; ItemToGrant = itemToGrant;
            }
        }

        /// <summary>Giá đặt TẠM (task-ascend.md §7 mục C.1 + plan.md §7.5) — chờ Balance Harness/
        /// playtest tinh chỉnh, chưa phải số liệu cân bằng cuối. Không bán Mảnh Hero cụ thể ở bản
        /// này (cần UI chọn hero) — gacha trùng hero đã cover phần đó (mục B).</summary>
        private static readonly CatalogItem[] CATALOG =
        {
            new("5 Essence I", CurrencyType.Gem, 100, CurrencyType.EssenceI, 5),
            new("5 Essence II", CurrencyType.Gem, 250, CurrencyType.EssenceII, 5),
            new("5 Essence III", CurrencyType.Gem, 500, CurrencyType.EssenceIII, 5),
            new("3 Core", CurrencyType.Gem, 400, CurrencyType.Core, 3),
            new("Potion", CurrencyType.Gold, 200, ItemType.Potion),
            new("Ether", CurrencyType.Gold, 300, ItemType.Ether),
            new("Antidote", CurrencyType.Gold, 250, ItemType.Antidote),
            new("Smoke Bomb", CurrencyType.Gold, 500, ItemType.SmokeBomb),
            new("Revive Feather", CurrencyType.Gold, 1_500, ItemType.ReviveFeather),
            new("Elemental Bomb", CurrencyType.Gold, 800, ItemType.ElementalBomb),
        };

        private GameObject _root;
        private TextMeshProUGUI _titleLabel;
        private Text _walletLabel;
        private Text[] _rowNameLabels;
        private Text[] _rowPriceLabels;
        private Button[] _buyButtons;
        private Text _closeLabel;
        private Button _closeButton;

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
            var prefab = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>(PrefabPath).WaitForCompletion();
            _root = Instantiate(prefab, transform);

            var panel = _root.transform.Find("Panel");
            LayoutProfileSwitcher.ApplyStretchPanelLandscape(panel.gameObject, "ShopPanel");
            _titleLabel = panel.Find("InnerBlue/TitleText").GetComponent<TextMeshProUGUI>();
            _walletLabel = panel.Find("WalletLabel").GetComponent<Text>();

            var list = panel.Find("RowListContainer");
            _rowNameLabels = new Text[CATALOG.Length];
            _rowPriceLabels = new Text[CATALOG.Length];
            _buyButtons = new Button[CATALOG.Length];
            for (int i = 0; i < CATALOG.Length; i++)
            {
                var row = list.Find($"Row_{i}");
                _rowNameLabels[i] = row.Find("NameLabel").GetComponent<Text>();
                var btn = row.Find("BuyButton");
                _rowPriceLabels[i] = btn.Find("Label").GetComponent<Text>();
                _buyButtons[i] = btn.GetComponent<Button>();

                int index = i; // capture đúng giá trị cho closure
                _buyButtons[i].onClick.AddListener(() => Buy(index));
            }

            _closeButton = panel.Find("CloseButton").GetComponent<Button>();
            _closeLabel = panel.Find("CloseButton/Label").GetComponent<Text>();
            _closeButton.onClick.AddListener(Close);
        }

        private void Close()
        {
            _audio?.PlaySfx("ui/sfx_ui_tick");
            _root.SetActive(false);
            _onClosed?.Invoke();
        }

        private void Buy(int index)
        {
            var item = CATALOG[index];
            if (_economy == null) return;
            if (_economy.Get(_profile.Wallet, item.PriceCurrency) < item.Price) return;

            _economy.TryConsume(_profile.Wallet, item.PriceCurrency, item.Price);
            if (item.ItemToGrant.HasValue)
                _economy.GrantItem(_profile.Inventory, item.ItemToGrant.Value, 1);
            else
                _economy.Grant(_profile.Wallet, item.MaterialType!.Value, item.MaterialAmount);

            _audio?.PlaySfx("ui/sfx_ui_confirm");
            Refresh();
        }

        private void Refresh()
        {
            if (_loc != null)
            {
                _titleLabel.text = _loc.Get("shop.label.title");
                _closeLabel.text = _loc.Get("shop.button.close");
            }

            long gold = _economy?.Get(_profile.Wallet, CurrencyType.Gold) ?? 0;
            long gem = _economy?.Get(_profile.Wallet, CurrencyType.Gem) ?? 0;
            _walletLabel.text = _loc != null
                ? _loc.Get("shop.label.wallet", gold, gem)
                : $"Gold {gold}   Gem {gem}";

            for (int i = 0; i < CATALOG.Length; i++)
            {
                var item = CATALOG[i];
                long balance = _economy?.Get(_profile.Wallet, item.PriceCurrency) ?? 0;
                _rowNameLabels[i].text = item.Label;
                _rowPriceLabels[i].text = $"{item.Price} {item.PriceCurrency}";
                _buyButtons[i].interactable = balance >= item.Price;
            }
        }
    }
}
