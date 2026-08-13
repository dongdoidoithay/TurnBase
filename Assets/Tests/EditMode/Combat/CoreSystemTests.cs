using System.Collections.Generic;
using Game.Combat;
using Game.Combat.Model;
using Game.Combat.Systems;
using Game.Core.Random;
using Game.Data;
using NUnit.Framework;

namespace Game.Tests.Combat
{
    /// <summary>Bảng nguyên tố 6×6 — plan.md §4.7.</summary>
    public class ElementTableTests
    {
        [Test]
        public void Cycle_FireBeatsWind_WindBeatsEarth_EarthBeatsWater_WaterBeatsFire()
        {
            Assert.AreEqual(1.3f, ElementTable.Multiplier(Element.Fire, Element.Wind), 0.001f);
            Assert.AreEqual(1.3f, ElementTable.Multiplier(Element.Wind, Element.Earth), 0.001f);
            Assert.AreEqual(1.3f, ElementTable.Multiplier(Element.Earth, Element.Water), 0.001f);
            Assert.AreEqual(1.3f, ElementTable.Multiplier(Element.Water, Element.Fire), 0.001f);
        }

        [Test]
        public void ReverseCycle_IsWeak()
        {
            Assert.AreEqual(0.7f, ElementTable.Multiplier(Element.Wind, Element.Fire), 0.001f);
            Assert.AreEqual(0.7f, ElementTable.Multiplier(Element.Fire, Element.Water), 0.001f);
        }

        [Test]
        public void LightAndDark_OpposeEachOther_ButNeutralToOthers()
        {
            Assert.AreEqual(1.4f, ElementTable.Multiplier(Element.Light, Element.Dark), 0.001f);
            Assert.AreEqual(1.4f, ElementTable.Multiplier(Element.Dark, Element.Light), 0.001f);
            Assert.AreEqual(1.0f, ElementTable.Multiplier(Element.Light, Element.Fire), 0.001f);
            Assert.AreEqual(1.0f, ElementTable.Multiplier(Element.Fire, Element.Dark), 0.001f);
        }

        [Test]
        public void SameElement_IsNeutral()
        {
            foreach (Element e in System.Enum.GetValues(typeof(Element)))
                Assert.AreEqual(1.0f, ElementTable.Multiplier(e, e), 0.001f, $"{e} vs {e}");
        }

        [Test]
        public void Neutral_AlwaysOne()
        {
            foreach (Element e in System.Enum.GetValues(typeof(Element)))
            {
                Assert.AreEqual(1.0f, ElementTable.Multiplier(Element.Neutral, e), 0.001f);
                Assert.AreEqual(1.0f, ElementTable.Multiplier(e, Element.Neutral), 0.001f);
            }
        }
    }

    /// <summary>ATB rời rạc — plan.md §4.4.</summary>
    public class TurnSchedulerTests
    {
        [Test]
        public void AdvanceToNextActor_FasterUnitGoesFirst()
        {
            var state = new BattleState { Rng = new XorShiftRandom(1) };
            var slow = TestFactory.Unit("slow", stats: TestFactory.Stats(dex: 5));
            var fast = TestFactory.Unit("fast", TeamSide.Enemy, stats: TestFactory.Stats(dex: 60));
            state.AddUnit(slow); state.AddUnit(fast);
            slow.FillResources(); fast.FillResources();

            var scheduler = new TurnScheduler(state);
            Assert.AreEqual(fast.Id, scheduler.AdvanceToNextActor().Id);
        }

        [Test]
        public void TieBreak_IsStableByUnitId()
        {
            var state = new BattleState { Rng = new XorShiftRandom(1) };
            var a = TestFactory.Unit("a");
            var b = TestFactory.Unit("b", TeamSide.Enemy);
            state.AddUnit(a); state.AddUnit(b);
            a.FillResources(); b.FillResources();

            var scheduler = new TurnScheduler(state);
            // Cùng SPD → id nhỏ hơn đi trước, lặp lại nhiều lần vẫn vậy
            for (int i = 0; i < 5; i++)
            {
                a.Atb = b.Atb = TurnScheduler.ATB_THRESHOLD;
                Assert.AreEqual(a.Id, scheduler.AdvanceToNextActor().Id);
            }
        }

