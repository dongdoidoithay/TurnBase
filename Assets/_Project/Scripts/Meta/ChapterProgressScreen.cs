using Game.Core;
using Game.Core.UI;
using Game.Data.Dto;
using Game.Services.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Meta
{
    /// <summary>
    /// plan.md §10.1 "Chapter Select — 5 chương" — task-chapter-arena.md. THUẦN ĐỌC tiến trình
    /// (chương nào đã qua/đang chơi/chưa mở), KHÔNG cho chọn lại chương để chơi — lối chơi hiện tại
    /// là tuyến tính (<see cref="MetaSceneInstaller.EnsureRun"/> luôn sinh NodeMap theo đúng
    /// <c>Progress.ChapterUnlocked</c>, không nhận chapter tuỳ ý). Mở từ bấm vào chính
    /// <c>TitleLabel</c> ("CHAPTER N") trên TopBar — không thêm nút TopBar mới (đã khá đầy).
    /// </summary>
    public sealed class ChapterProgressScreen : MonoBehaviour
    {
        private const string PrefabPath = "Prefabs/UI/Screens/UI_ChapterProgress";
        private const int CHAPTER_COUNT = 5;

        private GameObject _root;
        private Text[] _nameLabels;
        private Text[] _statusLabels;
        private Image[] _rowCards;
        private Button _closeButton;

        private IAudioService _audio;
        private System.Action _onClosed;

        public void Open(PlayerProfileDto profile, System.Action onClosed)
        {
            ServiceLocator.TryGet(out _audio);
            if (_root == null) BuildShell();

            _onClosed = onClosed;
            _root.SetActive(true);
            Refresh(profile);
        }

        private void BuildShell()
        {
            var prefab = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>(PrefabPath).WaitForCompletion();
            _root = Instantiate(prefab, transform);

            var panel = _root.transform.Find("Panel");
            LayoutProfileSwitcher.ApplyStretchPanelLandscape(panel.gameObject, "ChapterProgressPanel");

            var list = panel.Find("RowListContainer");
            _nameLabels = new Text[CHAPTER_COUNT];
            _statusLabels = new Text[CHAPTER_COUNT];
            _rowCards = new Image[CHAPTER_COUNT];
            for (int i = 0; i < CHAPTER_COUNT; i++)
            {
                var row = list.Find($"Row_{i}");
                _nameLabels[i] = row.Find("NameLabel").GetComponent<Text>();
                _statusLabels[i] = row.Find("ProgressLabel").GetComponent<Text>();
                _rowCards[i] = row.GetComponent<Image>();
                // Thuần đọc — không có hành động theo dòng, khác Quest/Mail có Claim.
                row.Find("ClaimButton").gameObject.SetActive(false);
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

        private void Refresh(PlayerProfileDto profile)
        {
            int unlocked = profile.Progress.ChapterUnlocked;
            for (int i = 0; i < CHAPTER_COUNT; i++)
            {
                int chapterNum = i + 1;
                var accent = MetaSceneInstaller.ChapterAccent(chapterNum);
                string name = chapterNum <= MetaSceneInstaller.CHAPTER_NAMES.Length
                    ? MetaSceneInstaller.CHAPTER_NAMES[chapterNum - 1] : $"Chapter {chapterNum}";

                _nameLabels[i].text = $"Chapter {chapterNum} — {name}";

                string status;
                Color cardTint;
                if (chapterNum < unlocked || unlocked > CHAPTER_COUNT)
                {
                    status = "CLEARED";
                    cardTint = new Color(accent.r * 0.3f, accent.g * 0.3f, accent.b * 0.3f, 0.5f);
                }
                else if (chapterNum == unlocked)
                {
                    status = "IN PROGRESS";
                    cardTint = new Color(accent.r * 0.5f, accent.g * 0.5f, accent.b * 0.5f, 0.75f);
                }
                else
                {
                    status = "LOCKED";
                    cardTint = new Color(0.2f, 0.18f, 0.22f, 0.5f);
                }
                _statusLabels[i].text = status;
                _statusLabels[i].color = chapterNum > unlocked ? new Color(0.5f, 0.46f, 0.5f) : accent;
                _rowCards[i].color = cardTint;
            }
        }
    }
}
