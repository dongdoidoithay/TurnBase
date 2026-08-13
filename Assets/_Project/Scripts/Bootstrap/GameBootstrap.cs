using System.Threading.Tasks;
using Game.Core;
using Game.Data.Dto;
using Game.Services.Audio;
using Game.Services.Localization;
using Game.Services.Save;
using Game.Services.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Bootstrap
{
    /// <summary>
    /// Entry point trong scene Boot — object-map.md §3.1.
    /// Luồng: Install services → Load save → Apply settings → Title/Home (task-title-screen.md,
    /// chờ bấm START) → sang Meta.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        /// <summary>true = bỏ qua Title/Home, vào thẳng Meta — fast-path debug/skip, KHÔNG phải
        /// hành vi mặc định của người chơi thật (task-title-screen.md §1).</summary>
        [SerializeField] private bool _autoAdvanceToMeta;

        [Header("Cây __Systems__/__UI__ dựng sẵn trên Hierarchy — không new GameObject() nữa")]
        [SerializeField] private Transform _serviceRoot;
        [SerializeField] private Transform _uiRoot;

        public static PlayerProfileDto Profile { get; private set; }
        public static bool IsReady { get; private set; }

        private async void Awake()
        {
            DontDestroyOnLoad(gameObject);

            ServiceInstaller.Install(_serviceRoot, _uiRoot);
            await LoadProfileAsync();

            IsReady = true;
            Debug.Log($"[Boot] Sẵn sàng. Player {Profile.PlayerId[..8]} · " +
                      $"vàng {Profile.Wallet.Gold} · {Profile.Heroes.Count} hero");

            if (_autoAdvanceToMeta) EnterMeta();
            else ShowTitleScreen();
        }

        /// <summary>task-title-screen.md — TitleCanvas dựng tĩnh trong Boot.unity
        /// (GameBootstrap/__UI__/UIRoot/TitleCanvas, sibling MetaCanvas, sortingOrder cao hơn).
        /// Không tách class riêng (khác Mail/Codex/Quest — những màn đó có logic thật, Title chỉ
        /// có đúng 1 việc "chờ bấm START") — gộp thẳng vào GameBootstrap, orchestrator duy nhất
        /// của Boot scene.</summary>
        private void ShowTitleScreen()
        {
            var loc = ServiceLocator.Get<ILocalizationService>();

            var metaCanvas = _uiRoot.Find("MetaCanvas");
            if (metaCanvas != null) metaCanvas.gameObject.SetActive(false);

            var titleCanvas = _uiRoot.Find("TitleCanvas").gameObject;

            // task-localization-pilot.md — "AETHER LEGION" là tên riêng, giá trị GIỐNG NHAU cả 2
            // ngôn ngữ trong strings.csv, nhưng vẫn tra qua key (không hard-code) để nhất quán +
            // chứng minh key hoạt động dù không đổi hiển thị.
            titleCanvas.transform.Find("TitleLabel").GetComponent<Text>().text = loc.Get("title.label.name");

            var subtitle = titleCanvas.transform.Find("SubtitleLabel").GetComponent<Text>();
            subtitle.text = loc.Get("title.label.subtitle", Profile.Heroes.Count, Profile.Wallet.Gold);

            var startButton = titleCanvas.transform.Find("StartButton").GetComponent<Button>();
            startButton.transform.Find("Label").GetComponent<Text>().text = loc.Get("title.button.start");
            startButton.onClick.AddListener(() =>
            {
                titleCanvas.SetActive(false);
                if (metaCanvas != null) metaCanvas.gameObject.SetActive(true);
                EnterMeta();
            });

            titleCanvas.SetActive(true);
        }

        private async Task LoadProfileAsync()
        {
            var repo = ServiceLocator.Get<IPlayerRepository>();
            Profile = await repo.LoadAsync();
            ProfileContext.Current = Profile;

            // Áp cài đặt NGAY sau khi load — trước khi bất kỳ âm thanh nào phát
            ServiceLocator.Get<ISettingsService>().Apply(Profile.Settings);
        }

        private void EnterMeta()
        {
            ServiceLocator.Get<IAudioService>().PlayMusic("bgm_menu");
            SceneManager.LoadScene("Meta");
        }

        // =====================================================================
        // Auto-save theo vòng đời app (plan.md §11.6)
        // =====================================================================

        private void OnApplicationPause(bool paused)
        {
            if (paused) SaveNow();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) SaveNow();
        }

        private void OnApplicationQuit() => SaveNow();

        public static void SaveNow()
        {
            if (!IsReady || Profile == null) return;
            if (!ServiceLocator.TryGet<IPlayerRepository>(out var repo)) return;

            // Đồng bộ cài đặt hiện tại vào profile trước khi ghi
            if (ServiceLocator.TryGet<ISettingsService>(out var settings))
                Profile.Settings = settings.Current;

            _ = repo.SaveAsync(Profile);
        }
    }
}
