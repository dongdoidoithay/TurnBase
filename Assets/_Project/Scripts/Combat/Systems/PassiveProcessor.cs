using Game.Combat.Events;
using Game.Combat.Model;
using Game.Core.Maths;
using Game.Core.Random;
using Game.Data;

namespace Game.Combat.Systems
{
    /// <summary>
    /// Kích hoạt <see cref="CombatUnit.Passive"/>/<see cref="CombatUnit.Awakening"/> theo
    /// <see cref="PassiveTrigger"/> — sibling của StatusProcessor/PoiseSystem, gọi trực tiếp
    /// (không qua CombatEventQueue vì hàng đợi đó chỉ là log 1 chiều cho Presenter).
    /// task-ascend.md §7 mục A — thêm trigger mới thì cập nhật cả đây lẫn object-map.md §7.2.
    /// </summary>
    public sealed class PassiveProcessor
    {
        private readonly BattleState _state;
        private readonly CombatEventQueue _events;
        private readonly StatusProcessor _status;

        public PassiveProcessor(BattleState state, CombatEventQueue events, StatusProcessor status)
        {
            _state = state; _events = events; _status = status;
        }

        // =====================================================================
        // ĐIỂM VÀO — 1 method / trigger có hook point tự nhiên trong pipeline
        // =====================================================================

        public void TriggerBattleStart(IRandomSource rng)
        {
            for (int i = 0; i < _state.Units.Count; i++)
                Fire(_state.Units[i], _state.Units[i], PassiveTrigger.OnBattleStart, rng);
        }

        public void TriggerTurnStart(CombatUnit actor, IRandomSource rng)
            => Fire(actor, actor, PassiveTrigger.OnTurnStart, rng);

        /// <summary>task-setbonus.md — <paramref name="spCost"/> = SP vừa trừ cho skill này, chỉ
        /// dùng bởi bộ Sage (PassiveData.SpRefundPercent); mọi passive khác bỏ qua tham số này.</summary>
        public void TriggerOnPerfectCommand(CombatUnit actor, IRandomSource rng, int spCost = 0)
            => Fire(actor, actor, PassiveTrigger.OnPerfectCommand, rng, spCost);

        public void TriggerOnHealDone(CombatUnit actor, IRandomSource rng)
            => Fire(actor, actor, PassiveTrigger.OnHealDone, rng);

        /// <summary>actor vừa gây damage lên target — passive của actor, target là "đối tượng ngữ cảnh".
        /// <paramref name="isCrit"/> (bộ Assassin)/<paramref name="isPerfect"/> (bộ Ember) chỉ dùng
        /// bởi passive có <see cref="PassiveData.RequiresCrit"/>/<see
        /// cref="PassiveData.RequiresPerfectGrade"/> = true; mọi passive OnHitDealt khác bỏ qua.</summary>
        public void TriggerOnHitDealt(CombatUnit actor, CombatUnit target, IRandomSource rng,
                                      bool isCrit = false, bool isPerfect = false)
            => Fire(actor, target, PassiveTrigger.OnHitDealt, rng, isCrit: isCrit, isPerfect: isPerfect);

        /// <summary>target vừa nhận damage từ attacker — passive của target, attacker là "đối tượng ngữ cảnh".</summary>
        public void TriggerOnDamageTaken(CombatUnit target, CombatUnit attacker, IRandomSource rng)
            => Fire(target, attacker, PassiveTrigger.OnDamageTaken, rng);

        public void TriggerOnKill(CombatUnit killer, IRandomSource rng)
        {
            if (killer == null) return;
            Fire(killer, killer, PassiveTrigger.OnKill, rng);
        }

        /// <summary>Bắn OnAllyDeath cho mọi đồng minh còn sống của victim (không tính chính victim).</summary>
        public void TriggerOnAllyDeath(CombatUnit victim, IRandomSource rng)
        {
            for (int i = 0; i < _state.Units.Count; i++)
            {
                var ally = _state.Units[i];
                if (ally.Id == victim.Id || ally.Side != victim.Side || !ally.IsAlive) continue;
                Fire(ally, ally, PassiveTrigger.OnAllyDeath, rng);
            }
        }

        public void TriggerOnBreakTriggered(CombatUnit target, IRandomSource rng)
            => Fire(target, target, PassiveTrigger.OnBreakTriggered, rng);

        /// <summary>task-setbonus.md — bộ Guardian. Gọi từ CombatSimulation.FinishTurn().</summary>
        public void TriggerOnTurnEnd(CombatUnit actor, IRandomSource rng)
            => Fire(actor, actor, PassiveTrigger.OnTurnEnd, rng);

