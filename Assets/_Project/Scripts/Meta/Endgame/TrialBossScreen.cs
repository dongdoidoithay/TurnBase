using Game.Core;
using Game.Core.UI;
using Game.Data.Dto;
using Game.Services.Audio;
using Game.Services.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Meta.Endgame
{
    /// <summary>
    /// Modal Trial Boss hằng tuần — task-endgame.md, plan.md §8.3. 3 dòng bậc thưởng (chỉ hiển
    /// thị trạng thái — nhận thưởng TỰ ĐỘNG ngay sau mỗi trận, xem
    /// <see cref="MetaSceneInstaller.ApplySpecialBattleResult"/>) + 1 dòng nút ATTACK. Mở từ nút
    /// TRIAL BOSS trên TopBar. Dựng từ UI_TrialBoss.prefab (nhân bản UI_Quest.prefab), cùng khuôn
    /// <see cref="Quest.QuestScreen"/>/<see cref="DungeonScreen"/>.
    /// </summary>
    public sealed class TrialBossScreen : MonoBehaviour
    {
        private const string PrefabPath = "Prefabs/UI/Screens/UI_TrialBoss";
        private const int TIER_ROW_COUNT = 3;
        private static readonly Color CARD_CLAIMED = new(0.15f, 0.13f, 0.12f, 0.25f);
        private static readonly Color CARD_LOCKED = new(0.15f, 0.13f, 0.12f, 0.55f);

        private GameObject _root;
        private TextMeshProUGUI _titleLabel;
        private Text _walletLabel;
        private Text _promptLabel;
        private Text[] _nameLabels;
        private Text[] _progressLabels;
        private Image[] _rowCards;
        private Button _attackButton;
        private Text _attackLabel;
        private Button _closeButton;
        private Text _closeLabel;

        private IAudioService _audio;
        private ILocalizationService _loc;
        private PlayerProfileDto _profile;
        private System.Action _onAttack;
        private System.Action _onClosed;

        public void Open(PlayerProfileDto profile, System.Action onAttack, System.Action onClosed)
        {
            ServiceLocator.TryGet(out _audio);
            ServiceLocator.TryGet(out _loc);
            if (_root == null) BuildShell();

            _profile = profile;
            _onAttack = onAttack;
            _onClosed = onClosed;
            TrialBossSystem.EnsureWeeklyReset(_profile, System.DateTime.UtcNow);
            _root.SetActive(true);
            Refresh();
        }

        private void BuildShell()
        {
            var prefab = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>(PrefabPath).WaitForCompletion();
            _root = Instantiate(prefab, transform);

            var panel = _root.transform.Find("Panel");
            LayoutProfileSwitcher.ApplyStretchPanelLandscape(panel.gameObject, "TrialBossPanel");
            _titleLabel = panel.Find("InnerBlue/TitleText").GetComponent<TextMeshProUGUI>();
            _walletLabel = panel.Find("WalletLabel").GetComponent<Text>();
            // Box gốc (200×26, Wrap+Truncate) đủ cho "Gold 999999 Gem 999" của QuestScreen nhưng
            // không đủ cho "Best damage this week: N" — nới rộng để số không bị wrap-rồi-mất.
            _walletLabel.GetComponent<RectTransform>().sizeDelta = new Vector2(320f, 26f);

            var list = panel.Find("RowListContainer");
            _nameLabels = new Text[TIER_ROW_COUNT];
            _progressLabels = new Text[TIER_ROW_COUNT];
            _rowCards = new Image[TIER_ROW_COUNT];
            for (int i = 0; i < TIER_ROW_COUNT; i++)
            {
                var row = list.Find($"Row_{i}");
                _nameLabels[i] = row.Find("NameLabel").GetComponent<Text>();
                _progressLabels[i] = row.Find("ProgressLabel").GetComponent<Text>();
                _rowCards[i] = row.GetComponent<Image>();
                // Bậc thưởng chỉ hiển thị trạng thái — không có hành động thủ công (tự nhận
                // thưởng ngay sau trận), ẩn nút để tránh gây hiểu nhầm là bấm được.
                var claimBtn = row.Find("ClaimButton");
                claimBtn.gameObject.SetActive(false);

                // NameLabel/ProgressLabel kế thừa hộp hẹp cố định từ UI_Quest.prefab (150×26 /
                // 90×26 — đủ cho "[Daily] Battles Won · +50" nhưng không đủ cho câu dài hơn ở
                // đây) — nới rộng bằng đúng khoảng trống ClaimButton vừa ẩn (~90 đơn vị) để câu
                // "Tier N — X dmg · Y Gem + Z Shards" không bị wrap dòng 2 rồi bị Truncate mất.
                var nameRt = _nameLabels[i].GetComponent<RectTransform>();
                nameRt.sizeDelta = new Vector2(260f, nameRt.sizeDelta.y);
                _nameLabels[i].fontSize = 11;

                var progRt = _progressLabels[i].GetComponent<RectTransform>();
                progRt.anchoredPosition = new Vector2(248f, progRt.anchoredPosition.y);
            }

            var attackRow = list.Find($"Row_{TIER_ROW_COUNT}");
            _promptLabel = attackRow.Find("NameLabel").GetComponent<Text>();
            _promptLabel.GetComponent<RectTransform>().sizeDelta = new Vector2(240f, 44f);
            _promptLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            _promptLabel.verticalOverflow = VerticalWrapMode.Overflow;
            attackRow.Find("ProgressLabel").gameObject.SetActive(false);
            _attackButton = attackRow.Find("ClaimButton").GetComponent<Button>();
            _attackLabel = attackRow.Find("ClaimButton/Label").GetComponent<Text>();
            _attackButton.onClick.AddListener(Attack);

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

        private void Attack()
        {
            _audio?.PlaySfx("ui/sfx_ui_confirm");
            Close();
            _onAttack?.Invoke();
        }

        private void Refresh()
        {
            if (_loc != null)
            {
                _titleLabel.text = _loc.Get("trialboss.label.title");
                _closeLabel.text = _loc.Get("trialboss.button.close");
                _attackLabel.text = _loc.Get("trialboss.button.attack");
                _promptLabel.text = _loc.Get("trialboss.label.prompt");
            }
            else
            {
                _promptLabel.text = "Fight the Trial Champion for a Damage Meter score.";
            }

            _walletLabel.text = _loc != null
                ? _loc.Get("trialboss.label.wallet", _profile.TrialBoss.BestDamageThisWeek.ToString("N0"))
                : $"Best damage this week: {_profile.TrialBoss.BestDamageThisWeek:N0}";

            string claimedText = _loc != null ? _loc.Get("trialboss.label.claimed") : "CLAIMED";
            string lockedText = _loc != null ? _loc.Get("trialboss.label.locked") : "LOCKED";
            var tiers = TrialBossSystem.Tiers;
            for (int i = 0; i < tiers.Count; i++)
            {
                var t = tiers[i];
                bool claimed = _profile.TrialBoss.ClaimedTier > i;
                _nameLabels[i].text = _loc != null
                    ? _loc.Get("trialboss.label.tier", i + 1, t.DamageThreshold.ToString("N0"), t.Gem, t.Shards)
                    : $"Tier {i + 1} — {t.DamageThreshold:N0} dmg · {t.Gem} Gem + {t.Shards} Shards";
                _progressLabels[i].text = claimed ? claimedText : lockedText;
                _rowCards[i].color = claimed ? CARD_CLAIMED : CARD_LOCKED;
            }
        }
    }
}
