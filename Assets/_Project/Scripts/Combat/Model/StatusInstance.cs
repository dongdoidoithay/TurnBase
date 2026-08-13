using Game.Data;

namespace Game.Combat.Model
{
    /// <summary>Một trạng thái đang tác động lên unit. Luật stack/duration ở plan.md §4.11.</summary>
    public sealed class StatusInstance
    {
        public StatusId Id;
        public int Stacks;
        public int RemainingTurns;
        public int SourceUnitId;
        /// <summary>Giá trị phụ thuộc loại: Shield = lượng hấp thụ còn lại; Bleed = ATK nguồn.</summary>
        public float Value;

        public StatusInstance(StatusId id, int stacks, int duration, int sourceUnitId, float value = 0f)
        {
            Id = id;
            Stacks = stacks;
            RemainingTurns = duration;
            SourceUnitId = sourceUnitId;
            Value = value;
        }

        public bool IsExpired => RemainingTurns <= 0 || Stacks <= 0;

        public StatusInstance Clone() => new(Id, Stacks, RemainingTurns, SourceUnitId, Value);

        public override string ToString() => $"{Id} x{Stacks} ({RemainingTurns} lượt)";
    }

    /// <summary>Bảng tra thuộc tính tĩnh của 22 status — plan.md §4.11.</summary>
    public static class StatusTable
    {
        public readonly struct Info
        {
            public readonly StatusGroup Group;
            public readonly int MaxStacks;
            public readonly int DefaultDuration;
            public readonly StatusTickTiming Tick;
            public readonly DispelType Dispel;
            public readonly bool BlocksAction;

            public Info(StatusGroup group, int maxStacks, int duration,
                        StatusTickTiming tick, DispelType dispel, bool blocksAction = false)
            {
                Group = group; MaxStacks = maxStacks; DefaultDuration = duration;
                Tick = tick; Dispel = dispel; BlocksAction = blocksAction;
            }
        }

        public static Info Get(StatusId id) => id switch
        {
            // DoT — tick ở ĐẦU lượt của unit mang status (pipeline bước 3)
            StatusId.Burn     => new(StatusGroup.Dot, 3, 3, StatusTickTiming.TurnStart, DispelType.Cleanse),
            StatusId.Poison   => new(StatusGroup.Dot, 3, 4, StatusTickTiming.TurnStart, DispelType.Cleanse),
            StatusId.Bleed    => new(StatusGroup.Dot, 5, 3, StatusTickTiming.TurnStart, DispelType.Cleanse),

            // Control
            StatusId.Stun     => new(StatusGroup.Control, 1, 1, StatusTickTiming.None, DispelType.Cleanse, true),
            StatusId.Freeze   => new(StatusGroup.Control, 1, 2, StatusTickTiming.None, DispelType.Cleanse, true),
            StatusId.Paralyze => new(StatusGroup.Control, 1, 3, StatusTickTiming.None, DispelType.Cleanse), // 50% mất lượt, xử lý riêng
            StatusId.Silence  => new(StatusGroup.Control, 1, 2, StatusTickTiming.None, DispelType.Cleanse),
            StatusId.Taunt    => new(StatusGroup.Control, 1, 2, StatusTickTiming.None, DispelType.Cleanse),
            StatusId.Sleep    => new(StatusGroup.Control, 1, 3, StatusTickTiming.None, DispelType.Cleanse, true),

            // Debuff
            StatusId.AtkDown  => new(StatusGroup.Debuff, 2, 2, StatusTickTiming.None, DispelType.Cleanse),
            StatusId.DefDown  => new(StatusGroup.Debuff, 2, 2, StatusTickTiming.None, DispelType.Cleanse),
            StatusId.SpdDown  => new(StatusGroup.Debuff, 2, 3, StatusTickTiming.None, DispelType.Cleanse),
            StatusId.Blind    => new(StatusGroup.Debuff, 1, 2, StatusTickTiming.None, DispelType.Cleanse),
            StatusId.Curse    => new(StatusGroup.Debuff, 1, 3, StatusTickTiming.None, DispelType.Cleanse),

            // Buff
            StatusId.AtkUp    => new(StatusGroup.Buff, 2, 3, StatusTickTiming.None, DispelType.Dispel),
            StatusId.DefUp    => new(StatusGroup.Buff, 2, 3, StatusTickTiming.None, DispelType.Dispel),
            StatusId.SpdUp    => new(StatusGroup.Buff, 2, 3, StatusTickTiming.None, DispelType.Dispel),
            StatusId.Shield   => new(StatusGroup.Buff, 1, 3, StatusTickTiming.None, DispelType.Dispel),
            StatusId.Regen    => new(StatusGroup.Buff, 2, 3, StatusTickTiming.TurnStart, DispelType.Dispel),
            StatusId.Immunity => new(StatusGroup.Buff, 1, 2, StatusTickTiming.None, DispelType.Dispel),
            StatusId.Counter  => new(StatusGroup.Buff, 1, 2, StatusTickTiming.None, DispelType.Dispel),
            StatusId.Reflect  => new(StatusGroup.Buff, 1, 2, StatusTickTiming.None, DispelType.Dispel),
            // task-tactic-row.md — Focus: Buff, duration 2 (survives FinishTurn of Guard turn → active next turn)
            StatusId.Focus    => new(StatusGroup.Buff, 1, 2, StatusTickTiming.None, DispelType.Dispel),

            _ => new(StatusGroup.Debuff, 1, 1, StatusTickTiming.None, DispelType.None)
        };

        public static bool IsBuff(StatusId id) => Get(id).Group == StatusGroup.Buff;
        public static bool IsDebuff(StatusId id) => Get(id).Group != StatusGroup.Buff;
        public static bool BlocksAction(StatusId id) => Get(id).BlocksAction;

        // ===== Hằng số hiệu ứng (plan.md §4.11) =====
        public const float BURN_HP_PCT_PER_STACK   = 0.05f;  // % MaxHP
        public const float BURN_DEF_DOWN           = 0.20f;
        public const float POISON_HP_PCT_PER_STACK = 0.08f;  // % HP hiện tại
        public const float BLEED_ATK_RATIO         = 0.50f;  // × ATK nguồn
        public const float REGEN_HP_PCT_PER_STACK  = 0.06f;
        public const float FREEZE_DMG_TAKEN_BONUS  = 0.30f;
        public const float ATK_MOD_PER_STACK       = 0.25f;
        public const float DEF_MOD_PER_STACK       = 0.25f;
        public const float SPD_MOD_PER_STACK       = 0.30f;
        public const float BLIND_ACC_DOWN          = 0.40f;
        public const float COUNTER_ATK_RATIO       = 0.60f;
        public const float REFLECT_RATIO           = 0.30f;
        public const float PARALYZE_SKIP_CHANCE    = 0.50f;
    }
}
