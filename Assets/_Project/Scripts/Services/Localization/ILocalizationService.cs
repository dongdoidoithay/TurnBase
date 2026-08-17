using System;

namespace Game.Services.Localization
{
    /// <summary>task-phase-5-gaps.md Phần D — loại tên đọc qua <see cref="ILocalizationService.GetName"/>,
    /// khớp đúng 4 prefix DefId/NameKey đã có sẵn trong data thật (<c>hero_</c>/<c>enemy_</c>/
    /// <c>skill_</c>/<c>boss_</c>). <c>Boss</c> thêm ở đợt localize tên unit trong Battle — 6 boss
    /// dùng tiền tố <c>boss_</c> riêng (KHÔNG phải <c>enemy_</c>), key <c>boss.*.name</c> đã có sẵn
    /// trong strings.csv (task-phase-5-gaps.md Phần D sinh ra) nhưng chưa từng có cách tra tới vì
    /// enum này thiếu giá trị Boss.</summary>
    public enum LocalizedNameKind { Hero, Enemy, Skill, Boss }

    /// <summary>task-localization-pilot.md — plan.md §11.7 "CSV key→value, VI/EN". Pilot hạ tầng:
    /// chỉ Title screen + SettingsScreen dùng key thật, phần còn lại của game vẫn hard-code chuỗi
    /// (xem task file §1 cho danh sách để dành lượt sau). task-phase-5-gaps.md Phần D mở rộng thêm
    /// tên hero/enemy/skill qua <see cref="GetName"/> — nhãn UI khác (nút, tiêu đề màn...) vẫn còn
    /// hard-code, để dành lượt sau (§D1 "ngoài phạm vi").</summary>
    public interface ILocalizationService
    {
        string CurrentLanguage { get; }

        /// <summary>Bắn sau <see cref="SetLanguage"/> nếu ngôn ngữ thực sự đổi — màn đang mở tự
        /// làm mới label nếu muốn (SettingsScreen làm vậy vì luôn đang mở khi đổi).</summary>
        event Action OnLanguageChanged;

        void SetLanguage(string language);

        /// <summary>Không tìm thấy key → trả về chính key (quy ước i18n phổ biến — dễ nhận ra chỗ
        /// thiếu bằng mắt, không crash/không rỗng vô hình).</summary>
        string Get(string key);

        /// <summary>Có placeholder <c>{0}</c>/<c>{1}</c>... qua <see cref="string.Format(string,object[])"/>.</summary>
        string Get(string key, params object[] args);

        /// <summary>Tên hiển thị hero/enemy/skill — tra <c>{kind}.{id không tiền tố}.name</c>
        /// (khớp đúng <c>NameKey</c> đã có sẵn 100% trên data thật). Thiếu key trong
        /// <c>strings.csv</c> (nội dung chưa dịch) → tự format title-case từ <paramref name="defId"/>
        /// (KHÔNG gọi <c>HeroDisplayUtil</c> — <c>Game.Services</c> không được tham chiếu
        /// <c>Game.Meta</c>, structure.md §6) thay vì hiện key thô.</summary>
        string GetName(string defId, LocalizedNameKind kind);
    }
}
