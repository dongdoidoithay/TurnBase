using System;
using System.Collections.Generic;
using Game.Combat.Model;
using Game.Combat.Systems;
using Game.Core.Random;
using Game.Data;

namespace Game.Combat.Ai
{
    public enum AIConditionType
    {
        Always = 0,
        SelfHpBelow = 1,
        AllyHpBelow = 2,
        AllyCountAlive = 3,
        EnemyCountAlive = 4,
        SelfHasStatus = 5,
        EnemyHasStatus = 6,
        RoundAtLeast = 7,
        SelfSpAbove = 8,
        TargetIsBroken = 9,
        PhaseIs = 10,
        SelfHpAbove = 11
    }

    [Serializable]
    public struct AICondition
    {
        public AIConditionType Type;
        public float Value;
        public StatusId Status;

        public AICondition(AIConditionType type, float value = 0f, StatusId status = StatusId.None)
        {
            Type = type; Value = value; Status = status;
        }

        public bool Evaluate(BattleState state, CombatUnit self, int phase)
        {
            switch (Type)
            {
                case AIConditionType.Always: return true;
                case AIConditionType.SelfHpBelow: return HpPct(self) < Value;
                case AIConditionType.SelfHpAbove: return HpPct(self) > Value;
                case AIConditionType.SelfHasStatus: return self.HasStatus(Status);
                case AIConditionType.SelfSpAbove: return self.Sp > Value;
                case AIConditionType.RoundAtLeast: return state.RoundNumber >= Value;
                case AIConditionType.PhaseIs: return Math.Abs(phase - Value) < 0.01f;

                case AIConditionType.AllyHpBelow:
                    for (int i = 0; i < state.Units.Count; i++)
                    {
                        var u = state.Units[i];
                        if (u.Side == self.Side && u.IsAlive && HpPct(u) < Value) return true;
                    }
                    return false;

                case AIConditionType.AllyCountAlive:
                    return state.CountAlive(self.Side) >= (int)Value;

                case AIConditionType.EnemyCountAlive:
                    return state.CountAlive(state.Opposite(self.Side)) >= (int)Value;

                case AIConditionType.EnemyHasStatus:
                    for (int i = 0; i < state.Units.Count; i++)
                    {
                        var u = state.Units[i];
                        if (u.Side != self.Side && u.IsAlive && u.HasStatus(Status)) return true;
                    }
                    return false;

                case AIConditionType.TargetIsBroken:
                    for (int i = 0; i < state.Units.Count; i++)
                    {
                        var u = state.Units[i];
                        if (u.Side != self.Side && u.IsAlive && u.IsBroken) return true;
                    }
                    return false;

                default: return false;
            }

            static float HpPct(CombatUnit u) => u.MaxHp > 0 ? (float)u.Hp / u.MaxHp * 100f : 0f;
        }
    }

    [Serializable]
    public sealed class AIRule
    {
        public AICondition When;
        public string SkillId = "";
        public int SkillSlot = -1;    // dùng khi SkillId rỗng
        public float Weight = 50f;
        public int RuleCooldown;      // số lượt không lặp lại rule này

        [NonSerialized] public int CooldownLeft;
    }

    [Serializable]
    public sealed class AIProfile
    {
        public string Id = "ai_default";
        public List<AIRule> Rules = new();
        /// <summary>Nhiễu ngẫu nhiên ±N điểm để không đoán trước 100% (plan.md §4.13.1).</summary>
        public float Noise = 10f;
    }

    /// <summary>Utility AI — plan.md §4.13. Chấm điểm rule, chọn cao nhất.</summary>
    public sealed class AIController
    {
        private readonly BattleState _state;
        private readonly TargetSelector _targeting;

        public AIController(BattleState state, TargetSelector targeting)
        {
            _state = state; _targeting = targeting;
        }

