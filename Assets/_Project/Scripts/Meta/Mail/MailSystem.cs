using System;
using System.Linq;
using Game.Data;
using Game.Data.Dto;
using Game.Services.Economy;

namespace Game.Meta.Mail
{
    /// <summary>task-mail.md — Hộp thư. Pure static logic (giống <c>QuestSystem</c>), tách khỏi
    /// UI. Việc TẠO mail (VD quà chào mừng) nằm ở nơi khác (VD
    /// <c>LocalPlayerRepository.CreateNew()</c>) — class này chỉ xử lý CLAIM những mail đã có sẵn
    /// trong <c>profile.Mail</c>. task-mail-extras.md: thêm dọn mail hết hạn + claim hàng loạt.</summary>
    public static class MailSystem
    {
        public static bool TryClaim(PlayerProfileDto profile, string mailId, IEconomyService economy)
        {
            var mail = profile?.Mail.Find(m => m.Id == mailId);
            if (mail == null || mail.Claimed) return false;

            foreach (var reward in mail.Rewards)
                economy.Grant(profile.Wallet, (CurrencyType)reward.CurrencyType, reward.Amount);

            mail.Claimed = true;
            return true;
        }

        /// <summary>Claim TOÀN BỘ mail chưa claim cùng lúc — trả số mail vừa claim.</summary>
        public static int ClaimAll(PlayerProfileDto profile, IEconomyService economy)
        {
            if (profile == null) return 0;
            int count = 0;
            // ToList() — TryClaim đổi Claimed ngay trên phần tử gốc, không cần né sửa-trong-lúc-lặp,
            // nhưng chốt danh sách trước cho rõ ý định (chỉ claim những mail ĐANG chưa claim lúc gọi).
            foreach (var mail in profile.Mail.Where(m => !m.Claimed).ToList())
                if (TryClaim(profile, mail.Id, economy)) count++;
            return count;
        }

        /// <summary>Xoá khỏi <c>profile.Mail</c> mọi mail có <c>ExpiresAtUtc</c> KHÔNG rỗng và đã
        /// qua hạn — kể cả mail đã claim (dọn dẹp thật, không chỉ ẩn). Mail rỗng <c>ExpiresAtUtc</c>
        /// (VD Welcome) không bao giờ bị đụng tới. Gọi ở đầu <c>MailScreen.Open()</c>, cùng mẫu
        /// <c>DungeonSystem.EnsureDailyReset</c>.</summary>
        public static int PurgeExpired(PlayerProfileDto profile, DateTime utcNow)
        {
            if (profile == null) return 0;
            return profile.Mail.RemoveAll(m =>
                !string.IsNullOrEmpty(m.ExpiresAtUtc)
                && DateTime.TryParse(m.ExpiresAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var expiry)
                && expiry <= utcNow);
        }

        public static int UnclaimedCount(PlayerProfileDto profile)
            => profile?.Mail.Count(m => !m.Claimed) ?? 0;
    }
}
