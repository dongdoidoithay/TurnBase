using Game.Combat.Events;
using Game.Combat.Model;
using Game.CombatView.Tutorial;
using Game.Data;
using NUnit.Framework;

namespace Game.Tests.CombatView
{
    /// <summary>task-phase-5-gaps.md Phần B — 5 bước chuyển đúng thứ tự, bỏ qua sự kiện sai
    /// thứ tự (không bị fooled bởi event từ hướng khác/phe khác), Skip nhảy thẳng Done.</summary>
    public class TutorialControllerTests
    {
        private TutorialController _ctrl;
        private BattleState _state;
        private CombatEventQueue _events;
        private int _heroId, _enemyId;

        [SetUp]
        public void SetUp()
        {
            _ctrl = new TutorialController();
            _state = new BattleState();
            _events = new CombatEventQueue();

            var hero = Game.Tests.TestFactory.Unit("hero", TeamSide.Player);
            var enemy = Game.Tests.TestFactory.Unit("enemy", TeamSide.Enemy);
            _state.AddUnit(hero);
            _state.AddUnit(enemy);
            _heroId = hero.Id;
            _enemyId = enemy.Id;
        }

        [Test]
        public void InitialStep_IsChooseSkill()
        {
            Assert.AreEqual(TutorialStep.ChooseSkill, _ctrl.Step);
            Assert.IsFalse(_ctrl.IsDone);
        }

        [Test]
        public void NotifySkillChosen_AtChooseSkill_AdvancesToActionCommand()
        {
            _ctrl.NotifySkillChosen();
            Assert.AreEqual(TutorialStep.ActionCommand, _ctrl.Step);
        }

        [Test]
        public void NotifySkillChosen_AfterAlreadyAdvanced_DoesNotAdvanceAgain()
        {
            _ctrl.NotifySkillChosen(); // ChooseSkill -> ActionCommand
            _ctrl.NotifySkillChosen(); // gọi lại lúc đang ActionCommand — không phải bước của nó
            Assert.AreEqual(TutorialStep.ActionCommand, _ctrl.Step);
        }

        [Test]
        public void NotifyCommandResolved_AtActionCommand_AdvancesToCounter()
        {
            _ctrl.NotifySkillChosen();
            _ctrl.NotifyCommandResolved(CommandGrade.Perfect);
            Assert.AreEqual(TutorialStep.Counter, _ctrl.Step);
        }

        [Test]
        public void NotifyCommandResolved_BeforeChooseSkill_IsIgnored()
        {
            // Gọi "trái thứ tự" (chưa qua ChooseSkill) — không được nhảy cóc.
            _ctrl.NotifyCommandResolved(CommandGrade.Perfect);
            Assert.AreEqual(TutorialStep.ChooseSkill, _ctrl.Step);
        }

        private void ReachCounter()
        {
            _ctrl.NotifySkillChosen();
            _ctrl.NotifyCommandResolved(CommandGrade.Good);
        }

        [Test]
        public void Tick_ElementAdvantageDamageFromPlayer_AdvancesCounterToBreak()
        {
            ReachCounter();
            _events.Emit(CombatEventType.DamageDealt, source: _heroId, target: _enemyId,
                         intValue: 40, floatValue: 1.5f);

            _ctrl.Tick(_events, _state);

            Assert.AreEqual(TutorialStep.Break, _ctrl.Step);
        }

        [Test]
        public void Tick_NeutralDamage_DoesNotAdvancePastCounter()
        {
            ReachCounter();
            _events.Emit(CombatEventType.DamageDealt, source: _heroId, target: _enemyId,
                         intValue: 40, floatValue: 1f);

            _ctrl.Tick(_events, _state);

            Assert.AreEqual(TutorialStep.Counter, _ctrl.Step);
        }

        [Test]
        public void Tick_ElementAdvantageDamageFromEnemy_DoesNotAdvance()
        {
            // Địch gây đòn khắc chế lên Player — KHÔNG phải điều tutorial dạy ở bước này (dạy
            // người chơi tự gây đòn khắc chế), không được advance nhầm.
            ReachCounter();
            _events.Emit(CombatEventType.DamageDealt, source: _enemyId, target: _heroId,
                         intValue: 40, floatValue: 1.5f);

            _ctrl.Tick(_events, _state);

            Assert.AreEqual(TutorialStep.Counter, _ctrl.Step);
        }