        [Test]
        public void PreviewOrder_ReturnsRequestedCount_AndDoesNotMutateState()
        {
            var state = new BattleState { Rng = new XorShiftRandom(1) };
            var a = TestFactory.Unit("a", stats: TestFactory.Stats(dex: 40));
            var b = TestFactory.Unit("b", TeamSide.Enemy, stats: TestFactory.Stats(dex: 10));
            state.AddUnit(a); state.AddUnit(b);
            a.FillResources(); b.FillResources();

            var scheduler = new TurnScheduler(state);
            int atbBefore = a.Atb;
            var order = scheduler.PreviewOrder(8);

            Assert.AreEqual(8, order.Count);
            Assert.AreEqual(atbBefore, a.Atb, "PreviewOrder không được thay đổi ATB thật");
            // Unit nhanh hơn xuất hiện nhiều hơn
            int countA = order.FindAll(id => id == a.Id).Count;
            Assert.Greater(countA, order.Count - countA);
        }

        [Test]
        public void SpdFloor_PreventsDeadlock()
        {
            // Edge case E22: SPD bị debuff về ~0 vẫn phải tiến tới ngưỡng
            var state = new BattleState { Rng = new XorShiftRandom(1) };
            var u = TestFactory.Unit("u", stats: TestFactory.Stats(dex: 0));
            state.AddUnit(u);
            u.FillResources();
            u.Statuses.Add(new StatusInstance(StatusId.SpdDown, 2, 99, -1));
            u.MarkStatsDirty();

            Assert.GreaterOrEqual(u.SpdEffective, BalanceCaps.SPD_MIN);
            Assert.DoesNotThrow(() => new TurnScheduler(state).AdvanceToNextActor());
        }
    }

    /// <summary>Trạng thái — plan.md §4.11.</summary>
    public class StatusProcessorTests
    {
        private (BattleState, StatusProcessor, CombatUnit, CombatUnit) Setup()
        {
            var state = new BattleState { Rng = new XorShiftRandom(TestFactory.SEED) };
            var events = new Game.Combat.Events.CombatEventQueue();
            var proc = new StatusProcessor(state, events);
            var src = TestFactory.Unit("src");
            var tgt = TestFactory.Unit("tgt", TeamSide.Enemy);
            state.AddUnit(src); state.AddUnit(tgt);
            src.FillResources(); tgt.FillResources();
            return (state, proc, src, tgt);
        }

        [Test]
        public void Apply_GuaranteedDebuff_Succeeds()
        {
            var (state, proc, src, tgt) = Setup();
            bool ok = proc.Apply(src, tgt, new StatusApplication(StatusId.Burn, 1f), state.Rng);

            Assert.IsTrue(ok);
            Assert.IsTrue(tgt.HasStatus(StatusId.Burn));
        }

        [Test]
        public void Apply_Immunity_BlocksAllNewDebuffs()
        {
            var (state, proc, src, tgt) = Setup();
            proc.Apply(src, tgt, new StatusApplication(StatusId.Immunity, 1f), state.Rng);

            Assert.IsFalse(proc.Apply(src, tgt, new StatusApplication(StatusId.Burn, 1f), state.Rng));
            Assert.IsFalse(proc.Apply(src, tgt, new StatusApplication(StatusId.Stun, 1f), state.Rng));
            Assert.IsFalse(tgt.HasStatus(StatusId.Burn));
            Assert.IsTrue(tgt.HasStatus(StatusId.Immunity), "Immunity không bị tiêu hao");
        }

        [Test]
        public void Apply_Immunity_DoesNotBlockBuffs()
        {
            var (state, proc, src, tgt) = Setup();
            proc.Apply(src, tgt, new StatusApplication(StatusId.Immunity, 1f), state.Rng);

            Assert.IsTrue(proc.Apply(src, tgt, new StatusApplication(StatusId.AtkUp, 1f), state.Rng));
        }

        [Test]
        public void Apply_Reapply_StacksUpToMaxThenOnlyRefreshes()
        {
            var (state, proc, src, tgt) = Setup();
            var app = new StatusApplication(StatusId.Burn, 1f, 3);

            proc.Apply(src, tgt, app, state.Rng);
            proc.Apply(src, tgt, app, state.Rng);
            proc.Apply(src, tgt, app, state.Rng);
            proc.Apply(src, tgt, app, state.Rng); // vượt max (3)

            Assert.AreEqual(3, tgt.StatusStacks(StatusId.Burn));
        }

