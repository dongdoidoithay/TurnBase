using Game.Data;
using Game.Data.Dto;
using Game.Meta.Equipment;
using NUnit.Framework;

namespace Game.Tests.Meta
{
    /// <summary>SetBonusResolver — task-setbonus.md, plan.md §7.4.</summary>
    public class SetBonusResolverTests
    {
        private static EquipmentInstanceDto Item(string uid, string setId)
            => new() { Uid = uid, DefId = "eq_test", SetId = setId };

        private static HeroInstanceDto Hero() => new() { Uid = "h1", DefId = "hero_ember_knight" };

        [Test]
        public void CountEquippedPieces_CountsBySetId_IgnoresEmptySlotsAndNoSetItems()
        {
            var profile = new PlayerProfileDto();
            profile.Equipment.Add(Item("i1", "ember"));
            profile.Equipment.Add(Item("i2", "ember"));
            profile.Equipment.Add(Item("i3", "")); // không thuộc bộ nào

            var hero = Hero();
            hero.Equipped[(int)EquipSlot.Weapon] = "i1";
            hero.Equipped[(int)EquipSlot.Armor] = "i2";
            hero.Equipped[(int)EquipSlot.Helm] = "i3";
            // Boots/Ring/Amulet để trống ("")

            var counts = SetBonusResolver.CountEquippedPieces(hero, profile);

            Assert.AreEqual(1, counts.Count, "Chỉ 'ember' được đếm — item không SetId bị bỏ qua");
            Assert.AreEqual(2, counts["ember"]);
        }

        [Test]
        public void GetActiveTwoPieceBonuses_BelowTwoPieces_ReturnsEmpty()
        {
            var profile = new PlayerProfileDto();
            profile.Equipment.Add(Item("i1", "ember"));
            var hero = Hero();
            hero.Equipped[(int)EquipSlot.Weapon] = "i1";

            var mods = SetBonusResolver.GetActiveTwoPieceBonuses(hero, profile);

            Assert.AreEqual(0, mods.Count, "1 món chưa đủ kích hoạt bonus 2-món");
        }

        [Test]
        public void GetActiveTwoPieceBonuses_ExactlyTwoPieces_ActivatesTwoPieceBonus()
        {
            var profile = new PlayerProfileDto();
            profile.Equipment.Add(Item("i1", "ember"));
            profile.Equipment.Add(Item("i2", "ember"));
            var hero = Hero();
            hero.Equipped[(int)EquipSlot.Weapon] = "i1";
            hero.Equipped[(int)EquipSlot.Armor] = "i2";

            var mods = SetBonusResolver.GetActiveTwoPieceBonuses(hero, profile);

            Assert.AreEqual(1, mods.Count);
            Assert.AreEqual(StatType.AtkPct, mods[0].Stat);
        }

        [Test]
        public void GetActiveFourPieceBonus_ExactlyTwoPieces_ReturnsNull()
        {
            var profile = new PlayerProfileDto();
            profile.Equipment.Add(Item("i1", "ember"));
            profile.Equipment.Add(Item("i2", "ember"));
            var hero = Hero();
            hero.Equipped[(int)EquipSlot.Weapon] = "i1";
            hero.Equipped[(int)EquipSlot.Armor] = "i2";

            Assert.IsNull(SetBonusResolver.GetActiveFourPieceBonus(hero, profile));
        }

        [Test]
        public void GetActiveFourPieceBonus_FourPieces_ActivatesFourPieceBonus()
        {
            var profile = new PlayerProfileDto();
            for (int i = 0; i < 4; i++) profile.Equipment.Add(Item($"i{i}", "ember"));
            var hero = Hero();
            hero.Equipped[(int)EquipSlot.Weapon] = "i0";
            hero.Equipped[(int)EquipSlot.Armor] = "i1";
            hero.Equipped[(int)EquipSlot.Helm] = "i2";
            hero.Equipped[(int)EquipSlot.Boots] = "i3";

            var passive = SetBonusResolver.GetActiveFourPieceBonus(hero, profile);

            Assert.IsNotNull(passive);
            Assert.AreEqual("set_ember_molten_edge", passive.Id);
        }

        [Test]
        public void GetActiveFourPieceBonus_FourPiecesOfUnknownSetId_ReturnsNull_NoThrow()
        {
            // Đề phòng dữ liệu hỏng/SetId lạ (không thuộc 8 bộ đã định nghĩa) — resolver không throw.
            var profile = new PlayerProfileDto();
            for (int i = 0; i < 4; i++) profile.Equipment.Add(Item($"i{i}", "not_a_real_set"));
            var hero = Hero();
            hero.Equipped[(int)EquipSlot.Weapon] = "i0";
            hero.Equipped[(int)EquipSlot.Armor] = "i1";
            hero.Equipped[(int)EquipSlot.Helm] = "i2";
            hero.Equipped[(int)EquipSlot.Boots] = "i3";

            Assert.IsNull(SetBonusResolver.GetActiveFourPieceBonus(hero, profile));
        }

        [Test]
        public void GetActiveTwoPieceBonuses_TwoDifferentSetsAtTwoPiecesEach_StacksBothBonuses()
        {
            var profile = new PlayerProfileDto();
            profile.Equipment.Add(Item("i1", "ember"));
            profile.Equipment.Add(Item("i2", "ember"));
            profile.Equipment.Add(Item("i3", "vampire"));
            profile.Equipment.Add(Item("i4", "vampire"));
            var hero = Hero();
            hero.Equipped[(int)EquipSlot.Weapon] = "i1";
            hero.Equipped[(int)EquipSlot.Armor] = "i2";
            hero.Equipped[(int)EquipSlot.Helm] = "i3";
            hero.Equipped[(int)EquipSlot.Boots] = "i4";

            var mods = SetBonusResolver.GetActiveTwoPieceBonuses(hero, profile);

            Assert.AreEqual(2, mods.Count);
            Assert.IsTrue(mods.Exists(m => m.Stat == StatType.AtkPct));
            Assert.IsTrue(mods.Exists(m => m.Stat == StatType.LifestealPct));
        }

        [Test]
        public void CountEquippedPieces_NullHeroOrEmptyEquipped_ReturnsEmpty_NoThrow()
        {
            var profile = new PlayerProfileDto();
            Assert.AreEqual(0, SetBonusResolver.CountEquippedPieces(null, profile).Count);
            Assert.AreEqual(0, SetBonusResolver.CountEquippedPieces(Hero(), profile).Count);
        }

        [Test]
        public void RollRandomSetId_AlwaysReturnsOneOfTheEightSets()
        {
            var rng = new Game.Core.Random.XorShiftRandom(42UL);
            for (int i = 0; i < 100; i++)
                CollectionAssert.Contains(SetBonusCatalog.SET_IDS, SetBonusCatalog.RollRandomSetId(rng));
        }
    }
}