        private void ReachBreak()
        {
            ReachCounter();
            _events.Emit(CombatEventType.DamageDealt, source: _heroId, target: _enemyId,
                         intValue: 40, floatValue: 1.5f);
            _ctrl.Tick(_events, _state);
        }

        [Test]
        public void Tick_EnemyPoiseBroken_AdvancesBreakToUltimate()
        {
            ReachBreak();
            _events.Emit(CombatEventType.PoiseBroken, source: _heroId, target: _enemyId);

            _ctrl.Tick(_events, _state);

            Assert.AreEqual(TutorialStep.Ultimate, _ctrl.Step);
        }

        [Test]
        public void Tick_PlayerOwnPoiseBroken_DoesNotAdvance()
        {
            // Unit của CHÍNH Player bị Break (địch làm) — không phải cái tutorial đang dạy.
            ReachBreak();
            _events.Emit(CombatEventType.PoiseBroken, source: _enemyId, target: _heroId);

            _ctrl.Tick(_events, _state);

            Assert.AreEqual(TutorialStep.Break, _ctrl.Step);
        }

        [Test]
        public void SingleTick_CanCascadeThroughMultipleSteps_WhenEventsArriveTogether()
        {
            // Nhiều event có thể dồn vào cùng 1 khung hình thật (Presenter phát theo lô) — 1 lần
            // Tick phải xử lý tuần tự đúng, không bỏ sót.
            ReachCounter();
            _events.Emit(CombatEventType.DamageDealt, source: _heroId, target: _enemyId,
                         intValue: 40, floatValue: 1.5f);
            _events.Emit(CombatEventType.PoiseBroken, source: _heroId, target: _enemyId);

            _ctrl.Tick(_events, _state);

            Assert.AreEqual(TutorialStep.Ultimate, _ctrl.Step);
        }

        [Test]
        public void Tick_UltimateGaugeFullThenConsumed_AdvancesToDone()
        {
            ReachBreak();
            _events.Emit(CombatEventType.PoiseBroken, source: _heroId, target: _enemyId);
            _ctrl.Tick(_events, _state); // -> Ultimate

            _state.UltimateGauge = BattleState.ULTIMATE_MAX;
            _ctrl.Tick(_events, _state); // gauge đầy, chưa dùng — vẫn Ultimate
            Assert.AreEqual(TutorialStep.Ultimate, _ctrl.Step);

            _state.ConsumeUltimate(); // đầy -> 0, đúng cạnh xuống
            _ctrl.Tick(_events, _state);

            Assert.AreEqual(TutorialStep.Done, _ctrl.Step);
            Assert.IsTrue(_ctrl.IsDone);
        }

        [Test]
        public void Tick_UltimateNeverReachesFull_StaysAtUltimateStep()
        {
            ReachBreak();
            _events.Emit(CombatEventType.PoiseBroken, source: _heroId, target: _enemyId);
            _ctrl.Tick(_events, _state); // -> Ultimate

            _state.UltimateGauge = 50; // chưa đầy
            _ctrl.Tick(_events, _state);

            Assert.AreEqual(TutorialStep.Ultimate, _ctrl.Step);
        }

        [Test]
        public void OnStepChanged_FiresWithEachNewStep()
        {
            var seen = new System.Collections.Generic.List<TutorialStep>();
            _ctrl.OnStepChanged += s => seen.Add(s);

            _ctrl.NotifySkillChosen();
            _ctrl.NotifyCommandResolved(CommandGrade.Good);

            CollectionAssert.AreEqual(
                new[] { TutorialStep.ActionCommand, TutorialStep.Counter }, seen);
        }

        [Test]
        public void Skip_JumpsDirectlyToDone_FromAnyStep()
        {
            _ctrl.Skip();
            Assert.AreEqual(TutorialStep.Done, _ctrl.Step);
            Assert.IsTrue(_ctrl.IsDone);
        }

        [Test]
        public void Skip_WhenAlreadyDone_DoesNotFireStepChangedAgain()
        {
            _ctrl.Skip();
            int fireCount = 0;
            _ctrl.OnStepChanged += _ => fireCount++;

            _ctrl.Skip();

            Assert.AreEqual(0, fireCount);
        }

        [Test]
        public void Tick_WithNullQueueAndState_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _ctrl.Tick(null, null));
        }
    }
}
