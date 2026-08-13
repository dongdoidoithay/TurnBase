using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Game.Data;
using Game.Data.Dto;
using UnityEngine;

namespace Game.Services.Save
{
    /// <summary>
    /// Lưu local: JSON + HMAC-SHA256 + ghi atomic + backup + migration.
    /// Ghi atomic (tmp → rename) chống hỏng save khi mất điện giữa chừng — plan.md §11.6.
    /// </summary>
    public sealed class LocalPlayerRepository : IPlayerRepository
    {
        private const string FILE_NAME = "save.json";
        private const string TEMP_NAME = "save.tmp";
        private const string BACKUP_NAME = "save.bak";

        // Khoá HMAC chỉ chống sửa file thủ công ở game offline.
        // Khi lên server, tính toàn vẹn do server quyết định — đây chỉ là lớp đầu tiên.
        private static readonly byte[] HMAC_KEY =
            Encoding.UTF8.GetBytes("TurnBase.v1.integrity.key.change-before-release");

        private readonly string _dir;
        private readonly SaveMigrationRunner _migrations;

        public LocalPlayerRepository(string directory = null, SaveMigrationRunner migrations = null)
        {
            _dir = directory ?? Application.persistentDataPath;
            _migrations = migrations ?? new SaveMigrationRunner();
        }

        private string MainPath => Path.Combine(_dir, FILE_NAME);
        private string TempPath => Path.Combine(_dir, TEMP_NAME);
        private string BackupPath => Path.Combine(_dir, BACKUP_NAME);

        public bool HasSave => File.Exists(MainPath);

        // =====================================================================

        public Task<PlayerProfileDto> LoadAsync()
        {
            var profile = LoadFrom(MainPath, out string error);

            if (profile == null)
            {
                if (!string.IsNullOrEmpty(error))
                    Debug.LogWarning($"[Save] Không đọc được save chính: {error}. Thử bản backup.");

                profile = LoadFrom(BackupPath, out string backupError);
                if (profile == null && !string.IsNullOrEmpty(backupError))
                    Debug.LogWarning($"[Save] Backup cũng hỏng: {backupError}. Tạo profile mới.");
            }

            profile ??= CreateNew();

            int from = profile.Version;
            if (_migrations.Run(profile))
                Debug.Log($"[Save] Đã nâng save từ version {from} lên {profile.Version}.");

            return Task.FromResult(profile);
        }

        public Task SaveAsync(PlayerProfileDto profile)
        {
            if (profile == null) return Task.CompletedTask;

            try
            {
                profile.Version = PlayerProfileDto.CURRENT_VERSION;
                profile.LastSavedUtc = DateTime.UtcNow.ToString("o");
                profile.Checksum = "";                       // loại checksum cũ khỏi phép tính
                string json = JsonUtility.ToJson(profile, false);
                profile.Checksum = ComputeHmac(json);

                string payload = JsonUtility.ToJson(profile, false);

                Directory.CreateDirectory(_dir);

                // 1. Ghi ra file tạm và flush xuống đĩa
                using (var fs = new FileStream(TempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var sw = new StreamWriter(fs, Encoding.UTF8))
                {
                    sw.Write(payload);
                    sw.Flush();
                    fs.Flush(true);
                }

                // 2. Giữ bản cũ làm backup
                if (File.Exists(MainPath))
                {
                    if (File.Exists(BackupPath)) File.Delete(BackupPath);
                    File.Move(MainPath, BackupPath);
                }

                // 3. Rename atomic — bước này hoặc thành công hoàn toàn, hoặc không xảy ra
                File.Move(TempPath, MainPath);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] Lưu thất bại: {e}");
            }

            return Task.CompletedTask;
        }

        public Task DeleteAsync()
        {
            try
            {
                // Giữ backup — người chơi bấm nhầm "xoá dữ liệu" là chuyện có thật
                if (File.Exists(MainPath))
                {
                    if (File.Exists(BackupPath)) File.Delete(BackupPath);
                    File.Move(MainPath, BackupPath);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] Xoá thất bại: {e}");
            }
            return Task.CompletedTask;
        }

        // =====================================================================

