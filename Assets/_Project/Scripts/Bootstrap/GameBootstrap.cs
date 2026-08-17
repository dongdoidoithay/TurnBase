using System.Collections;
using System.Threading.Tasks;
using Game.Core;
using Game.Core.Scenes;
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
    /// Luồng: Install services → Splash (task-splash-loading.md, 2 giây tối thiểu, chạy song song
    /// Load save) → Title/Home (task-title-screen.md, chờ bấm START) → sang Meta.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour, ISceneTransitionService
    {
        /// <summary>true = bỏ qua Splash/Title/Home, vào thẳng Meta — fast-path debug/skip, KHÔNG
        /// phải hành vi mặc định của người chơi thật (task-title-screen.md §1). Splash CŨNG bị bỏ
        /// qua ở fast-path này (task-splash-loading.md) — buộc chờ 2 giây mỗi lần Play khi đang lặp
        /// nhanh (rapid iteration) sẽ làm phiền đúng luồng mà cờ này sinh ra để phục vụ.</summary>
        [SerializeField] private bool _autoAdvanceToMeta;

        /// <summary>plan.md §10.1 "Splash — Logo, 2 giây". Thời lượng TỐI THIỂU — chạy SONG SONG
        /// với <see cref="LoadProfileAsync"/> (không cộng dồn), chỉ kéo dài thêm nếu load xong
        /// TRƯỚC 2 giây; nếu load lâu hơn thì Splash tự nhiên hiện đúng bằng thời gian load thật,
        /// không chờ thêm.</summary>
        private const int SPLASH_MIN_MILLISECONDS = 2000;

        [Header("Cây __Systems__/__UI__ dựng sẵn trên Hierarchy — không new GameObject() nữa")]
        [SerializeField] private Transform _serviceRoot;
        [SerializeField] private Transform _uiRoot;

        public static PlayerProfileDto Profile { get; private set; }
        public static bool IsReady { get; private set; }

        private static readonly string[] LOADING_TIPS =
        {
            "Tip: Perfect timing on Action Command grants +30% damage.",
            "Tip: Break an enemy's Poise for 1.5x damage until they recover.",
            "Tip: Elements matter — Fire beats Wind, Water beats Fire, Earth beats Water, Wind beats Earth.",
            "Tip: Swap Row lets you change formation mid-turn without ending it.",
            "Tip: Ascend a hero to unlock new skill slots and their Ultimate.",
            "Tip: Analyze reveals an enemy's full stats and resistances for the rest of the battle.",
        };

        /// <summary>Thời gian TỐI THIỂU hiện overlay — <see cref="SceneManager.LoadScene(string)"/>
        /// vẫn ĐỒNG BỘ (không đổi sang LoadSceneAsync — scene trong game này nhỏ, load gần như tức
        /// thì; chuyển hẳn sang async là việc lớn hơn nhiều, đụng 8 điểm gọi, để ngoài phạm vi).
        /// Overlay chỉ cần đủ lâu để đọc được 1 dòng mẹo, không phải progress bar thật.</summary>
        private const float LOADING_MIN_SECONDS = 0.6f;

        /// <summary>Impl <see cref="ISceneTransitionService"/> — đăng ký qua ServiceLocator ở
        /// <see cref="Awake"/>, gọi từ Meta/CombatView qua
        /// <c>ServiceLocator.Get&lt;ISceneTransitionService&gt;()</c> (không tham chiếu thẳng
        /// GameBootstrap — xem doc-comment interface). Coroutine chạy trên CHÍNH GameBootstrap
        /// (DontDestroyOnLoad) nên sống sót qua scene mới dù được gọi từ script SẼ BỊ HUỶ khi scene
        /// đổi (MetaSceneInstaller/BattleSceneInstaller không phải DontDestroyOnLoad).</summary>
        public void LoadSceneWithOverlay(string sceneName) => StartCoroutine(LoadSceneRoutine(sceneName));

        private IEnumerator LoadSceneRoutine(string sceneName)
        {
            var loadingCanvas = _uiRoot.Find("LoadingCanvas")?.gameObject;
            if (loadingCanvas != null)
            {
                var tip = loadingCanvas.transform.Find("TipLabel")?.GetComponent<Text>();
                if (tip != null) tip.text = LOADING_TIPS[Random.Range(0, LOADING_TIPS.Length)];
                loadingCanvas.SetActive(true);
            }

            yield return null; // để overlay render ít nhất 1 frame trước khi bị chặn bởi LoadScene đồng bộ
            yield return new WaitForSecondsRealtime(LOADING_MIN_SECONDS);

            SceneManager.LoadScene(sceneName); // vẫn đồng bộ — trả về khi scene mới đã load xong

            if (loadingCanvas != null) loadingCanvas.SetActive(false);
        }

        private async void Awake()
        {
            DontDestroyOnLoad(gameObject);

            ServiceInstaller.Install(_serviceRoot, _uiRoot);
            ServiceLocator.Register<ISceneTransitionService>(this);

            if (_autoAdvanceToMeta)
            {
                await LoadProfileAsync();
            }
            else
            {
                var splashCanvas = _uiRoot.Find("SplashCanvas").gameObject;
                splashCanvas.SetActive(true);

                var loadTask = LoadProfileAsync();
                var minDelayTask = Task.Delay(SPLASH_MIN_MILLISECONDS);
                await Task.WhenAll(loadTask, minDelayTask);

                splashCanvas.SetActive(false);
            }

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
            LoadSceneWithOverlay("Meta");
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