        /// <summary>Gọi sau mỗi lần HP của unit thay đổi — tự kiểm tra ngưỡng của Passive/Awakening.</summary>
        public void CheckHpThreshold(CombatUnit unit, IRandomSource rng)
        {
            if (!unit.IsAlive || unit.MaxHp <= 0) return;
            float ratio = (float)unit.Hp / unit.MaxHp;
            CheckThreshold(unit, unit.Passive, ratio, rng);
            CheckThreshold(unit, unit.Awakening, ratio, rng);
            CheckThreshold(unit, unit.SetBonus, ratio, rng);
        }

        /// <summary>Edge case E12 (plan.md §4.14): toàn đội người chơi gục nhưng có unit mang
        /// passive/Awakening <see cref="PassiveTrigger.OnTeamWipe"/> → hồi sinh unit đó TRƯỚC khi
        /// tính thua. Gọi từ CombatSimulation.CheckEnd() ngay khi phát hiện Defeat, TRƯỚC khi
        /// Finish(). Trả true nếu đã hồi sinh (caller phải re-evaluate kết quả trận).
        /// Không đi qua Fire/Apply — owner đang IsDead khi trigger này nổ (mọi trigger khác đều
        /// gate IsAlive), và hồi sinh là hành động HP/status riêng, không phải Modifier/Applies
        /// chung của Apply().</summary>
        public bool TryAutoRevive(IRandomSource rng)
        {
            for (int i = 0; i < _state.Units.Count; i++)
            {
                var unit = _state.Units[i];
                if (unit.Side != TeamSide.Player || unit.IsAlive || unit.PermanentlyDown) continue;
                if (TryReviveFromPassive(unit, unit.Passive, rng)) return true;
                if (TryReviveFromPassive(unit, unit.Awakening, rng)) return true;
            }
            return false;
        }

        private bool TryReviveFromPassive(CombatUnit unit, PassiveData passive, IRandomSource rng)
        {
            if (passive == null || passive.Consumed || passive.Trigger != PassiveTrigger.OnTeamWipe) return false;

            float reviveRatio = passive.Threshold > 0f ? passive.Threshold : 0.3f;
            _status.ClearAll(unit);
            unit.MarkStatsDirty();
            unit.SetHp(GameMath.Max(1, GameMath.RoundToInt(unit.MaxHp * reviveRatio)));
            unit.TurnsDown = 0;
            unit.Poise = unit.PoiseMax;
            passive.Consumed = true;
            _events.Emit(CombatEventType.UnitRevived, unit.Id, unit.Id, intValue: unit.Hp);

            if (passive.Applies.Length > 0)
                for (int i = 0; i < passive.Applies.Length; i++)
                    if (passive.Applies[i].TargetSelf) _status.Apply(unit, unit, passive.Applies[i], rng);

            return true;
        }

        private void CheckThreshold(CombatUnit owner, PassiveData passive, float hpRatio, IRandomSource rng)
        {
            if (passive == null || passive.Consumed) return;
            if (passive.Trigger != PassiveTrigger.OnHpBelowThreshold) return;
            if (hpRatio > passive.Threshold) return;
            Apply(owner, owner, passive, rng);
        }

        // =====================================================================
        // NỘI BỘ
        // =====================================================================

        private void Fire(CombatUnit owner, CombatUnit contextTarget, PassiveTrigger trigger,
                          IRandomSource rng, int spCost = 0, bool isCrit = false, bool isPerfect = false)
        {
            if (!owner.IsAlive) return;
            FireOne(owner, owner.Passive, contextTarget, trigger, rng, spCost, isCrit, isPerfect);
            FireOne(owner, owner.Awakening, contextTarget, trigger, rng, spCost, isCrit, isPerfect);
            FireOne(owner, owner.SetBonus, contextTarget, trigger, rng, spCost, isCrit, isPerfect);
        }

        private void FireOne(CombatUnit owner, PassiveData passive, CombatUnit contextTarget,
                             PassiveTrigger trigger, IRandomSource rng, int spCost = 0,
                             bool isCrit = false, bool isPerfect = false)
        {
            if (passive == null || passive.Trigger != trigger || passive.Consumed) return;
            // task-setbonus.md — bộ Assassin/Ember: chỉ nổ khi đòn vừa rồi khớp điều kiện riêng.
            if (passive.RequiresCrit && !isCrit) return;
            if (passive.RequiresPerfectGrade && !isPerfect) return;
            // OnHpBelowThreshold được xử lý riêng qua CheckHpThreshold (cần so HP mỗi lần đổi,
            // không phải 1 sự kiện rời rạc như các trigger khác).
            if (trigger == PassiveTrigger.OnHpBelowThreshold) return;
            Apply(owner, contextTarget, passive, rng, spCost);
        }

