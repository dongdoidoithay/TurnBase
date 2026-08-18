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
    /// Modal Arena PvP — task-arena.md, plan.md v1.1. 5 dòng đối thủ (snapshot RNG sinh, KHÔNG
    /// phải người chơi thật khác — xem task file §1), mỗi dòng CHALLENGE 1 lần/mùa (14 ngày). Mở
    /// từ nút ARENA trên TopBar. Dựng từ UI_Arena.prefab (nhân bản UI_Quest.prefab), cùng khuôn
    /// <see cref="TowerScreen"/>/<see cref="TrialBossScreen"/>.
    /// </summary>
    public sealed class ArenaScreen : MonoBehaviour
    {
        private const string PrefabPath = "Prefabs/UI/Screens/UI_Arena";
        private const int ROW_COUNT = ArenaSystem.OPPONENT_COUNT;
        private static readonly Color CARD_CLAIMED = new(0.15f, 0.13f, 0.12f, 0.25f);
        private static readonly Color CARD_AVAILABLE = new(0.15f, 0.13f, 0.12f, 0.55f);

        private GameObject _root;
        private TextMeshProUGUI _titleLabel;
        private Text _ratingLabel;
        private Text[] _nameLabels;
        private Text[] _progressLabels;
        private Image[] _rowCards;
        private Button[] _challengeButtons;
        private Text[] _challengeLabels;
        private Button _closeButton;
        private Text _closeLabel;

        private IAudioService _audio;
        private ILocalizationService _loc;
        private PlayerProfileDto _profile;
        private System.Action<int> _onChallenge;
        private System.Action _onClosed;

        public void Open(PlayerProfileDto profile, System.Action<int> onChallenge, System.Action onClosed)
        {
            ServiceLocator.TryGet(out _audio);
            ServiceLocator.TryGet(out _loc);
            if (_root == null) BuildShell();

            _profile = profile;
            _onChallenge = onChallenge;
            _onClosed = onClosed;
            _root.SetActive(true);
            Refresh();
        }

        private void BuildShell()
        {
            var prefab = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>(PrefabPath).WaitForCompletion();
            _root = Instantiate(prefab, transform);

            var panel = _root.transform.Find("Panel");
            LayoutProfileSwitcher.ApplyStretchPanelLandscape(panel.gameObject, "ArenaPanel");
            _titleLabel = panel.Find("InnerBlue/TitleText").GetComponent<TextMeshProUGUI>();
            _ratingLabel = panel.Find("WalletLabel").GetComponent<Text>();

            var list = panel.Find("RowListContainer");
            _nameLabels = new Text[ROW_COUNT];
            _progressLabels = new Text[ROW_COUNT];
            _rowCards = new Image[ROW_COUNT];
            _challengeButtons = new Button[ROW_COUNT];
            _challengeLabels = new Text[ROW_COUNT];
            for (int i = 0; i < ROW_COUNT; i++)
            {
                var row = list.Find($"Row_{i}");
                _nameLabels[i] = row.Find("NameLabel").GetComponent<Text>();
                _progressLabels[i] = row.Find("ProgressLabel").GetComponent<Text>();
                _rowCards[i] = row.GetComponent<Image>();
                var btn = row.Find("ClaimButton");
                _challengeButtons[i] = btn.GetComponent<Button>();
                _challengeLabels[i] = btn.Find("Label").GetComponent<Text>();

                int index = i; // capture đúng giá trị cho closure
                _challengeButtons[i].onClick.AddListener(() => Challenge(index));
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

        private void Challenge(int index)
        {
            _audio?.PlaySfx("ui/sfx_ui_confirm");
            _onChallenge?.Invoke(index);
        }

        private void Refresh()
        {
            if (_loc != null)
            {
                _titleLabel.text = _loc.Get("arena.label.title");
                _closeLabel.text = _loc.Get("arena.button.close");
                string challengeText = _loc.Get("arena.button.challenge");
                for (int i = 0; i < _challengeLabels.Length; i++) _challengeLabels[i].text = challengeText;
            }

            _ratingLabel.text = _loc != null
                ? _loc.Get("arena.label.rating", _profile.Arena.Rating)
                : $"Rating: {_profile.Arena.Rating}";

            string claimedText = _loc != null ? _loc.Get("arena.label.claimed") : "CLAIMED";
            var opponents = _profile.Arena.Opponents;
            for (int i = 0; i < ROW_COUNT; i++)
            {
                // Luôn hiện đủ 5 dòng — chỉ rơi vào nhánh "..." nếu PickArenaOpponents chưa từng
                // chạy (lý thuyết không xảy ra vì OpenArena luôn gọi trước khi Open() màn này).
                bool hasOpponent = i < opponents.Count;
                if (!hasOpponent)
                {
                    _nameLabels[i].text = "...";
                    _progressLabels[i].text = "";
                    _challengeButtons[i].interactable = false;
                    _rowCards[i].color = CARD_CLAIMED;
                    continue;
                }

                var opp = opponents[i];
                _nameLabels[i].text = _loc != null
                    ? _loc.Get("arena.label.opponent", i + 1, ShortNames(opp.HeroDefIds), opp.Level)
                    : $"Tier {i + 1} — {ShortNames(opp.HeroDefIds)} · Lv.{opp.Level}";
                _progressLabels[i].text = opp.Claimed ? claimedText : $"+{opp.HonorReward} Honor";
                _challengeButtons[i].interactable = !opp.Claimed;
                _rowCards[i].color = opp.Claimed ? CARD_CLAIMED : CARD_AVAILABLE;
            }
        }

        private static string ShortNames(string[] heroDefIds)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < heroDefIds.Length; i++)
            {
                if (i > 0) sb.Append('/');
                string raw = heroDefIds[i].StartsWith("hero_") ? heroDefIds[i][5..] : heroDefIds[i];
                int us = raw.IndexOf('_');
                sb.Append(us > 0 ? raw[..us] : raw);
            }
            return sb.ToString();
        }
    }
}
