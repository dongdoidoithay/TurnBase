using Game.Data;
using Game.Data.Dto;
using Game.Services.Economy;
using NUnit.Framework;

namespace Game.Tests.Services
{
    public class EconomyServiceTests
    {
        private IEconomyService _economy;
        private WalletDto _wallet;

        [SetUp]
        public void SetUp()
        {
            _economy = new EconomyService();
            _wallet = new WalletDto();
        }

        // ---------- Gold/Gem (field trực tiếp) ----------

        [Test]
        public void Grant_Gold_Adds()
        {
            _economy.Grant(_wallet, CurrencyType.Gold, 100);
            Assert.AreEqual(100, _economy.Get(_wallet, CurrencyType.Gold));
        }

        [Test]
        public void Grant_NegativeDelta_ClampsAtZero_NeverGoesNegative()
        {
            _economy.Grant(_wallet, CurrencyType.Gold, 50);
            _economy.Grant(_wallet, CurrencyType.Gold, -1000);
            Assert.AreEqual(0, _economy.Get(_wallet, CurrencyType.Gold));
        }

        [Test]
        public void TryConsume_InsufficientBalance_ReturnsFalse_AndDoesNotChangeBalance()
        {
            _economy.Grant(_wallet, CurrencyType.Gold, 10);
            bool ok = _economy.TryConsume(_wallet, CurrencyType.Gold, 100);

            Assert.IsFalse(ok);
            Assert.AreEqual(10, _economy.Get(_wallet, CurrencyType.Gold));
        }

        [Test]
        public void TryConsume_SufficientBalance_DeductsAndReturnsTrue()
        {
            _economy.Grant(_wallet, CurrencyType.Gold, 100);
            bool ok = _economy.TryConsume(_wallet, CurrencyType.Gold, 40);

            Assert.IsTrue(ok);
            Assert.AreEqual(60, _economy.Get(_wallet, CurrencyType.Gold));
        }

        [Test]
        public void TryConsume_NegativeAmount_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => _economy.TryConsume(_wallet, CurrencyType.Gold, -1));
        }

        // ---------- Materials (EssenceI/II/III, Core — qua wallet.Materials list) ----------

        [Test]
        public void Materials_GrantAndGet_RoundTrips()
        {
            _economy.Grant(_wallet, CurrencyType.EssenceI, 5);
            Assert.AreEqual(5, _economy.Get(_wallet, CurrencyType.EssenceI));
        }

        [Test]
        public void Materials_DifferentTypes_TrackedIndependently()
        {
            _economy.Grant(_wallet, CurrencyType.EssenceI, 5);
            _economy.Grant(_wallet, CurrencyType.EssenceII, 3);
            _economy.Grant(_wallet, CurrencyType.Core, 1);

            Assert.AreEqual(5, _economy.Get(_wallet, CurrencyType.EssenceI));
            Assert.AreEqual(3, _economy.Get(_wallet, CurrencyType.EssenceII));
            Assert.AreEqual(1, _economy.Get(_wallet, CurrencyType.Core));
        }

        [Test]
        public void Materials_TryConsume_InsufficientFails_SufficientSucceeds()
        {
            _economy.Grant(_wallet, CurrencyType.EssenceII, 10);

            Assert.IsFalse(_economy.TryConsume(_wallet, CurrencyType.EssenceII, 20));
            Assert.IsTrue(_economy.TryConsume(_wallet, CurrencyType.EssenceII, 10));
            Assert.AreEqual(0, _economy.Get(_wallet, CurrencyType.EssenceII));
        }

        [Test]
        public void Materials_UnrequestedType_DefaultsToZero()
        {
            Assert.AreEqual(0, _economy.Get(_wallet, CurrencyType.Core));
        }

        [Test]
        public void Materials_DoNotLeakIntoGoldOrGem()
        {
            _economy.Grant(_wallet, CurrencyType.EssenceI, 999);
            Assert.AreEqual(0, _wallet.Gold);
            Assert.AreEqual(0, _wallet.Gem);
        }

        // ---------- Hero shards (khoá theo defId, qua wallet.HeroShards list) ----------

        [Test]
        public void Shards_GrantAndGet_PerHero_TrackedIndependently()
        {
            _economy.GrantShards(_wallet, "hero_a", 10);
            _economy.GrantShards(_wallet, "hero_b", 3);

            Assert.AreEqual(10, _economy.GetShards(_wallet, "hero_a"));
            Assert.AreEqual(3, _economy.GetShards(_wallet, "hero_b"));
        }

        [Test]
        public void Shards_TryConsume_InsufficientFails()
        {
            _economy.GrantShards(_wallet, "hero_a", 5);
            Assert.IsFalse(_economy.TryConsumeShards(_wallet, "hero_a", 6));
            Assert.AreEqual(5, _economy.GetShards(_wallet, "hero_a"));
        }

        [Test]
        public void Shards_TryConsume_Sufficient_Deducts()
        {
            _economy.GrantShards(_wallet, "hero_a", 20);
            Assert.IsTrue(_economy.TryConsumeShards(_wallet, "hero_a", 15));
            Assert.AreEqual(5, _economy.GetShards(_wallet, "hero_a"));
        }

        [Test]
        public void Shards_UnknownHero_DefaultsToZero()
            => Assert.AreEqual(0, _economy.GetShards(_wallet, "hero_never_granted"));

        // ---------- task-consumable-items.md — vật phẩm (khoá theo ItemType, qua
        // profile.Inventory.Items list, KHÔNG phải Wallet) ----------

        [Test]
        public void Items_GrantAndGet_PerType_TrackedIndependently()
        {
            var inv = new InventoryDto();
            _economy.GrantItem(inv, ItemType.Potion, 3);
            _economy.GrantItem(inv, ItemType.Ether, 1);

            Assert.AreEqual(3, _economy.GetItemCount(inv, ItemType.Potion));
            Assert.AreEqual(1, _economy.GetItemCount(inv, ItemType.Ether));
        }

        [Test]
        public void Items_TryConsume_InsufficientFails_NoChange()
        {
            var inv = new InventoryDto();
            _economy.GrantItem(inv, ItemType.Potion, 2);

            Assert.IsFalse(_economy.TryConsumeItem(inv, ItemType.Potion, 3));
            Assert.AreEqual(2, _economy.GetItemCount(inv, ItemType.Potion));
        }

        [Test]
        public void Items_TryConsume_Sufficient_Deducts()
        {
            var inv = new InventoryDto();
            _economy.GrantItem(inv, ItemType.Potion, 5);

            Assert.IsTrue(_economy.TryConsumeItem(inv, ItemType.Potion, 3));
            Assert.AreEqual(2, _economy.GetItemCount(inv, ItemType.Potion));
        }

        [Test]
        public void Items_UnknownType_DefaultsToZero()
        {
            var inv = new InventoryDto();
            Assert.AreEqual(0, _economy.GetItemCount(inv, ItemType.ElementalBomb));
        }
    }
}
