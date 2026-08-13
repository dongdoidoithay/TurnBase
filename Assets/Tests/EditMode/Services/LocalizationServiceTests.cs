using Game.Services.Localization;
using NUnit.Framework;

namespace Game.Tests.Services
{
    /// <summary>task-localization-pilot.md — kiểm qua ĐÚNG file CSV thật
    /// (Resources/Localization/strings.csv), giống cách EquipmentGeneratorTests/CodexSystemTests
    /// đã test trực tiếp trên catalog Resources thật thay vì mock, khớp quy ước dự án.</summary>
    public class LocalizationServiceTests
    {
        [Test]
        public void Get_ExistingKey_ReturnsViValueByDefault()
        {
            var loc = new LocalizationService();

            Assert.AreEqual("vi", loc.CurrentLanguage);
            Assert.AreEqual("BẮT ĐẦU", loc.Get("title.button.start"));
        }

        [Test]
        public void SetLanguage_ChangesActiveTranslation()
        {
            var loc = new LocalizationService();

            loc.SetLanguage("en");

            Assert.AreEqual("en", loc.CurrentLanguage);
            Assert.AreEqual("START", loc.Get("title.button.start"));
        }

        [Test]
        public void Get_MissingKey_ReturnsKeyItself_NoThrow()
        {
            var loc = new LocalizationService();

            Assert.AreEqual("does.not.exist", loc.Get("does.not.exist"));
        }

        [Test]
        public void Get_WithFormatArgs_SubstitutesPlaceholders()
        {
            var loc = new LocalizationService();

            string result = loc.Get("title.label.subtitle", 5, 1000);

            Assert.AreEqual("5 Tướng · 1000 Vàng", result);
        }

        [Test]
        public void Get_WithFormatArgs_EnglishAfterSwitch()
        {
            var loc = new LocalizationService();
            loc.SetLanguage("en");

            string result = loc.Get("title.label.subtitle", 5, 1000);

            Assert.AreEqual("5 Heroes · 1000 Gold", result);
        }

        [Test]
        public void SetLanguage_SameLanguage_DoesNotFireEvent()
        {
            var loc = new LocalizationService();
            bool fired = false;
            loc.OnLanguageChanged += () => fired = true;

            loc.SetLanguage("vi"); // đã là "vi" từ đầu — không đổi gì

            Assert.IsFalse(fired);
        }

        [Test]
        public void SetLanguage_DifferentLanguage_FiresEventExactlyOnce()
        {
            var loc = new LocalizationService();
            int fireCount = 0;
            loc.OnLanguageChanged += () => fireCount++;

            loc.SetLanguage("en");
            loc.SetLanguage("en"); // gọi lại cùng giá trị — không bắn thêm

            Assert.AreEqual(1, fireCount);
        }

        // ---------- GetName — task-phase-5-gaps.md Phần D ----------
        // Đọc thẳng strings.csv thật (đã sinh bằng Tools/Localization/Generate Name Keys) —
        // 24 hero/66 enemy/65 skill, khớp NameKey có sẵn 100% trên data thật.

        [Test]
        public void GetName_ExistingHeroKey_ReturnsGeneratedTitleCaseName()
        {
            var loc = new LocalizationService();

            Assert.AreEqual("Ember Knight", loc.GetName("hero_ember_knight", LocalizedNameKind.Hero));
        }

        [Test]
        public void GetName_ExistingEnemyKey_ReturnsGeneratedTitleCaseName()
        {
            var loc = new LocalizationService();

            Assert.AreEqual("Abyss Stalker", loc.GetName("enemy_abyss_stalker", LocalizedNameKind.Enemy));
        }

        [Test]
        public void GetName_ExistingSkillKey_ReturnsGeneratedTitleCaseName()
        {
            var loc = new LocalizationService();

            Assert.AreEqual("Basic Attack", loc.GetName("skill_basic_attack", LocalizedNameKind.Skill));
        }

        [Test]
        public void GetName_SameAcrossLanguages_ProperNounNotFakelyTranslated()
        {
            // Tên riêng (fantasy proper noun) — VI/EN dùng chung 1 giá trị, không bịa dịch.
            var loc = new LocalizationService();

            string vi = loc.GetName("hero_ember_knight", LocalizedNameKind.Hero);
            loc.SetLanguage("en");
            string en = loc.GetName("hero_ember_knight", LocalizedNameKind.Hero);

            Assert.AreEqual(vi, en);
        }

        [Test]
        public void GetName_KeyNotInCsv_FallsBackToTitleCase_NotRawKey()
        {
            var loc = new LocalizationService();

            string result = loc.GetName("hero_totally_made_up_id", LocalizedNameKind.Hero);

            Assert.AreEqual("Totally Made Up Id", result);
            StringAssert.DoesNotContain("hero.", result); // không lộ key thô ra UI
        }

        [Test]
        public void GetName_NullOrEmptyDefId_ReturnsInputUnchanged_NoThrow()
        {
            var loc = new LocalizationService();

            Assert.IsNull(loc.GetName(null, LocalizedNameKind.Hero));
            Assert.AreEqual("", loc.GetName("", LocalizedNameKind.Enemy));
        }

        [Test]
        public void GetName_IdWithoutExpectedPrefix_StillFormatsReasonably()
        {
            // DefId không có tiền tố "hero_" (không nên xảy ra với data thật, nhưng không được throw).
            var loc = new LocalizationService();

            string result = loc.GetName("ember_knight", LocalizedNameKind.Hero);

            Assert.AreEqual("Ember Knight", result);
        }
    }
}
