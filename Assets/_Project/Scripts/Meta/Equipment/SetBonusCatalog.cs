using Game.Combat.Model;
using Game.Core.Random;
using Game.Data;

namespace Game.Meta.Equipment
{
    /// <summary>
    /// 8 bộ trang bị thật — plan.md §7.4, task-setbonus.md. Hard-code (KHÔNG ScriptableObject) —
    /// lý do y hệt <see cref="Game.Meta.Hero.AwakeningCatalog"/>: <see cref="StatModifier"/>/
    /// <see cref="StatusApplication"/> có field readonly bị Unity serializer bỏ qua âm thầm.
    ///
    /// V1 — số liệu (%/duration/stack) là PLACEHOLDER chờ Balance Harness, không phải cân bằng
    /// cuối. <see cref="FourPiece"/> trả instance MỚI mỗi lần gọi (không share) để
    /// <c>PassiveData.Consumed</c> không rò rỉ giữa các trận/unit khác nhau — cùng lý do
    /// <see cref="Game.Meta.Hero.AwakeningCatalog.Get"/> làm vậy.
    /// </summary>
    public static class SetBonusCatalog
    {
        public static readonly string[] SET_IDS =
        {
            "ember", "bastion", "tempest", "assassin", "sage", "guardian", "breaker", "vampire"
        };

        /// <summary>SetId roll ở tầng INSTANCE lúc sinh trang bị (EquipmentGenerator.RollFrom) —
        /// đều 1/8 mỗi bộ, độc lập với def/rarity, giống cách Rarity/sub-stat đã roll độc lập.</summary>
        public static string RollRandomSetId(IRandomSource rng) => SET_IDS[rng.NextInt(SET_IDS.Length)];

        /// <summary>Bonus 2 món — chỉ StatModifier thuần, mọi field đều đã được DamageCalculator/
        /// ActionResolver đọc thật từ trước (đã kiểm bằng grep trực tiếp, xem task-setbonus.md §0).</summary>
        public static StatModifier[] TwoPiece(string setId) => setId switch
        {
            "ember"    => new[] { new StatModifier(StatType.AtkPct, 12f, "set_ember") },
            "bastion"  => new[] { new StatModifier(StatType.DefPct, 15f, "set_bastion") },
            "tempest"  => new[] { new StatModifier(StatType.Spd, 8f, "set_tempest") },
            "assassin" => new[] { new StatModifier(StatType.CritPct, 10f, "set_assassin") },
            "sage"     => new[] { new StatModifier(StatType.MaxSpPct, 15f, "set_sage") },
            "guardian" => new[] { new StatModifier(StatType.MaxHpPct, 12f, "set_guardian") },
            "breaker"  => new[] { new StatModifier(StatType.PoiseDmgPct, 15f, "set_breaker") },
            "vampire"  => new[] { new StatModifier(StatType.LifestealPct, 8f, "set_vampire") },
            _ => System.Array.Empty<StatModifier>()
        };

        /// <summary>Bonus 4 món — đủ 8/8 bộ (task-setbonus.md §1.2, Tempest thêm sau cùng đợt
        /// §1.1 ban đầu hoãn lại).</summary>
        public static PassiveData FourPiece(string setId) => setId switch
        {
            // Đòn Perfect gây Burn 3 lượt lên địch vừa đánh trúng. Dùng OnHitDealt (không phải
            // OnPerfectCommand — trigger đó không có contextTarget là địch, xem task-setbonus.md
            // "phát hiện thứ 4") + RequiresPerfectGrade để chỉ nổ khi bấm Perfect.
            "ember" => new PassiveData
            {
                Id = "set_ember_molten_edge",
                Trigger = PassiveTrigger.OnHitDealt,
                RequiresPerfectGrade = true,
                Applies = new[] { new StatusApplication(StatusId.Burn, 1f, duration: 3, stacks: 1, targetSelf: false) }
            },

            // HP < 50% → shield 20% MaxHP, 1 lần/trận (Consumed qua CheckHpThreshold).
            "bastion" => new PassiveData
            {
                Id = "set_bastion_last_stand",
                Trigger = PassiveTrigger.OnHpBelowThreshold,
                Threshold = 0.5f,
                ShieldPercentMaxHp = 20f
            },

            // Hành động đầu tiên mỗi round → +20% dmg (task-setbonus.md §1.2). Đọc trực tiếp
            // CombatUnit.IsFirstActorThisRound trong DamageCalculator — không qua Applies/status.
            "tempest" => new PassiveData
            {
                Id = "set_tempest_first_strike",
                Trigger = PassiveTrigger.OnHitDealt,
                RequiresFirstActionOfRound = true,
                ExtraDamagePercent = 20f
            },

            // Crit → thêm Bleed cố định (đơn giản hoá — không scale %-theo-damage-vừa-gây, xem
            // task-setbonus.md §3; RequiresCrit đảm bảo chỉ nổ đúng lúc Crit, không phải mọi đòn).
            "assassin" => new PassiveData
            {
                Id = "set_assassin_crimson_edge",
                Trigger = PassiveTrigger.OnHitDealt,
                RequiresCrit = true,
                Applies = new[] { new StatusApplication(StatusId.Bleed, 0.5f, duration: 2, stacks: 1, targetSelf: false) }
            },

            // Perfect → hoàn 30% SP cost skill vừa dùng.
            "sage" => new PassiveData
            {
                Id = "set_sage_mana_flow",
                Trigger = PassiveTrigger.OnPerfectCommand,
                SpRefundPercent = 30f
            },

            // Hồi 8% MaxHP mỗi khi kết thúc lượt.
            "guardian" => new PassiveData
            {
                Id = "set_guardian_stalwart_heart",
                Trigger = PassiveTrigger.OnTurnEnd,
                HealPercentMaxHp = 8f
            },

            // Break mục tiêu → CẢ ĐỘI +15% ATK 2 lượt.
            "breaker" => new PassiveData
            {
                Id = "set_breaker_shatterpoint",
                Trigger = PassiveTrigger.OnBreakTriggered,
                Applies = new[]
                {
                    new StatusApplication(StatusId.AtkUp, 1f, duration: 2, stacks: 1, targetSelf: false)
                    {
                        TargetAllAllies = true
                    }
                }
            },

            // Giết địch → hồi 15% MaxHP.
            "vampire" => new PassiveData
            {
                Id = "set_vampire_bloodthirst",
                Trigger = PassiveTrigger.OnKill,
                HealPercentMaxHp = 15f
            },

            _ => null
        };
    }
}
