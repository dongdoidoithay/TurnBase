using System.Collections.Generic;
using Game.Core;
using Game.Data;
using Game.Data.Dto;
using Game.Meta.Mail;
using Game.Services.Economy;
using NUnit.Framework;

namespace Game.Tests.Meta
{
    /// <summary>MailSystem — task-mail.md.</summary>
    public class MailSystemTests
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

        private static PlayerProfileDto ProfileWithMail(out MailDto mail)
        {
            var p = new PlayerProfileDto();
            mail = new MailDto
            {
                Id = "m1",
                Title = "Test Mail",
                Rewards = new List<MailRewardDto>
                {
                    new(CurrencyType.Gold, 500),
                    new(CurrencyType.Gem, 20),
                },
            };
            p.Mail.Add(mail);
            return p;
        }

        [Test]
        public void TryClaim_Unclaimed_GrantsAllRewards_MarksClaimed()
        {
            var p = ProfileWithMail(out var mail);

            bool ok = MailSystem.TryClaim(p, mail.Id, Economy());

            Assert.IsTrue(ok);
            Assert.IsTrue(mail.Claimed);
            Assert.AreEqual(500, Economy().Get(p.Wallet, CurrencyType.Gold));
            Assert.AreEqual(20, Economy().Get(p.Wallet, CurrencyType.Gem));
        }

        [Test]
        public void TryClaim_AlreadyClaimed_Fails_NoDoubleGrant()
        {
            var p = ProfileWithMail(out var mail);
            MailSystem.TryClaim(p, mail.Id, Economy());

            bool ok = MailSystem.TryClaim(p, mail.Id, Economy());

            Assert.IsFalse(ok);
            Assert.AreEqual(500, Economy().Get(p.Wallet, CurrencyType.Gold), "Không được cộng dồn lần 2");
        }

        [Test]
        public void TryClaim_UnknownId_Fails_NoOp()
        {
            var p = ProfileWithMail(out _);

            bool ok = MailSystem.TryClaim(p, "does_not_exist", Economy());

            Assert.IsFalse(ok);
            Assert.AreEqual(0, Economy().Get(p.Wallet, CurrencyType.Gold));
        }

        [Test]
        public void UnclaimedCount_CountsOnlyUnclaimed()
        {
            var p = ProfileWithMail(out var mail);
            p.Mail.Add(new MailDto { Id = "m2", Claimed = true });
            p.Mail.Add(new MailDto { Id = "m3", Claimed = false });

            Assert.AreEqual(2, MailSystem.UnclaimedCount(p));

            MailSystem.TryClaim(p, mail.Id, Economy());
            Assert.AreEqual(1, MailSystem.UnclaimedCount(p));
        }

        [Test]
        public void UnclaimedCount_NullProfile_ReturnsZero_NoThrow()
        {
            Assert.AreEqual(0, MailSystem.UnclaimedCount(null));
        }

        // ---------- ClaimAll (task-mail-extras.md) ----------

        [Test]
        public void ClaimAll_ClaimsEveryUnclaimedMail_ReturnsCount()
        {
            var p = ProfileWithMail(out var mail1);
            var mail2 = new MailDto { Id = "m2", Rewards = new List<MailRewardDto> { new(CurrencyType.Gold, 300) } };
            var mail3Claimed = new MailDto { Id = "m3", Claimed = true, Rewards = new List<MailRewardDto> { new(CurrencyType.Gold, 999) } };
            p.Mail.Add(mail2);
            p.Mail.Add(mail3Claimed);

            int count = MailSystem.ClaimAll(p, Economy());

            Assert.AreEqual(2, count, "Chỉ claim 2 mail chưa claim (m1, m2) — m3 đã claim từ trước");
            Assert.IsTrue(mail1.Claimed);
            Assert.IsTrue(mail2.Claimed);
            Assert.AreEqual(500 + 300, Economy().Get(p.Wallet, CurrencyType.Gold),
                "Không cộng dồn lại m3 (999) — đã claim từ trước");
        }

        [Test]
        public void ClaimAll_NoUnclaimedMail_ReturnsZero_NoOp()
        {
            var p = ProfileWithMail(out var mail);
            MailSystem.TryClaim(p, mail.Id, Economy());

            int count = MailSystem.ClaimAll(p, Economy());

            Assert.AreEqual(0, count);
        }

        // ---------- PurgeExpired (task-mail-extras.md) ----------

        [Test]
        public void PurgeExpired_RemovesOnlyPastExpiryMail_KeepsNeverExpiringAndFutureMail()
        {
            var now = System.DateTime.UtcNow;
            var p = new PlayerProfileDto();
            p.Mail.Add(new MailDto { Id = "expired", ExpiresAtUtc = now.AddDays(-1).ToString("o") });
            p.Mail.Add(new MailDto { Id = "future", ExpiresAtUtc = now.AddDays(5).ToString("o") });
            p.Mail.Add(new MailDto { Id = "never_expires", ExpiresAtUtc = "" });

            int removed = MailSystem.PurgeExpired(p, now);

            Assert.AreEqual(1, removed);
            Assert.IsNull(p.Mail.Find(m => m.Id == "expired"));
            Assert.IsNotNull(p.Mail.Find(m => m.Id == "future"));
            Assert.IsNotNull(p.Mail.Find(m => m.Id == "never_expires"));
        }

        [Test]
        public void PurgeExpired_RemovesExpiredMail_EvenIfAlreadyClaimed()
        {
            var now = System.DateTime.UtcNow;
            var p = new PlayerProfileDto();
            p.Mail.Add(new MailDto { Id = "expired_claimed", Claimed = true, ExpiresAtUtc = now.AddDays(-1).ToString("o") });

            int removed = MailSystem.PurgeExpired(p, now);

            Assert.AreEqual(1, removed, "Dọn dẹp thật — kể cả mail đã claim rồi, không chỉ ẩn");
            Assert.AreEqual(0, p.Mail.Count);
        }

        [Test]
        public void PurgeExpired_NullProfile_ReturnsZero_NoThrow()
        {
            Assert.AreEqual(0, MailSystem.PurgeExpired(null, System.DateTime.UtcNow));
        }
    }
}
