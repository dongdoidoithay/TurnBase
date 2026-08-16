using Game.Core;
using Game.Core.UI;
using Game.Data;
using Game.Data.Dto;
using Game.Services.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Meta.Endgame
{
    /// <summary>
    /// Modal Dungeon hằng ngày — task-endgame.md, plan.md §8.3. 4 dòng cố định (Gold/Exp/
    /// Material/Stone, khớp thứ tự <see cref="DungeonSystem"/>). Mở từ nút DUNGEON trên TopBar.
    /// Dựng từ UI_Dungeon.prefab (nhân bản UI_Quest.prefab, giữ đúng khuôn Row NameLabel/
    /// ProgressLabel/ClaimButton), cùng khuôn <see cref="Quest.QuestScreen"/>.
    /// </summary>
    public sealed class DungeonScreen : MonoBehaviour
    {
        private const string PrefabPath = "Prefabs/UI/Screens/UI_Dungeon";

        private static readonly DungeonKind[] KINDS =
            { DungeonKind.Gold, DungeonKind.Exp, DungeonKind.Material, DungeonKind.Stone };
        private static readonly Color CARD_UNAVAILABLE = new(0.15f, 0.13f, 0.12f, 0.25f);
        private static readonly Color CARD_AVAILABLE = new(0.15f, 0.13f, 0.12f, 0.55f);

        private GameObject _root;
        private Text[] _nameLabels;
        private Text[] _progressLabels;
        private Image[] _rowCards;
        private Button[] _enterButtons;
        private Button _closeButton;

        private IAudioService _audio;
        private PlayerProfileDto _profile;
        private System.Action<DungeonKind> _onEnter;
        private System.Action _onClosed;

        public void Open(PlayerProfileDto profile, System.Action<DungeonKind> onEnter, System.Action onClosed)
        {
            ServiceLocator.TryGet(out _audio);
            if (_root == null) BuildShell();

            _profile = profile;
            _onEnter = onEnter;
            _onClosed = onClosed;
            DungeonSystem.EnsureDailyReset(_profile, System.DateTime.UtcNow);
            _root.SetActive(true);
            Refresh();
        }

        private void BuildShell()
        {
            var prefab = Resources.Load<GameObject>(PrefabPath);
            _root = Instantiate(prefab, transform);

            var panel = _root.transform.Find("Panel");
            LayoutProfileSwitcher.ApplyStretchPanelLandscape(panel.gameObject, "DungeonPanel");
            var list = panel.Find("RowListContainer");
            _nameLabels = new Text[KINDS.Length];
            _progressLabels = new Text[KINDS.Length];
            _rowCards = new Image[KINDS.Length];
            _enterButtons = new Button[KINDS.Length];
            for (int i = 0; i < KINDS.Length; i++)
            {
                var row = list.Find($"Row_{i}");
                _nameLabels[i] = row.Find("NameLabel").GetComponent<Text>();
                _progressLabels[i] = row.Find("ProgressLabel").GetComponent<Text>();
                _rowCards[i] = row.GetComponent<Image>();
                var btn = row.Find("ClaimButton");
                _enterButtons[i] = btn.GetComponent<Button>();
                btn.Find("Label").GetComponent<Text>().text = "ENTER";

                var kind = KINDS[i]; // capture đúng giá trị cho closure
                _enterButtons[i].onClick.AddListener(() => Enter(kind));
            }

            _closeButton = panel.Find("CloseButton").GetComponent<Button>();
            _closeButton.onClick.AddListener(Close);
        }

        private void Close()
        {
            _audio?.PlaySfx("ui/sfx_ui_tick");
            _root.SetActive(false);
            _onClosed?.Invoke();
        }

        private void Enter(DungeonKind kind)
        {
            _audio?.PlaySfx("ui/sfx_ui_confirm");
            Close();
            _onEnter?.Invoke(kind);
        }

        private void Refresh()
        {
            var now = System.DateTime.UtcNow;
            for (int i = 0; i < KINDS.Length; i++)
            {
                var kind = KINDS[i];
                int floor = DungeonSystem.FloorCleared(_profile, kind);
                int next = System.Math.Min(DungeonSystem.NextFloor(_profile, kind), DungeonSystem.MAX_FLOOR);
                bool availableToday = DungeonSystem.IsAvailableToday(kind, now);

                // NameLabel là ô Text hẹp cố định (150×26, kế thừa từ UI_Quest.prefab) — bỏ chữ
                // "Dungeon" (Title modal đã ghi rõ) để tránh wrap dòng 2 bị Truncate mất số tầng.
                _nameLabels[i].text = $"{kind} — Floor {next}/{DungeonSystem.MAX_FLOOR}";
                _progressLabels[i].text = !availableToday
                    ? "Not today"
                    : floor >= DungeonSystem.MAX_FLOOR ? "Cleared today" : $"{floor}/{DungeonSystem.MAX_FLOOR} cleared";
                _enterButtons[i].interactable = DungeonSystem.CanEnter(_profile, kind, now);
                _rowCards[i].color = availableToday ? CARD_AVAILABLE : CARD_UNAVAILABLE;
            }
        }
    }
}
