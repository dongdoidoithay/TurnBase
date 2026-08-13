using System;
using Game.Data.Dto;

namespace Game.Services.Settings
{
    public interface ISettingsService
    {
        SettingsDto Current { get; }
        event Action<SettingsDto> OnChanged;
        void Apply(SettingsDto settings);
        void Modify(Action<SettingsDto> mutate);
    }

    /// <summary>
    /// Nguồn sự thật duy nhất cho cài đặt. Mọi hệ thống nghe OnChanged để tự cập nhật
    /// (Audio, ActionCommandUI, ScreenShake, Localization) — object-map.md §6.3.
    /// </summary>
    public sealed class SettingsService : ISettingsService
    {
        public SettingsDto Current { get; private set; } = new();
        public event Action<SettingsDto> OnChanged;

        public void Apply(SettingsDto settings)
        {
            Current = settings ?? new SettingsDto();
            Clamp(Current);
            OnChanged?.Invoke(Current);
        }

        public void Modify(Action<SettingsDto> mutate)
        {
            if (mutate == null) return;
            mutate(Current);
            Clamp(Current);
            OnChanged?.Invoke(Current);
        }

        private static void Clamp(SettingsDto s)
        {
            s.Bgm = Math.Clamp(s.Bgm, 0f, 1f);
            s.Sfx = Math.Clamp(s.Sfx, 0f, 1f);
            s.BattleSpeed = Math.Clamp(s.BattleSpeed, 1, 3);
            s.TextScale = Math.Clamp(s.TextScale, 1f, 1.5f);
            // Bù độ trễ hợp lý: −200ms (bấm sớm) đến +200ms
            s.ActionCommandOffsetMs = Math.Clamp(s.ActionCommandOffsetMs, -200, 200);
            if (string.IsNullOrEmpty(s.Language)) s.Language = "vi";
        }
    }
}
