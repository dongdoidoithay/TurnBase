using System.Collections.Generic;
using Game.Core.Maths;
using Game.Data;

namespace Game.Combat.Model
{
    /// <summary>
    /// Một unit trong trận (hero, enemy, boss, minion). C# THUẦN — không Unity API.
    /// Stat cuối cùng được cache và tính lại khi MarkStatsDirty() được gọi.
    /// </summary>
    public sealed class CombatUnit
    {
        // ===== Định danh =====
        public int Id;
        public string DefId = "";
        public string DisplayNameKey = "";
        public TeamSide Side;
        public int SlotIndex;
        public Row Row;
        public bool IsBoss;
        public bool IsMinion;
        public int OwnerId = -1;           // minion: id của chủ
        public int MinionTurnsLeft;

        public HeroClass Class;
        public Element Element;
        public int Level = 1;

        // ===== Tài nguyên =====
        public int Hp;
        public int Sp;
        public int Atb;
        public int Poise;
        public int PoiseMax = 30;
        public int BrokenTurnsLeft;
        public int PoiseRecoverDelay;

        public bool IsDead => Hp <= 0;
        public bool IsAlive => Hp > 0;
        public bool IsBroken => BrokenTurnsLeft > 0;

        /// <summary>Hero gục quá số lượt cho phép → không hồi sinh được nữa (edge case E11).</summary>
        public int TurnsDown;
        public bool PermanentlyDown;

        // ===== Dữ liệu gốc =====
        public PrimaryStats BasePrimary;
        public readonly List<StatModifier> EquipmentModifiers = new();
        /// <summary>Modifier do PassiveProcessor bơm vào khi 1 passive/Awakening kích hoạt
        /// (vd stat vĩnh viễn từ OnBattleStart) — tách khỏi EquipmentModifiers để không lẫn nguồn.</summary>
        public readonly List<StatModifier> PassiveModifiers = new();
        public readonly List<StatusInstance> Statuses = new();
        public readonly List<SkillRuntime> Skills = new();
        public PassiveData Passive;
        public PassiveData Awakening;
        /// <summary>Bonus 4-món của bộ trang bị đang mặc đủ (task-setbonus.md) — 1 slot đơn, không
        /// phải list, vì 1 hero chỉ có 6 slot trang bị nên không thể mặc đủ 4 món của 2 bộ khác
        /// nhau cùng lúc (cần tối thiểu 8 slot). null nếu không đủ 4 món bộ nào.</summary>
        public PassiveData SetBonus;
        /// <summary>task-setbonus.md §1.2 — bộ Tempest: true CHỈ cho đúng 1 unit/round, unit đầu
        /// tiên thật sự hành động (không tính unit bị skip do chết/stun/paralyze). Set bởi
        /// CombatSimulation.BeginTurn(), reset bởi BeginRound(). Đọc trực tiếp trong
        /// DamageCalculator.Calculate — không cần đổi chữ ký nhận BattleState.</summary>
        public bool IsFirstActorThisRound;
        /// <summary>task-setbonus.md §1.3 — khác IsFirstActorThisRound: tính CẢ turn bị skip
        /// (chết/stun/paralyze) là "đã qua lượt round này", để 1 unit bị khoá hành động vĩnh viễn
        /// không chặn round kết thúc mãi mãi. Dùng để CombatSimulation biết khi nào hết round
        /// (mọi unit còn sống đều đã HasActedThisRound=true) và tăng State.RoundNumber thật.</summary>
        public bool HasActedThisRound;
        /// <summary>task-tactic-row.md — đã dùng SwapRow lượt này; nút SWAP bị disable. Reset bởi BeginTurn().</summary>
        public bool HasSwappedRowThisTurn;

        // ===== Cache stat =====
        private DerivedStats _cached;
        private bool _dirty = true;

        public void MarkStatsDirty() => _dirty = true;

        public DerivedStats Stats
        {
            get
            {
                if (_dirty) { _cached = ComputeStats(); _dirty = false; }
                return _cached;
            }
        }

        public int MaxHp => Stats.MaxHp;
        public int MaxSp => Stats.MaxSp;
        public float SpdEffective => GameMath.Max(Stats.Spd, BalanceCaps.SPD_MIN);

