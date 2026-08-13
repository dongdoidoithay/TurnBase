using Game.Core;
using Game.Core.Random;
using Game.Data;
using Game.Data.Dto;
using Game.Meta.Dungeon;
using Game.Services.Economy;
using NUnit.Framework;

namespace Game.Tests.Meta
{
    /// <summary>NodeChoiceSystem — task-eventrest.md. Pure logic tách khỏi UI, test được xác
    /// định qua <see cref="FixedRandom"/> (cùng mẫu <c>GachaPityTests.FixedRandom</c>).</summary>
    public class NodeChoiceSystemTests
    {
        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            ServiceLocator.Register<IEconomyService>(new EconomyService());
        }

        [TearDown]
        public void TearDown() => ServiceLocator.Clear();

        private static IEconomyService Economy() => ServiceLocator.Get<IEconomyService>();

        private static PlayerProfileDto Profile()
        {
            var p = new PlayerProfileDto();
            p.Heroes.Clear();
            return p;
        }

        private static HeroInstanceDto AddHero(PlayerProfileDto p, string uid, int star = 1, int[] skillLevels = null)
        {
            var hero = new HeroInstanceDto
            {
                Uid = uid,
                DefId = uid,
                Star = star,
                SkillLevels = skillLevels ?? new[] { 1, 1, 1, 1, 1 },
            };
            p.Heroes.Add(hero);
            return hero;
        }

        // ---------- Rest ----------

        [Test]
        public void ResolveRest_Option0_Recover_Grants50Gold()
        {
            var p = Profile();
            var rng = new FixedRandom(0.1f);

            var result = NodeChoiceSystem.ResolveRest(p, 0, Economy(), rng);

            Assert.IsTrue(result.Applied);
            Assert.AreEqual(50, Economy().Get(p.Wallet, CurrencyType.Gold));
        }

        [Test]
        public void ResolveRest_Option1_Train_EligibleHero_GrantsFreeSkillLevel()
        {
            var p = Profile();
            var hero = AddHero(p, "h1", skillLevels: new[] { 1, 1, 1, 1, 1 });
            var rng = new FixedRandom(0.1f); // NextInt(1) → index 0 → chỉ hero/slot duy nhất đủ điều kiện

            var result = NodeChoiceSystem.ResolveRest(p, 1, Economy(), rng);

            Assert.IsTrue(result.Applied);
            Assert.IsTrue(System.Array.Exists(hero.SkillLevels, lvl => lvl == 2), "Phải có đúng 1 skill lên cấp 2");
        }

        [Test]
        public void ResolveRest_Option1_Train_NoEligibleHero_Fails_NoOp()
        {
            var p = Profile(); // không hero nào
            var rng = new FixedRandom(0.1f);

            var result = NodeChoiceSystem.ResolveRest(p, 1, Economy(), rng);

            Assert.IsFalse(result.Applied);
            Assert.AreEqual(0, Economy().Get(p.Wallet, CurrencyType.Gold), "Không được tốn/cấp gì khi Applied=false");
        }

        [Test]
        public void ResolveRest_Option1_Train_AllHeroesMaxed_Fails_NoOp()
        {
            var p = Profile();
            var maxed = new int[5];
            for (int i = 0; i < 5; i++) maxed[i] = SkillUpgradeSystemMaxLevel();
            AddHero(p, "h1", star: 6, skillLevels: maxed);
            var rng = new FixedRandom(0.1f);

            var result = NodeChoiceSystem.ResolveRest(p, 1, Economy(), rng);

            Assert.IsFalse(result.Applied);
        }

        private static int SkillUpgradeSystemMaxLevel() => Game.Meta.Hero.SkillUpgradeSystem.MAX_SKILL_LEVEL;

        [Test]
        public void IsRestTrainAvailable_MatchesEligibility()
        {
            var empty = Profile();
            Assert.IsFalse(NodeChoiceSystem.IsRestTrainAvailable(empty));

            var withHero = Profile();
            AddHero(withHero, "h1");
            Assert.IsTrue(NodeChoiceSystem.IsRestTrainAvailable(withHero));
        }

