using Game.Combat;
using Game.Combat.Ai;
using Game.Combat.Model;
using Game.Data;

namespace Game.Tests
{
    /// <summary>Dựng unit/skill mẫu cho test. Mọi test dùng chung để số liệu nhất quán.</summary>
    public static class TestFactory
    {
        public const ulong SEED = 123456789UL;

        public static PrimaryStats Stats(float str = 10, float con = 10, float @int = 10,
                                         float dex = 10, float aur = 10, float luk = 10)
            => new(str, con, @int, dex, aur, luk);

        public static CombatUnit Unit(
            string id = "u", TeamSide side = TeamSide.Player, Row row = Row.Front,
            Element element = Element.Neutral, PrimaryStats? stats = null,
            int poiseMax = 30, HeroClass cls = HeroClass.Slayer)
        {
            return new CombatUnit
            {
                DefId = id,
                DisplayNameKey = id,
                Side = side,
                Row = row,
                Element = element,
                Class = cls,
                PoiseMax = poiseMax,
                BasePrimary = stats ?? Stats()
            };
        }

        public static SkillData BasicAttack(
            Element element = Element.Neutral,
            DamageType dmgType = DamageType.Physical,
            float power = 1.0f, int hits = 1,
            TargetMode target = TargetMode.SingleEnemy,
            int spCost = 0, int cooldown = 0, int poiseDmg = 3,
            params StatusApplication[] applies)
        {
            return new SkillData
            {
                Id = "skill_test",
                Type = dmgType == DamageType.Magical ? SkillType.Magical : SkillType.Physical,
                DamageType = dmgType,
                Element = element,
                Target = target,
                PowerMultiplier = power,
                HitCount = hits,
                SpCost = spCost,
                Cooldown = cooldown,
                PoiseDamage = poiseDmg,
                CommandType = ActionCommandType.SingleTap,
                Applies = applies ?? System.Array.Empty<StatusApplication>()
            };
        }

        public static SkillData HealSkill(float healPower = 1.4f,
                                          TargetMode target = TargetMode.LowestHpAlly,
                                          int spCost = 10)
            => new()
            {
                Id = "skill_heal_test",
                Type = SkillType.Heal,
                DamageType = DamageType.Magical,
                Target = target,
                HealPower = healPower,
                SpCost = spCost,
                CommandType = ActionCommandType.SingleTap
            };

        /// <summary>Trận 1v1 chuẩn để test nhanh.</summary>
        public static CombatSimulation Duel(
            out CombatUnit hero, out CombatUnit enemy,
            SkillData heroSkill = null, SkillData enemySkill = null,
            ulong seed = SEED, int turnLimit = 0)
        {
            var sim = new CombatSimulation(seed, turnLimit);

            hero = Unit("hero", TeamSide.Player);
            hero.Skills.Add(new SkillRuntime(heroSkill ?? BasicAttack(), 0));

            enemy = Unit("enemy", TeamSide.Enemy);
            enemy.Skills.Add(new SkillRuntime(enemySkill ?? BasicAttack(), 0));

            sim.AddUnit(hero);
            sim.AddUnit(enemy, SimpleAi());
            sim.Start();
            return sim;
        }

        public static AIProfile SimpleAi()
        {
            var p = new AIProfile { Id = "ai_test", Noise = 0f };
            p.Rules.Add(new AIRule
            {
                When = new AICondition(AIConditionType.Always),
                SkillSlot = 0,
                Weight = 50f
            });
            return p;
        }

        /// <summary>Trận 4v4 để test AoE, hàng trận, đội hình.</summary>
        public static CombatSimulation TeamBattle(out CombatUnit[] heroes, out CombatUnit[] enemies,
                                                  ulong seed = SEED)
        {
            var sim = new CombatSimulation(seed);
            heroes = new CombatUnit[4];
            enemies = new CombatUnit[4];

            for (int i = 0; i < 4; i++)
            {
                heroes[i] = Unit($"hero{i}", TeamSide.Player, i < 2 ? Row.Front : Row.Back);
                heroes[i].Skills.Add(new SkillRuntime(BasicAttack(), 0));
                sim.AddUnit(heroes[i]);

                enemies[i] = Unit($"enemy{i}", TeamSide.Enemy, i < 2 ? Row.Front : Row.Back);
                enemies[i].Skills.Add(new SkillRuntime(BasicAttack(), 0));
                sim.AddUnit(enemies[i], SimpleAi());
            }

            sim.Start();
            return sim;
        }
    }
}