        private PlayerProfileDto LoadFrom(string path, out string error)
        {
            error = null;
            if (!File.Exists(path)) return null;

            try
            {
                string payload = File.ReadAllText(path, Encoding.UTF8);
                var profile = JsonUtility.FromJson<PlayerProfileDto>(payload);
                if (profile == null) { error = "JSON không hợp lệ"; return null; }

                string stored = profile.Checksum;
                profile.Checksum = "";
                string expected = ComputeHmac(JsonUtility.ToJson(profile, false));
                profile.Checksum = stored;

                if (!string.IsNullOrEmpty(stored) && stored != expected)
                {
                    // Không chặn người chơi — chỉ ghi nhận. Chống gian lận thật sự cần server.
                    Debug.LogWarning("[Save] Checksum không khớp — file có thể đã bị sửa tay.");
                }

                return profile;
            }
            catch (Exception e)
            {
                error = e.Message;
                return null;
            }
        }

        private static string ComputeHmac(string content)
        {
            using var hmac = new HMACSHA256(HMAC_KEY);
            byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(content));
            return Convert.ToBase64String(hash);
        }

        public static PlayerProfileDto CreateNew()
        {
            string now = DateTime.UtcNow.ToString("o");
            var p = new PlayerProfileDto
            {
                PlayerId = Guid.NewGuid().ToString("N"),
                CreatedAtUtc = now,
                LastSavedUtc = now
            };
            p.Wallet.Gold = 5_000;
            p.Wallet.Gem = 300;
            p.Wallet.Energy = 120;
            p.Wallet.EnergyLastTickUtc = now;
            p.Wallet.Ticket = 1;
            p.Progress.ChapterUnlocked = 1;
            // All 8 formation presets unlocked from the start (task-formation-synergy.md).
            // IDs must stay in sync with FormationSystem.AllIds — Game.Services can't ref Game.Meta.
            p.Progress.UnlockedFormations.AddRange(new[]
            {
                "formation_balanced", "formation_phalanx", "formation_arrowhead",
                "formation_vanguard_line", "formation_siege", "formation_flanking",
                "formation_turtle", "formation_blitz",
            });

            // Đủ 6 hero (toàn bộ heroes.csv hiện có) — cấp full ngay từ đầu để màn chọn đội
            // hình (TeamSelectScreen) có ý nghĩa thật (party chỉ 4/6, phải chọn bớt).
            // P4 (Gacha) sẽ là nguồn hero chính thức; đây chỉ để P2 Vertical Slice chơi được ngay.
            string[] starters =
            {
                "hero_ember_knight", "hero_shadow_fang", "hero_frost_sage",
                "hero_dawn_cleric", "hero_gale_thief", "hero_bone_caller",
            };
            foreach (var defId in starters)
            {
                // Level mặc định 1 (theo field default của HeroInstanceDto) — trước đây hard-code
                // 10 nhưng Exp không có gì "chống lưng" (luôn 0), gây lệch khi HeroLevelSystem
                // tính EXP còn thiếu tới cấp kế = số âm. Level không hard-code nữa thì hero mới sẽ
                // lên cấp thật qua EXP từ trận đấu (Game.Services không được phép ref Game.Meta để
                // gọi thẳng HeroLevelSystem — luật kiến trúc structure.md §6).
                p.Heroes.Add(new HeroInstanceDto
                {
                    Uid = Guid.NewGuid().ToString("N"),
                    DefId = defId,
                    Star = 1
                });
            }

            // task-mail.md — quà chào mừng qua hộp thư (mẫu F2P chuẩn) thay vì cấp thẳng vào ví.
            // Chỉ construct DTO thô ở đây — KHÔNG gọi Game.Meta.Mail.MailSystem (Game.Services
            // không được phép ref Game.Meta, cùng lý do Star/Level hero ở trên không gọi thẳng
            // HeroLevelSystem).
            p.Mail.Add(new MailDto
            {
                Id = "welcome",
                Title = "Welcome, Commander!",
                Body = "A small gift to start your journey. Good luck out there.",
                Rewards = new List<MailRewardDto>
                {
                    new(CurrencyType.Gold, 2_000),
                    new(CurrencyType.Gem, 100),
                },
                CreatedAtUtc = now,
            });

            return p;
        }
    }
}
