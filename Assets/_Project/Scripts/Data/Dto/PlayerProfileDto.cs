using System;
using System.Collections.Generic;

namespace Game.Data.Dto
{
    /// <summary>
    /// Gốc của save — plan.md §11.6. DTO thuần, serialize được, dùng lại làm payload API ở v2.
    /// ĐỔI SCHEMA thì phải chạy checklist object-map.md §7.10 (tăng version + migration).
    /// </summary>
    [Serializable]
    public class PlayerProfileDto
    {
        public const int CURRENT_VERSION = 1;

        public int Version = CURRENT_VERSION;
        public string PlayerId = "";
        public string CreatedAtUtc = "";
        public string LastSavedUtc = "";

        public ProfileInfoDto Profile = new();
        public WalletDto Wallet = new();
        public List<HeroInstanceDto> Heroes = new();
        public List<EquipmentInstanceDto> Equipment = new();
        public InventoryDto Inventory = new();
        public ProgressDto Progress = new();
        public RunStateDto Run = new();
        public GachaStateDto Gacha = new();
        public QuestProgressDto Quests = new();
        public SettingsDto Settings = new();
        public LifetimeStatsDto Stats = new();
        public DungeonProgressDto Dungeon = new();
        public TrialBossProgressDto TrialBoss = new();
        public TowerProgressDto Tower = new();
        public List<MailDto> Mail = new();
        public ArenaProgressDto Arena = new();

        public string Checksum = "";
    }

    /// <summary>task-mail.md — hộp thư, plan.md chỉ ghi "Mail = Đền bù LiveOps" không có spec chi
    /// tiết. Reward dùng mẫu <see cref="SubStatDto"/> (int enum-cast + value) thay vì
    /// <see cref="CurrencyEntryDto"/> (Key string) vì Mail luôn biết rõ <see cref="CurrencyType"/>,
    /// không cần key tuỳ ý.</summary>
    [Serializable]
    public class MailDto
    {
        public string Id = "";
        public string Title = "";
        public string Body = "";
        public List<MailRewardDto> Rewards = new();
        public bool Claimed;
        public string CreatedAtUtc = "";
        /// <summary>task-mail-extras.md — rỗng = không hết hạn (mail Welcome dùng mặc định này có
        /// chủ đích, quà chào mừng không nên hết hạn). Cùng quy ước ISO 8601 với CreatedAtUtc.</summary>
        public string ExpiresAtUtc = "";
    }

    [Serializable]
    public class MailRewardDto
    {
        public int CurrencyType;
        public long Amount;

        public MailRewardDto() { }
        public MailRewardDto(Game.Data.CurrencyType type, long amount) { CurrencyType = (int)type; Amount = amount; }
    }

    [Serializable]
    public class ProfileInfoDto
    {
        public string Name = "Hero";
        public int Level = 1;
        public long Exp;
        public string AvatarId = "av_01";
    }

    [Serializable]
    public class WalletDto
    {
        public long Gold;
        public long Gem;
        public int Energy;
        public string EnergyLastTickUtc = "";
        public int Ticket;
        public long Honor;

        /// <summary>Vật liệu: khoá là CurrencyType dạng số hoặc id vật liệu.</summary>
        public List<CurrencyEntryDto> Materials = new();
        /// <summary>Mảnh hero: khoá là defId của hero.</summary>
        public List<CurrencyEntryDto> HeroShards = new();
    }

    /// <summary>
    /// Cặp khoá-giá trị dạng list thay vì Dictionary — JsonUtility của Unity
    /// KHÔNG serialize được Dictionary.
    /// </summary>
    [Serializable]
    public class CurrencyEntryDto
    {
        public string Key = "";
        public long Value;

        public CurrencyEntryDto() { }
        public CurrencyEntryDto(string key, long value) { Key = key; Value = value; }
    }

    [Serializable]
    public class HeroInstanceDto
    {
        public string Uid = "";
        public string DefId = "";
        public int Level = 1;
        public long Exp;
        public int Star = 1;
        public bool Awakened;
        public int[] SkillLevels = { 1, 1, 1, 1, 1 };
        /// <summary>6 slot theo thứ tự EquipSlot; chuỗi rỗng = trống.</summary>
        public string[] Equipped = { "", "", "", "", "", "" };
    }

    [Serializable]
    public class EquipmentInstanceDto
    {
        public string Uid = "";
        public string DefId = "";
        public int Rarity;
        public int Level;
        public int MainStatType;
        public float MainStatValue;
        public List<SubStatDto> SubStats = new();
        public string SetId = "";
        public bool Locked;
    }

