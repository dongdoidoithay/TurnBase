using Game.Core;
using Game.Core.Events;
using Game.Core.Times;
using Game.Core.UI;
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

            WireSettingsToAudio();
            WireSettingsToLocalization();
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
    }
}