        [Test]
        public void TickTurnStart_Burn_DealsDamage()
        {
            var (state, proc, src, tgt) = Setup();
            proc.Apply(src, tgt, new StatusApplication(StatusId.Burn, 1f), state.Rng);
            int before = tgt.Hp;

            proc.TickTurnStart(tgt);

            Assert.Less(tgt.Hp, before);
        }

        [Test]
        public void TickTurnEnd_ReducesDurationAndExpires()
        {
            var (state, proc, src, tgt) = Setup();
            proc.Apply(src, tgt, new StatusApplication(StatusId.AtkUp, 1f, duration: 2), state.Rng);

            proc.TickTurnEnd(tgt);
            Assert.IsTrue(tgt.HasStatus(StatusId.AtkUp));

            proc.TickTurnEnd(tgt);
            Assert.IsFalse(tgt.HasStatus(StatusId.AtkUp), "Hết hạn phải bị gỡ");
        }

        [Test]
        public void Shield_AbsorbsBeforeHp_ThenBreaks()
        {
            var (state, proc, src, tgt) = Setup();
            proc.ApplyShield(src, tgt, 50f, 3, state.Rng);

            int remaining = proc.AbsorbWithShield(tgt, 30);
            Assert.AreEqual(0, remaining, "Shield 50 hấp thụ hết 30 damage");
            Assert.IsTrue(tgt.HasStatus(StatusId.Shield));

            remaining = proc.AbsorbWithShield(tgt, 40);
            Assert.AreEqual(20, remaining, "Còn 20 shield, damage 40 → 20 lọt qua");
            Assert.IsFalse(tgt.HasStatus(StatusId.Shield), "Shield vỡ thì bị gỡ");
        }

        [Test]
        public void Cleanse_RemovesDebuffsOnly()
        {
            var (state, proc, src, tgt) = Setup();
            proc.Apply(src, tgt, new StatusApplication(StatusId.Burn, 1f), state.Rng);
            proc.Apply(src, tgt, new StatusApplication(StatusId.AtkUp, 1f), state.Rng);

            proc.Cleanse(tgt, 5);

            Assert.IsFalse(tgt.HasStatus(StatusId.Burn), "Debuff bị xoá");
            Assert.IsTrue(tgt.HasStatus(StatusId.AtkUp), "Buff không bị Cleanse");
        }

        /// <summary>Edge case E21 — 2 status cùng ID từ 2 nguồn khác nhau: cùng 1 slot stack,
        /// giá trị lấy theo nguồn MẠNH HƠN, bất kể thứ tự áp dụng trước/sau.</summary>
        [Test]
        public void E21_SameStatusFromTwoSources_KeepsStrongerSourceValue_RegardlessOfOrder()
        {
            var (state, proc, weakSrc, tgt) = Setup();
            var strongSrc = TestFactory.Unit("strong", stats: TestFactory.Stats(str: 999));
            state.AddUnit(strongSrc); strongSrc.FillResources();

            // Yếu áp trước, mạnh áp sau → phải ghi đè lên giá trị mạnh hơn.
            proc.Apply(weakSrc, tgt, new StatusApplication(StatusId.Bleed, 1f, stacks: 1), state.Rng);
            proc.Apply(strongSrc, tgt, new StatusApplication(StatusId.Bleed, 1f, stacks: 1), state.Rng);
            float valueAfterStrong = tgt.GetStatus(StatusId.Bleed).Value;
            Assert.Greater(valueAfterStrong, 0f);

            // Đảo ngược: mạnh áp trước, yếu áp sau — KHÔNG được ghi đè lại giá trị mạnh đã có.
            proc.RemoveStatus(tgt, StatusId.Bleed);
            proc.Apply(strongSrc, tgt, new StatusApplication(StatusId.Bleed, 1f, stacks: 1), state.Rng);
            proc.Apply(weakSrc, tgt, new StatusApplication(StatusId.Bleed, 1f, stacks: 1), state.Rng);

            Assert.AreEqual(valueAfterStrong, tgt.GetStatus(StatusId.Bleed).Value, 0.01f,
                "Edge case E21: nguồn yếu áp SAU không được ghi đè giá trị của nguồn mạnh hơn");
        }

