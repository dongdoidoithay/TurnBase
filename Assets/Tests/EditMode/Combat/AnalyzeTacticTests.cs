using Game.Combat;
using Game.Combat.Model;
using Game.Data;
using NUnit.Framework;

namespace Game.Tests.Combat
{
    /// <summary>
    /// Kiểm tra Analyze tactic (plan.md §5.6): cost 5 SP, reveal stat địch,
    /// ghi vào BattleState.AnalyzedEnemyIds (vĩnh viễn trong trận).
    /// </summary>
    public class AnalyzeTacticTests
    {
        // Dựng sim với hero SPD cao đảm bảo hero đi trước. Stats() mặc định int=10 →
        // MaxSp = 20 + 10*3 = 50, FillResources() nạp đầy nên hero.Sp = 50 sau Start().
        private static CombatSimulation FastHeroSim(out CombatUnit hero, out CombatUnit enemy)
        {
            var sim = new CombatSimulation(TestFactory.SEED);
            hero = TestFactory.Unit("hero", TeamSide.Player,
                                   stats: TestFactory.Stats(dex: 999));
            hero.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(), 0));

            enemy = TestFactory.Unit("enemy", TeamSide.Enemy,
                                     stats: TestFactory.Stats(dex: 1));
            enemy.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(), 0));

            sim.AddUnit(hero);
            sim.AddUnit(enemy, TestFactory.SimpleAi());
            sim.Start();
            return sim;
        }

        [Test]
        public void Analyze_LowSp_NoMark()
        {
            var sim = FastHeroSim(out var hero, out _);
            sim.Advance();  // hero đi trước (dex=999)
            hero.SetSp(4);

            sim.SubmitIntent(new ActionIntent(hero.Id, 0, -1, isAnalyze: true));

            Assert.AreEqual(0, sim.State.AnalyzedEnemyIds.Count,
                "Không đủ 5 SP → không đánh dấu địch");
            Assert.AreEqual(4, hero.Sp, "SP không thay đổi khi không đủ để Analyze");
        }

        [Test]
        public void Analyze_SufficientSp_MarksEnemyAndDeductsSp()
        {
            var sim = FastHeroSim(out var hero, out var enemy);
            sim.Advance();  // hero đi trước
            hero.SetSp(10);

            sim.SubmitIntent(new ActionIntent(hero.Id, 0, -1, isAnalyze: true));

            Assert.AreEqual(1, sim.State.AnalyzedEnemyIds.Count, "Đúng 1 địch bị đánh dấu");
            Assert.IsTrue(sim.State.AnalyzedEnemyIds.Contains(enemy.Id),
                "ID địch có trong AnalyzedEnemyIds");
            Assert.AreEqual(5, hero.Sp, "Trừ đúng 5 SP (10-5=5)");
        }

        [Test]
        public void Analyze_SameEnemyTwice_SetDeduplicates()
        {
            var sim = FastHeroSim(out var hero, out var enemy);

            // Lượt 1: hero analyze
            sim.Advance();
            hero.SetSp(20);
            sim.SubmitIntent(new ActionIntent(hero.Id, 0, -1, isAnalyze: true));
            Assert.AreEqual(1, sim.State.AnalyzedEnemyIds.Count);
            Assert.IsTrue(sim.State.AnalyzedEnemyIds.Contains(enemy.Id));

            // Địch đi, hero lấy lượt lần 2 (turn-start SpRegen tự hồi lại một phần SP, không
            // liên quan tới hành vi Analyze cần kiểm — set lại SP cố định để tách biệt).
            sim.Advance();  // địch đi
            sim.Advance();  // hero lấy lượt lần 2
            hero.SetSp(20);

            // Lượt 2: analyze lại cùng địch → HashSet không thêm trùng
            sim.SubmitIntent(new ActionIntent(hero.Id, 0, -1, isAnalyze: true));
            Assert.AreEqual(1, sim.State.AnalyzedEnemyIds.Count,
                "HashSet không tạo bản sao — analyze cùng địch 2 lần vẫn count=1");
            Assert.AreEqual(15, hero.Sp, "Đã trừ đúng 5 SP (20-5=15)");
        }
    }
}
