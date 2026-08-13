using System.IO;
using System.Linq;
using Game.Data;
using Game.Data.Dto;
using Game.Services.Save;
using NUnit.Framework;

namespace Game.Tests.Services
{
    /// <summary>LocalPlayerRepository.CreateNew — trước task-mail.md hoàn toàn chưa có test nào
    /// (grep xác nhận). Chỉ test phần liên quan tới Mail — CreateNew đã có behavior khác (Wallet/
    /// Heroes/Progress) chạy ổn định lâu nay, không cần bao phủ lại toàn bộ ở đây.</summary>
    public class LocalPlayerRepositoryTests
    {
        // ---------- Load save CŨ THẬT — roadmap.md P4 "Save migration: chưa test qua version
        // thật". Mô phỏng đúng kịch bản ĐÃ THẬT SỰ xảy ra nhiều lần trong dự án: field mới
        // (Mail/Dungeon/TrialBoss/Tower/Progress.UnlockedFormations...) được thêm vào
        // PlayerProfileDto theo default value, KHÔNG bump PlayerProfileDto.CURRENT_VERSION (vẫn
        // là 1 xuyên suốt) — dựa hoàn toàn vào SaveMigrationRunner.EnsureNotNull. JSON dưới đây cố
        // ý CHỈ có field từ thời kỳ đầu nhất (Version/PlayerId/Wallet.Gold/1 hero) — không có
        // Mail/Progress/Run/Gacha/Quests/Settings/Stats/Dungeon/TrialBoss/Tower/Inventory/
        // Equipment — khắt khe hơn bất kỳ save thật nào từng tồn tại, để dò lỗ hổng tối đa. ----------

        private const string OLD_SAVE_JSON =
            "{\"Version\":1,\"PlayerId\":\"legacy-player-001\"," +
            "\"Wallet\":{\"Gold\":777},\"Heroes\":[{\"Uid\":\"u1\",\"DefId\":\"hero_ember_knight\",\"Star\":2}]}";

        private string _scratchDir;

