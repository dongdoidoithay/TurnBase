using System;
using Game.Data;
using Game.Data.Dto;
using Game.Meta.Endgame;
using Game.Meta.Hero;
using Game.Services.Economy;

namespace Game.Meta.Notifications
{
    /// <summary>task-reddot.md — plan.md §10.6. Bản đơn giản hoá có chủ đích so với đặc tả gốc
    /// (cây phân cấp Root→BottomNav→SubTab→Item + <c>SetDirty(path)</c> lan lên cha): TopBar dự án
    /// này CHỈ có 1 tầng nút phẳng (không có sub-tab/item con thật), nên 1 tầng "pull" tính lại toàn
    /// bộ mỗi lần <see cref="Refresh"/> đơn giản hơn hẳn 1 hệ push/dirty-propagation cho quy mô hiện
    /// tại — đúng lối "đơn giản hoá có chủ đích" đã lặp lại nhiều lần trong dự án (xem object-map.md
    /// §12.1).
    ///
    /// Quy tắc "chỉ hiện khi có hành động MIỄN PHÍ khả thi" được diễn giải là: hành động dùng ĐƯỢC
    /// NGAY bằng tài nguyên NGƯỜI CHƠI ĐÃ CÓ SẴN (không cần mua thêm) — KHÔNG phải nghĩa đen "chi phí
    /// 0". Đây là quy ước phổ biến của thể loại (khớp tinh thần câu "không hiện chỉ vì có đồ bán" —
    /// phân biệt "cần MUA thêm" khỏi "đã đủ, chỉ cần bấm").
    ///
    /// 4 node phủ THẬT (không theo đúng danh sách ví dụ Hero/Summon/Dungeon/Shop của plan.md —
    /// xem lý do từng node bên dưới):
    /// - Hero: có hero nâng skill được VÀ đủ Gold trả ngay.
    /// - Dungeon: có dungeon hằng ngày còn lượt hôm nay (Energy hiện KHÔNG bị enforce ở đâu trong
    ///   code thật — xem object-map.md — nên "vào được" ĐÃ là miễn phí thật, không cần thêm check).
    /// - TrialBoss/Tower: có thưởng mốc tuần đã đủ điều kiện nhưng CHƯA nhận (thưởng chờ sẵn, không
    ///   phải "có thể đánh" — khác nghĩa hơn nhưng đúng tinh thần "hành động miễn phí khả thi" hơn).
    /// Summon bị BỎ (Summon Ticket — currency "miễn phí" duy nhất hợp lý — chưa từng được wiring bất
    /// kỳ đâu trong code thật, gacha hiện chỉ tiêu Gem trả phí; ép 1 tín hiệu giả sẽ là bịa hành vi).
    /// Shop bị BỎ (không có nút Shop nào trên TopBar — chỉ vào qua node bản đồ — và quy tắc chính
    /// bản thân đã loại trừ Shop vì mọi thứ trong đó đều PHẢI mua).</summary>
    public static class RedDotService
    {
        public static bool IsHeroDirty(PlayerProfileDto profile, IEconomyService economy)
        {
            if (profile == null || economy == null) return false;
            long gold = economy.Get(profile.Wallet, CurrencyType.Gold);
            foreach (var hero in profile.Heroes)
                for (int slot = 0; slot < hero.SkillLevels.Length; slot++)
                {
                    if (!SkillUpgradeSystem.CanUpgrade(hero, slot)) continue;
                    if (gold >= SkillUpgradeSystem.UpgradeCost(hero.SkillLevels[slot])) return true;
                }
            return false;
        }

        private static readonly DungeonKind[] DailyKinds =
        {
            DungeonKind.Gold, DungeonKind.Exp, DungeonKind.Material, DungeonKind.Stone,
        };

        public static bool IsDungeonDirty(PlayerProfileDto profile, DateTime utcNow)
        {
            if (profile == null) return false;
            foreach (var kind in DailyKinds)
                if (DungeonSystem.CanEnter(profile, kind, utcNow)) return true;
            return false;
        }

        public static bool IsTrialBossDirty(PlayerProfileDto profile)
            => profile != null && TrialBossSystem.HasClaimable(profile);

        public static bool IsTowerDirty(PlayerProfileDto profile)
            => profile != null && TowerSystem.HasClaimable(profile);
    }
}
