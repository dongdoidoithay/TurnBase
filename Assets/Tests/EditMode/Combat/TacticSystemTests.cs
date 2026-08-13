using Game.Combat;
using Game.Combat.Model;
using Game.Combat.Systems;
using Game.Core.Random;
using Game.Data;
using NUnit.Framework;

namespace Game.Tests.Combat
{
    /// <summary>
    /// Kiểm tra hàng TACTIC (plan.md §5.6): Guard / Escape / SwapRow / Focus.
    /// Guard + Escape: engine đã có, test chỉ xác nhận API mới không break.
    /// SwapRow + Focus: engine mới — test chi tiết.
    /// </summary>
    public class TacticSystemTests
    {
        // Dựng sim với hero SPD cao đảm bảo hero đi trước
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

        // ── SwapRow ──────────────────────────────────────────────────────────

        [Test]
        public void SwapRow_FrontHero_BecomesBack()
        {
            var sim = FastHeroSim(out var hero, out _);
            Assert.AreEqual(Row.Front, hero.Row);

            sim.Advance();  // hero goes first
            sim.SubmitIntent(new ActionIntent(hero.Id, 0, -1, isSwapRow: true));

            Assert.AreEqual(Row.Back, hero.Row);
        }

        [Test]
        public void SwapRow_BackHero_BecomesFirst()
        {
            var sim = new CombatSimulation(TestFactory.SEED);
            var hero = TestFactory.Unit("hero", TeamSide.Player, row: Row.Back,
                                       stats: TestFactory.Stats(dex: 999));
            hero.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(), 0));
            var enemy = TestFactory.Unit("enemy", TeamSide.Enemy, stats: TestFactory.Stats(dex: 1));
            enemy.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(), 0));
            sim.AddUnit(hero);
            sim.AddUnit(enemy, TestFactory.SimpleAi());
            sim.Start();

            sim.Advance();
            sim.SubmitIntent(new ActionIntent(hero.Id, 0, -1, isSwapRow: true));

            Assert.AreEqual(Row.Front, hero.Row);
        }

        [Test]
        public void SwapRow_DoesNotEndTurn_HeroCanStillAct()
        {
            var sim = FastHeroSim(out var hero, out _);

            bool needsInput = sim.Advance();
            Assert.IsTrue(needsInput, "Hero should go first");

            sim.SubmitIntent(new ActionIntent(hero.Id, 0, -1, isSwapRow: true));

            // Phase phải vẫn là AwaitInput — hero chưa kết thúc lượt
            Assert.AreEqual(SimPhase.AwaitInput, sim.Phase,
                "SwapRow không kết thúc lượt — hero vẫn ở AwaitInput");
        }

        [Test]
        public void SwapRow_SetsHasSwappedFlag()
        {
            var sim = FastHeroSim(out var hero, out _);

            sim.Advance();
            Assert.IsFalse(hero.HasSwappedRowThisTurn);

            sim.SubmitIntent(new ActionIntent(hero.Id, 0, -1, isSwapRow: true));

            Assert.IsTrue(hero.HasSwappedRowThisTurn);
        }

        [Test]
        public void SwapRow_SecondCall_Ignored()
        {
            var sim = FastHeroSim(out var hero, out _);
            sim.Advance();

            // Lần 1: Front → Back
            sim.SubmitIntent(new ActionIntent(hero.Id, 0, -1, isSwapRow: true));
            Assert.AreEqual(Row.Back, hero.Row);

            // Lần 2: bị ignore (đã swapped), hàng vẫn là Back
            sim.SubmitIntent(new ActionIntent(hero.Id, 0, -1, isSwapRow: true));
            Assert.AreEqual(Row.Back, hero.Row, "Lần 2 SwapRow phải bị bỏ qua");
        }

        [Test]
        public void SwapRow_FlagResetsNextTurn()
        {
            var sim = FastHeroSim(out var hero, out var enemy);
            sim.Advance();

            // Turn 1: SwapRow rồi kết thúc lượt bằng skill
            sim.SubmitIntent(new ActionIntent(hero.Id, 0, -1, isSwapRow: true));
            int targetId = enemy.Id;
            sim.SubmitIntent(new ActionIntent(hero.Id, 0, targetId)); // skill slot 0

            // Advance hết lượt địch, tới lượt hero kế tiếp
            int guard = 0;
            while (!sim.IsFinished && !sim.NeedsPlayerInput && guard++ < 20)
                sim.Advance();

            if (sim.NeedsPlayerInput && sim.CurrentActor?.Id == hero.Id)
                Assert.IsFalse(hero.HasSwappedRowThisTurn,
                    "HasSwappedRowThisTurn phải reset khi bắt đầu lượt mới");
        }

        // ── Focus ─────────────────────────────────────────────────────────────

        [Test]
        public void Focus_AppliesFocusStatus()
        {
            var sim = FastHeroSim(out var hero, out _);

            sim.Advance();
            sim.SubmitIntent(new ActionIntent(hero.Id, 0, -1, isFocus: true));

            // Focus phải còn active sau khi FinishTurn (duration 2→1)
            Assert.IsTrue(hero.HasStatus(StatusId.Focus),
                "Focus status phải còn active sau turn dùng Focus (duration 2 → 1 sau TickTurnEnd)");
        }

        [Test]
        public void Focus_EndsTurn()
        {
            var sim = FastHeroSim(out var hero, out _);

            sim.Advance();
            sim.SubmitIntent(new ActionIntent(hero.Id, 0, -1, isFocus: true));

            Assert.AreNotEqual(SimPhase.AwaitInput, sim.Phase,
                "Focus kết thúc lượt — Phase không được là AwaitInput sau Focus");
        }

        [Test]
        public void Focus_ForcesCritInDamageCalculator()
        {
            // dex=60 → Acc cao → luôn hit; Focus → luôn crit với mọi seed
            var attacker = TestFactory.Unit("a", stats: TestFactory.Stats(dex: 60));
            attacker.Statuses.Add(new StatusInstance(StatusId.Focus, 1, 2, -1));
            attacker.FillResources();

            var defender = TestFactory.Unit("d", TeamSide.Enemy);
            defender.FillResources();
            var skill = new SkillRuntime(TestFactory.BasicAttack(dmgType: DamageType.Physical), 0);

            // Test nhiều seed để đảm bảo Focus force-crit là deterministic, không phụ thuộc RNG
            for (ulong seed = 1; seed <= 10; seed++)
            {
                var r = DamageCalculator.Calculate(attacker, defender, skill,
                                                   CommandGrade.Good, new XorShiftRandom(seed));
                if (r.IsMiss) continue;  // bảo vệ khỏi edge case hit, không expect ở dex=60
                Assert.IsTrue(r.IsCrit, $"Focus phải force crit ở mọi seed ({seed})");
            }
        }

        // ── Guard + Escape (xác nhận API mới không break engine cũ) ─────────

        [Test]
        public void Guard_ViaIsGuard_EndsTurn()
        {
            var sim = FastHeroSim(out var hero, out _);

            sim.Advance();
            sim.SubmitIntent(new ActionIntent(hero.Id, 0, -1, isGuard: true));

            Assert.AreNotEqual(SimPhase.AwaitInput, sim.Phase,
                "Guard phải kết thúc lượt");
        }

        [Test]
        public void Guard_GrantsSP()
        {
            var sim = FastHeroSim(out var hero, out _);
            hero.FillResources();
            hero.SetSp(0);  // drain SP để có chỗ tăng
            int spBefore = hero.Sp;

            sim.Advance();
            sim.SubmitIntent(new ActionIntent(hero.Id, 0, -1, isGuard: true));

            Assert.Greater(hero.Sp, spBefore, "Guard phải hồi SP");
        }

        [Test]
        public void Escape_WhenAllowed_Succeeds()
        {
            var sim = FastHeroSim(out var hero, out _);
            sim.State.AllowEscape = true;

            sim.Advance();
            sim.SubmitIntent(new ActionIntent(hero.Id, 0, -1, isEscape: true));

            Assert.AreEqual(BattleResult.Escaped, sim.State.Result,
                "Escape khi AllowEscape=true phải kết thúc trận với Escaped");
        }

        [Test]
        public void Escape_WhenDisallowed_DoesNotEscape()
        {
            var sim = FastHeroSim(out var hero, out _);
            sim.State.AllowEscape = false;

            sim.Advance();
            sim.SubmitIntent(new ActionIntent(hero.Id, 0, -1, isEscape: true));

            Assert.AreNotEqual(BattleResult.Escaped, sim.State.Result,
                "Escape khi AllowEscape=false (Boss) không được thoát");
        }
    }
}
