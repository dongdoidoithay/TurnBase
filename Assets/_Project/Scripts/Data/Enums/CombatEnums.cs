namespace Game.Data
{
    /// <summary>Nguyên tố — bảng khắc chế ở plan.md §4.7.</summary>
    public enum Element { Neutral = 0, Fire = 1, Water = 2, Earth = 3, Wind = 4, Light = 5, Dark = 6 }

    /// <summary>6 lớp nhân vật — plan.md §5.1.</summary>
    public enum HeroClass { Vanguard = 0, Slayer = 1, Arcanist = 2, Warden = 3, Trickster = 4, Summoner = 5 }

    public enum Rarity { Common = 0, Rare = 1, Epic = 2, Legendary = 3, Mythic = 4 }

    public enum DamageType { Physical = 0, Magical = 1, True = 2 }

    public enum SkillType { Physical = 0, Magical = 1, Heal = 2, Support = 3, Summon = 4, Tactic = 5 }

    /// <summary>plan.md §4.12 — 11 chế độ nhắm mục tiêu.</summary>
    public enum TargetMode
    {
        SingleEnemy = 0,
        AllEnemies = 1,
        RandomEnemy = 2,
        FrontRowEnemies = 3,
        BackRowEnemies = 4,
        LowestHpEnemy = 5,
        SingleAlly = 6,
        AllAllies = 7,
        LowestHpAlly = 8,
        Self = 9,
        DeadAlly = 10
    }

    /// <summary>plan.md §4.8 — 4 loại cửa sổ bấm nhịp.</summary>
    public enum ActionCommandType { None = 0, SingleTap = 1, Combo = 2, Charge = 3, Guard = 4 }

    /// <summary>Kết quả Action Command.</summary>
    public enum CommandGrade { Miss = 0, Good = 1, Perfect = 2 }

    /// <summary>Hàng đội hình — hàng sau nhận 70% sát thương vật lý đơn mục tiêu.</summary>
    public enum Row { Front = 0, Back = 1 }

    public enum TeamSide { Player = 0, Enemy = 1 }

    /// <summary>plan.md §4.11 — 22 trạng thái.</summary>
    public enum StatusId
    {
        None = 0,
        // DoT
        Burn = 1, Poison = 2, Bleed = 3,
        // Control
        Stun = 10, Freeze = 11, Paralyze = 12, Silence = 13, Taunt = 14, Sleep = 15,
        // Debuff
        AtkDown = 20, DefDown = 21, SpdDown = 22, Blind = 23, Curse = 24,
        // Buff
        AtkUp = 30, DefUp = 31, SpdUp = 32, Shield = 33, Regen = 34,
        Immunity = 35, Counter = 36, Reflect = 37,
        Focus = 38
    }

    public enum StatusGroup { Dot = 0, Control = 1, Debuff = 2, Buff = 3 }

    /// <summary>Buff bị Dispel, debuff bị Cleanse.</summary>
    public enum DispelType { None = 0, Cleanse = 1, Dispel = 2 }

    /// <summary>Thời điểm tick trong pipeline lượt (plan.md §4.3).</summary>
    public enum StatusTickTiming { None = 0, TurnStart = 1, TurnEnd = 2 }

    public enum BattleResult { InProgress = 0, Victory = 1, Defeat = 2, Escaped = 3, Timeout = 4 }

    public enum ArchetypeId
    {
        Grunt = 0, Brute = 1, Skirmisher = 2, Archer = 3, Healer = 4, Buffer = 5,
        Debuffer = 6, Caster = 7, Tank = 8, Swarm = 9, Bomber = 10, Elite = 11, Boss = 12
    }

    /// <summary>Trigger của passive — thêm giá trị mới thì phải cập nhật PassiveProcessor (object-map.md §7.2 bước 5).</summary>
    public enum PassiveTrigger
    {
        None = 0,
        OnBattleStart = 1,
        OnTurnStart = 2,
        OnHitDealt = 3,
        OnDamageTaken = 4,
        OnKill = 5,
        OnAllyDeath = 6,
        OnHpBelowThreshold = 7,
        OnPerfectCommand = 8,
        OnHealDone = 9,
        OnBreakTriggered = 10,
        /// <summary>Toàn đội người chơi gục — edge case E12 (plan.md §4.14). Threshold = % MaxHp
        /// hồi lại (mặc định 0.3 nếu không set). Khác mọi trigger khác: owner ĐANG CHẾT khi
        /// trigger nổ (PassiveProcessor.Fire bình thường gate IsAlive nên có method riêng
        /// TryAutoRevive, không đi qua Fire/Apply).</summary>
        OnTeamWipe = 11,
        /// <summary>task-setbonus.md — bộ Guardian (hồi 8% MaxHP mỗi khi kết thúc lượt). Bắn từ
        /// CombatSimulation.FinishTurn(), SAU StatusProcessor/PoiseSystem.TickTurnEnd (đúng thứ tự
        /// "kết thúc lượt" nhất quán với 2 hệ đó).</summary>
        OnTurnEnd = 12
    }
}