        // =====================================================================
        // Tính stat — thứ tự áp dụng modifier theo plan.md §4.5:
        // Base → +Flat(trang bị) → ×(1+Σ%trang bị) → ×(1+Σ%buff) → ×(1−Σ%debuff) → Clamp
        // =====================================================================
        private DerivedStats ComputeStats()
        {
            var d = DerivedStats.FromPrimary(BasePrimary);

            // --- Bước 1: trang bị + passive (flat rồi percent) ---
            float hpPct = 0f, spPct = 0f, atkPct = 0f, defPct = 0f, spdPct = 0f;

            void Accumulate(List<StatModifier> mods)
            {
                for (int i = 0; i < mods.Count; i++)
                {
                    var m = mods[i];
                    switch (m.Stat)
                    {
                        case StatType.MaxHp:        d.MaxHp += GameMath.RoundToInt(m.Value); break;
                        case StatType.MaxSp:        d.MaxSp += GameMath.RoundToInt(m.Value); break;
                        case StatType.Atk:          d.AtkPhys += m.Value; d.AtkMag += m.Value; break;
                        case StatType.Def:          d.Def += m.Value; break;
                        case StatType.Spd:          d.Spd += m.Value; break;
                        case StatType.Acc:          d.Acc += m.Value; break;
                        case StatType.Eva:          d.Eva += m.Value; break;
                        case StatType.Res:          d.Res += m.Value / 100f; break;
                        case StatType.EffAcc:       d.EffAcc += m.Value / 100f; break;
                        case StatType.MaxHpPct:     hpPct += m.Value / 100f; break;
                        case StatType.MaxSpPct:     spPct += m.Value / 100f; break;
                        case StatType.AtkPct:       atkPct += m.Value / 100f; break;
                        case StatType.DefPct:       defPct += m.Value / 100f; break;
                        case StatType.SpdPct:       spdPct += m.Value / 100f; break;
                        case StatType.CritPct:      d.Crit += m.Value / 100f; break;
                        case StatType.CritDmgPct:   d.CritDmg += m.Value / 100f; break;
                        case StatType.LifestealPct: d.Lifesteal += m.Value / 100f; break;
                        case StatType.DmgBonusPct:  d.DmgBonus += m.Value / 100f; break;
                        case StatType.DmgReductPct: d.DmgReduct += m.Value / 100f; break;
                        case StatType.PoiseDmgPct:  d.PoiseDmgBonus += m.Value / 100f; break;
                    }
                }
            }

            Accumulate(EquipmentModifiers);
            Accumulate(PassiveModifiers);
            d.MaxHp = GameMath.RoundToInt(d.MaxHp * (1f + hpPct));
            d.MaxSp = GameMath.RoundToInt(d.MaxSp * (1f + spPct));
            d.AtkPhys *= 1f + atkPct;
            d.AtkMag *= 1f + atkPct;
            d.Def *= 1f + defPct;
            d.Spd *= 1f + spdPct;

            // --- Bước 2: buff/debuff (cộng % theo stack, KHÔNG nhân dồn) ---
            float atkMod = 0f, defMod = 0f, spdMod = 0f, accMod = 0f;

            for (int i = 0; i < Statuses.Count; i++)
            {
                var s = Statuses[i];
                switch (s.Id)
                {
                    case StatusId.AtkUp:   atkMod += StatusTable.ATK_MOD_PER_STACK * s.Stacks; break;
                    case StatusId.AtkDown: atkMod -= StatusTable.ATK_MOD_PER_STACK * s.Stacks; break;
                    case StatusId.DefUp:   defMod += StatusTable.DEF_MOD_PER_STACK * s.Stacks; break;
                    case StatusId.DefDown: defMod -= StatusTable.DEF_MOD_PER_STACK * s.Stacks; break;
                    case StatusId.SpdUp:   spdMod += StatusTable.SPD_MOD_PER_STACK * s.Stacks; break;
                    case StatusId.SpdDown: spdMod -= StatusTable.SPD_MOD_PER_STACK * s.Stacks; break;
                    case StatusId.Blind:   accMod -= StatusTable.BLIND_ACC_DOWN; break;
                    case StatusId.Burn:    defMod -= StatusTable.BURN_DEF_DOWN; break; // Burn giảm DEF
                }
            }

            d.AtkPhys *= GameMath.Max(0.1f, 1f + atkMod);
            d.AtkMag  *= GameMath.Max(0.1f, 1f + atkMod);
            d.Def     *= GameMath.Max(0f,   1f + defMod);
            d.Spd     *= GameMath.Max(0.1f, 1f + spdMod);
            d.Acc     *= GameMath.Max(0.1f, 1f + accMod);

            // --- Bước 3: buff phòng thủ cộng thêm ---
            if (HasStatus(StatusId.DefUp)) d.DmgReduct += 0.10f;

            d.ClampToCaps();
            return d;
        }

        // ===== Truy vấn status =====
        public bool HasStatus(StatusId id)
        {
            for (int i = 0; i < Statuses.Count; i++)
                if (Statuses[i].Id == id) return true;
            return false;
        }

        public StatusInstance GetStatus(StatusId id)
        {
            for (int i = 0; i < Statuses.Count; i++)
                if (Statuses[i].Id == id) return Statuses[i];
            return null;
        }

        public int StatusStacks(StatusId id) => GetStatus(id)?.Stacks ?? 0;

        /// <summary>Bị Stun/Freeze/Sleep → mất lượt hoàn toàn (pipeline bước 5).</summary>
        public bool IsActionBlocked()
        {
            for (int i = 0; i < Statuses.Count; i++)
                if (StatusTable.BlocksAction(Statuses[i].Id)) return true;
            return false;
        }

        public bool IsSilenced() => HasStatus(StatusId.Silence);

        // ===== Skill =====
        public SkillRuntime GetSkill(int index)
            => index >= 0 && index < Skills.Count ? Skills[index] : null;

        public SkillRuntime FindSkill(string skillId)
        {
            for (int i = 0; i < Skills.Count; i++)
                if (Skills[i].Data.Id == skillId) return Skills[i];
            return null;
        }

