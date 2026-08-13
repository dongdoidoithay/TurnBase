using Game.Core;
using Game.Data;
using Game.Data.Dto;
using Game.Services.Economy;

namespace Game.Meta.Hero
{
    /// <summary>Nâng skill 1→8 — plan.md §5.5/§11.4 (Skill Level ảnh hưởng power/CD qua
    /// <see cref="Game.Combat.Model.SkillRuntime.EffectivePower"/>, +6%/cấp).</summary>
    public static class SkillUpgradeSystem
    {
        public const int MAX_SKILL_LEVEL = 8;

        /// <summary>Chưa có bảng giá riêng theo skill trong plan.md — dùng công thức tăng dần
        /// giống EquipmentService.EnhanceCost (80·(cấp+1)) nhưng đắt hơn vì skill mạnh hơn 1 món đồ.</summary>
        public static long UpgradeCost(int currentLevel) => 200L * currentLevel;

        public static bool CanUpgrade(HeroInstanceDto hero, int skillSlot)
            => hero != null && skillSlot >= 0 && skillSlot < hero.SkillLevels.Length
               && hero.SkillLevels[skillSlot] < MAX_SKILL_LEVEL
               && AscendSystem.IsSkillSlotUnlocked(hero.Star, skillSlot);

        public static bool TryUpgrade(PlayerProfileDto profile, HeroInstanceDto hero, int skillSlot)
        {
            if (!CanUpgrade(hero, skillSlot)) return false;
            if (!ServiceLocator.TryGet<IEconomyService>(out var economy)) return false;

            long cost = UpgradeCost(hero.SkillLevels[skillSlot]);
            if (!economy.TryConsume(profile.Wallet, CurrencyType.Gold, cost)) return false;

            hero.SkillLevels[skillSlot]++;
            return true;
        }

        /// <summary>Lên cấp MIỄN PHÍ — dùng cho phần thưởng (task-eventrest.md, Rest "Train"),
        /// KHÔNG đụng Gold/Wallet, tách biệt <see cref="TryUpgrade"/> để không đổi hành vi mua bán
        /// hiện có ở Hero Detail.</summary>
        public static bool GrantFreeLevel(HeroInstanceDto hero, int skillSlot)
        {
            if (!CanUpgrade(hero, skillSlot)) return false;
            hero.SkillLevels[skillSlot]++;
            return true;
        }
    }
}