        [Test]
        public void Dispel_RemovesBuffsOnly_AndCanSteal()
        {
            var (state, proc, src, tgt) = Setup();
            proc.Apply(src, tgt, new StatusApplication(StatusId.AtkUp, 1f), state.Rng);
            proc.Apply(src, tgt, new StatusApplication(StatusId.Burn, 1f), state.Rng);

            proc.Dispel(tgt, 5, src, state.Rng);

            Assert.IsFalse(tgt.HasStatus(StatusId.AtkUp));
            Assert.IsTrue(tgt.HasStatus(StatusId.Burn), "Debuff không bị Dispel");
            Assert.IsTrue(src.HasStatus(StatusId.AtkUp), "Buff bị cướp sang người dispel");
        }

        [Test]
        public void FreezeMeltsOnFireHit()
        {
            var (state, proc, src, tgt) = Setup();
            proc.Apply(src, tgt, new StatusApplication(StatusId.Freeze, 1f), state.Rng);

            proc.OnHitByElement(tgt, Element.Water);
            Assert.IsTrue(tgt.HasStatus(StatusId.Freeze), "Chỉ Fire mới làm tan Freeze");

            proc.OnHitByElement(tgt, Element.Fire);
            Assert.IsFalse(tgt.HasStatus(StatusId.Freeze), "Edge case E05");
        }

        /// <summary>Edge case E05 — vế còn lại: damage ×(1+FREEZE_DMG_TAKEN_BONUS) khi đang Freeze,
        /// trước khi Freeze tan (thứ tự đã đúng trong ActionResolver.ApplyOneHit: tính damage rồi
        /// mới OnHitByElement làm tan băng).</summary>
        [Test]
        public void Freeze_IncreasesDamageTaken_ByFreezeBonus()
        {
            var a = TestFactory.Unit("a", stats: TestFactory.Stats(dex: 60));
            var frozen = TestFactory.Unit("f", TeamSide.Enemy);
            var notFrozen = TestFactory.Unit("n", TeamSide.Enemy);
            a.FillResources(); frozen.FillResources(); notFrozen.FillResources();
            frozen.Statuses.Add(new StatusInstance(StatusId.Freeze, 1, 3, -1));
            var skill = new SkillRuntime(TestFactory.BasicAttack(), 0);

            int dmgFrozen = DamageCalculator.Calculate(a, frozen, skill, CommandGrade.Good, new XorShiftRandom(TestFactory.SEED)).Amount;
            int dmgNormal = DamageCalculator.Calculate(a, notFrozen, skill, CommandGrade.Good, new XorShiftRandom(TestFactory.SEED)).Amount;

            Assert.AreEqual(dmgNormal * (1f + StatusTable.FREEZE_DMG_TAKEN_BONUS), dmgFrozen, dmgNormal * 0.05f,
                "Edge case E05: unit đang Freeze phải ăn thêm damage đúng hệ số FREEZE_DMG_TAKEN_BONUS");
        }

        [Test]
        public void SleepWakesOnDamage()
        {
            var (state, proc, src, tgt) = Setup();
            proc.Apply(src, tgt, new StatusApplication(StatusId.Sleep, 1f), state.Rng);

            proc.OnDamaged(tgt);
            Assert.IsFalse(tgt.HasStatus(StatusId.Sleep), "Edge case E06");
        }