        private void Apply(CombatUnit owner, CombatUnit contextTarget, PassiveData passive,
                           IRandomSource rng, int spCost = 0)
        {
            bool grantsPermanentStat = passive.Modifiers.Length > 0;

            if (grantsPermanentStat)
            {
                for (int i = 0; i < passive.Modifiers.Length; i++)
                    owner.PassiveModifiers.Add(passive.Modifiers[i]);
                owner.MarkStatsDirty();
            }

            if (passive.Applies.Length > 0)
            {
                for (int i = 0; i < passive.Applies.Length; i++)
                {
                    var app = passive.Applies[i];
                    // task-setbonus.md — bộ Breaker: buff CẢ ĐỘI thay vì 1 mục tiêu đơn.
                    if (app.TargetAllAllies)
                    {
                        for (int u = 0; u < _state.Units.Count; u++)
                        {
                            var ally = _state.Units[u];
                            if (ally.Side == owner.Side && ally.IsAlive)
                                _status.Apply(owner, ally, app, rng);
                        }
                        continue;
                    }
                    // Trigger không có "đối tượng đối phương" rõ ràng (OnBattleStart/OnTurnStart/
                    // OnAllyDeath/OnHpBelowThreshold) chỉ hỗ trợ TargetSelf=true — bỏ qua nếu data
                    // sai cấu hình thay vì áp nhầm lên contextTarget (chính là owner ở các trigger đó).
                    var target = app.TargetSelf ? owner : contextTarget;
                    if (target != null) _status.Apply(owner, target, app, rng);
                }
            }

            // task-setbonus.md — bộ Sage: hoàn % SP cost của skill vừa dùng khi Perfect.
            if (passive.SpRefundPercent > 0f && spCost > 0)
                owner.AddSp(GameMath.RoundToInt(spCost * passive.SpRefundPercent / 100f));

            // task-setbonus.md — bộ Bastion (OnHpBelowThreshold): shield = %MaxHP. KHÔNG qua
            // Applies[] vì StatusProcessor.ComputeStatusValue trả 0 cho Shield ở đường chung —
            // gọi thẳng ApplyShield với amount tính sẵn.
            if (passive.ShieldPercentMaxHp > 0f)
                _status.ApplyShield(owner, owner, owner.MaxHp * passive.ShieldPercentMaxHp / 100f,
                                     duration: 3, rng);

            // task-setbonus.md — bộ Vampire (OnKill)/Guardian (OnTurnEnd): hồi % MaxHP tức thời.
            // Cùng pattern ActionResolver.ResolveHeal (SetHp cắt tại MaxHp, actual = hiệu số).
            if (passive.HealPercentMaxHp > 0f)
            {
                int before = owner.Hp;
                owner.SetHp(owner.Hp + GameMath.RoundToInt(owner.MaxHp * passive.HealPercentMaxHp / 100f));
                int actual = owner.Hp - before;
                if (actual > 0)
                    _events.Emit(CombatEventType.HealApplied, owner.Id, owner.Id,
                                 intValue: actual, intValue2: owner.Hp);
            }

            // ExtraDamagePercent: đã nối vào DamageCalculator (task-extra-damage.md) — nhưng đọc
            // TRỰC TIẾP từ DamageCalculator.Calculate (attacker.Passive/.Awakening), KHÔNG qua
            // PassiveProcessor ở đây, vì Calculate chạy TRƯỚC khi ActionResolver gọi
            // TriggerOnHitDealt (damage đã tính xong lúc trigger nổ, sửa lại là quá muộn).

            // "Dùng 1 lần/trận" (Consumed) CHỈ áp cho 2 dạng: cấp stat vĩnh viễn (grantsPermanentStat
            // — tránh cộng dồn nếu lỡ trigger lại) và OnHpBelowThreshold (đúng nghĩa gốc field —
            // ngưỡng HP là 1 lần cảnh báo/cứu nguy, không phải proc mỗi lượt HP còn thấp). Các
            // trigger proc lặp lại (OnKill, OnHitDealt, OnHealDone, OnPerfectCommand, OnAllyDeath,
            // OnBreakTriggered) CỐ Ý không khoá — đó chính là cơ chế snowball của 6 Awakening V1.
            if (grantsPermanentStat || passive.Trigger == PassiveTrigger.OnHpBelowThreshold)
                passive.Consumed = true;
        }
    }
}
