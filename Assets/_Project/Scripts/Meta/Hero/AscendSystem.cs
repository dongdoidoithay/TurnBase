using Game.Core;
using Game.Data;
using Game.Data.Dto;
using Game.Services.Economy;

namespace Game.Meta.Hero
{
    /// <summary>
    /// Nâng sao 1→6 — plan.md §5.4, tiêu đủ Mảnh hero + Vật liệu + Gold (task-ascend.md §3).
    /// Trần cấp theo sao (+10 mỗi bậc) đã tự động qua <see cref="HeroLevelSystem.LevelCap"/>,
    /// không cần code riêng ở đây. "Mở Awakening" ở ★6 BẬT CỜ <see cref="HeroInstanceDto.Awakened"/>
    /// — hiệu ứng passive thật được áp khi ra trận qua <c>AwakeningCatalog</c>/<c>PassiveProcessor</c>
    /// (task-ascend.md §7 mục A), <c>BattleSceneInstaller</c> tự suy Awakened từ star.
    /// </summary>
    public static class AscendSystem
    {
        public const int MAX_STAR = 6;

        public readonly struct MaterialCost
        {
            public readonly CurrencyType Type;
            public readonly long Amount;
            public MaterialCost(CurrencyType type, long amount) { Type = type; Amount = amount; }
        }

        public readonly struct AscendCost
        {
            public readonly long Gold;
            public readonly long Shards;
            public readonly MaterialCost[] Materials;
            public AscendCost(long gold, long shards, params MaterialCost[] materials)
            {
                Gold = gold; Shards = shards; Materials = materials;
            }
        }

        /// <summary>Index i = chi phí để lên từ ★(i+1) sang ★(i+2) — bảng đúng plan.md §5.4.</summary>
        private static readonly AscendCost[] COSTS =
        {
            new(5_000, 10, new MaterialCost(CurrencyType.EssenceI, 5)),
            new(15_000, 20, new MaterialCost(CurrencyType.EssenceI, 10)),
            new(40_000, 40, new MaterialCost(CurrencyType.EssenceII, 15)),
            new(100_000, 70, new MaterialCost(CurrencyType.EssenceII, 25), new MaterialCost(CurrencyType.Core, 5)),
            new(250_000, 120, new MaterialCost(CurrencyType.EssenceIII, 40), new MaterialCost(CurrencyType.Core, 15)),
        };

        public static bool CanAscend(HeroInstanceDto hero) => hero != null && hero.Star < MAX_STAR;

        public static AscendCost? CostForNextStar(HeroInstanceDto hero)
            => CanAscend(hero) ? COSTS[hero.Star - 1] : null;

        /// <summary>Giao dịch nguyên tử — kiểm tra ĐỦ Gold + Mảnh + mọi Vật liệu trước, chỉ trừ
        /// khi chắc chắn đủ hết. Không bao giờ trừ dở dang (VD trừ Gold rồi mới phát hiện thiếu Mảnh).</summary>
        public static bool TryAscend(PlayerProfileDto profile, HeroInstanceDto hero)
        {
            if (!CanAscend(hero)) return false;
            if (!ServiceLocator.TryGet<IEconomyService>(out var economy)) return false;

            var cost = COSTS[hero.Star - 1];

            if (economy.Get(profile.Wallet, CurrencyType.Gold) < cost.Gold) return false;
            if (economy.GetShards(profile.Wallet, hero.DefId) < cost.Shards) return false;
            foreach (var m in cost.Materials)
                if (economy.Get(profile.Wallet, m.Type) < m.Amount) return false;

            economy.TryConsume(profile.Wallet, CurrencyType.Gold, cost.Gold);
            economy.TryConsumeShards(profile.Wallet, hero.DefId, cost.Shards);
            foreach (var m in cost.Materials)
                economy.TryConsume(profile.Wallet, m.Type, m.Amount);

            hero.Star++;
            if (hero.Star >= MAX_STAR) hero.Awakened = true;

            return true;
        }

        // =====================================================================
        // Unlock skill slot theo sao (task-ascend.md §4) — slot 0/1/2 luôn mở,
        // slot 3 (skill C) cần ★4, slot 4 (Ultimate) cần ★5.
        // =====================================================================

        public static bool IsSkillSlotUnlocked(int star, int slotIndex) => slotIndex switch
        {
            <= 2 => true,
            3 => star >= 4,
            4 => star >= 5,
            _ => false
        };
    }
}
