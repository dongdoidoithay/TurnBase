using System.Collections.Generic;
using Game.Combat.Model;
using Game.Data.Dto;

namespace Game.Meta.Equipment
{
    /// <summary>
    /// Tính bonus 2-món/4-món đang kích hoạt cho 1 hero — task-setbonus.md, plan.md §7.4. Pure
    /// logic, không qua DI/ServiceLocator, giống <see cref="Game.Meta.Hero.AwakeningCatalog"/>.
    /// </summary>
    public static class SetBonusResolver
    {
        private const int PIECES_FOR_TWO = 2;
        private const int PIECES_FOR_FOUR = 4;

        /// <summary>Đếm số món đang mặc theo từng SetId — tra <c>hero.Equipped[]</c> (Uid) qua
        /// <c>profile.Equipment</c> (Uid → SetId). Bỏ qua slot trống ("") hoặc Uid không khớp món
        /// nào (dữ liệu hỏng — không throw, chỉ bỏ qua).</summary>
        public static Dictionary<string, int> CountEquippedPieces(HeroInstanceDto hero, PlayerProfileDto profile)
        {
            var counts = new Dictionary<string, int>();
            if (hero?.Equipped == null) return counts;

            foreach (var uid in hero.Equipped)
            {
                if (string.IsNullOrEmpty(uid)) continue;
                var item = profile.Equipment.Find(e => e.Uid == uid);
                if (item == null || string.IsNullOrEmpty(item.SetId)) continue;

                counts.TryGetValue(item.SetId, out int c);
                counts[item.SetId] = c + 1;
            }
            return counts;
        }

        /// <summary>Mọi StatModifier từ bộ nào đã đủ ≥2 món (có thể nhiều bộ cùng lúc — 6 slot
        /// trang bị, mặc 2+2+2 của 3 bộ khác nhau vẫn hợp lệ).</summary>
        public static List<StatModifier> GetActiveTwoPieceBonuses(HeroInstanceDto hero, PlayerProfileDto profile)
        {
            var result = new List<StatModifier>();
            foreach (var kv in CountEquippedPieces(hero, profile))
            {
                if (kv.Value < PIECES_FOR_TWO) continue;
                result.AddRange(SetBonusCatalog.TwoPiece(kv.Key));
            }
            return result;
        }

        /// <summary>Bonus 4-món của bộ ĐẦU TIÊN đủ ≥4 món (thực tế chỉ có thể đủ 4 món của TỐI ĐA
        /// 1 bộ tại 1 thời điểm — hero chỉ có 6 slot, 2 bộ 4-món cần tối thiểu 8 slot, không thể
        /// xảy ra — xem task-setbonus.md §2.6). Trả null nếu không đủ 4 món bộ nào, hoặc bộ đủ
        /// 4 món chưa có bonus 4-món thật (VD Tempest, xem task-setbonus.md §1.1).</summary>
        public static PassiveData GetActiveFourPieceBonus(HeroInstanceDto hero, PlayerProfileDto profile)
        {
            foreach (var kv in CountEquippedPieces(hero, profile))
            {
                if (kv.Value < PIECES_FOR_FOUR) continue;
                var passive = SetBonusCatalog.FourPiece(kv.Key);
                if (passive != null) return passive;
            }
            return null;
        }
    }
}