    [Serializable]
    public class SubStatDto
    {
        public int StatType;
        public float Value;

        public SubStatDto() { }
        public SubStatDto(int statType, float value) { StatType = statType; Value = value; }
    }

    [Serializable]
    public class InventoryDto
    {
        public List<CurrencyEntryDto> Items = new();
    }

    [Serializable]
    public class ProgressDto
    {
        public int ChapterUnlocked = 1;
        /// <summary>Khoá = số chương, giá trị = node cao nhất đã qua.</summary>
        public List<CurrencyEntryDto> StageCleared = new();
        public int TowerFloor;
        public bool TutorialCompleted;
        public List<string> UnlockedFormations = new();
    }

    [Serializable]
    public class RunStateDto
    {
        public bool Active;
        public int ChapterId;
        public long Seed;
        public int CurrentNodeId = -1;
        public List<MapNodeDto> MapNodes = new();
        public List<string> TeamUids = new();
        /// <summary>Snapshot trận đang dở (edge case E17): seed + chuỗi intent.</summary>
        public BattleSnapshotDto BattleSnapshot;
    }

    [Serializable]
    public class MapNodeDto
    {
        public int Id;
        public int Type;      // NodeType
        public int Tier;
        public int RowIndex;
        public int ColIndex;
        public int State;     // NodeState
        public List<int> NextIds = new();
    }

    /// <summary>Trận đang dở khi người chơi thoát app giữa chừng — edge case E17 (plan.md §4.14).
    /// CHỈ lưu intent của PHE PLAYER — enemy tự tái tạo giống hệt qua AI + cùng <see cref="Seed"/>
    /// khi replay (xem <c>Game.Combat.BattleReplay</c>), không cần lưu/replay intent enemy riêng
    /// (đã có bảo đảm determinism toàn hệ thống).</summary>
    [Serializable]
    public class BattleSnapshotDto
    {
        public bool Valid;
        public long Seed;
        public int NodeId = -1;
        public List<string> HeroDefIds = new();
        public List<string> EnemyDefIds = new();
        public List<int> IntentActorIds = new();
        public List<int> IntentSkillSlots = new();
        public List<int> IntentTargetIds = new();
        public List<int> IntentGrades = new();
    }

    [Serializable]
    public class GachaStateDto
    {
        public int PullsSinceLegendary;
        public int PullsSinceEpic;
        public int TotalPulls;
        /// <summary>100 lần gần nhất — yêu cầu minh bạch của store.</summary>
        public List<string> History = new();
    }

    [Serializable]
    public class QuestProgressDto
    {
        public List<CurrencyEntryDto> Daily = new();
        public List<CurrencyEntryDto> Weekly = new();
        public List<CurrencyEntryDto> Chain = new();
        public List<string> ClaimedQuestIds = new();
        public List<string> UnlockedAchievements = new();
        public string LastDailyResetUtc = "";
        public string LastWeeklyResetUtc = "";
        public int BattlePassLevel;
        public bool BattlePassPremium;
    }

    [Serializable]
    public class SettingsDto
    {
        public float Bgm = 0.7f;
        public float Sfx = 0.9f;
        public bool ActionCommandEnabled = true;
        /// <summary>Bù độ trễ đầu vào, ms — hiệu chỉnh ở Settings (plan.md §4.8.3).</summary>
        public int ActionCommandOffsetMs;
        public bool ScreenShake = true;
        public bool Haptics = true;
        public bool AutoBattle;
        public int BattleSpeed = 1;
        public string Language = "vi";
        public float TextScale = 1f;
        public bool ColorblindMode;
        public bool ShowEnemyIntent = true;
    }

    [Serializable]
    public class LifetimeStatsDto
    {
        public int BattlesWon;
        public int BattlesLost;
        public long PerfectHits;
        public long BreaksTriggered;
        public long TotalDamageDealt;
        public int HeroesCollected;
        /// <summary>task-quest.md — cộng dồn TRỌN ĐỜI (khác QuestProgressDto.Daily, reset mỗi
        /// ngày). Field mới, an toàn với save cũ (JsonUtility bỏ qua field lạ, mặc định 0).</summary>
        public int HeroLevelUps;
    }

    /// <summary>Dungeon hằng ngày (task-endgame.md, plan.md §8.3) — 4 loại (Gold/Exp/Material/
    /// Stone, DungeonKind 0-3), mỗi loại chỉ mở đúng vài ngày trong tuần. Field mới, an toàn với
    /// save cũ (JsonUtility bỏ qua field lạ, mặc định rỗng).</summary>
    [Serializable]
    public class DungeonProgressDto
    {
        public string LastDailyResetUtc = "";
        /// <summary>Khoá = tên <c>DungeonKind</c> (Gold/Exp/Material/Stone), giá trị = tầng đã
        /// qua HÔM NAY (0-10, reset mỗi ngày cùng lúc với <see cref="LastDailyResetUtc"/>).</summary>
        public List<CurrencyEntryDto> FloorClearedToday = new();
    }

