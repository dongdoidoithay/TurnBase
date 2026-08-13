using Game.Combat;
using Game.Combat.Model;
using Game.Core.Maths;
using Game.Data;
using NUnit.Framework;

namespace Game.Tests.Combat
{
    /// <summary>ItemResolver + CombatSimulation item wiring — task-consumable-items.md. Test qua
    /// API công khai <c>CombatSimulation.SubmitIntent</c> (cùng cách SimulationTests test Guard/
    /// Escape), không tự dựng ItemResolver riêng — field nội bộ CombatSimulation không public.</summary>
    public class ItemResolverTests
    {
        private static CombatSimulation TwoAllyDuel(out CombatUnit hero1, out CombatUnit hero2,
            out CombatUnit enemy, ulong seed = TestFactory.SEED)
        {
            var sim = new CombatSimulation(seed);
            hero1 = TestFactory.Unit("hero1", TeamSide.Player);
            hero1.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(), 0));
            hero2 = TestFactory.Unit("hero2", TeamSide.Player);
            hero2.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(), 0));
            enemy = TestFactory.Unit("enemy", TeamSide.Enemy);
            enemy.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(), 0));

            sim.AddUnit(hero1);
            sim.AddUnit(hero2);
            sim.AddUnit(enemy, TestFactory.SimpleAi());
            sim.Start();
            return sim;
        }

        // ---------- Potion ----------

        [Test]
        public void Potion_HealsLowestHpPercentAlly_By35PercentOfTargetMaxHp()
        {
            var sim = TwoAllyDuel(out var hero1, out var hero2, out _);
            hero1.SetHp(hero1.MaxHp); // đầy
            hero2.SetHp(1); // gần chết — phải được chọn
            sim.State.ItemLoadout[ItemType.Potion] = 3;

            sim.Advance();
            sim.SubmitIntent(new ActionIntent(hero1.Id, (int)ItemType.Potion, -1, CommandGrade.Miss, isUseItem: true));

            int expected = 1 + GameMath.FloorToInt(hero2.MaxHp * 0.35f);
            Assert.AreEqual(expected, hero2.Hp, "Potion phải hồi 35% MaxHP của TARGET (hero2 máu thấp nhất), không đổi theo actor");
            Assert.AreEqual(hero1.MaxHp, hero1.Hp, "hero1 (actor, đầy máu) không phải người được hồi");
            Assert.AreEqual(2, sim.State.ItemLoadout[ItemType.Potion], "Phải trừ đúng 1 sau khi dùng");
            Assert.AreEqual(1, sim.State.ItemsUsed[ItemType.Potion]);
        }

        [Test]
        public void Potion_NotInLoadout_DoesNothing_TurnStillEnds()
        {
            var sim = TwoAllyDuel(out var hero1, out var hero2, out _);
            hero2.SetHp(1);
            // Không set ItemLoadout — không mang Potion.

            sim.Advance();
            sim.SubmitIntent(new ActionIntent(hero1.Id, (int)ItemType.Potion, -1, CommandGrade.Miss, isUseItem: true));

            Assert.AreEqual(1, hero2.Hp, "Không mang item thì không có gì xảy ra");
            Assert.IsFalse(sim.IsFinished);
        }

        // ---------- Ether ----------

        [Test]
        public void Ether_RestoresLowestSpAlly_By40()
        {
            var sim = TwoAllyDuel(out var hero1, out var hero2, out _);
            hero1.SetSp(hero1.MaxSp);
            hero2.SetSp(0);
            sim.State.ItemLoadout[ItemType.Ether] = 1;

            sim.Advance();
            sim.SubmitIntent(new ActionIntent(hero1.Id, (int)ItemType.Ether, -1, CommandGrade.Miss, isUseItem: true));

            Assert.AreEqual(40, hero2.Sp, "Ether hồi flat 40 SP cho ally SP thấp nhất");
            Assert.AreEqual(0, sim.State.ItemLoadout[ItemType.Ether]);
        }

        // ---------- Antidote ----------

        [Test]
        public void Antidote_CleansesDotOnly_NotOtherDebuffs()
        {
            var sim = TwoAllyDuel(out var hero1, out var hero2, out _);
            hero2.Statuses.Add(new StatusInstance(StatusId.Burn, 1, 3, hero1.Id));
            hero2.Statuses.Add(new StatusInstance(StatusId.AtkDown, 1, 3, hero1.Id));
            sim.State.ItemLoadout[ItemType.Antidote] = 1;

            sim.Advance();
            sim.SubmitIntent(new ActionIntent(hero1.Id, (int)ItemType.Antidote, -1, CommandGrade.Miss, isUseItem: true));

            Assert.IsFalse(hero2.HasStatus(StatusId.Burn), "DoT phải bị cleanse");
            Assert.IsTrue(hero2.HasStatus(StatusId.AtkDown), "Debuff KHÔNG phải DoT không được đụng tới");
            Assert.AreEqual(0, sim.State.ItemLoadout[ItemType.Antidote]);
        }

        [Test]
        public void Antidote_NoDotPresent_Fails_DoesNotConsumeItem()
        {
            var sim = TwoAllyDuel(out var hero1, out var hero2, out _);
            hero2.Statuses.Add(new StatusInstance(StatusId.AtkDown, 1, 3, hero1.Id)); // không phải DoT
            sim.State.ItemLoadout[ItemType.Antidote] = 1;

            sim.Advance();
            sim.SubmitIntent(new ActionIntent(hero1.Id, (int)ItemType.Antidote, -1, CommandGrade.Miss, isUseItem: true));

            Assert.AreEqual(1, sim.State.ItemLoadout[ItemType.Antidote], "Không có DoT nào để cleanse thì không tốn item");
            Assert.IsFalse(sim.State.ItemsUsed.ContainsKey(ItemType.Antidote));
        }

        // ---------- Smoke Bomb ----------

        [Test]
        public void SmokeBomb_AlwaysEscapes_EvenWhenSlowerThanEnemy()
        {
            var sim = new CombatSimulation(TestFactory.SEED);
            var hero = TestFactory.Unit("hero", TeamSide.Player, stats: TestFactory.Stats(dex: 1));
            hero.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(), 0));
            var enemy = TestFactory.Unit("enemy", TeamSide.Enemy, stats: TestFactory.Stats(dex: 100));
            enemy.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(), 0));
            sim.AddUnit(hero);
            sim.AddUnit(enemy, TestFactory.SimpleAi());
            sim.Start();
            sim.State.ItemLoadout[ItemType.SmokeBomb] = 1;

            sim.Advance();
            sim.SubmitIntent(new ActionIntent(hero.Id, (int)ItemType.SmokeBomb, -1, CommandGrade.Miss, isUseItem: true));

            Assert.AreEqual(BattleResult.Escaped, sim.State.Result, "Smoke Bomb luôn thoát 100%, không phụ thuộc SPD như TryEscape thường");
        }

        // ---------- Revive Feather ----------

        [Test]
        public void ReviveFeather_RevivesDeadAlly_At40PercentMaxHp()
        {
            var sim = TwoAllyDuel(out var hero1, out var hero2, out _);
            hero2.SetHp(0); // gục
            Assert.IsTrue(hero2.IsDead);
            sim.State.ItemLoadout[ItemType.ReviveFeather] = 1;

            sim.Advance();
            sim.SubmitIntent(new ActionIntent(hero1.Id, (int)ItemType.ReviveFeather, -1, CommandGrade.Miss, isUseItem: true));

            Assert.IsTrue(hero2.IsAlive);
            int expected = GameMath.Max(1, GameMath.FloorToInt(hero2.MaxHp * 0.4f));
            Assert.AreEqual(expected, hero2.Hp);
            Assert.AreEqual(0, sim.State.ItemLoadout[ItemType.ReviveFeather]);
        }

        [Test]
        public void ReviveFeather_NoDeadAlly_Fails_DoesNotConsumeItem()
        {
            var sim = TwoAllyDuel(out var hero1, out var hero2, out _);
            sim.State.ItemLoadout[ItemType.ReviveFeather] = 1;

            sim.Advance();
            sim.SubmitIntent(new ActionIntent(hero1.Id, (int)ItemType.ReviveFeather, -1, CommandGrade.Miss, isUseItem: true));

            Assert.AreEqual(1, sim.State.ItemLoadout[ItemType.ReviveFeather]);
            Assert.IsTrue(hero2.IsAlive);
        }

        // ---------- Elemental Bomb ----------

        [Test]
        public void ElementalBomb_DamagesAllAliveEnemies_AndReducesPoise()
        {
            var sim = new CombatSimulation(TestFactory.SEED);
            var hero = TestFactory.Unit("hero", TeamSide.Player);
            hero.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(), 0));
            var enemy1 = TestFactory.Unit("enemy1", TeamSide.Enemy, poiseMax: 50);
            enemy1.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(), 0));
            var enemy2 = TestFactory.Unit("enemy2", TeamSide.Enemy, poiseMax: 50);
            enemy2.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(), 0));
            sim.AddUnit(hero);
            sim.AddUnit(enemy1, TestFactory.SimpleAi());
            sim.AddUnit(enemy2, TestFactory.SimpleAi());
            sim.Start();
            sim.State.ItemLoadout[ItemType.ElementalBomb] = 1;

            int hp1Before = enemy1.Hp, hp2Before = enemy2.Hp;
            int poise1Before = enemy1.Poise, poise2Before = enemy2.Poise;

            sim.Advance();
            sim.SubmitIntent(new ActionIntent(hero.Id, (int)ItemType.ElementalBomb, -1, CommandGrade.Miss, isUseItem: true));

            Assert.Less(enemy1.Hp, hp1Before, "Elemental Bomb là AoE — cả 2 enemy phải trúng damage");
            Assert.Less(enemy2.Hp, hp2Before);
            Assert.Less(enemy1.Poise, poise1Before, "-20 Poise phải áp dụng");
            Assert.Less(enemy2.Poise, poise2Before);
            Assert.AreEqual(0, sim.State.ItemLoadout[ItemType.ElementalBomb]);
        }

        [Test]
        public void ElementalBomb_NoAliveEnemy_Fails_DoesNotConsumeItem()
        {
            var sim = new CombatSimulation(TestFactory.SEED);
            var hero = TestFactory.Unit("hero", TeamSide.Player);
            hero.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(), 0));
            var enemy = TestFactory.Unit("enemy", TeamSide.Enemy);
            enemy.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(), 0));
            sim.AddUnit(hero);
            sim.AddUnit(enemy, TestFactory.SimpleAi());
            sim.Start();
            enemy.SetHp(0); // Ngoài kịch bản thật (trận sẽ kết thúc trước) — chỉ test guard riêng của ItemResolver.
            sim.State.ItemLoadout[ItemType.ElementalBomb] = 1;

            sim.Advance();
            sim.SubmitIntent(new ActionIntent(hero.Id, (int)ItemType.ElementalBomb, -1, CommandGrade.Miss, isUseItem: true));

            Assert.AreEqual(1, sim.State.ItemLoadout[ItemType.ElementalBomb]);
        }
    }
}