        [SetUp]
        public void SetUpScratchDir()
        {
            _scratchDir = Path.Combine(Path.GetTempPath(), "turnbase-save-test-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchDir);
        }

        [TearDown]
        public void CleanUpScratchDir()
        {
            if (Directory.Exists(_scratchDir)) Directory.Delete(_scratchDir, recursive: true);
        }

        private LocalPlayerRepository WriteOldSaveAndBuildRepo(string json = OLD_SAVE_JSON)
        {
            File.WriteAllText(Path.Combine(_scratchDir, "save.json"), json);
            return new LocalPlayerRepository(_scratchDir);
        }

        [Test]
        public async System.Threading.Tasks.Task LoadAsync_VeryOldMinimalSave_DoesNotThrow_PreservesRealData()
        {
            var repo = WriteOldSaveAndBuildRepo();

            var profile = await repo.LoadAsync();

            Assert.AreEqual("legacy-player-001", profile.PlayerId);
            Assert.AreEqual(777, profile.Wallet.Gold);
            Assert.AreEqual(1, profile.Heroes.Count);
            Assert.AreEqual("hero_ember_knight", profile.Heroes[0].DefId);
            Assert.AreEqual(PlayerProfileDtoVersion(), profile.Version);
        }

        [Test]
        public async System.Threading.Tasks.Task LoadAsync_VeryOldMinimalSave_NoNullCollections_OnFieldsAddedLater()
        {
            var repo = WriteOldSaveAndBuildRepo();

            var profile = await repo.LoadAsync();

            // Field thêm sau (Mail/Formation/Endgame...) — đây chính xác là danh sách
            // EnsureNotNull đang vá; assert từng cái để bắt hồi quy nếu ai xoá nhầm 1 dòng.
            Assert.IsNotNull(profile.Mail, "Mail null → MailScreen sẽ NullReferenceException");
            Assert.IsNotNull(profile.Progress.UnlockedFormations,
                "UnlockedFormations null → FormationSystem cycle sẽ NullReferenceException");
            Assert.IsNotNull(profile.Run.MapNodes);
            Assert.IsNotNull(profile.Run.TeamUids);
            Assert.IsNotNull(profile.Gacha.History);
            Assert.IsNotNull(profile.Quests.Daily);
            Assert.IsNotNull(profile.Inventory.Items);
            Assert.IsNotNull(profile.Wallet.Materials);
            Assert.IsNotNull(profile.Wallet.HeroShards);

            // Dungeon/TrialBoss/Tower (task-endgame.md) — thêm SAU cả EnsureNotNull được viết lần
            // đầu, kiểm THẬT xem JsonUtility có để null hay không thay vì đoán.
            Assert.IsNotNull(profile.Dungeon, "Dungeon null → DungeonScreen sẽ NullReferenceException");
            Assert.IsNotNull(profile.Dungeon.FloorClearedToday);
            Assert.IsNotNull(profile.TrialBoss, "TrialBoss null → TrialBossScreen sẽ NullReferenceException");
            Assert.IsNotNull(profile.Tower, "Tower null → TowerScreen sẽ NullReferenceException");
        }

        [Test]
        public async System.Threading.Tasks.Task LoadAsync_SaveWithMismatchedChecksum_StillLoads_DoesNotBlockPlayer()
        {
            // File bị sửa tay/hỏng nhẹ — LocalPlayerRepository.LoadFrom chỉ cảnh báo, không chặn
            // (đúng doc-comment "không chặn người chơi — chỉ ghi nhận").
            string tampered = OLD_SAVE_JSON.Replace("}", ",\"Checksum\":\"not-a-real-hmac\"}");
            var repo = WriteOldSaveAndBuildRepo(tampered);

            PlayerProfileDto profile = null;
            Assert.DoesNotThrowAsync(async () => profile = await repo.LoadAsync());
            Assert.AreEqual("legacy-player-001", profile.PlayerId);
        }

        [Test]
        public async System.Threading.Tasks.Task LoadAsync_CorruptJson_FallsBackToNewProfile_DoesNotThrow()
        {
            File.WriteAllText(Path.Combine(_scratchDir, "save.json"), "{not valid json!!!");
            var repo = new LocalPlayerRepository(_scratchDir);

            PlayerProfileDto profile = null;
            Assert.DoesNotThrowAsync(async () => profile = await repo.LoadAsync());
            Assert.IsNotNull(profile);
            Assert.IsNotEmpty(profile.PlayerId, "Profile mới phải có PlayerId thật (CreateNew), không rỗng");
        }

        [Test]
        public async System.Threading.Tasks.Task SaveThenLoad_RoundTrip_PreservesData_ForRealFileOnDisk()
        {
            // Không chỉ test đọc save CŨ — xác nhận cả chu trình Save→Load thật với file trên đĩa
            // (không mock), đúng đường thật game dùng mỗi lần thoát/mở lại.
            var repo = new LocalPlayerRepository(_scratchDir);
            var original = LocalPlayerRepository.CreateNew();
            original.Wallet.Gold = 999_999;

            await repo.SaveAsync(original);
            var reloaded = await repo.LoadAsync();

            Assert.AreEqual(original.PlayerId, reloaded.PlayerId);
            Assert.AreEqual(999_999, reloaded.Wallet.Gold);
            Assert.AreEqual(original.Heroes.Count, reloaded.Heroes.Count);
            Assert.AreEqual(original.Mail.Count, reloaded.Mail.Count);
        }

        private static int PlayerProfileDtoVersion() => PlayerProfileDto.CURRENT_VERSION;


        [Test]
        public void CreateNew_GrantsExactlyOneUnclaimedWelcomeMail()
        {
            var p = LocalPlayerRepository.CreateNew();

            Assert.AreEqual(1, p.Mail.Count);
            var mail = p.Mail[0];
            Assert.AreEqual("welcome", mail.Id);
            Assert.IsFalse(mail.Claimed);
            Assert.IsTrue(mail.Rewards.Count > 0, "Welcome mail phải có ít nhất 1 reward");
        }

        [Test]
        public void CreateNew_WelcomeMail_RewardsAreGoldAndGem()
        {
            var p = LocalPlayerRepository.CreateNew();
            var mail = p.Mail[0];

            Assert.IsTrue(mail.Rewards.Any(r => (CurrencyType)r.CurrencyType == CurrencyType.Gold && r.Amount > 0));
            Assert.IsTrue(mail.Rewards.Any(r => (CurrencyType)r.CurrencyType == CurrencyType.Gem && r.Amount > 0));
        }
    }
}
