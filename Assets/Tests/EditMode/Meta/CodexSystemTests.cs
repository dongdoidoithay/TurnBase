using Game.Data.Dto;
using Game.Meta.Codex;
using NUnit.Framework;

namespace Game.Tests.Meta
{
    /// <summary>CodexSystem — task-codex.md. Dùng catalog THẬT (Resources/Data/Heroes,
    /// Data/Enemies) qua <see cref="CodexSystem.AllHeroes"/>/<see cref="CodexSystem.AllEnemies"/>
    /// — cùng cách <c>EquipmentGeneratorTests.Roll_UsesRealCatalog_NeverThrows</c> đã làm, không
    /// mock được Resources.LoadAll.</summary>
    public class CodexSystemTests
    {
        [Test]
        public void AllHeroes_And_AllEnemies_LoadRealCatalog_NeverEmpty()
        {
            Assert.Greater(CodexSystem.AllHeroes.Count, 0, "Catalog hero thật phải load được (24 hero hiện có)");
            Assert.Greater(CodexSystem.AllEnemies.Count, 0, "Catalog enemy thật phải load được (66 enemy hiện có)");
        }

        [Test]
        public void IsHeroUnlocked_OwnedHero_ReturnsTrue()
        {
            var def = CodexSystem.AllHeroes[0];
            var p = new PlayerProfileDto();
            p.Heroes.Add(new HeroInstanceDto { Uid = "h1", DefId = def.DefId });

            Assert.IsTrue(CodexSystem.IsHeroUnlocked(p, def));
        }

        [Test]
        public void IsHeroUnlocked_NotOwned_ReturnsFalse()
        {
            var def = CodexSystem.AllHeroes[0];
            var p = new PlayerProfileDto();
            p.Heroes.Clear();

            Assert.IsFalse(CodexSystem.IsHeroUnlocked(p, def));
        }

        [Test]
        public void IsHeroUnlocked_NullProfile_ReturnsFalse_NoThrow()
        {
            Assert.IsFalse(CodexSystem.IsHeroUnlocked(null, CodexSystem.AllHeroes[0]));
        }

        [Test]
        public void IsEnemyUnlocked_ChapterAtOrBelowUnlocked_ReturnsTrue()
        {
            var def = CodexSystem.AllEnemies[0]; // sorted by Chapter — phần tử đầu có Chapter thấp nhất
            var p = new PlayerProfileDto();
            p.Progress.ChapterUnlocked = def.Chapter;

            Assert.IsTrue(CodexSystem.IsEnemyUnlocked(p, def));
        }

        [Test]
        public void IsEnemyUnlocked_ChapterAboveUnlocked_ReturnsFalse()
        {
            var def = CodexSystem.AllEnemies[^1]; // sorted by Chapter — phần tử cuối có Chapter cao nhất
            var p = new PlayerProfileDto();
            p.Progress.ChapterUnlocked = 0;

            Assert.IsFalse(CodexSystem.IsEnemyUnlocked(p, def));
        }

        [Test]
        public void IsEnemyUnlocked_NullProfile_ReturnsFalse_NoThrow()
        {
            Assert.IsFalse(CodexSystem.IsEnemyUnlocked(null, CodexSystem.AllEnemies[0]));
        }
    }
}