        /// <summary>Edge case E06 — vế còn lại: Sleep KHÔNG giảm/chặn damage (unit ngủ vẫn ăn full
        /// damage, chỉ khác là tỉnh dậy SAU khi damage đã áp dụng — đúng thứ tự trong
        /// ActionResolver.ApplyOneHit).</summary>
        [Test]
        public void Sleep_DoesNotReduceDamageTaken()
        {
            var a = TestFactory.Unit("a", stats: TestFactory.Stats(dex: 60));
            var sleeping = TestFactory.Unit("s", TeamSide.Enemy);
            var awake = TestFactory.Unit("w", TeamSide.Enemy);
            a.FillResources(); sleeping.FillResources(); awake.FillResources();
            sleeping.Statuses.Add(new StatusInstance(StatusId.Sleep, 1, 3, -1));
            var skill = new SkillRuntime(TestFactory.BasicAttack(), 0);

            int dmgSleeping = DamageCalculator.Calculate(a, sleeping, skill, CommandGrade.Good, new XorShiftRandom(TestFactory.SEED)).Amount;
            int dmgAwake = DamageCalculator.Calculate(a, awake, skill, CommandGrade.Good, new XorShiftRandom(TestFactory.SEED)).Amount;

            Assert.AreEqual(dmgAwake, dmgSleeping, "Edge case E06: Sleep không được giảm sát thương nhận vào");
        }

        [Test]
        public void AtkUpAndAtkDown_ModifyStatsAdditively()
        {
            var (state, proc, src, tgt) = Setup();
            float baseAtk = tgt.Stats.AtkPhys;

            proc.Apply(src, tgt, new StatusApplication(StatusId.AtkUp, 1f, stacks: 2), state.Rng);
            Assert.AreEqual(baseAtk * 1.5f, tgt.Stats.AtkPhys, 0.5f, "2 stack = +50%, không phải 1.25²");

            proc.RemoveStatus(tgt, StatusId.AtkUp);
            proc.Apply(src, tgt, new StatusApplication(StatusId.AtkDown, 1f, stacks: 1), state.Rng);
            Assert.AreEqual(baseAtk * 0.75f, tgt.Stats.AtkPhys, 0.5f);
        }
    }

    /// <summary>Poise/Break — plan.md §4.9.</summary>
    public class PoiseSystemTests
    {
        private (BattleState, PoiseSystem, CombatUnit, CombatUnit) Setup(int poiseMax = 30)
        {
            var state = new BattleState { Rng = new XorShiftRandom(TestFactory.SEED) };
            var events = new Game.Combat.Events.CombatEventQueue();
            var status = new StatusProcessor(state, events);
            var passive = new PassiveProcessor(state, events, status);
            var poise = new PoiseSystem(state, events, status, passive);
            var src = TestFactory.Unit("src");
            var tgt = TestFactory.Unit("tgt", TeamSide.Enemy, poiseMax: poiseMax);
            state.AddUnit(src); state.AddUnit(tgt);
            src.FillResources(); tgt.FillResources();
            return (state, poise, src, tgt);
        }

        [Test]
        public void DamagePoise_ReducesPoise()
        {
            var (state, poise, src, tgt) = Setup();
            poise.DamagePoise(src, tgt, 10, state.Rng);
            Assert.AreEqual(20, tgt.Poise);
        }

        [Test]
        public void PoiseReachingZero_TriggersBreak_AndResetsAtb()
        {
            var (state, poise, src, tgt) = Setup(poiseMax: 20);
            tgt.Atb = 900;

            bool broke = poise.DamagePoise(src, tgt, 25, state.Rng);

            Assert.IsTrue(broke);
            Assert.IsTrue(tgt.IsBroken);
            Assert.AreEqual(0, tgt.Atb, "Break làm mất lượt kế tiếp");
        }

        [Test]
        public void BrokenUnit_DoesNotTakeMorePoiseDamage()
        {
            var (state, poise, src, tgt) = Setup(poiseMax: 20);
            poise.DamagePoise(src, tgt, 25, state.Rng);

            Assert.IsFalse(poise.DamagePoise(src, tgt, 25, state.Rng), "Đang Break thì không Break tiếp");
        }

        [Test]
        public void Break_ExtendsDebuffDurationByOne()
        {
            var state = new BattleState { Rng = new XorShiftRandom(TestFactory.SEED) };
            var events = new Game.Combat.Events.CombatEventQueue();
            var status = new StatusProcessor(state, events);
            var passive = new PassiveProcessor(state, events, status);
            var poise = new PoiseSystem(state, events, status, passive);
            var src = TestFactory.Unit("src");
            var tgt = TestFactory.Unit("tgt", TeamSide.Enemy, poiseMax: 10);
            state.AddUnit(src); state.AddUnit(tgt);
            src.FillResources(); tgt.FillResources();

            status.Apply(src, tgt, new StatusApplication(StatusId.Burn, 1f, duration: 3), state.Rng);
            status.Apply(src, tgt, new StatusApplication(StatusId.AtkUp, 1f, duration: 3), state.Rng);

            poise.DamagePoise(src, tgt, 20, state.Rng);

            Assert.AreEqual(4, tgt.GetStatus(StatusId.Burn).RemainingTurns, "Debuff +1 lượt");
            Assert.AreEqual(3, tgt.GetStatus(StatusId.AtkUp).RemainingTurns, "Buff không đổi");
        }

