using System.Text;
using Game.Core;
using Game.Core.Random;
using Game.Data;
using Game.Data.Dto;
using Game.Meta.Gacha;
using Game.Meta.Hero;
using Game.Services.Audio;
using Game.Services.Economy;
using Game.Services.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Meta
{
    /// <summary>
    /// Modal Summon (gacha) — task-ascend.md §7 mục B.3. Mở từ SummonButton trên TopBar (không
    /// qua map node, khác Shop). Dựng từ UI_Summon.prefab (Instantiate + Find), cùng khuôn
    /// <see cref="HeroDetailScreen"/>/<see cref="ShopScreen"/>. Kết quả hiển thị dạng text nhiều
    /// dòng thay vì list card riêng — đủ cho bản tối giản, tránh phải dựng thêm 1 prefab con.
    /// </summary>
    public sealed class SummonScreen : MonoBehaviour
    {
        private const string PrefabPath = "Prefabs/UI/Screens/UI_Summon";

        private GameObject _root;
        private Text _walletLabel, _resultsLabel;
        private Button _pullOneButton, _pullTenButton, _closeButton;

        private IAudioService _audio;
        private IEconomyService _economy;
        private ILocalizationService _loc;
        private PlayerProfileDto _profile;
        private IRandomSource _rng;
        private System.Action _onProfileChanged;

        // task-phase-5-gaps.md Phần D — HeroDisplayUtil vẫn là fallback cuối khi service (hiếm khi)
        // chưa đăng ký, không đổi hành vi cũ trong trường hợp đó.
        private string HeroName(string defId) =>
            _loc != null ? _loc.GetName(defId, LocalizedNameKind.Hero) : HeroDisplayUtil.FormatName(defId);

        public void Open(PlayerProfileDto profile, System.Action onProfileChanged)
        {
            ServiceLocator.TryGet(out _audio);
            ServiceLocator.TryGet(out _economy);
            ServiceLocator.TryGet(out _loc);
            if (_root == null) BuildShell();

            _profile = profile;
            _onProfileChanged = onProfileChanged;
            _rng ??= new XorShiftRandom((ulong)System.DateTime.UtcNow.Ticks);

            _root.SetActive(true);
            _resultsLabel.text = "";
            Refresh();
        }

        private void BuildShell()
        {
            var prefab = Resources.Load<GameObject>(PrefabPath);
            _root = Instantiate(prefab, transform);

            var panel = _root.transform.Find("Panel");
            _walletLabel = panel.Find("WalletLabel").GetComponent<Text>();
            _resultsLabel = panel.Find("ResultsText").GetComponent<Text>();

            _pullOneButton = panel.Find("PullOneButton").GetComponent<Button>();
            _pullTenButton = panel.Find("PullTenButton").GetComponent<Button>();
            _pullOneButton.onClick.AddListener(() => Pull(1));
            _pullTenButton.onClick.AddListener(() => Pull(10));

            _closeButton = panel.Find("CloseButton").GetComponent<Button>();
            _closeButton.onClick.AddListener(Close);
        }

        private void Close()
        {
            _audio?.PlaySfx("ui/sfx_ui_tick");
            _root.SetActive(false);
        }

        private void Pull(int count)
        {
            if (!GachaSystem.CanPull(_profile, count)) return;

            var results = GachaSystem.Pull(_profile, count, _rng);
            if (results.Count == 0) return;

            var sb = new StringBuilder();
            foreach (var r in results)
            {
                string name = r.HeroDefId != null ? HeroName(r.HeroDefId) : "???";
                string tag = r.IsNewHero ? "NEW" : r.ShardsGranted > 0 ? $"+{r.ShardsGranted} shard" : "";
                sb.AppendLine($"{r.Rarity} · {name}  {tag}");
            }
            _resultsLabel.text = sb.ToString();

            _audio?.PlaySfx("ui/sfx_ui_confirm");
            Refresh();
            _onProfileChanged?.Invoke();
        }

        private void Refresh()
        {
            long gem = _economy?.Get(_profile.Wallet, CurrencyType.Gem) ?? 0;
            _walletLabel.text = $"Gem {gem}";
            _pullOneButton.interactable = gem >= GachaSystem.SINGLE_PULL_COST;
            _pullTenButton.interactable = gem >= GachaSystem.TEN_PULL_COST;
        }
    }
}
