using Game.Core;
using Game.Data.Dto;
using Game.Services.Audio;
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
        private Text _walletLabel;
        private Text[] _nameLabels;
        private Text[] _progressLabels;
        private Image[] _rowCards;
        private Button _attackButton;
        private Button _closeButton;

        private IAudioService _audio;
        private PlayerProfileDto _profile;
        private System.Action _onAttack;
        private System.Action _onClosed;

        public void Open(PlayerProfileDto profile, System.Action onAttack, System.Action onClosed)
        {
            ServiceLocator.TryGet(out _audio);
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
            var prefab = Resources.Load<GameObject>(PrefabPath);
            _root = Instantiate(prefab, transform);

            var panel = _root.transform.Find("Panel");
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
            var attackNameLabel = attackRow.Find("NameLabel").GetComponent<Text>();
            attackNameLabel.text = "Fight the Trial Champion for a Damage Meter score.";
            attackNameLabel.GetComponent<RectTransform>().sizeDelta = new Vector2(240f, 44f);
            attackNameLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            attackNameLabel.verticalOverflow = VerticalWrapMode.Overflow;
            attackRow.Find("ProgressLabel").gameObject.SetActive(false);
            _attackButton = attackRow.Find("ClaimButton").GetComponent<Button>();
            attackRow.Find("ClaimButton/Label").GetComponent<Text>().text = "ATTACK";
            _attackButton.onClick.AddListener(Attack);

            _closeButton = panel.Find("CloseButton").GetComponent<Button>();
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
            _walletLabel.text = $"Best damage this week: {_profile.TrialBoss.BestDamageThisWeek:N0}";

            var tiers = TrialBossSystem.Tiers;
            for (int i = 0; i < tiers.Count; i++)
            {
                var t = tiers[i];
                bool claimed = _profile.TrialBoss.ClaimedTier > i;
                _nameLabels[i].text = $"Tier {i + 1} — {t.DamageThreshold:N0} dmg · {t.Gem} Gem + {t.Shards} Shards";
                _progressLabels[i].text = claimed ? "CLAIMED" : "LOCKED";
                _rowCards[i].color = claimed ? CARD_CLAIMED : CARD_LOCKED;
            }
        }
    }
}
