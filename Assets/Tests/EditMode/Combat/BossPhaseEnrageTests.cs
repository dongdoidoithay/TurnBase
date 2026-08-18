using Game.Combat;
using Game.Combat.Events;
using Game.Combat.Model;
using Game.Data;
using NUnit.Framework;

namespace Game.Tests.Combat
{
    /// <summary>CombatSimulation.RefreshBossPhases/RefreshBossEnrage — task-boss-phase-enrage.md,
    /// plan.md §4.13.3. Cả 2 hàm private nên test qua hiệu ứng quan sát được (Events/Phase/Stats),
    /// đúng mẫu integration test đã dùng cho GoldenScenarioTests.</summary>
    public class BossPhaseEnrageTests
    {
        private static CombatSimulation BuildBossDuel(out CombatUnit hero, out CombatUnit boss, bool bossIsBoss = true)
        {
            var sim = new CombatSimulation(TestFactory.SEED, turnLimit: 2000);

            hero = TestFactory.Unit("hero", TeamSide.Player, stats: TestFactory.Stats(str: 30, con: 30));
            hero.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(power: 1f, poiseDmg: 0), 0));

            // HP pool cực lớn — bất kỳ đòn thường nào cũng chỉ là % nhỏ, không rủi ro giết nhầm
            // giữa lúc test đang canh preset Hp thủ công (SetHp) để mô phỏng ngưỡng phase.
            boss = TestFactory.Unit("boss", TeamSide.Enemy, stats: TestFactory.Stats(str: 5, con: 3000));
            boss.IsBoss = bossIsBoss;
            boss.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(power: 0.01f, poiseDmg: 0), 0));

            sim.AddUnit(hero);
            sim.AddUnit(boss, TestFactory.SimpleAi());
            sim.Start();
            return sim;
        }

        /// <summary>Chạy tới khi hero ra 1 đòn TRÚNG THẬT (bỏ qua miss — cơ chế ACC/EVA có xác suất
        /// trượt, không nên là nguồn flaky của test này). Dùng để trigger lại RefreshBossPhases sau
        /// khi đã preset <c>boss.Hp</c> thủ công về đúng % mong muốn, tách hẳn khỏi công thức sát
        /// thương thật (không cần biết chính xác 1 đòn gây bao nhiêu dmg).</summary>
        private static void PlayUntilHeroLandsHit(CombatSimulation sim, CombatUnit hero, CombatUnit target)
        {
            int hpBefore = target.Hp;
            int guard = 0;
            while (!sim.IsFinished && guard++ < 500)
            {
                bool waiting = sim.Advance();
                if (!waiting) break;
                var actorId = sim.CurrentActor.Id;
                var targetId = actorId == hero.Id ? target.Id : hero.Id;
                var intent = new ActionIntent(actorId, 0, targetId);
                sim.SubmitIntent(intent);
                if (actorId == hero.Id && target.Hp < hpBefore) return; // trúng thật, dừng
                if (actorId == hero.Id) hpBefore = target.Hp;
            }
        }

        [Test]
        public void HpBelow30Pct_JumpsDirectlyToPhase3_ResetsPoise_EmitsOnePhaseChanged()
        {
            var sim = BuildBossDuel(out var hero, out var boss);
            boss.SetHp((int)(boss.MaxHp * 0.25f)); // đã dưới 30% TRƯỚC khi đòn tới
            boss.Poise = 1; // gần Break, để xác nhận reset thật sau khi đổi phase

            PlayUntilHeroLandsHit(sim, hero, boss);

            Assert.AreEqual(3, boss.Phase, "HP dưới 30% → nhảy thẳng phase 3 (bỏ qua phase 2)");
            Assert.AreEqual(boss.PoiseMax, boss.Poise, "Phase mới phải reset Poise đầy");

            int phaseChangedCount = 0;
            foreach (var e in sim.Events.All)
                if (e.Type == CombatEventType.PhaseChanged && e.SourceUnitId == boss.Id)
                {
                    phaseChangedCount++;
                    Assert.AreEqual(3, e.IntValue);
                }
            Assert.AreEqual(1, phaseChangedCount, "chỉ 1 event dù nhảy qua cả 2 mốc cùng lúc");
        }

        [Test]
        public void HpBelow60PctOnly_MovesToPhase2Only()
        {
            var sim = BuildBossDuel(out var hero, out var boss);
            boss.SetHp((int)(boss.MaxHp * 0.45f)); // dưới 60% nhưng vẫn trên 30%

            PlayUntilHeroLandsHit(sim, hero, boss);

            Assert.AreEqual(2, boss.Phase);
        }

        [Test]
        public void HpAboveBothThresholds_StaysPhase1_NoEvent()
        {
            var sim = BuildBossDuel(out var hero, out var boss);
            boss.SetHp((int)(boss.MaxHp * 0.95f)); // trên cả 2 ngưỡng, 1 đòn chip nhỏ không đủ chạm 60%

            PlayUntilHeroLandsHit(sim, hero, boss);

            Assert.AreEqual(1, boss.Phase);
            foreach (var e in sim.Events.All)
                Assert.AreNotEqual(CombatEventType.PhaseChanged, e.Type);
        }

        [Test]
        public void NonBossUnit_NeverGetsPhaseTracking_EvenAtLowHp()
        {
            var sim = BuildBossDuel(out var hero, out var grunt, bossIsBoss: false); // lính thường
            grunt.SetHp((int)(grunt.MaxHp * 0.05f)); // gần chết

            PlayUntilHeroLandsHit(sim, hero, grunt);

            Assert.AreEqual(1, grunt.Phase, "không phải boss thì Phase không bao giờ đổi");
        }

        [Test]
        public void EnrageRound_Reached_AppliesStackingAtkSpdBuff()
        {
            var sim = BuildBossDuel(out var hero, out var boss);
            sim.State.EnrageRound = 2; // rút ngắn cho test nhanh

            float baselineAtk = boss.Stats.AtkPhys;

            int guard = 0;
            while (sim.State.RoundNumber < 6 && !sim.IsFinished && guard++ < 2000)
            {
                bool waiting = sim.Advance();
                if (!waiting) break;
                var actorId = sim.CurrentActor.Id;
                var targetId = actorId == hero.Id ? boss.Id : hero.Id;
                sim.SubmitIntent(new ActionIntent(actorId, 0, targetId));
            }

            Assert.GreaterOrEqual(boss.EnrageStacks, 1, "qua EnrageRound thì phải có ít nhất 1 nấc Enrage");
            Assert.Greater(boss.Stats.AtkPhys, baselineAtk, "Enrage phải thật sự tăng ATK hiệu dụng");

            bool sawEnrageEvent = false;
            foreach (var e in sim.Events.All)
                if (e.Type == CombatEventType.Enraged && e.SourceUnitId == boss.Id) sawEnrageEvent = true;
            Assert.IsTrue(sawEnrageEvent);
        }

        [Test]
        public void BeforeEnrageRound_NoBuffApplied()
        {
            var sim = BuildBossDuel(out var hero, out var boss);
            sim.State.EnrageRound = 999; // không bao giờ tới trong phạm vi test

            int guard = 0;
            while (sim.State.RoundNumber < 3 && !sim.IsFinished && guard++ < 1000)
            {
                bool waiting = sim.Advance();
                if (!waiting) break;
                var actorId = sim.CurrentActor.Id;
                var targetId = actorId == hero.Id ? boss.Id : hero.Id;
                sim.SubmitIntent(new ActionIntent(actorId, 0, targetId));
            }

            Assert.AreEqual(0, boss.EnrageStacks);
        }
    }
}