        public readonly struct Decision
        {
            public readonly SkillRuntime Skill;
            public readonly int TargetId;
            public Decision(SkillRuntime skill, int targetId) { Skill = skill; TargetId = targetId; }
            public bool IsValid => Skill != null;
        }

        /// <summary>
        /// Chọn hành động. <paramref name="peek"/>=true thì dùng RNG fork (không tiêu seed) —
        /// dùng cho Intent Preview.
        /// </summary>
        public Decision Choose(CombatUnit self, AIProfile profile, int phase,
                               IRandomSource rng, bool peek = false)
        {
            var r = peek ? rng.Fork() : rng;

            if (profile == null || profile.Rules.Count == 0)
                return FallbackBasicAttack(self);

            SkillRuntime bestSkill = null;
            float bestScore = float.MinValue;

            for (int i = 0; i < profile.Rules.Count; i++)
            {
                var rule = profile.Rules[i];
                if (rule.CooldownLeft > 0) continue;
                if (!rule.When.Evaluate(_state, self, phase)) continue;

                var skill = ResolveSkill(self, rule);
                // Edge case E19: chỉ chọn skill thật sự dùng được
                if (skill == null || !self.CanUseSkill(skill, _state.IsUltimateReady)) continue;

                float score = rule.Weight + r.NextFloat(-profile.Noise, profile.Noise);
                if (score > bestScore) { bestScore = score; bestSkill = skill; }
            }

            if (bestSkill == null) return FallbackBasicAttack(self);

            if (!peek)
            {
                // Đặt cooldown cho rule đã dùng
                for (int i = 0; i < profile.Rules.Count; i++)
                {
                    var rule = profile.Rules[i];
                    if (ResolveSkill(self, rule) == bestSkill && rule.RuleCooldown > 0)
                        rule.CooldownLeft = rule.RuleCooldown;
                }
            }

            var target = PickTarget(self, bestSkill);
            return new Decision(bestSkill, target?.Id ?? -1);
        }

        private SkillRuntime ResolveSkill(CombatUnit self, AIRule rule)
        {
            if (!string.IsNullOrEmpty(rule.SkillId)) return self.FindSkill(rule.SkillId);
            if (rule.SkillSlot >= 0) return self.GetSkill(rule.SkillSlot);
            return null;
        }

        private Decision FallbackBasicAttack(CombatUnit self)
        {
            // Tìm skill khả dụng bất kỳ, ưu tiên ô 0 (đánh thường, không tốn SP)
            var basic = self.GetSkill(0);
            if (basic != null && self.CanUseSkill(basic, _state.IsUltimateReady))
                return new Decision(basic, PickTarget(self, basic)?.Id ?? -1);

            for (int i = 0; i < self.Skills.Count; i++)
            {
                if (!self.CanUseSkill(self.Skills[i], _state.IsUltimateReady)) continue;
                return new Decision(self.Skills[i], PickTarget(self, self.Skills[i])?.Id ?? -1);
            }
            return default;
        }

        private CombatUnit PickTarget(CombatUnit self, SkillRuntime skill)
        {
            if (skill.Data.TargetsAllies)
            {
                // Hồi máu/hỗ trợ → chọn đồng minh HP% thấp nhất
                CombatUnit best = null;
                float bestPct = float.MaxValue;
                for (int i = 0; i < _state.Units.Count; i++)
                {
                    var u = _state.Units[i];
                    if (u.Side != self.Side || !u.IsAlive) continue;
                    float pct = (float)u.Hp / u.MaxHp;
                    if (pct < bestPct) { bestPct = pct; best = u; }
                }
                return best;
            }
            return _targeting.AutoSuggest(self, _state.Opposite(self.Side));
        }

        public static void TickRuleCooldowns(AIProfile profile)
        {
            if (profile == null) return;
            for (int i = 0; i < profile.Rules.Count; i++)
                if (profile.Rules[i].CooldownLeft > 0) profile.Rules[i].CooldownLeft--;
        }
    }
}