    /// <summary>Trial Boss hằng tuần (task-endgame.md, plan.md §8.3) — 1 boss HP cực cao, xếp
    /// hạng CỤC BỘ theo tổng damage tốt nhất trong tuần (offline v1, không leaderboard nhiều
    /// người chơi — xem plan.md §0 "Meta/Online: Offline v1, kiến trúc server-ready").</summary>
    [Serializable]
    public class TrialBossProgressDto
    {
        /// <summary>Số tuần kể từ epoch ((utcNow - epoch).Days / 7) — đổi tuần thì reset. Dùng số
        /// nguyên thay vì chuỗi ISO week để không phụ thuộc API .NET có thể thiếu trên runtime
        /// Unity (khác <c>LastDailyResetUtc</c> dạng chuỗi "yyyy-MM-dd" ở QuestProgressDto).</summary>
        public long LastWeekKey = -1;
        public long BestDamageThisWeek;
        /// <summary>Bậc thưởng cao nhất đã nhận trong tuần (0 = chưa nhận gì) — mỗi bậc chỉ
        /// nhận 1 lần/tuần dù vượt ngưỡng nhiều lần.</summary>
        public int ClaimedTier;
    }

    /// <summary>Tháp Vô Tận (task-endgame.md, plan.md §8.3) — 100 tầng, HP KHÔNG hồi giữa tầng
    /// trong 1 lượt leo (chỉ tân trang giữa các LƯỢT leo khác nhau — mỗi lượt luôn bắt đầu lại từ
    /// tầng 1 với HP đầy, đúng cách mọi trận khác luôn bắt đầu full HP). Xếp hạng CỤC BỘ theo tầng
    /// cao nhất đạt được trong tuần — cùng khuôn <see cref="TrialBossProgressDto"/>. <see
    /// cref="ProgressDto.TowerFloor"/> (đã có sẵn, trước đây không dùng) giữ mốc CAO NHẤT MỌI THỜI
    /// ĐẠI, không reset — tách biệt với <see cref="BestFloorThisWeek"/> vốn reset hằng tuần để
    /// tính bậc thưởng.</summary>
    [Serializable]
    public class TowerProgressDto
    {
        public long LastWeekKey = -1;
        public int BestFloorThisWeek;
        /// <summary>Bậc thưởng cao nhất đã nhận trong tuần (0 = chưa nhận gì).</summary>
        public int ClaimedTier;
    }

    /// <summary>task-arena.md, plan.md v1.1 "Arena — Mùa 14 ngày, PvP async với snapshot đội hình
    /// do AI điều khiển, Honor". KHÔNG có backend thật (dự án chỉ local-save) nên "đối thủ" là
    /// snapshot hero THẬT nhưng do RNG sinh (ghost battle) — không giả vờ là dữ liệu người chơi
    /// khác. Cùng khuôn <see cref="TrialBossProgressDto"/> nhưng theo MÙA (14 ngày) thay vì tuần.</summary>
    [Serializable]
    public class ArenaProgressDto
    {
        /// <summary>Số kỳ 14 ngày kể từ epoch — cùng kỹ thuật <see cref="TrialBossProgressDto.
        /// LastWeekKey"/> (số nguyên, không phụ thuộc parse chuỗi ISO week/API .NET thiếu trên
        /// runtime Unity).</summary>
        public long LastSeasonKey = -1;
        public List<ArenaOpponentDto> Opponents = new();
        /// <summary>Số hiển thị kiểu ELO — CHỈ TĂNG khi thắng, KHÔNG giảm khi thua (đúng tinh thần
        /// "không phạt nặng" đã dùng ở NodeChoice/Enhance — thua Arena không mất gì ngoài thời
        /// gian).</summary>
        public long Rating = 1000;
    }

    [Serializable]
    public class ArenaOpponentDto
    {
        public string[] HeroDefIds = Array.Empty<string>();
        public int Level = 1;
        public int Star = 1;
        public long HonorReward;
        /// <summary>Đã nhận Honor bậc này trong MÙA hiện tại chưa — reset về false khi
        /// <see cref="ArenaProgressDto.LastSeasonKey"/> đổi (đối thủ cũng sinh lại toàn bộ).</summary>
        public bool Claimed;
    }
}