        /// <summary>Skill dùng được không: đủ SP, hết CD, không bị Silence (trừ đánh thường), và
        /// nếu là ô Ultimate (slot 4) của hero người chơi thì gauge chung phải đầy —
        /// <paramref name="ultimateReady"/> mặc định true để không phá caller cũ/test cũ chưa
        /// truyền vào (chỉ có ý nghĩa khi thật sự là Ultimate của phe Player).</summary>
        public bool CanUseSkill(SkillRuntime skill, bool ultimateReady = true)
        {
            if (skill == null) return false;
            if (skill.CooldownLeft > 0) return false;
            if (skill.Data.SpCost > Sp) return false;
            if (IsSilenced() && skill.SlotIndex != 0) return false;
            if (skill.SlotIndex == 4 && Side == TeamSide.Player && !ultimateReady) return false;
            return true;
        }

        // ===== Thay đổi tài nguyên =====
        public void SetHp(int value) => Hp = GameMath.Clamp(value, 0, MaxHp);
        public void SetSp(int value) => Sp = GameMath.Clamp(value, 0, MaxSp);
        public void AddSp(int delta) => SetSp(Sp + delta);

        public void FillResources()
        {
            MarkStatsDirty();
            Hp = MaxHp;
            Sp = MaxSp;
            Poise = PoiseMax;
            Atb = 0;
        }

        public override string ToString()
            => $"#{Id} {DefId} [{Side}/{Row}] HP {Hp}/{MaxHp} SP {Sp} ATB {Atb} Poise {Poise}";
    }

    /// <summary>Trạng thái runtime của 1 skill trên 1 unit.</summary>
    public sealed class SkillRuntime
    {
        public SkillData Data;
        public int SlotIndex;
        public int Level = 1;
        public int CooldownLeft;

        public SkillRuntime(SkillData data, int slotIndex, int level = 1)
        {
            Data = data; SlotIndex = slotIndex; Level = level;
        }

        /// <summary>Skill level tăng sức mạnh: +6% power mỗi cấp sau cấp 1.</summary>
        public float EffectivePower => Data.PowerMultiplier * (1f + 0.06f * (Level - 1));
    }

    /// <summary>Modifier stat từ trang bị / set bonus / synergy.</summary>
    public readonly struct StatModifier
    {
        public readonly StatType Stat;
        public readonly float Value;
        public readonly string Source;

        public StatModifier(StatType stat, float value, string source = "")
        {
            Stat = stat; Value = value; Source = source;
        }
    }

    /// <summary>Passive thuần dữ liệu.</summary>
    public sealed class PassiveData
    {
        public string Id = "";
        public PassiveTrigger Trigger = PassiveTrigger.None;
        public float Threshold;          // dùng cho OnHpBelowThreshold (0..1)
        public StatModifier[] Modifiers = System.Array.Empty<StatModifier>();
        public StatusApplication[] Applies = System.Array.Empty<StatusApplication>();
        public float ExtraDamagePercent;
        /// <summary>task-setbonus.md — bộ Assassin: chỉ nổ khi đòn OnHitDealt là Crit (mọi passive
        /// OnHitDealt khác trong catalog không set field này, không đổi hành vi).</summary>
        public bool RequiresCrit;
        /// <summary>task-setbonus.md — bộ Ember: chỉ nổ khi đòn OnHitDealt được bấm ở CommandGrade
        /// Perfect (OnPerfectCommand không có contextTarget là địch nên không dùng được cho hiệu
        /// ứng cần áp lên địch).</summary>
        public bool RequiresPerfectGrade;
        /// <summary>task-setbonus.md §1.2 — bộ Tempest: chỉ cộng <see cref="ExtraDamagePercent"/>
        /// (qua DamageCalculator, đọc CombatUnit.IsFirstActorThisRound trực tiếp) khi actor là
        /// người hành động đầu tiên trong round hiện tại.</summary>
        public bool RequiresFirstActionOfRound;
        /// <summary>task-setbonus.md — bộ Sage: hoàn % SP cost của skill vừa dùng khi Perfect.
        /// Chỉ có ý nghĩa với Trigger == OnPerfectCommand.</summary>
        public float SpRefundPercent;
        /// <summary>task-setbonus.md — bộ Vampire (OnKill)/Guardian (OnTurnEnd): hồi % MaxHP NGAY
        /// LẬP TỨC cho owner khi trigger nổ (khác Applies/StatusApplication chỉ áp status, không
        /// heal tức thời).</summary>
        public float HealPercentMaxHp;
        /// <summary>task-setbonus.md — bộ Bastion (OnHpBelowThreshold): shield = % MaxHP. KHÔNG đi
        /// qua Applies/StatusApplication vì <c>StatusProcessor.ComputeStatusValue</c> (đường
        /// chung) trả 0 cho Shield — phải gọi thẳng <c>StatusProcessor.ApplyShield</c> với amount
        /// tính sẵn.</summary>
        public float ShieldPercentMaxHp;
        public bool Consumed;            // passive 1 lần/trận
    }
}
