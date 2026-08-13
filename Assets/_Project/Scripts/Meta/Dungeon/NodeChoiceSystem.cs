using System.Collections.Generic;
using Game.Core.Random;
using Game.Data;
using Game.Data.Dto;
using Game.Meta.Equipment;
using Game.Meta.Hero;
using Game.Services.Economy;

namespace Game.Meta.Dungeon
{
    /// <summary>1 lựa chọn hiển thị trên <c>NodeChoiceScreen</c> trước khi resolve — không lộ
    /// xác suất/kết quả thật, chỉ label + flavor text ngắn.</summary>
    public readonly struct NodeChoiceOption
    {
        public readonly string Label;
        public readonly string Flavor;
        public NodeChoiceOption(string label, string flavor) { Label = label; Flavor = flavor; }
    }

    /// <summary>Kết quả sau khi resolve 1 lựa chọn — <see cref="Applied"/> false nghĩa là lựa chọn
    /// không khả dụng (VD Rest "Train" không có hero nào đủ điều kiện), UI phải chặn bấm trước đó
    /// bằng <see cref="NodeChoiceSystem.IsRestTrainAvailable"/> chứ không dựa vào field này để ẩn
    /// nút — field này chỉ là an toàn cuối, không phải cơ chế chính.</summary>
    public readonly struct NodeChoiceResult
    {
        public readonly bool Applied;
        public readonly string ResultText;
        public NodeChoiceResult(bool applied, string resultText) { Applied = applied; ResultText = resultText; }
    }

    /// <summary>task-eventrest.md — Event/Rest node redesign. Pure static logic (giống
    /// <c>DungeonSystem</c>/<c>TrialBossSystem</c>), tách khỏi UI để test được xác định qua
    /// <see cref="IRandomSource"/> seed cố định. <c>MetaSceneInstaller</c>/<c>NodeChoiceScreen</c>
    /// chỉ gọi + hiển thị, không tự chứa logic rủi ro.
    ///
    /// KHÔNG có "hồi HP" thật — game không có HP dai dẳng giữa các trận node map (xem
    /// task-eventrest.md §0), nên lựa chọn Rest "Recover" chỉ cấp Gold, giữ đúng TINH THẦN lựa
    /// chọn (an toàn/ít giá trị) mà không giả vờ có cơ chế hồi máu không tồn tại.</summary>
    public static class NodeChoiceSystem
    {
        public static readonly NodeChoiceOption[] RestOptions =
        {
            new("Recover", "Rest safely. +50 gold."),
            new("Train", "Spend the night drilling forms. +1 skill level for a random hero."),
        };

        public static readonly NodeChoiceOption[] EventOptions =
        {
            new("Play it safe", "A modest, guaranteed find."),
            new("Take a chance", "Could pay off big — or not."),
            new("All in", "Long odds, but the prize is real."),
        };

        /// <summary>UI dùng để làm mờ nút "Train" TRƯỚC khi bấm — không hero nào sở hữu 1 skill
        /// slot vừa unlock (theo Star) vừa chưa MAX level thì option này không có gì để làm.</summary>
        public static bool IsRestTrainAvailable(PlayerProfileDto profile)
        {
            if (profile == null) return false;
            foreach (var hero in profile.Heroes)
                for (int slot = 0; slot < hero.SkillLevels.Length; slot++)
                    if (SkillUpgradeSystem.CanUpgrade(hero, slot))
                        return true;
            return false;
        }

        public static NodeChoiceResult ResolveRest(PlayerProfileDto profile, int optionIndex,
            IEconomyService economy, IRandomSource rng)
        {
            switch (optionIndex)
            {
                case 0:
                    economy.Grant(profile.Wallet, CurrencyType.Gold, 50);
                    return new NodeChoiceResult(true, "You rest well. +50 gold.");

                case 1:
                    var eligible = new List<(HeroInstanceDto hero, int slot)>();
                    foreach (var hero in profile.Heroes)
                        for (int slot = 0; slot < hero.SkillLevels.Length; slot++)
                            if (SkillUpgradeSystem.CanUpgrade(hero, slot))
                                eligible.Add((hero, slot));

                    if (eligible.Count == 0)
                        return new NodeChoiceResult(false, "No hero has a skill left to train.");

                    var (chosenHero, chosenSlot) = eligible[rng.NextInt(eligible.Count)];
                    SkillUpgradeSystem.GrantFreeLevel(chosenHero, chosenSlot);
                    return new NodeChoiceResult(true, $"{chosenHero.DefId} — skill {chosenSlot + 1} level up!");

                default:
                    return new NodeChoiceResult(false, "");
            }
        }

        public static NodeChoiceResult ResolveEvent(PlayerProfileDto profile, int optionIndex,
            IEconomyService economy, IRandomSource rng)
        {
            switch (optionIndex)
            {
                case 0:
                    economy.Grant(profile.Wallet, CurrencyType.Gold, 30);
                    return new NodeChoiceResult(true, "A quiet find. +30 gold.");

                case 1:
                    if (rng.NextFloat() < 0.5f)
                    {
                        economy.Grant(profile.Wallet, CurrencyType.Gold, 150);
                        return new NodeChoiceResult(true, "It paid off! +150 gold.");
                    }
                    else
                    {
                        long lost = -System.Math.Min(50, economy.Get(profile.Wallet, CurrencyType.Gold));
                        economy.Grant(profile.Wallet, CurrencyType.Gold, lost);
                        return new NodeChoiceResult(true, $"Bad luck... {lost} gold.");
                    }

                case 2:
                    if (rng.NextFloat() < 0.25f)
                    {
                        var item = EquipmentGenerator.Roll(null, Rarity.Rare, rng);
                        if (item != null) profile.Equipment.Add(item);
                        return new NodeChoiceResult(true, "Jackpot! You found a rare piece of equipment.");
                    }
                    else
                    {
                        long lost = -System.Math.Min(80, economy.Get(profile.Wallet, CurrencyType.Gold));
                        economy.Grant(profile.Wallet, CurrencyType.Gold, lost);
                        return new NodeChoiceResult(true, $"No luck this time... {lost} gold.");
                    }

                default:
                    return new NodeChoiceResult(false, "");
            }
        }
    }
}
