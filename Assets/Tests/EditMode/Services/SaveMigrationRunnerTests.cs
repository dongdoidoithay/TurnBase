using System.Collections.Generic;
using Game.Data.Dto;
using Game.Services.Save;
using NUnit.Framework;

namespace Game.Tests.Services
{
    /// <summary>
    /// roadmap.md P4 — "Save migration: chưa test qua version thật". <c>SaveMigrationRunner</c>
    /// (object-map.md §8 <c>T-SVC-MIG</c>) chưa từng có test riêng dù đã sống trong
    /// <c>LocalPlayerRepository.LoadAsync</c> từ đầu. <c>PlayerProfileDto.CURRENT_VERSION</c> vẫn
    /// là <c>1</c> — dự án CHƯA TỪNG bump version thật (mọi field mới từ trước tới nay đều thêm
    /// qua default value + <c>EnsureNotNull</c>, không qua <c>ISaveMigration</c> nào), nên KHÔNG
    /// có migration thật nào tồn tại để test qua. Test ở đây kiểm cơ chế RUNNER bằng
    /// <see cref="ISaveMigration"/> giả lập cục bộ (không đụng production) — sẵn sàng cho lần đầu
    /// tiên dự án thật sự bump version. Phần "test qua save cũ thật" (kịch bản đã THẬT SỰ xảy ra
    /// nhiều lần trong dự án — thêm field mới không bump version) nằm ở
    /// <c>LocalPlayerRepositoryTests.cs</c> (load file JSON thật qua <c>LoadAsync</c> công khai).
    /// </summary>
    public class SaveMigrationRunnerTests
    {
        private sealed class FakeMigration : ISaveMigration
        {
            public int FromVersion { get; }
            public int ToVersion { get; }
            private readonly System.Action<PlayerProfileDto> _apply;

            public FakeMigration(int from, int to, System.Action<PlayerProfileDto> apply = null)
            {
                FromVersion = from;
                ToVersion = to;
                _apply = apply;
            }

            public void Apply(PlayerProfileDto profile) => _apply?.Invoke(profile);
        }

        private static PlayerProfileDto NewProfile(int version)
        {
            var p = LocalPlayerRepository.CreateNew();
            p.Version = version;
            return p;
        }

        [Test]
        public void Run_ProfileAlreadyCurrentVersion_DoesNothing_ReturnsFalse()
        {
            var runner = new SaveMigrationRunner();
            var profile = NewProfile(PlayerProfileDto.CURRENT_VERSION);

            bool changed = runner.Run(profile);

            Assert.IsFalse(changed);
            Assert.AreEqual(PlayerProfileDto.CURRENT_VERSION, profile.Version);
        }

        [Test]
        public void Run_OlderVersion_WithMatchingMigration_AppliesAndBumpsVersion()
        {
            // Version 0 mô phỏng save còn cũ hơn cả field Version (JsonUtility mặc định int = 0
            // khi field vắng mặt trong JSON — kịch bản thật có thể xảy ra với save từ bản đầu
            // tiên chưa có field Version).
            bool applied = false;
            var migration = new FakeMigration(0, PlayerProfileDto.CURRENT_VERSION, p => applied = true);
            var runner = new SaveMigrationRunner(new ISaveMigration[] { migration });
            var profile = NewProfile(0);

            bool changed = runner.Run(profile);

            Assert.IsTrue(changed);
            Assert.IsTrue(applied, "Migration.Apply phải được gọi thật");
            Assert.AreEqual(PlayerProfileDto.CURRENT_VERSION, profile.Version);
        }

        [Test]
        public void Run_OlderVersion_NoMatchingMigration_JumpsStraightToCurrentVersion()
        {
            // Đúng hành vi ghi trong SaveMigrationRunner.Run — không tìm thấy migration cho
            // version hiện tại thì nhảy thẳng lên CURRENT_VERSION, chấp nhận vì DTO dùng default.
            var runner = new SaveMigrationRunner(); // không đăng ký migration nào
            var profile = NewProfile(0);

            bool changed = runner.Run(profile);

            Assert.IsTrue(changed);
            Assert.AreEqual(PlayerProfileDto.CURRENT_VERSION, profile.Version);
        }

        [Test]
        public void Run_MigrationDoesNotAdvanceVersion_GuardStopsInfiniteLoop()
        {
            // Migration cấu hình SAI (ToVersion == FromVersion) — mô phỏng bug tương lai. Guard
            // (100 vòng) trong SaveMigrationRunner.Run phải chặn treo test/app thật.
            var brokenMigration = new FakeMigration(0, 0);
            var runner = new SaveMigrationRunner(new ISaveMigration[] { brokenMigration });
            var profile = NewProfile(0);

            Assert.DoesNotThrow(() => runner.Run(profile));
            // Không bao giờ thoát vòng lặp bằng cách tự tăng version — vẫn kẹt ở 0, nhưng KHÔNG treo.
            Assert.AreEqual(0, profile.Version);
        }

        [Test]
        public void Run_NullProfile_ReturnsFalse_NoThrow()
        {
            var runner = new SaveMigrationRunner();
            Assert.DoesNotThrow(() => Assert.IsFalse(runner.Run(null)));
        }

        [Test]
        public void Constructor_SortsMigrationsByFromVersion_RegardlessOfInputOrder()
        {
            var applyOrder = new List<int>();
            var migrations = new ISaveMigration[]
            {
                new FakeMigration(2, 3, _ => applyOrder.Add(2)),
                new FakeMigration(0, 1, _ => applyOrder.Add(0)),
                new FakeMigration(1, 2, _ => applyOrder.Add(1)),
            };
            var runner = new SaveMigrationRunner(migrations);
            var profile = NewProfile(0);
            // CURRENT_VERSION thật = 1 nên vòng lặp dừng sau bước 0→1 — chỉ xác nhận migration
            // ĐÚNG (FromVersion=0) được tìm và chạy trước, không phụ thuộc thứ tự khai báo mảng.
            runner.Run(profile);

            Assert.AreEqual(1, applyOrder.Count);
            Assert.AreEqual(0, applyOrder[0]);
        }

        [Test]
        public void Run_PreservesExistingData_OnlyTouchesVersionAndNullCollections()
        {
            var runner = new SaveMigrationRunner();
            var profile = NewProfile(0);
            profile.Wallet.Gold = 12345;
            string firstHeroDefId = profile.Heroes[0].DefId;

            runner.Run(profile);

            Assert.AreEqual(12345, profile.Wallet.Gold);
            Assert.AreEqual(firstHeroDefId, profile.Heroes[0].DefId);
        }
    }
}
