using Game.Combat;
using Game.Combat.Model;
using Game.Data;
using NUnit.Framework;

namespace Game.Tests.Combat
{
    /// <summary>plan.md §4.15 Defeat — lựa chọn "Hồi sinh bằng Gem"
    /// (<see cref="CombatSimulation.TryReviveWithGem"/>). Trừ Gem là việc của tầng Meta
    /// (BattleSceneInstaller, không test được ở EditMode thuần vì cần IEconomyService thật) — test
    /// ở đây chỉ phủ phần logic Combat: hồi HP đúng %, đặt lại Result, và chặn gọi khi chưa thua
    /// thật/gọi lặp.</summary>
    public class CombatSimulationReviveTests
    {
        [Test]
        public void TryReviveWithGem_ReturnsFalse_WhenNotDefeated()
        {
            var sim = TestFactory.Duel(out _, out _);
            // Vừa Start() — Result vẫn InProgress, chưa ai thua.
            Assert.IsFalse(sim.TryReviveWithGem());
        }

        [Test]
        public void TryReviveWithGem_RevivesDeadPlayerUnits_To40PercentMaxHp()
        {
            var sim = TestFactory.Duel(out var hero, out var enemy);
            hero.SetHp(0);
            sim.State.Result = BattleResult.Defeat;

            bool revived = sim.TryReviveWithGem();

            Assert.IsTrue(revived);
            Assert.IsTrue(hero.IsAlive);
            Assert.AreEqual((int)(hero.MaxHp * 0.4f), hero.Hp,
                "Đúng RevivePercent=0.4 đã dùng cho Revive Feather (task-consumable-items.md) — không bịa tỉ lệ mới.");
            Assert.AreEqual(BattleResult.InProgress, sim.State.Result);
            Assert.IsFalse(sim.IsFinished);
        }

        [Test]
        public void TryReviveWithGem_DoesNotReviveEnemies()
        {
            var sim = TestFactory.Duel(out var hero, out var enemy);
            hero.SetHp(0);
            enemy.SetHp(0);
            sim.State.Result = BattleResult.Defeat;

            sim.TryReviveWithGem();

            Assert.IsFalse(enemy.IsAlive, "Chỉ hồi sinh phe Player — địch chết vẫn phải chết.");
        }

        [Test]
        public void TryReviveWithGem_CannotBeCalledTwice()
        {
            var sim = TestFactory.Duel(out var hero, out _);
            hero.SetHp(0);
            sim.State.Result = BattleResult.Defeat;

            Assert.IsTrue(sim.TryReviveWithGem());
            Assert.IsFalse(sim.TryReviveWithGem(),
                "Result không còn Defeat sau lần hồi đầu — lần gọi thứ 2 phải bị chặn (an toàn cuối, tránh trừ Gem 2 lần cho 1 lượt hồi).");
        }

        [Test]
        public void TryReviveWithGem_ResumesSimulation_WithoutException()
        {
            // Mô phỏng đúng đường thật: thua qua Finish() thật (Phase=Finished thật), không set tay.
            var weakStats = TestFactory.Stats(str: 1, con: 1);
            var sim = new CombatSimulation(TestFactory.SEED, 0);
            var hero = TestFactory.Unit("hero", TeamSide.Player, stats: weakStats);
            hero.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(power: 0.01f), 0));
            var enemy = TestFactory.Unit("enemy", TeamSide.Enemy);
            enemy.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(power: 5f), 0));
            sim.AddUnit(hero);
            sim.AddUnit(enemy, TestFactory.SimpleAi());
            sim.Start();

            int guard = 0;
            while (!sim.IsFinished && guard++ < 500)
            {
                bool needsInput = sim.Advance();
                if (needsInput) sim.SubmitIntent(sim.DefaultAutoIntent());
            }
            Assume.That(sim.State.Result, Is.EqualTo(BattleResult.Defeat),
                "Test cần thua thật để phủ đúng đường Finish() (Phase=Finished thật) — nếu hero sống sót thì kịch bản stats không còn đúng ý đồ, không phải lỗi TryReviveWithGem.");
            Assert.AreEqual(SimPhase.Finished, sim.Phase);

            Assert.IsTrue(sim.TryReviveWithGem());

            // Advance() sau khi hồi sinh phải chạy tiếp được (nhánh default của switch(Phase) tự
            // đưa Phase=Finished về TurnStart) — không throw, không kẹt ở IsFinished cũ.
            Assert.DoesNotThrow(() => sim.Advance());
        }
    }
}
