using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Random;
using Game.Data;
using Game.Data.Dto;
using Game.Meta.Content;
using Game.Services.Economy;
using UnityEngine;

namespace Game.Meta.Gacha
{
    /// <summary>
    /// Gacha pity — plan.md §9.3. task-ascend.md §7 mục B. Dùng <see cref="IRandomSource"/>
    /// (không phải UnityEngine.Random) vì plan.md yêu cầu bắt buộc <c>GachaPityTests</c> chứng
    /// minh tỉ lệ khớp ±0.05% trên 1 triệu roll — cần RNG seed xác định, chạy nhanh trong EditMode.
    /// </summary>
    public static class GachaSystem
    {
        public const long SINGLE_PULL_COST = 300;
        public const long TEN_PULL_COST = 2_700; // −10% so với 10×300

        private const int HISTORY_CAP = 100;

        public readonly struct GachaPullResult
        {
            public readonly string HeroDefId;
            public readonly Rarity Rarity;
            public readonly bool IsNewHero;
            public readonly long ShardsGranted;

            public GachaPullResult(string heroDefId, Rarity rarity, bool isNewHero, long shardsGranted)
            {
                HeroDefId = heroDefId; Rarity = rarity; IsNewHero = isNewHero; ShardsGranted = shardsGranted;
            }
        }

        public static long PullCost(int count) => count == 10 ? TEN_PULL_COST : SINGLE_PULL_COST * count;

        // =====================================================================
        // PITY — chép đúng pseudocode plan.md §9.3
        // =====================================================================

        /// <summary>Soft pity Legendary từ lần 45 (+2%/lần), hard pity Legendary lần 60,
        /// hard pity Epic lần 10. KHÔNG đụng tới hero pool/economy — pure function để test 1M roll.</summary>
        public static Rarity RollRarity(GachaStateDto state, IRandomSource rng)
        {
            state.PullsSinceLegendary++;
            state.PullsSinceEpic++;

            if (state.PullsSinceLegendary >= 60) return ResetLegendary(state);

            float legRate = 0.015f;
            if (state.PullsSinceLegendary >= 45)
                legRate += 0.02f * (state.PullsSinceLegendary - 44);

            float r = rng.NextFloat();
            if (r < legRate) return ResetLegendary(state);
            if (state.PullsSinceEpic >= 10) return ResetEpic(state);
            if (r < legRate + 0.12f) return ResetEpic(state);
            if (r < legRate + 0.12f + 0.365f) return Rarity.Rare;
            return Rarity.Common;
        }

        private static Rarity ResetLegendary(GachaStateDto s)
        {
            s.PullsSinceLegendary = 0;
            s.PullsSinceEpic = 0;
            return Rarity.Legendary;
        }

        private static Rarity ResetEpic(GachaStateDto s)
        {
            s.PullsSinceEpic = 0;
            return Rarity.Epic;
        }

        // =====================================================================
        // PULL — giao dịch nguyên tử, giống style AscendSystem.TryAscend
        // =====================================================================

        public static bool CanPull(PlayerProfileDto profile, int count)
            => ServiceLocator.TryGet<IEconomyService>(out var economy)
               && economy.Get(profile.Wallet, CurrencyType.Gem) >= PullCost(count);

        /// <summary>Trừ Gem 1 lần rồi roll `count` lần. Trả về list rỗng nếu không đủ Gem
        /// (không trừ dở dang).</summary>
        public static List<GachaPullResult> Pull(PlayerProfileDto profile, int count, IRandomSource rng)
        {
            var results = new List<GachaPullResult>(count);
            if (!ServiceLocator.TryGet<IEconomyService>(out var economy)) return results;

            long cost = PullCost(count);
            if (economy.Get(profile.Wallet, CurrencyType.Gem) < cost) return results;
            economy.TryConsume(profile.Wallet, CurrencyType.Gem, cost);

            for (int i = 0; i < count; i++)
                results.Add(PullOne(profile, economy, rng));

            // task-quest.md — quest "SummonsPerformed" theo dõi số lần pull THẬT trong ngày, không
            // đọc thẳng profile.Gacha.TotalPulls (đó là counter trọn đời, không reset theo ngày).
            Game.Meta.Quest.QuestSystem.IncrementDailyProgress(profile, QuestConditionType.SummonsPerformed, count);

            return results;
        }

        private static GachaPullResult PullOne(PlayerProfileDto profile, IEconomyService economy, IRandomSource rng)
        {
            var rarity = RollRarity(profile.Gacha, rng);
            profile.Gacha.TotalPulls++;

            var pool = HeroPool(rarity);
            string heroDefId = pool.Count > 0 ? pool[rng.NextInt(pool.Count)] : null;

            profile.Gacha.History.Add(heroDefId ?? "none");
            if (profile.Gacha.History.Count > HISTORY_CAP)
                profile.Gacha.History.RemoveAt(0);

            if (heroDefId == null) return new GachaPullResult(null, rarity, false, 0);

            bool owned = profile.Heroes.Exists(h => h.DefId == heroDefId);
            if (owned)
            {
                long shards = DuplicateShards(rarity);
                economy.GrantShards(profile.Wallet, heroDefId, shards);
                return new GachaPullResult(heroDefId, rarity, false, shards);
            }

            profile.Heroes.Add(new HeroInstanceDto { Uid = Guid.NewGuid().ToString("N"), DefId = heroDefId });
            return new GachaPullResult(heroDefId, rarity, true, 0);
        }

        /// <summary>Mảnh cấp khi trùng hero — PLACEHOLDER theo rarity, chờ Balance Harness tinh
        /// chỉnh (task-ascend.md §7 mục B.1), không phải số liệu cân bằng cuối.</summary>
        private static long DuplicateShards(Rarity rarity) => rarity switch
        {
            Rarity.Common => 5,
            Rarity.Rare => 8,
            Rarity.Epic => 12,
            Rarity.Legendary => 20,
            _ => 20
        };

        private static List<string> HeroPool(Rarity rarity)
        {
            var pool = new List<string>();
            var defs = Resources.LoadAll<HeroDefinitionSO>("Data/Heroes");
            for (int i = 0; i < defs.Length; i++)
                if (defs[i].Rarity == rarity) pool.Add(defs[i].DefId);
            return pool;
        }
    }
}
