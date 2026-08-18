using System.Collections.Generic;
using Game.Data.Dto;

namespace Game.Services.Save
{
    /// <summary>Một bước nâng save từ version N lên N+1.</summary>
    public interface ISaveMigration
    {
        int FromVersion { get; }
        int ToVersion { get; }
        void Apply(PlayerProfileDto profile);
    }

    /// <summary>
    /// Chạy tuần tự các migration cho tới version hiện tại — plan.md §11.6.
    /// Migration phải chịu được trường hợp field cũ thiếu/null.
    /// </summary>
    public sealed class SaveMigrationRunner
    {
        private readonly List<ISaveMigration> _migrations = new();

        public SaveMigrationRunner(IEnumerable<ISaveMigration> migrations = null)
        {
            if (migrations != null) _migrations.AddRange(migrations);
            _migrations.Sort((a, b) => a.FromVersion.CompareTo(b.FromVersion));
        }

        public void Register(ISaveMigration m)
        {
            _migrations.Add(m);
            _migrations.Sort((a, b) => a.FromVersion.CompareTo(b.FromVersion));
        }

        /// <summary>Trả về true nếu có migration nào chạy.</summary>
        public bool Run(PlayerProfileDto profile)
        {
            if (profile == null) return false;

            bool changed = false;
            int guard = 0;

            while (profile.Version < PlayerProfileDto.CURRENT_VERSION)
            {
                if (++guard > 100) break;   // chống vòng lặp do migration khai báo sai

                var m = Find(profile.Version);
                if (m == null)
                {
                    // Không có migration cho version này → nhảy thẳng lên hiện tại.
                    // Chấp nhận được vì DTO dùng field mặc định cho mọi field mới.
                    profile.Version = PlayerProfileDto.CURRENT_VERSION;
                    changed = true;
                    break;
                }

                m.Apply(profile);
                profile.Version = m.ToVersion;
                changed = true;
            }

            EnsureNotNull(profile);
            return changed;
        }

        private ISaveMigration Find(int fromVersion)
        {
            for (int i = 0; i < _migrations.Count; i++)
                if (_migrations[i].FromVersion == fromVersion) return _migrations[i];
            return null;
        }

        /// <summary>
        /// JsonUtility trả null cho collection thiếu trong file cũ.
        /// Vá lại ở đây để phần còn lại của game không phải kiểm tra null khắp nơi.
        /// </summary>
        private static void EnsureNotNull(PlayerProfileDto p)
        {
            p.Profile ??= new ProfileInfoDto();
            p.Wallet ??= new WalletDto();
            p.Wallet.Materials ??= new List<CurrencyEntryDto>();
            p.Wallet.HeroShards ??= new List<CurrencyEntryDto>();
            p.Heroes ??= new List<HeroInstanceDto>();
            p.Equipment ??= new List<EquipmentInstanceDto>();
            p.Inventory ??= new InventoryDto();
            p.Inventory.Items ??= new List<CurrencyEntryDto>();
            p.Progress ??= new ProgressDto();
            p.Progress.StageCleared ??= new List<CurrencyEntryDto>();
            p.Progress.UnlockedFormations ??= new List<string>();
            p.Run ??= new RunStateDto();
            p.Run.MapNodes ??= new List<MapNodeDto>();
            p.Run.TeamUids ??= new List<string>();
            p.Gacha ??= new GachaStateDto();
            p.Gacha.History ??= new List<string>();
            p.Quests ??= new QuestProgressDto();
            p.Quests.Daily ??= new List<CurrencyEntryDto>();
            p.Quests.Weekly ??= new List<CurrencyEntryDto>();
            p.Quests.Chain ??= new List<CurrencyEntryDto>();
            p.Quests.ClaimedQuestIds ??= new List<string>();
            p.Quests.UnlockedAchievements ??= new List<string>();
            p.Settings ??= new SettingsDto();
            p.Stats ??= new LifetimeStatsDto();
            p.Mail ??= new List<MailDto>();
            p.Arena ??= new ArenaProgressDto();
            p.Arena.Opponents ??= new List<ArenaOpponentDto>();
        }
    }
}
