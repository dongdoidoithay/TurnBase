using System.Collections.Generic;
using System.Linq;
using Game.Data.Dto;
using Game.Meta.Content;
using UnityEngine;

namespace Game.Meta.Codex
{
    /// <summary>task-codex.md — Codex hero/enemy (plan.md chỉ ghi "Codex hero/enemy/item", không
    /// có spec chi tiết; "item" ngoài phạm vi vì hệ vật phẩm tiêu hao chưa xây, xem task-codex.md
    /// §0). Pure static, tách khỏi UI để test được. Không có tracking "đã gặp" enemy thật —
    /// <see cref="IsEnemyUnlocked"/> dùng proxy <c>enemyDef.Chapter &lt;= ChapterUnlocked</c>.</summary>
    public static class CodexSystem
    {
        private static List<HeroDefinitionSO> _heroes;
        private static List<EnemyDefinitionSO> _enemies;

        public static IReadOnlyList<HeroDefinitionSO> AllHeroes
            => _heroes ??= Resources.LoadAll<HeroDefinitionSO>("Data/Heroes")
                                     .OrderBy(h => h.DefId).ToList();

        public static IReadOnlyList<EnemyDefinitionSO> AllEnemies
            => _enemies ??= Resources.LoadAll<EnemyDefinitionSO>("Data/Enemies")
                                      .OrderBy(e => e.Chapter).ThenBy(e => e.DefId).ToList();

        public static bool IsHeroUnlocked(PlayerProfileDto profile, HeroDefinitionSO def)
            => profile != null && def != null && profile.Heroes.Exists(h => h.DefId == def.DefId);

        public static bool IsEnemyUnlocked(PlayerProfileDto profile, EnemyDefinitionSO def)
            => profile?.Progress != null && def != null && def.Chapter <= profile.Progress.ChapterUnlocked;
    }
}
