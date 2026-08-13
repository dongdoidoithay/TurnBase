using Game.Combat;
using Game.Combat.Model;
using Game.Combat.Systems;
using Game.Data;
using NUnit.Framework;

namespace Game.Tests.Combat
{
    /// <summary>
    /// Kiểm tra policy Auto 7 ưu tiên cho player (plan.md §4.16).
    /// Mỗi test dựng đúng điều kiện cho priority đó, xác nhận slot được chọn.
    /// </summary>
    public class AutoBattlePolicyTests
    {
        // ── Helper: hero đi trước (dex cao), 1 địch bình thường ──────────────

        private static CombatSimulation FastHeroSim(out CombatUnit hero, out CombatUnit enemy,
                                                     int heroSkillCount = 1)
        {
            var sim = new CombatSimulation(TestFactory.SEED);
            hero = TestFactory.Unit("hero", TeamSide.Player,
                                   stats: TestFactory.Stats(dex: 999));
            // Slot 0: basic attack (thêm trước)
            hero.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(power: 1.0f), 0));
            for (int i = 1; i < heroSkillCount; i++)
                hero.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(power: 1.5f), i));

            enemy = TestFactory.Unit("enemy", TeamSide.Enemy,
                                    stats: TestFactory.Stats(dex: 1));
            enemy.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(), 0));

            sim.AddUnit(hero);
            sim.AddUnit(enemy, TestFactory.SimpleAi());
            sim.Start();
            return sim;
        }

        // ── Priority 1: Heal khi đồng minh HP < 35% ──────────────────────────

        [Test]
        public void AutoIntent_Priority1_HealsWhenAllyLowHP()
        {
            var sim = new CombatSimulation(TestFactory.SEED);
            var hero = TestFactory.Unit("hero", TeamSide.Player, stats: TestFactory.Stats(dex: 999));
            hero.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(power: 1.0f), 0));
            hero.Skills.Add(new SkillRuntime(TestFactory.HealSkill(), 1));

            var ally = TestFactory.Unit("ally", TeamSide.Player, stats: TestFactory.Stats(dex: 1));
            ally.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(), 0));

            var enemy = TestFactory.Unit("enemy", TeamSide.Enemy, stats: TestFactory.Stats(dex: 1));
            enemy.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(), 0));

            sim.AddUnit(hero);
            sim.AddUnit(ally, TestFactory.SimpleAi());
            sim.AddUnit(enemy, TestFactory.SimpleAi());
            sim.Start();

            // Set ally HP thấp < 35%
            ally.FillResources();
            ally.SetHp((int)(ally.MaxHp * 0.30f));

            sim.Advance(); // hero đi trước
            var intent = sim.DefaultAutoIntent();

            Assert.AreEqual(1, intent.SkillSlot, "Priority 1: phải chọn slot heal (slot 1)");
            Assert.AreEqual(CommandGrade.Good, intent.Grade);
        }

        [Test]
        public void AutoIntent_Priority1_SkipsHeal_WhenNoAllyLowHP()
        {
            var sim = new CombatSimulation(TestFactory.SEED);
            var hero = TestFactory.Unit("hero", TeamSide.Player, stats: TestFactory.Stats(dex: 999));
            hero.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(power: 1.0f), 0));
            hero.Skills.Add(new SkillRuntime(TestFactory.HealSkill(), 1));

            var ally = TestFactory.Unit("ally", TeamSide.Player, stats: TestFactory.Stats(dex: 1));
            ally.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(), 0));

            var enemy = TestFactory.Unit("enemy", TeamSide.Enemy, stats: TestFactory.Stats(dex: 1));
            enemy.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(), 0));

            sim.AddUnit(hero);
            sim.AddUnit(ally, TestFactory.SimpleAi());
            sim.AddUnit(enemy, TestFactory.SimpleAi());
            sim.Start();

            // ally HP đầy — không kích hoạt priority 1
            ally.FillResources();

            sim.Advance();
            var intent = sim.DefaultAutoIntent();

            Assert.AreNotEqual(1, intent.SkillSlot,
                "Không được chọn heal khi không có đồng minh HP thấp");
        }

        // ── Priority 2: Revive khi đồng minh gục ─────────────────────────────

        [Test]
        public void AutoIntent_Priority2_RevivesWhenAllyDead()
        {
            var reviveData = new SkillData
            {
                Id = "skill_revive_test",
                Type = SkillType.Heal,
                Target = TargetMode.DeadAlly,
                RevivePercent = 0.3f,
                CommandType = ActionCommandType.SingleTap
            };

            var sim = new CombatSimulation(TestFactory.SEED);
            var hero = TestFactory.Unit("hero", TeamSide.Player, stats: TestFactory.Stats(dex: 999));
            hero.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(power: 1.0f), 0));
            hero.Skills.Add(new SkillRuntime(reviveData, 1));

            var deadAlly = TestFactory.Unit("ally", TeamSide.Player, stats: TestFactory.Stats(dex: 1));
            deadAlly.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(), 0));

            var enemy = TestFactory.Unit("enemy", TeamSide.Enemy, stats: TestFactory.Stats(dex: 1));
            enemy.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(), 0));

            sim.AddUnit(hero);
            sim.AddUnit(deadAlly, TestFactory.SimpleAi());
            sim.AddUnit(enemy, TestFactory.SimpleAi());
            sim.Start();

            // Làm chết đồng minh
            deadAlly.FillResources();
            deadAlly.SetHp(0); // IsAlive = false

            sim.Advance();
            var intent = sim.DefaultAutoIntent();

            Assert.AreEqual(1, intent.SkillSlot, "Priority 2: phải chọn slot revive (slot 1)");
        }

        // ── Priority 3: Attack địch đang Break ───────────────────────────────

        [Test]
        public void AutoIntent_Priority3_AttacksBrokenEnemy()
        {
            var sim = new CombatSimulation(TestFactory.SEED);
            var hero = TestFactory.Unit("hero", TeamSide.Player, stats: TestFactory.Stats(dex: 999));
            // Slot 0: power 1.0, Slot 1: power 2.5 (strongest)
            hero.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(power: 1.0f), 0));
            hero.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(power: 2.5f), 1));

            var enemy = TestFactory.Unit("enemy", TeamSide.Enemy, stats: TestFactory.Stats(dex: 1));
            enemy.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(), 0));

            sim.AddUnit(hero);
            sim.AddUnit(enemy, TestFactory.SimpleAi());
            sim.Start();

            // Địch đang Break
            enemy.FillResources();
            enemy.BrokenTurnsLeft = 2;

            sim.Advance();
            var intent = sim.DefaultAutoIntent();

            Assert.AreEqual(1, intent.SkillSlot,
                "Priority 3: phải chọn skill damage cao nhất khi địch Break (slot 1, power 2.5)");
        }

        // ── Priority 4: Ultimate khi đầy + ≥2 địch ───────────────────────────

        [Test]
        public void AutoIntent_Priority4_UsesUltimate_WhenReadyAndMultipleEnemies()
        {
            var sim = new CombatSimulation(TestFactory.SEED);
            var hero = TestFactory.Unit("hero", TeamSide.Player, stats: TestFactory.Stats(dex: 999));
            hero.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(power: 1.0f), 0));
            // Slot 4: ultimate (power 2.0)
            var ultData = TestFactory.BasicAttack(power: 2.0f);
            hero.Skills.Add(new SkillRuntime(ultData, 4));

            var e1 = TestFactory.Unit("e1", TeamSide.Enemy, stats: TestFactory.Stats(dex: 1));
            e1.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(), 0));
            var e2 = TestFactory.Unit("e2", TeamSide.Enemy, stats: TestFactory.Stats(dex: 1));
            e2.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(), 0));

            sim.AddUnit(hero);
            sim.AddUnit(e1, TestFactory.SimpleAi());
            sim.AddUnit(e2, TestFactory.SimpleAi());
            sim.Start();

            // Kích hoạt Ultimate
            hero.FillResources();
            e1.FillResources();
            e2.FillResources();
            sim.State.UltimateGauge = BattleState.ULTIMATE_MAX;

            sim.Advance();
            var intent = sim.DefaultAutoIntent();

            Assert.AreEqual(4, intent.SkillSlot,
                "Priority 4: phải chọn ultimate (slot 4) khi đầy và ≥2 địch");
        }

        [Test]
        public void AutoIntent_Priority4_SkipsUltimate_WhenOnlyOneEnemy()
        {
            var sim = new CombatSimulation(TestFactory.SEED);
            var hero = TestFactory.Unit("hero", TeamSide.Player, stats: TestFactory.Stats(dex: 999));
            hero.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(power: 1.0f), 0));
            hero.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(power: 2.0f), 4));

            var enemy = TestFactory.Unit("enemy", TeamSide.Enemy, stats: TestFactory.Stats(dex: 1));
            enemy.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(), 0));

            sim.AddUnit(hero);
            sim.AddUnit(enemy, TestFactory.SimpleAi());
            sim.Start();

            hero.FillResources();
            enemy.FillResources();
            sim.State.UltimateGauge = BattleState.ULTIMATE_MAX;

            sim.Advance();
            var intent = sim.DefaultAutoIntent();

            Assert.AreNotEqual(4, intent.SkillSlot,
                "Không dùng ultimate khi chỉ có 1 địch (AoE lãng phí)");
        }

        [Test]
        public void AutoIntent_Priority4_SkipsUltimate_WhenNotReady()
        {
            var sim = new CombatSimulation(TestFactory.SEED);
            var hero = TestFactory.Unit("hero", TeamSide.Player, stats: TestFactory.Stats(dex: 999));
            hero.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(power: 1.0f), 0));
            hero.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(power: 2.0f), 4));

            var e1 = TestFactory.Unit("e1", TeamSide.Enemy, stats: TestFactory.Stats(dex: 1));
            e1.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(), 0));
            var e2 = TestFactory.Unit("e2", TeamSide.Enemy, stats: TestFactory.Stats(dex: 1));
            e2.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(), 0));

            sim.AddUnit(hero);
            sim.AddUnit(e1, TestFactory.SimpleAi());
            sim.AddUnit(e2, TestFactory.SimpleAi());
            sim.Start();

            hero.FillResources();
            e1.FillResources();
            e2.FillResources();
            sim.State.UltimateGauge = 0; // chưa đầy

            sim.Advance();
            var intent = sim.DefaultAutoIntent();

            Assert.AreNotEqual(4, intent.SkillSlot,
                "Không dùng ultimate khi gauge chưa đầy");
        }

        // ── Priority 5: Skill khắc chế element ───────────────────────────────

        [Test]
        public void AutoIntent_Priority5_UsesElementCounter()
        {
            var sim = new CombatSimulation(TestFactory.SEED);
            var hero = TestFactory.Unit("hero", TeamSide.Player, stats: TestFactory.Stats(dex: 999));
            // Slot 0: Neutral (không khắc chế), Slot 1: Fire (strong vs. Wind enemy)
            hero.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(element: Element.Neutral, power: 1.5f), 0));
            hero.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(element: Element.Fire, power: 1.0f), 1));

            // Wind enemy: Fire khắc chế Wind (ElementTable.IsStrong(Fire, Wind) = true)
            var enemy = TestFactory.Unit("enemy", TeamSide.Enemy, element: Element.Wind,
                                         stats: TestFactory.Stats(dex: 1));
            enemy.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(), 0));

            sim.AddUnit(hero);
            sim.AddUnit(enemy, TestFactory.SimpleAi());
            sim.Start();

            hero.FillResources();
            enemy.FillResources();

            sim.Advance();
            var intent = sim.DefaultAutoIntent();

            Assert.AreEqual(1, intent.SkillSlot,
                "Priority 5: phải chọn skill Fire (slot 1) khắc chế địch Wind dù power thấp hơn slot 0");
        }

        // ── Priority 6: Damage cao nhất ───────────────────────────────────────

        [Test]
        public void AutoIntent_Priority6_HighestDamageSkill()
        {
            var sim = new CombatSimulation(TestFactory.SEED);
            var hero = TestFactory.Unit("hero", TeamSide.Player, stats: TestFactory.Stats(dex: 999));
            // Slot 0: power 1.0, Slot 1: power 2.0 (mạnh hơn, không phải ultimate)
            hero.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(power: 1.0f), 0));
            hero.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(power: 2.0f), 1));

            var enemy = TestFactory.Unit("enemy", TeamSide.Enemy, stats: TestFactory.Stats(dex: 1));
            enemy.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(), 0));

            sim.AddUnit(hero);
            sim.AddUnit(enemy, TestFactory.SimpleAi());
            sim.Start();

            hero.FillResources();
            enemy.FillResources();

            sim.Advance();
            var intent = sim.DefaultAutoIntent();

            Assert.AreEqual(1, intent.SkillSlot,
                "Priority 6: phải chọn skill mạnh nhất (slot 1, power 2.0)");
        }

        // ── Priority 7: Đánh thường khi không có gì khác ─────────────────────

        [Test]
        public void AutoIntent_Priority7_FallsBackToBasicAttack()
        {
            // Hero chỉ có slot 0 (basic attack) — tất cả priority 1-6 fail
            var sim = FastHeroSim(out var hero, out var enemy);
            hero.FillResources();
            enemy.FillResources();

            sim.Advance();
            var intent = sim.DefaultAutoIntent();

            Assert.AreEqual(0, intent.SkillSlot, "Priority 7: fallback về slot 0 (đánh thường)");
        }

        // ── Grade luôn là Good ────────────────────────────────────────────────

        [Test]
        public void AutoIntent_Grade_IsAlwaysGood()
        {
            var sim = FastHeroSim(out var hero, out var enemy);
            hero.FillResources();
            enemy.FillResources();

            sim.Advance();
            var intent = sim.DefaultAutoIntent();

            Assert.AreEqual(CommandGrade.Good, intent.Grade,
                "Auto-battle luôn dùng CommandGrade.Good (plan.md §4.16)");
        }

        // ── Priority order: Heal > Break ──────────────────────────────────────

        [Test]
        public void AutoIntent_HealPriority_OverBreakPriority()
        {
            // Cả 2 điều kiện priority 1 (ally low HP) và priority 3 (enemy broken) cùng đúng
            // Priority 1 phải thắng
            var sim = new CombatSimulation(TestFactory.SEED);
            var hero = TestFactory.Unit("hero", TeamSide.Player, stats: TestFactory.Stats(dex: 999));
            hero.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(power: 2.0f), 0));
            hero.Skills.Add(new SkillRuntime(TestFactory.HealSkill(), 1));

            var ally = TestFactory.Unit("ally", TeamSide.Player, stats: TestFactory.Stats(dex: 1));
            ally.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(), 0));

            var enemy = TestFactory.Unit("enemy", TeamSide.Enemy, stats: TestFactory.Stats(dex: 1));
            enemy.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(), 0));

            sim.AddUnit(hero);
            sim.AddUnit(ally, TestFactory.SimpleAi());
            sim.AddUnit(enemy, TestFactory.SimpleAi());
            sim.Start();

            ally.FillResources();
            ally.SetHp((int)(ally.MaxHp * 0.20f)); // low HP < 35%
            enemy.FillResources();
            enemy.BrokenTurnsLeft = 2;             // enemy cũng broken

            sim.Advance();
            var intent = sim.DefaultAutoIntent();

            Assert.AreEqual(1, intent.SkillSlot,
                "Priority 1 (heal) phải thắng priority 3 (attack broken)");
        }

        // ── Skill on cooldown bị bỏ qua ──────────────────────────────────────

        [Test]
        public void AutoIntent_SkipsSkill_WhenOnCooldown()
        {
            var sim = new CombatSimulation(TestFactory.SEED);
            var hero = TestFactory.Unit("hero", TeamSide.Player, stats: TestFactory.Stats(dex: 999));
            // Slot 0: power 1.0, Slot 1: power 3.0 nhưng cooldown đang 2
            var strongSkillData = TestFactory.BasicAttack(power: 3.0f, cooldown: 2);
            var strongSkill = new SkillRuntime(strongSkillData, 1);
            strongSkill.CooldownLeft = 2; // đang cooldown

            hero.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(power: 1.0f), 0));
            hero.Skills.Add(strongSkill);

            var enemy = TestFactory.Unit("enemy", TeamSide.Enemy, stats: TestFactory.Stats(dex: 1));
            enemy.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(), 0));

            sim.AddUnit(hero);
            sim.AddUnit(enemy, TestFactory.SimpleAi());
            sim.Start();

            hero.FillResources();
            enemy.FillResources();

            sim.Advance();
            var intent = sim.DefaultAutoIntent();

            Assert.AreNotEqual(1, intent.SkillSlot,
                "Phải bỏ qua skill đang cooldown, dù power cao hơn");
        }
    }
}
