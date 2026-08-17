using Game.Core;
using Game.Core.UI;
using Game.Data;
using Game.Data.Dto;
using Game.Services.Audio;
using Game.Services.Economy;
using Game.Services.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Meta.Quest
{
    /// <summary>
    /// Modal Quest — task-quest.md. 3 Daily Quest + 3 Achievement dùng chung 1 danh sách 6 dòng.
    /// Mở từ nút QUEST trên TopBar. Dựng từ UI_Quest.prefab (Instantiate + Find), cùng khuôn
    /// <see cref="Meta.ShopScreen"/>/<see cref="Meta.SummonScreen"/>.
    /// </summary>
    public sealed class QuestScreen : MonoBehaviour
    {
        private const string PrefabPath = "Prefabs/UI/Screens/UI_Quest";
        private const int ROW_COUNT = 6; // 3 Daily + 3 Achievement, cố định V1

        private GameObject _root;
        private TextMeshProUGUI _titleLabel;
        private Text _walletLabel;
        private Text[] _nameLabels;
        private Text[] _progressLabels;
        private Button[] _claimButtons;
        private Text[] _claimLabels;
        private Button _closeButton;
        private Text _closeLabel;

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
            QuestSystem.EnsureDailyReset(_profile, System.DateTime.UtcNow);
            _root.SetActive(true);
            Refresh();
        }

        private void BuildShell()
        {
            var prefab = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>(PrefabPath).WaitForCompletion();
            _root = Instantiate(prefab, transform);

            var panel = _root.transform.Find("Panel");
            LayoutProfileSwitcher.ApplyStretchPanelLandscape(panel.gameObject, "QuestPanel");
            _titleLabel = panel.Find("InnerBlue/TitleText").GetComponent<TextMeshProUGUI>();
            _walletLabel = panel.Find("WalletLabel").GetComponent<Text>();

            var list = panel.Find("RowListContainer");
            _nameLabels = new Text[ROW_COUNT];
            _progressLabels = new Text[ROW_COUNT];
            _claimButtons = new Button[ROW_COUNT];
            _claimLabels = new Text[ROW_COUNT];
            for (int i = 0; i < ROW_COUNT; i++)
            {
                var row = list.Find($"Row_{i}");
                _nameLabels[i] = row.Find("NameLabel").GetComponent<Text>();
                _progressLabels[i] = row.Find("ProgressLabel").GetComponent<Text>();
                var btn = row.Find("ClaimButton");
                _claimButtons[i] = btn.GetComponent<Button>();
                _claimLabels[i] = btn.Find("Label").GetComponent<Text>();

                int index = i; // capture đúng giá trị cho closure
                _claimButtons[i].onClick.AddListener(() => Claim(index));
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

        private void Claim(int index)
        {
            var dailies = QuestSystem.DailyQuests;
            bool ok = index < dailies.Count
                ? QuestSystem.TryClaimDaily(_profile, dailies[index].Id)
                : QuestSystem.TryClaimAchievement(_profile, QuestSystem.Achievements[index - dailies.Count].Id);

            if (!ok) return;
            _audio?.PlaySfx("ui/sfx_ui_confirm");
            Refresh();
        }

        private void Refresh()
        {
            string dailyTag = _loc != null ? _loc.Get("quest.label.daily_tag") : "[Daily]";
            string achvTag = _loc != null ? _loc.Get("quest.label.achv_tag") : "[Achv]";
            string doneText = _loc != null ? _loc.Get("quest.label.done") : "DONE";
            string readyText = _loc != null ? _loc.Get("quest.label.ready") : "READY";
            string lockedText = _loc != null ? _loc.Get("quest.label.locked") : "LOCKED";

            if (_loc != null)
            {
                _titleLabel.text = _loc.Get("quest.label.title");
                _closeLabel.text = _loc.Get("quest.button.close");
                string claimText = _loc.Get("quest.button.claim");
                for (int i = 0; i < _claimLabels.Length; i++) _claimLabels[i].text = claimText;
            }

            long gem = _economy?.Get(_profile.Wallet, CurrencyType.Gem) ?? 0;
            _walletLabel.text = _loc != null ? _loc.Get("quest.label.wallet", gem) : $"Gem {gem}";

            var dailies = QuestSystem.DailyQuests;
            for (int i = 0; i < dailies.Count; i++)
            {
                var q = dailies[i];
                int progress = QuestSystem.GetDailyProgress(_profile, q.Id);
                bool claimed = _profile.Quests.ClaimedQuestIds.Contains(q.Id);
                _nameLabels[i].text = $"{dailyTag} {FormatAchievementName(q.Condition.ToString())} · +{q.GemReward}";
                _progressLabels[i].text = claimed
                    ? doneText
                    : $"{System.Math.Min(progress, q.Target)}/{q.Target}";
                _claimButtons[i].interactable = !claimed && progress >= q.Target;
            }

            var achievements = QuestSystem.Achievements;
            for (int i = 0; i < achievements.Count; i++)
            {
                int slot = dailies.Count + i;
                var a = achievements[i];
                bool claimed = _profile.Quests.UnlockedAchievements.Contains(a.Id);
                bool unlocked = a.IsUnlocked(_profile);
                _nameLabels[slot].text = $"{achvTag} {FormatAchievementName(a.NameKey)} · +{a.GemReward}";
                _progressLabels[slot].text = claimed ? doneText : (unlocked ? readyText : lockedText);
                _claimButtons[slot].interactable = !claimed && unlocked;
            }
        }

        /// <summary>"achievement.collect_6_heroes" → "Collect 6 Heroes", "BattlesWon" → "Battles
        /// Won" — chưa có hệ localization thật (NameKey/QuestConditionType chỉ là khoá kỹ thuật),
        /// rút gọn tại chỗ để không tràn ô NameLabel (rộng vừa tên skill ngắn, không phải cả câu).</summary>
        private static string FormatAchievementName(string raw)
        {
            int dot = raw.LastIndexOf('.');
            if (dot >= 0) raw = raw[(dot + 1)..];
            raw = raw.Replace('_', ' ');
            raw = System.Text.RegularExpressions.Regex.Replace(raw, "(?<=[a-z])(?=[A-Z])", " ");

            var words = raw.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < words.Length; i++)
                words[i] = char.ToUpperInvariant(words[i][0]) + words[i][1..];
            return string.Join(" ", words);
        }
    }
}