        // ---------- Event ----------

        [Test]
        public void ResolveEvent_Option0_PlaySafe_Grants30Gold_Always()
        {
            var p = Profile();
            var result = NodeChoiceSystem.ResolveEvent(p, 0, Economy(), new FixedRandom(0.99f));

            Assert.IsTrue(result.Applied);
            Assert.AreEqual(30, Economy().Get(p.Wallet, CurrencyType.Gold));
        }

        [Test]
        public void ResolveEvent_Option1_TakeAChance_LowRoll_Wins150Gold()
        {
            var p = Profile();
            var result = NodeChoiceSystem.ResolveEvent(p, 1, Economy(), new FixedRandom(0.1f));

            Assert.IsTrue(result.Applied);
            Assert.AreEqual(150, Economy().Get(p.Wallet, CurrencyType.Gold));
        }

        [Test]
        public void ResolveEvent_Option1_TakeAChance_HighRoll_Loses50Gold()
        {
            var p = Profile();
            Economy().Grant(p.Wallet, CurrencyType.Gold, 200);
            var result = NodeChoiceSystem.ResolveEvent(p, 1, Economy(), new FixedRandom(0.9f));

            Assert.IsTrue(result.Applied);
            Assert.AreEqual(150, Economy().Get(p.Wallet, CurrencyType.Gold), "200 - 50 = 150");
        }

        [Test]
        public void ResolveEvent_Option1_TakeAChance_HighRoll_ClampsAtZero_NeverGoesNegative()
        {
            var p = Profile();
            Economy().Grant(p.Wallet, CurrencyType.Gold, 10); // ít hơn 50
            var result = NodeChoiceSystem.ResolveEvent(p, 1, Economy(), new FixedRandom(0.9f));

            Assert.IsTrue(result.Applied);
            Assert.AreEqual(0, Economy().Get(p.Wallet, CurrencyType.Gold));
            Assert.IsTrue(result.ResultText.Contains("-10"), "ResultText phải phản ánh đúng số thật bị mất (10), không phải -50 danh nghĩa");
        }

        [Test]
        public void ResolveEvent_Option2_AllIn_LowRoll_GrantsRareEquipment()
        {
            var p = Profile();
            int before = p.Equipment.Count;
            var result = NodeChoiceSystem.ResolveEvent(p, 2, Economy(), new FixedRandom(0.1f));

            Assert.IsTrue(result.Applied);
            Assert.AreEqual(before + 1, p.Equipment.Count);
            Assert.AreEqual((int)Rarity.Rare, p.Equipment[^1].Rarity);
        }

        [Test]
        public void ResolveEvent_Option2_AllIn_HighRoll_Loses80Gold()
        {
            var p = Profile();
            Economy().Grant(p.Wallet, CurrencyType.Gold, 200);
            int before = p.Equipment.Count;
            var result = NodeChoiceSystem.ResolveEvent(p, 2, Economy(), new FixedRandom(0.9f));

            Assert.IsTrue(result.Applied);
            Assert.AreEqual(120, Economy().Get(p.Wallet, CurrencyType.Gold), "200 - 80 = 120");
            Assert.AreEqual(before, p.Equipment.Count, "Không được nhận đồ khi trượt");
        }

        /// <summary>IRandomSource giả — NextFloat()/NextFloat(min,max) trả về giá trị cố định,
        /// NextInt luôn trả 0 — cùng mẫu <c>GachaPityTests.FixedRandom</c>.</summary>
        private sealed class FixedRandom : IRandomSource
        {
            private readonly float _value;
            public FixedRandom(float value) { _value = value; }
            public ulong Seed => 0;
            public long CallCount { get; private set; }
            public float NextFloat() { CallCount++; return _value; }
            public float NextFloat(float min, float max) => min + _value * (max - min);
            public int NextInt(int maxExclusive) => 0;
            public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
            public bool Chance(float chance) => _value < chance;
            public IRandomSource Fork() => new FixedRandom(_value);
        }
    }
}
