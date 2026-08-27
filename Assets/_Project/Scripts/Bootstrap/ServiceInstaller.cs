using Game.Core;
using Game.Core.Events;
using Game.Core.Times;
using Game.Core.UI;
using Game.Core.UI.Popups;
using Game.Services.Audio;
using Game.Services.Economy;
using Game.Services.Localization;
using Game.Services.Save;
using Game.Services.Settings;
using UnityEngine;

namespace Game.Bootstrap
{
    /// <summary>
    /// COMPOSITION ROOT — nơi DUY NHẤT được phép `new` service (plan.md §11.7).
    /// Đổi từ local sang server ở v2 = đổi đúng dòng đăng ký IPlayerRepository.
    /// </summary>
    public static class ServiceInstaller
    {
        public static void Install(Transform serviceRoot, Transform uiRoot)
        {
            ServiceLocator.Clear();

            // UIRoot dựng sẵn trên Hierarchy (__UI__/UIRoot, DontDestroyOnLoad) — các scene
            // khác gắn Canvas riêng của mình vào đây thay vì tự nổi Canvas ở scene root.
            ServiceLocator.Register<IUiRootHost>(new UiRootHost(uiRoot));

            // --- Không phụ thuộc Unity ---
            ServiceLocator.Register<IEventBus>(new EventBus());
            ServiceLocator.Register<IGameClock>(new SystemGameClock());
            ServiceLocator.Register<ISettingsService>(new SettingsService());
            // Cổng duy nhất cho mọi thay đổi Wallet — object-map.md §6.2, không sửa Wallet
            // trực tiếp ở nơi khác nữa.
            ServiceLocator.Register<IEconomyService>(new EconomyService());
            // task-localization-pilot.md — pilot hạ tầng, chỉ Title screen + SettingsScreen dùng
            // key thật, phần còn lại của game vẫn hard-code chuỗi (xem task file).
            ServiceLocator.Register<ILocalizationService>(new LocalizationService());

            // --- Lưu game ---
            // v2: đổi dòng dưới thành `new RemotePlayerRepository(apiClient)` là xong.
            ServiceLocator.Register<IPlayerRepository>(
                new LocalPlayerRepository(migrations: new SaveMigrationRunner()));

            // --- Cần MonoBehaviour ---
            // AudioRoot được dựng sẵn trên Hierarchy (__Systems__/ServiceRoot/AudioRoot),
            // không new GameObject() nữa — rơi về serviceRoot nếu thiếu (an toàn khi test scene lẻ).
            var audioRoot = serviceRoot != null ? serviceRoot.Find("AudioRoot") : null;
            ServiceLocator.Register<IAudioService>(AudioService.Create(audioRoot != null ? audioRoot : serviceRoot));

            // object-map.md §3 — 1 nguồn duy nhất phát sự kiện đổi hướng màn hình, thay N
            // LayoutProfileSwitcher tự poll Screen.width/height riêng lẻ mỗi frame (xem
            // ScreenOrientationService.cs).
            ServiceLocator.Register<IScreenOrientationService>(ScreenOrientationService.Create(serviceRoot));

            // Popup/thông báo dùng chung toàn game (Toast/ConfirmDialog/RewardPopup) — dựng
            // trên PopupLayer con của uiRoot nên sống xuyên suốt mọi scene, luôn nổi trên cùng.
            ServiceLocator.Register<IPopupService>(PopupService.Create(uiRoot));

            WireSettingsToAudio();
            WireSettingsToLocalization();
            WireSettingsToTextScale(uiRoot);
        }

        /// <summary>Cài đặt đổi → Audio tự cập nhật, không màn hình nào phải gọi tay.</summary>
        private static void WireSettingsToAudio()
        {
            var settings = ServiceLocator.Get<ISettingsService>();
            var audio = ServiceLocator.Get<IAudioService>();

            settings.OnChanged += s => audio.SetVolumes(s.Bgm, s.Sfx);
            audio.SetVolumes(settings.Current.Bgm, settings.Current.Sfx);
        }

        /// <summary>task-localization-pilot.md — cùng mẫu <see cref="WireSettingsToAudio"/>:
        /// `SettingsDto.Language` đổi (VD qua SettingsScreen) → `ILocalizationService` tự đổi theo,
        /// không màn hình nào phải gọi tay `SetLanguage`.</summary>
        private static void WireSettingsToLocalization()
        {
            var settings = ServiceLocator.Get<ISettingsService>();
            var localization = ServiceLocator.Get<ILocalizationService>();

            settings.OnChanged += s => localization.SetLanguage(s.Language);
            localization.SetLanguage(settings.Current.Language);
        }

        /// <summary>task-accessibility.md — `SettingsDto.TextScale` đổi → quét lại TOÀN BỘ UI đang
        /// dựng dưới `uiRoot` (mọi Canvas Meta/Battle/Title/Splash/Loading/Settings đều là con của
        /// đây, xem <see cref="IUiRootHost"/>). Giới hạn đã biết: màn hình dựng-lười LẦN ĐẦU SAU
        /// lần đổi setting gần nhất (VD mở Shop lần đầu sau khi đã chỉnh TextScale) chưa tự áp —
        /// chỉ áp lại đúng khi setting đổi TIẾP theo hoặc khởi động lại app. Battle HUD tự áp thêm ở
        /// <c>BattleHudScreen.Bind()</c> vì scene này dựng lại mỗi trận.</summary>
        private static void WireSettingsToTextScale(Transform uiRoot)
        {
            if (uiRoot == null) return;
            var settings = ServiceLocator.Get<ISettingsService>();

            settings.OnChanged += s => Game.Meta.Accessibility.TextScaleApplier.Apply(uiRoot, s.TextScale);
            Game.Meta.Accessibility.TextScaleApplier.Apply(uiRoot, settings.Current.TextScale);
        }
    }
}
