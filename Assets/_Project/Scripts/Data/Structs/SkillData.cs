using System;

namespace Game.Data
{
    /// <summary>Một hiệu ứng trạng thái mà skill áp dụng.</summary>
    [Serializable]
    public struct StatusApplication
    {
        public StatusId Status;
        public float Chance;       // 0..1, trước khi trừ RES
        public int Duration;       // 0 = dùng mặc định của StatusTable
        public int Stacks;
        public bool TargetSelf;    // true = áp lên chính người dùng skill
        /// <summary>task-setbonus.md — bộ Breaker (Break mục tiêu → CẢ ĐỘI +ATK). true = áp lên
        /// TOÀN BỘ đồng minh còn sống cùng phe owner (bao gồm owner), bỏ qua TargetSelf/contextTarget.
        /// Chỉ PassiveProcessor.Apply đọc field này — SkillData.Applies (đòn skill thường) không
        /// dùng, mặc định false không đổi hành vi nào hiện có.</summary>
        public bool TargetAllAllies;

        public StatusApplication(StatusId status, float chance, int duration = 0, int stacks = 1, bool targetSelf = false)
        {
            Status = status; Chance = chance; Duration = duration; Stacks = stacks; TargetSelf = targetSelf;
            TargetAllAllies = false;
        }
    }

    /// <summary>
    /// Dữ liệu skill THUẦN (không ScriptableObject) để Game.Combat chạy headless được.
    /// SkillDefinition (SO) chỉ bọc struct này lại — plan.md §11.1.
    /// </summary>
    [Serializable]
    public class SkillData
    {
        public string Id = "skill_unknown";
        public string NameKey = "";
        public string DescKey = "";

        public SkillType Type = SkillType.Physical;
        public DamageType DamageType = DamageType.Physical;
        public Element Element = Element.Neutral;
        public TargetMode Target = TargetMode.SingleEnemy;

        public int SpCost;
        public int Cooldown;
        public int ExtraAtbCost;

        public float PowerMultiplier = 1f;
        public float FlatDamage;
        public float DefIgnore;          // 0..1
        public int HitCount = 1;
        public int RandomTargetCount = 1; // dùng khi Target = RandomEnemy

        public int PoiseDamage = 3;
        public bool IsBreaker;
        public bool IsAoe;

        public ActionCommandType CommandType = ActionCommandType.SingleTap;
        public int ComboBeats = 1;

        /// <summary>Hồi máu = HealPower × ATK của người dùng (dùng cho SkillType.Heal).</summary>
        public float HealPower;
        /// <summary>Shield = ShieldPower × DEF của người dùng.</summary>
        public float ShieldPower;
        /// <summary>Hồi sinh với % MaxHP (Target = DeadAlly).</summary>
        public float RevivePercent;
        /// <summary>Hút máu riêng của skill, cộng thêm vào Lifesteal của unit.</summary>
        public float LifestealBonus;
        /// <summary>Số SP hồi cho mục tiêu (Mana Well).</summary>
        public int SpRestore;

        public StatusApplication[] Applies = Array.Empty<StatusApplication>();

        /// <summary>Xoá bao nhiêu debuff của đồng minh (Cleanse). 0 = không.</summary>
        public int CleanseCount;
        /// <summary>Xoá bao nhiêu buff của địch (Dispel/Steal). 0 = không.</summary>
        public int DispelCount;
        /// <summary>True = buff bị dispel sẽ chuyển sang người dùng (Pilfer).</summary>
        public bool StealsDispelled;

        public string SummonId = "";
        public int SummonCount;
        public int SummonDuration = 3;

        public string VfxKey = "";
        public string SfxKey = "";
        public string AnimTrigger = "attack";
        public string IconKey = "";

        public bool DealsDamage =>
            (Type == SkillType.Physical || Type == SkillType.Magical) && PowerMultiplier > 0f;

        public bool TargetsAllies =>
            Target is TargetMode.SingleAlly or TargetMode.AllAllies or TargetMode.LowestHpAlly
                   or TargetMode.Self or TargetMode.DeadAlly;

        public SkillData Clone() => (SkillData)MemberwiseClone();
    }
}