        [Test]
        public void PoiseRecovers_AfterDelay()
        {
            var (state, poise, src, tgt) = Setup();
            poise.DamagePoise(src, tgt, 10, state.Rng);
            Assert.AreEqual(20, tgt.Poise);

            poise.TickTurnEnd(tgt);   // delay 2 → 1
            poise.TickTurnEnd(tgt);   // delay 1 → 0
            poise.TickTurnEnd(tgt);   // hồi đầy

            Assert.AreEqual(tgt.PoiseMax, tgt.Poise);
        }
    }

    /// <summary>Nhắm mục tiêu — plan.md §4.12.</summary>
    public class TargetSelectorTests
    {
        [Test]
        public void Taunt_OverridesPlayerChoice()
        {
            var sim = TestFactory.TeamBattle(out var heroes, out var enemies);
            var selector = new TargetSelector(sim.State);

            enemies[2].Statuses.Add(new StatusInstance(StatusId.Taunt, 1, 3, -1));

            var skill = TestFactory.BasicAttack();
            var result = selector.Resolve(heroes[0], skill, enemies[0].Id, sim.State.Rng);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(enemies[2].Id, result[0].Id, "Taunt ghi đè lựa chọn của người chơi");
        }

        [Test]
        public void DeadTaunter_DoesNotOverride()
        {
            var sim = TestFactory.TeamBattle(out var heroes, out var enemies);
            var selector = new TargetSelector(sim.State);

            enemies[2].Statuses.Add(new StatusInstance(StatusId.Taunt, 1, 3, -1));
            enemies[2].Hp = 0;

            var result = selector.Resolve(heroes[0], TestFactory.BasicAttack(), enemies[0].Id, sim.State.Rng);

            Assert.AreEqual(enemies[0].Id, result[0].Id, "Edge case E07");
        }

        [Test]
        public void AllEnemies_ReturnsOnlyAliveEnemies()
        {
            var sim = TestFactory.TeamBattle(out var heroes, out var enemies);
            var selector = new TargetSelector(sim.State);
            enemies[1].Hp = 0;

            var skill = TestFactory.BasicAttack(target: TargetMode.AllEnemies);
            var result = selector.Resolve(heroes[0], skill, -1, sim.State.Rng);

            Assert.AreEqual(3, result.Count);
            CollectionAssert.DoesNotContain(result, enemies[1]);
        }

        [Test]
        public void LowestHpAlly_PicksMostInjured()
        {
            var sim = TestFactory.TeamBattle(out var heroes, out _);
            var selector = new TargetSelector(sim.State);
            heroes[2].SetHp(heroes[2].MaxHp / 5);

            var skill = TestFactory.HealSkill(target: TargetMode.LowestHpAlly);
            var result = selector.Resolve(heroes[0], skill, -1, sim.State.Rng);

            Assert.AreEqual(heroes[2].Id, result[0].Id);
        }

        [Test]
        public void FrontRow_FallsBackToAll_WhenFrontEmpty()
        {
            var sim = TestFactory.TeamBattle(out var heroes, out var enemies);
            var selector = new TargetSelector(sim.State);
            enemies[0].Hp = 0;
            enemies[1].Hp = 0;

            var skill = TestFactory.BasicAttack(target: TargetMode.FrontRowEnemies);
            var result = selector.Resolve(heroes[0], skill, -1, sim.State.Rng);

            Assert.AreEqual(2, result.Count, "Hàng trước chết hết → nhắm toàn bộ còn sống");
        }
    }

