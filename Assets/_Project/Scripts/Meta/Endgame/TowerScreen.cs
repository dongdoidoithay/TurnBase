using Game.Core;
using Game.Core.UI;
using Game.Data.Dto;
using Game.Services.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Meta.Endgame
{
    /// <summary>
    /// Modal Tháp Vô Tận hằng tuần — task-endgame.md, plan.md §8.3. 5 dòng bậc thưởng (chỉ hiển
    /// thị trạng thái — nhận thưởng TỰ ĐỘNG ngay sau mỗi lượt leo, xem
    /// <see cref="MetaSceneInstaller.ApplySpecialBattleResult"/>) + 1 dòng nút CLIMB. Mở từ nút
    /// TOWER trên TopBar. Dựng từ UI_Tower.prefab (nhân bản UI_Quest.prefab — giữ nguyên đủ 6 row
    /// gốc, khác Dungeon/TrialBoss phải trim bớt), cùng khuôn <see cref="TrialBossScreen"/>.
    /// </summary>
    public sealed class TowerScreen : MonoBehaviour
    {
        private const string PrefabPath = "Prefabs/UI/Screens/UI_Tower";
        private static readonly int TIER_ROW_COUNT = TowerSystem.Tiers.Count;
        private static readonly Color CARD_CLAIMED = new(0.15f, 0.13f, 0.12f, 0.25f);
        private static readonly Color CARD_LOCKED = new(0.15f, 0.13f, 0.12f, 0.55f);

        private GameObject _root;
        private Text _walletLabel;
        private Text[] _nameLabels;
        private Text[] _progressLabels;
        private Image[] _rowCards;
        private Button _climbButton;
        private Button _closeButton;

        private IAudioService _audio;
        private PlayerProfileDto _profile;
        private System.Action _onClimb;
        private System.Action _onClosed;

        public void Open(PlayerProfileDto profile, System.Action onClimb, System.Action onClosed)
        {
            ServiceLocator.TryGet(out _audio);
            if (_root == null) BuildShell();

            _profile = profile;
            _onClimb = onClimb;
            _onClosed = onClosed;
            TowerSystem.EnsureWeeklyReset(_profile, System.DateTime.UtcNow);
            _root.SetActive(true);
            Refresh();
        }

        private void BuildShell()
        {
            var prefab = Resources.Load<GameObject>(PrefabPath);
            _root = Instantiate(prefab, transform);

            var panel = _root.transform.Find("Panel");
            LayoutProfileSwitcher.ApplyStretchPanelLandscape(panel.gameObject, "TowerPanel");
            _walletLabel = panel.Find("WalletLabel").GetComponent<Text>();

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
                row.Find("ClaimButton").gameObject.SetActive(false);

                // Box gốc (150×26/90×26) đủ cho text ngắn của QuestScreen nhưng không đủ cho câu
                // dài hơn ở đây — nới rộng để tránh wrap-rồi-Truncate (xem task-endgame.md).
                var nameRt = _nameLabels[i].GetComponent<RectTransform>();
                nameRt.sizeDelta = new Vector2(260f, nameRt.sizeDelta.y);
                _nameLabels[i].fontSize = 11;

                var progRt = _progressLabels[i].GetComponent<RectTransform>();
                progRt.anchoredPosition = new Vector2(248f, progRt.anchoredPosition.y);
            }

            var attackRow = list.Find($"Row_{TIER_ROW_COUNT}");
            var attackNameLabel = attackRow.Find("NameLabel").GetComponent<Text>();
            attackNameLabel.text = "Ready to climb?";
            attackNameLabel.GetComponent<RectTransform>().sizeDelta = new Vector2(240f, attackNameLabel.GetComponent<RectTransform>().sizeDelta.y);
            attackRow.Find("ProgressLabel").gameObject.SetActive(false);
            _climbButton = attackRow.Find("ClaimButton").GetComponent<Button>();
            attackRow.Find("ClaimButton/Label").GetComponent<Text>().text = "CLIMB";
            _climbButton.onClick.AddListener(Climb);

            _closeButton = panel.Find("CloseButton").GetComponent<Button>();
            _closeButton.onClick.AddListener(Close);
        }

        private void Close()
        {
            _audio?.PlaySfx("ui/sfx_ui_tick");
            _root.SetActive(false);
            _onClosed?.Invoke();
        }

        private void Climb()
        {
            _audio?.PlaySfx("ui/sfx_ui_confirm");
            Close();
            _onClimb?.Invoke();
        }

        private void Refresh()
        {
            _walletLabel.text = $"This week: {_profile.Tower.BestFloorThisWeek}   ·   All-time: {_profile.Progress.TowerFloor}";

            var tiers = TowerSystem.Tiers;
            for (int i = 0; i < tiers.Count; i++)
            {
                var t = tiers[i];
                bool claimed = _profile.Tower.ClaimedTier > i;
                string reward = t.MythicEquipment
                    ? $"{t.Gem:N0} Gem · {t.Core} Core · 1 Mythic gear"
                    : t.Core > 0 ? $"{t.Gem:N0} Gem · {t.Core} Core" : $"{t.Gem:N0} Gem";
                _nameLabels[i].text = $"Floor {t.FloorThreshold} — {reward}";
                _progressLabels[i].text = claimed ? "CLAIMED" : "LOCKED";
                _rowCards[i].color = claimed ? CARD_CLAIMED : CARD_LOCKED;
            }
        }
    }
}