    /// <summary>Edge case cần dựng thẳng ActionResolver (không qua CombatSimulation đầy đủ) —
    /// plan.md §4.14 E03/E08/E10.</summary>
    public class ActionResolverEdgeCaseTests
    {
        private (BattleState state, ActionResolver resolver, StatusProcessor status, CombatUnit a, CombatUnit b) Setup()
        {
            var state = new BattleState { Rng = new XorShiftRandom(TestFactory.SEED) };
            var events = new Game.Combat.Events.CombatEventQueue();
            var status = new StatusProcessor(state, events);
            var passive = new PassiveProcessor(state, events, status);
            var poise = new PoiseSystem(state, events, status, passive);
            var targeting = new TargetSelector(state);
            var resolver = new ActionResolver(state, events, status, passive, poise, targeting);
            var a = TestFactory.Unit("a", TeamSide.Player, stats: TestFactory.Stats(dex: 60));
            var b = TestFactory.Unit("b", TeamSide.Enemy);
            state.AddUnit(a); state.AddUnit(b);
            a.FillResources(); b.FillResources();
            return (state, resolver, status, a, b);
        }

        [Test]
        public void E03_ActorDiesFromCounterMidCombo_CurrentHitStillCompletes()
        {
            var (state, resolver, status, a, b) = Setup();
            status.Apply(a, b, new StatusApplication(StatusId.Counter, 1f, duration: 3), state.Rng);
            a.SetHp(1); // chết ngay ở đòn phản đầu tiên
            int bHpBefore = b.Hp;

            var skill = new SkillRuntime(TestFactory.BasicAttack(power: 0.3f, hits: 3, dmgType: DamageType.Physical), 0);
            resolver.Execute(a, skill, b.Id, CommandGrade.Good, state.Rng);

            Assert.IsTrue(a.IsDead, "Edge case E03: actor phải chết do Counter phản đòn");
            Assert.Less(b.Hp, bHpBefore,
                "Hit đầu tiên (đã gây chết actor qua Counter) vẫn phải áp damage lên target trước khi combo dừng");
        }

        [Test]
        public void E08_ShieldFullyAbsorbs_HpUnchanged_AndOnDamageTakenPassiveDoesNotFire()
        {
            var (state, resolver, status, a, b) = Setup();
            status.ApplyShield(a, b, 9999f, 3, state.Rng); // khiên khổng lồ, chắc chắn hấp thụ hết
            b.Passive = new PassiveData
            {
                Trigger = PassiveTrigger.OnDamageTaken,
                Applies = new[] { new StatusApplication(StatusId.DefUp, 1f, duration: 2, stacks: 1, targetSelf: true) }
            };
            int hpBefore = b.Hp;
            var skill = new SkillRuntime(TestFactory.BasicAttack(power: 0.5f), 0);

            resolver.Execute(a, skill, b.Id, CommandGrade.Good, state.Rng);

            Assert.AreEqual(hpBefore, b.Hp, "Edge case E08: Shield hấp thụ hết damage → HP không đổi");
            Assert.IsFalse(b.HasStatus(StatusId.DefUp),
                "Shield hấp thụ hết KHÔNG được tính là 'bị đánh' cho passive OnDamageTaken");
        }

        [Test]
        public void E10_ReviveClearsAllStatus_AndSetsHpByRevivePercent()
        {
            var (state, resolver, status, a, _) = Setup();
            var dead = TestFactory.Unit("dead", TeamSide.Player);
            state.AddUnit(dead); dead.FillResources(); dead.Hp = 0;
            dead.Statuses.Add(new StatusInstance(StatusId.AtkDown, 1, 3, -1));

            var reviveSkill = new SkillData
            {
                Id = "skill_revive_test",
                Type = SkillType.Heal,
                DamageType = DamageType.Magical,
                Target = TargetMode.DeadAlly,
                RevivePercent = 0.5f,
                CommandType = ActionCommandType.SingleTap
            };

            resolver.Execute(a, new SkillRuntime(reviveSkill, 0), dead.Id, CommandGrade.Good, state.Rng);

            Assert.IsTrue(dead.IsAlive, "Edge case E10: hồi sinh phải làm unit sống lại");
            Assert.Greater(dead.Hp, 0);
            Assert.LessOrEqual(dead.Hp, dead.MaxHp);
            Assert.IsFalse(dead.HasStatus(StatusId.AtkDown), "Hồi sinh phải xoá toàn bộ status cũ");
        }
    }
}
