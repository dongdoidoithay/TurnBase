using System.Collections.Generic;
using Game.Combat;
using Game.Combat.Model;
using Game.Combat.Systems;
using Game.Core.Random;
using Game.Data;
using NUnit.Framework;

namespace Game.Tests.Combat
{
    /// <summary>Determinism — plan.md §4.17. Vi phạm ở đây là lỗi nghiêm trọng nhất của dự án.</summary>
    public class DeterminismTests
    {
        [Test]
        public void SameSeed_ProducesIdenticalEventSequence()
        {
            string SigOf(ulong seed)
            {
                var sim = TestFactory.Duel(out _, out _, seed: seed);
                sim.RunToCompletion();
                return sim.Events.ToSignature();
            }

            Assert.AreEqual(SigOf(999UL), SigOf(999UL));
        }

        [Test]
        public void DifferentSeed_ProducesDifferentSequence()
        {
            var a = TestFactory.Duel(out _, out _, seed: 111UL);
            var b = TestFactory.Duel(out _, out _, seed: 222UL);
            a.RunToCompletion();
            b.RunToCompletion();

            Assert.AreNotEqual(a.Events.ToSignature(), b.Events.ToSignature());
        }

        [Test]
        public void SameSeed_ProducesIdenticalResultAndTurnCount()
        {
            var a = TestFactory.Duel(out _, out _, seed: 4242UL);
            var b = TestFactory.Duel(out _, out _, seed: 4242UL);

            Assert.AreEqual(a.RunToCompletion(), b.RunToCompletion());
            Assert.AreEqual(a.State.TurnCounter, b.State.TurnCounter);
        }

        [Test]
        public void RngFork_DoesNotConsumeParentSeed()
        {
            var rng = new XorShiftRandom(555UL);
            rng.NextFloat();
            long before = rng.CallCount;

            var fork = rng.Fork();
            for (int i = 0; i < 20; i++) fork.NextFloat();

            Assert.AreEqual(before, rng.CallCount, "Fork phải độc lập — dùng cho Intent Preview");
        }

        [Test]
        public void XorShift_NeverProducesOutOfRangeValues()
        {
            var rng = new XorShiftRandom(7UL);
            for (int i = 0; i < 10_000; i++)
            {
                float f = rng.NextFloat();
                Assert.GreaterOrEqual(f, 0f);
                Assert.Less(f, 1f);

                int n = rng.NextInt(10);
                Assert.GreaterOrEqual(n, 0);
                Assert.Less(n, 10);
            }
        }
    }

    /// <summary>Fuzz — 10.000 trận không exception, luôn kết thúc (plan.md §11.10).</summary>
    public class FuzzBattleTests
    {
        [Test]
        public void RandomBattles_AlwaysTerminate_WithoutException()
        {
            const int RUNS = 2000;   // 10.000 để chạy nightly, 2.000 cho vòng lặp dev
            var results = new Dictionary<BattleResult, int>();

            for (int i = 0; i < RUNS; i++)
            {
                ulong seed = (ulong)(i * 7919 + 13);
                var sim = BuildRandomBattle(seed);

                BattleResult r = BattleResult.InProgress;
                Assert.DoesNotThrow(() => r = sim.RunToCompletion(), $"Seed {seed} ném exception");

                Assert.AreNotEqual(BattleResult.InProgress, r, $"Seed {seed} không kết thúc");
                Assert.LessOrEqual(sim.State.TurnCounter, CombatSimulation.SAFETY_TURN_LIMIT,
                                   $"Seed {seed} vượt giới hạn lượt");

                results.TryGetValue(r, out int c);
                results[r] = c + 1;
            }

            // Trận ngẫu nhiên cân bằng phải cho cả thắng lẫn thua, không nghiêng tuyệt đối
            Assert.Greater(results.GetValueOrDefault(BattleResult.Victory), 0);
            Assert.Greater(results.GetValueOrDefault(BattleResult.Defeat), 0);
            TestContext.WriteLine($"Kết quả {RUNS} trận: " +
                string.Join(", ", results));
        }

        private static CombatSimulation BuildRandomBattle(ulong seed)
        {
            var rng = new XorShiftRandom(seed);
            var sim = new CombatSimulation(seed);

            var elements = new[] { Element.Fire, Element.Water, Element.Earth,
                                   Element.Wind, Element.Light, Element.Dark };
            var statuses = new[] { StatusId.Burn, StatusId.Poison, StatusId.Stun,
                                   StatusId.Freeze, StatusId.AtkDown, StatusId.DefDown,
                                   StatusId.Silence, StatusId.Taunt, StatusId.Bleed };

            for (int side = 0; side < 2; side++)
            {
                var team = side == 0 ? TeamSide.Player : TeamSide.Enemy;
                int count = rng.NextInt(2, 5);

                for (int i = 0; i < count; i++)
                {
                    var u = TestFactory.Unit(
                        $"{team}{i}", team, i < 2 ? Row.Front : Row.Back,
                        elements[rng.NextInt(elements.Length)],
                        TestFactory.Stats(
                            rng.NextInt(5, 30), rng.NextInt(5, 30), rng.NextInt(5, 30),
                            rng.NextInt(5, 30), rng.NextInt(5, 30), rng.NextInt(5, 30)),
                        poiseMax: rng.NextInt(20, 80));

                    // Ô 0 luôn là đánh thường không tốn SP
                    u.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(u.Element), 0));

                    // Thêm 1-2 skill ngẫu nhiên có status
                    int extra = rng.NextInt(1, 3);
                    for (int s = 0; s < extra; s++)
                    {
                        var app = new StatusApplication(
                            statuses[rng.NextInt(statuses.Length)],
                            rng.NextFloat(0.3f, 1f), rng.NextInt(1, 4));

                        var skill = TestFactory.BasicAttack(
                            u.Element,
                            rng.Chance(0.5f) ? DamageType.Physical : DamageType.Magical,
                            rng.NextFloat(0.5f, 2.2f),
                            rng.NextInt(1, 5),
                            rng.Chance(0.3f) ? TargetMode.AllEnemies : TargetMode.SingleEnemy,
                            spCost: rng.NextInt(0, 20),
                            cooldown: rng.NextInt(0, 4),
                            applies: app);

                        u.Skills.Add(new SkillRuntime(skill, u.Skills.Count));
                    }

                    sim.AddUnit(u, TestFactory.SimpleAi());
                }
            }

            sim.Start();
            return sim;
        }
    }

    /// <summary>Các tình huống biên ở plan.md §4.14.</summary>
    public class EdgeCaseTests
    {
        [Test]
        public void E04_BothTeamsWiped_PlayerLoses()
        {
            var sim = TestFactory.Duel(out var hero, out var enemy);
            hero.Hp = 0;
            enemy.Hp = 0;

            Assert.AreEqual(BattleResult.Defeat, sim.State.EvaluateResult(),
                            "Quy ước E04: hoà cũng tính là thua");
        }

        [Test]
        public void E09_HealDoesNotExceedMaxHp()
        {
            var sim = TestFactory.Duel(out var hero, out _, TestFactory.HealSkill(healPower: 99f,
                                       target: TargetMode.Self));
            hero.SetHp(hero.MaxHp - 5);

            sim.Advance();
            sim.SubmitIntent(new ActionIntent(hero.Id, 0, hero.Id, CommandGrade.Perfect));

            Assert.AreEqual(hero.MaxHp, hero.Hp);
            Assert.IsFalse(hero.HasStatus(StatusId.Shield),
                "Edge case E09: phần hồi dư ra KHÔNG được tự động thành Shield");
        }

        [Test]
        public void E11_UnitDownTooLong_BecomesPermanentlyDown()
        {
            var sim = TestFactory.Duel(out var hero, out _);
            hero.Hp = 0;

            for (int i = 0; i < 5; i++) ActionResolver.TickDownedUnits(sim.State);

            Assert.IsTrue(hero.PermanentlyDown);
        }

        [Test]
        public void E12_TeamWipe_WithAutoRevivePassive_RevivesBeforeDefeat()
        {
            var sim = TestFactory.Duel(out var hero, out _);
            hero.Passive = new PassiveData { Trigger = PassiveTrigger.OnTeamWipe, Threshold = 0.5f };
            hero.Hp = 0; // đội (1 unit) gục — mô phỏng trạng thái ngay trước khi CheckEnd() chốt Defeat

            sim.Advance();

            Assert.AreEqual(BattleResult.InProgress, sim.State.Result,
                "Edge case E12: AutoRevive phải cứu trận TRƯỚC KHI Defeat được chốt");
            Assert.IsTrue(hero.IsAlive);
            Assert.AreEqual(85, hero.Hp, "Threshold=0.5 trên MaxHp=170 → hồi đúng 85");
            Assert.IsTrue(hero.Passive.Consumed, "Passive AutoRevive chỉ dùng 1 lần/trận");
        }

        [Test]
        public void E12_TeamWipe_WithoutAutoRevivePassive_StaysDefeat()
        {
            var sim = TestFactory.Duel(out var hero, out _);
            hero.Hp = 0;

            sim.Advance();

            Assert.AreEqual(BattleResult.Defeat, sim.State.Result);
            Assert.IsFalse(hero.IsAlive);
        }

        [Test]
        public void E12_TeamWipe_PassiveAlreadyConsumed_DoesNotReviveTwice()
        {
            var sim = TestFactory.Duel(out var hero, out _);
            hero.Passive = new PassiveData { Trigger = PassiveTrigger.OnTeamWipe, Threshold = 0.5f, Consumed = true };
            hero.Hp = 0;

            sim.Advance();

            Assert.AreEqual(BattleResult.Defeat, sim.State.Result,
                "Passive đã Consumed từ trước (VD dùng ở trận khác/lượt trước) không được cứu lần 2");
            Assert.IsFalse(hero.IsAlive);
        }

        [Test]
        public void E12_TeamWipe_ZeroThreshold_FallsBackToDefault30Percent()
        {
            var sim = TestFactory.Duel(out var hero, out _);
            hero.Passive = new PassiveData { Trigger = PassiveTrigger.OnTeamWipe }; // Threshold mặc định 0
            hero.Hp = 0;

            sim.Advance();

            Assert.AreEqual(BattleResult.InProgress, sim.State.Result);
            Assert.AreEqual(51, hero.Hp, "Threshold=0 (chưa set) → fallback 30% của MaxHp=170");
        }

        [Test]
        public void E13_TurnLimitCausesTimeout()
        {
            // Hai unit siêu trâu, damage cực thấp → không ai chết trước giới hạn lượt
            var sim = new CombatSimulation(TestFactory.SEED, turnLimit: 10);
            var tanky = TestFactory.Stats(str: 1, con: 500);

            var a = TestFactory.Unit("a", TeamSide.Player, stats: tanky);
            a.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(power: 0.001f), 0));
            var b = TestFactory.Unit("b", TeamSide.Enemy, stats: tanky);
            b.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(power: 0.001f), 0));

            sim.AddUnit(a, TestFactory.SimpleAi());
            sim.AddUnit(b, TestFactory.SimpleAi());
            sim.Start();

            Assert.AreEqual(BattleResult.Timeout, sim.RunToCompletion());
        }

        /// <summary>Edge case E13 — vế còn lại: trận KHÔNG có turnLimit (mặc định 0) vẫn phải
        /// timeout ở SAFETY_TURN_LIMIT=200 (nhánh khác hẳn nhánh TurnLimit đã test ở trên).</summary>
        [Test]
        public void E13_NoTurnLimit_TimesOutAtSafetyLimit()
        {
            var sim = new CombatSimulation(TestFactory.SEED); // turnLimit mặc định = 0
            var tanky = TestFactory.Stats(str: 1, con: 500);

            var a = TestFactory.Unit("a", TeamSide.Player, stats: tanky);
            a.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(power: 0.001f), 0));
            var b = TestFactory.Unit("b", TeamSide.Enemy, stats: tanky);
            b.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(power: 0.001f), 0));

            sim.AddUnit(a, TestFactory.SimpleAi());
            sim.AddUnit(b, TestFactory.SimpleAi());
            sim.Start();

            Assert.AreEqual(BattleResult.Timeout, sim.RunToCompletion());
            Assert.Greater(sim.State.TurnCounter, CombatSimulation.SAFETY_TURN_LIMIT,
                "Edge case E13: không có turnLimit vẫn phải chặn ở SAFETY_TURN_LIMIT (200)");
        }

        [Test]
        public void E14_BackRowAdvancesWhenFrontDies()
        {
            var sim = TestFactory.TeamBattle(out var heroes, out var enemies);
            Assert.AreEqual(Row.Back, heroes[2].Row);

            heroes[0].Hp = 0;
            heroes[1].Hp = 0;
            Assert.AreEqual(Row.Back, heroes[2].Row,
                "Edge case E14: KHÔNG được đổi hàng ngay lập tức khi front vừa chết");

            // Chạy vài lượt để pipeline dồn hàng
            for (int i = 0; i < 6 && !sim.IsFinished; i++)
            {
                if (sim.Advance()) sim.SubmitIntent(sim.DefaultAutoIntent());
            }

            Assert.AreEqual(Row.Front, heroes[2].Row, "Hàng sau phải lên hàng trước, ở đầu lượt kế");
        }

        [Test]
        public void E15_UltimateNotReady_FallsBackToBasicAttack_GaugeUnchanged()
        {
            var sim = TestFactory.Duel(out var hero, out var enemy);
            // SlotIndex=4 = Ultimate; vị trí trong List (1) không liên quan tới cổng gauge, chỉ
            // SlotIndex field mới quyết định — xem CombatUnit.CanUseSkill.
            hero.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(power: 2f), 4));
            sim.Advance();
            Assert.IsFalse(sim.State.IsUltimateReady, "Gauge mặc định = 0, chưa đầy");

            sim.SubmitIntent(new ActionIntent(hero.Id, 1, enemy.Id, CommandGrade.Good));

            Assert.AreEqual(0, sim.State.UltimateGauge,
                "Edge case E15: gauge chưa đầy → Ultimate không dùng được, rơi về đánh thường, gauge không đổi");
        }

        [Test]
        public void E15_UltimateReady_ConsumesGaugeToZero_OnSuccessfulUse()
        {
            var sim = TestFactory.Duel(out var hero, out var enemy);
            hero.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(power: 1f), 4));
            sim.Advance();
            sim.State.AddUltimate(BattleState.ULTIMATE_MAX);
            Assert.IsTrue(sim.State.IsUltimateReady);

            sim.SubmitIntent(new ActionIntent(hero.Id, 1, enemy.Id, CommandGrade.Good));

            Assert.IsTrue(hero.IsAlive);
            Assert.AreEqual(0, sim.State.UltimateGauge, "Dùng Ultimate thành công phải tiêu hết gauge về 0");
        }

        [Test]
        public void E15_ActorDiesFromReflectDuringUltimate_GaugePreserved()
        {
            // Dùng damage Magical + status Reflect (không phải Physical + Counter) để KHÔNG phụ
            // thuộc RNG né đòn (plan.md §4.6 bước 3: chỉ vật lý mới né được) — kết quả xác định
            // 100%, đúng kỷ luật determinism của Game.Combat.
            var sim = TestFactory.Duel(out var hero, out var enemy);
            hero.Skills.Add(new SkillRuntime(
                TestFactory.BasicAttack(power: 0.1f, dmgType: DamageType.Magical), 4));
            sim.Advance();
            sim.State.AddUltimate(BattleState.ULTIMATE_MAX);
            enemy.Statuses.Add(new StatusInstance(StatusId.Reflect, 1, 3, -1));
            hero.SetHp(1); // chết ngay ở đòn dội đầu tiên (đúng pattern E03)

            sim.SubmitIntent(new ActionIntent(hero.Id, 1, enemy.Id, CommandGrade.Good));

            Assert.IsTrue(hero.IsDead, "Hero phải chết do Reflect dội lại từ chính đòn Ultimate");
            Assert.AreEqual(BattleState.ULTIMATE_MAX, sim.State.UltimateGauge,
                "Edge case E15: actor chết giữa lúc dùng Ultimate → gauge KHÔNG bị tiêu, giữ nguyên cho hero khác dùng sau");
        }

        [Test]
        public void E19_AiNeverPicksUnusableSkill()
        {
            var sim = new CombatSimulation(TestFactory.SEED);
            var u = TestFactory.Unit("u", TeamSide.Enemy);
            u.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(spCost: 0), 0));
            // Skill quá đắt, không bao giờ dùng được
            u.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(spCost: 9999), 1));

            var ai = new Game.Combat.Ai.AIProfile { Noise = 0f };
            ai.Rules.Add(new Game.Combat.Ai.AIRule
            {
                When = new Game.Combat.Ai.AICondition(Game.Combat.Ai.AIConditionType.Always),
                SkillSlot = 1,
                Weight = 100f
            });

            var hero = TestFactory.Unit("hero");
            hero.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(), 0));
            sim.AddUnit(hero);
            sim.AddUnit(u, ai);
            sim.Start();

            var controller = new Game.Combat.Ai.AIController(sim.State, new TargetSelector(sim.State));
            var decision = controller.Choose(u, ai, 0, sim.State.Rng);

            Assert.AreEqual(0, decision.Skill.SlotIndex, "Phải rơi về skill dùng được");
        }

        [Test]
        public void E22_SpdNeverReachesZero()
        {
            var u = TestFactory.Unit("u", stats: TestFactory.Stats(dex: 0));
            u.FillResources();
            u.Statuses.Add(new StatusInstance(StatusId.SpdDown, 2, 99, -1));
            u.MarkStatsDirty();

            Assert.GreaterOrEqual(u.SpdEffective, BalanceCaps.SPD_MIN);
        }

        [Test]
        public void E24_DamageNeverBelowOne()
        {
            var a = TestFactory.Unit("a", stats: TestFactory.Stats(str: 1));
            var d = TestFactory.Unit("d", TeamSide.Enemy, stats: TestFactory.Stats(con: 9999));
            a.FillResources(); d.FillResources();
            d.EquipmentModifiers.Add(new StatModifier(StatType.DmgReductPct, 999f));
            d.MarkStatsDirty();

            var skill = new SkillRuntime(TestFactory.BasicAttack(dmgType: DamageType.Magical,
                                                                power: 0.001f), 0);
            var r = DamageCalculator.Calculate(a, d, skill, CommandGrade.Miss,
                                               new XorShiftRandom(1));

            Assert.GreaterOrEqual(r.Amount, 1);
        }

        [Test]
        public void E01_TargetDiesMidCombo_SwitchesToReplacementTarget()
        {
            var sim = new CombatSimulation(TestFactory.SEED);
            var a = TestFactory.Unit("a", TeamSide.Player, stats: TestFactory.Stats(dex: 60));
            a.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(), 0));
            var b = TestFactory.Unit("b", TeamSide.Enemy);
            var c = TestFactory.Unit("c", TeamSide.Enemy);
            sim.AddUnit(a); sim.AddUnit(b); sim.AddUnit(c);
            sim.Start();
            b.SetHp(1); // chết ngay ở hit đầu combo
            int cHpBefore = c.Hp;

            // Dựng resolver riêng (thuần) để test combo-switch, không cần chạy hết pipeline lượt.
            var multiHit = new SkillRuntime(TestFactory.BasicAttack(
                power: 50f, hits: 3, dmgType: DamageType.Magical), 0); // Magical: không né, loại trừ nhiễu miss-chance
            var status = new StatusProcessor(sim.State, sim.Events);
            var passive = new PassiveProcessor(sim.State, sim.Events, status);
            var poise = new PoiseSystem(sim.State, sim.Events, status, passive);
            var resolver = new ActionResolver(sim.State, sim.Events, status, passive, poise, new TargetSelector(sim.State));

            resolver.Execute(a, multiHit, b.Id, CommandGrade.Good, sim.State.Rng);

            Assert.IsTrue(b.IsDead);
            Assert.Less(c.Hp, cHpBefore,
                "Edge case E01: hit còn lại của combo phải chuyển sang địch khác (c) sau khi b chết giữa chừng");
        }

        [Test]
        public void E02_ActorDiesFromDotAtTurnStart_SkipsTurn_NoSpChange_AtbReset()
        {
            var sim = new CombatSimulation(TestFactory.SEED);
            var hero = TestFactory.Unit("hero", TeamSide.Player);
            hero.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(), 0));
            var enemy = TestFactory.Unit("enemy", TeamSide.Enemy);
            enemy.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(power: 0.001f), 0));
            sim.AddUnit(hero, TestFactory.SimpleAi());
            sim.AddUnit(enemy, TestFactory.SimpleAi());
            sim.Start();

            hero.SetHp(1);
            hero.Statuses.Add(new StatusInstance(StatusId.Poison, 99, 5, enemy.Id));
            int spBefore = hero.Sp;

            // Chạy tới khi hero chết (DoT phải giết hero ngay lượt đầu tiên hero được gọi tới,
            // trước khi hero kịp hành động — không có SubmitIntent nào cho hero cả).
            int guard = 0;
            while (!sim.IsFinished && hero.IsAlive && guard++ < 50)
                sim.Advance();

            Assert.IsTrue(hero.IsDead, "Edge case E02: hero phải chết do DoT trước khi kịp hành động");
            Assert.AreEqual(spBefore, hero.Sp, "Bỏ lượt do DoT không được hoàn/đổi SP (không có hành động nào xảy ra)");
            // ConsumeTurn trừ đúng ATB_THRESHOLD (không clamp về 0 nếu dư) — "reset" nghĩa là bị
            // tiêu như lượt bình thường, không phải giữ nguyên nằm chờ ở mức kích hoạt lượt.
            Assert.Less(hero.Atb, TurnScheduler.ATB_THRESHOLD,
                "Bỏ lượt do DoT vẫn phải tiêu ATB như lượt bình thường (không bị bỏ qua/giữ nguyên)");
        }

        [Test]
        public void E07_DeadTaunter_HasStatusRemoved_AfterRealDeath()
        {
            var sim = TestFactory.Duel(out var hero, out var enemy,
                TestFactory.BasicAttack(power: 999f, dmgType: DamageType.Magical)); // Magical: không né
            enemy.Statuses.Add(new StatusInstance(StatusId.Taunt, 1, 3, -1));
            enemy.SetHp(1);

            sim.Advance();
            sim.SubmitIntent(new ActionIntent(hero.Id, 0, enemy.Id, CommandGrade.Perfect));

            Assert.IsTrue(enemy.IsDead);
            Assert.IsFalse(enemy.HasStatus(StatusId.Taunt),
                "Edge case E07: status Taunt phải bị GỠ khi unit chết thật (không chỉ bị ignore trong targeting)");
        }

        [Test]
        public void E18_AllowEscape_DefaultsTrue_CanBeDisabledForBossFights()
        {
            var sim = TestFactory.Duel(out var hero, out var enemy);
            Assert.IsTrue(sim.State.AllowEscape, "Mặc định phải cho phép Escape (trận thường)");

            sim.State.AllowEscape = false; // giả lập BattleSceneInstaller set cho node Boss
            sim.Advance();
            var before = sim.State.Result;
            sim.SubmitIntent(new ActionIntent(hero.Id, 0, -1, CommandGrade.Good, isEscape: true));

            Assert.AreNotEqual(BattleResult.Escaped, sim.State.Result,
                "Edge case E18: AllowEscape=false (node Boss) phải chặn thoát trận");
        }

        [Test]
        public void E20_MinionDisappears_AfterOwnerDies()
        {
            var sim = new CombatSimulation(TestFactory.SEED);
            var owner = TestFactory.Unit("owner", TeamSide.Player);
            owner.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(), 0));
            var enemy = TestFactory.Unit("enemy", TeamSide.Enemy);
            enemy.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(power: 0.01f), 0));
            sim.AddUnit(owner, TestFactory.SimpleAi());
            sim.AddUnit(enemy, TestFactory.SimpleAi());

            var minion = new CombatUnit
            {
                DefId = "minion_test", Side = TeamSide.Player, Row = Row.Front, SlotIndex = 90,
                IsMinion = true, OwnerId = owner.Id, MinionTurnsLeft = 99, PoiseMax = 20,
                BasePrimary = owner.BasePrimary * 0.45f
            };
            minion.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(), 0));
            sim.AddUnit(minion);

            sim.Start();
            owner.Hp = 0; // chủ chết ngay (giả lập bị giết giữa trận)

            for (int i = 0; i < 8 && !sim.IsFinished; i++)
            {
                if (sim.Advance()) sim.SubmitIntent(sim.DefaultAutoIntent());
            }

            Assert.IsTrue(minion.IsDead, "Edge case E20: minion phải biến mất sau khi chủ chết");
        }
    }

    /// <summary>Vòng đời trận đấu đầy đủ.</summary>
    public class CombatSimulationTests
    {
        /// <summary>task-setbonus.md §1.2 — bộ Tempest. CombatUnit.IsFirstActorThisRound phải
        /// luôn đúng 1 unit trong toàn bộ State.Units tại bất kỳ thời điểm nào trong trận, và đổi
        /// người khi sang round mới (chứng minh BeginRound() reset + BeginTurn() gán lại đúng).</summary>
        [Test]
        public void IsFirstActorThisRound_ExactlyOneUnitFlagged_ChangesAcrossRounds()
        {
            // Stats mặc định của TeamBattle (4v4, đánh thường cả 2 phe) kết thúc quá nhanh (1-2
            // round) để quan sát việc đổi round — dựng riêng 1 trận "trâu bò" (CON cao, ATK thấp)
            // để đảm bảo sống đủ lâu qua round 2, giống cách các test cần trận dài đã làm.
            var sim = new CombatSimulation(555UL);
            var tanky = TestFactory.Stats(str: 3, con: 300);
            var heroes = new CombatUnit[2];
            var enemies = new CombatUnit[2];
            for (int i = 0; i < 2; i++)
            {
                heroes[i] = TestFactory.Unit($"hero{i}", TeamSide.Player, stats: tanky);
                heroes[i].Skills.Add(new SkillRuntime(TestFactory.BasicAttack(power: 0.3f), 0));
                sim.AddUnit(heroes[i]);
                enemies[i] = TestFactory.Unit($"enemy{i}", TeamSide.Enemy, stats: tanky);
                enemies[i].Skills.Add(new SkillRuntime(TestFactory.BasicAttack(power: 0.3f), 0));
                sim.AddUnit(enemies[i], TestFactory.SimpleAi());
            }
            sim.Start();

            var allUnits = new List<CombatUnit>();
            allUnits.AddRange(heroes); allUnits.AddRange(enemies);

            List<CombatUnit> Flagged()
            {
                var list = new List<CombatUnit>();
                foreach (var u in allUnits) if (u.IsFirstActorThisRound) list.Add(u);
                return list;
            }

            for (int step = 0; step < 3; step++)
            {
                if (sim.Advance()) sim.SubmitIntent(sim.DefaultAutoIntent());
                Assert.AreEqual(1, Flagged().Count, $"Bước {step}: phải luôn đúng 1 unit được đánh dấu trong round 1");
            }

            int round1Number = sim.State.RoundNumber;
            int guard = 0;
            while (sim.State.RoundNumber == round1Number && !sim.IsFinished && guard < 200)
            {
                if (sim.Advance()) sim.SubmitIntent(sim.DefaultAutoIntent());
                guard++;
            }
            Assert.IsFalse(sim.IsFinished, "Trận không nên kết thúc trong phạm vi test này");

            if (sim.Advance()) sim.SubmitIntent(sim.DefaultAutoIntent());
            Assert.AreEqual(1, Flagged().Count, "Round mới vẫn phải đúng 1 unit được đánh dấu (reset + gán lại)");
        }

        [Test]
        public void Duel_CompletesWithDecisiveResult()
        {
            var sim = TestFactory.Duel(out _, out _);
            var result = sim.RunToCompletion();

            Assert.IsTrue(result is BattleResult.Victory or BattleResult.Defeat);
            Assert.Greater(sim.State.TurnCounter, 0);
        }

        [Test]
        public void Simulation_EmitsExpectedEventTypes()
        {
            var sim = TestFactory.Duel(out _, out _);
            sim.RunToCompletion();

            var types = new HashSet<Game.Combat.Events.CombatEventType>();
            foreach (var e in sim.Events.All) types.Add(e.Type);

            Assert.Contains(Game.Combat.Events.CombatEventType.BattleStarted, types.ToArrayCompat());
            Assert.Contains(Game.Combat.Events.CombatEventType.TurnStarted, types.ToArrayCompat());
            Assert.Contains(Game.Combat.Events.CombatEventType.DamageDealt, types.ToArrayCompat());
            Assert.Contains(Game.Combat.Events.CombatEventType.UnitDied, types.ToArrayCompat());
            Assert.Contains(Game.Combat.Events.CombatEventType.BattleEnded, types.ToArrayCompat());
        }

        [Test]
        public void IntentLog_CanReplayIdenticalBattle()
        {
            var original = TestFactory.Duel(out _, out _, seed: 31337UL);
            original.RunToCompletion();
            var log = new List<ActionIntent>(original.IntentLog);

            // Phát lại đúng chuỗi intent với cùng seed
            var replay = TestFactory.Duel(out _, out _, seed: 31337UL);
            int idx = 0;
            replay.RunToCompletion(_ => idx < log.Count ? log[idx++] : default);

            Assert.AreEqual(original.State.Result, replay.State.Result);
            Assert.AreEqual(original.Events.ToSignature(), replay.Events.ToSignature(),
                            "Replay từ (seed + intent list) phải cho kết quả y hệt");
        }

        [Test]
        public void SkillCost_DeductsSpAndSetsCooldown()
        {
            var skill = TestFactory.BasicAttack(spCost: 12, cooldown: 3);
            var sim = TestFactory.Duel(out var hero, out var enemy, skill);
            int spBefore = hero.MaxSp;

            sim.Advance();
            sim.SubmitIntent(new ActionIntent(hero.Id, 0, enemy.Id, CommandGrade.Good));

            // SP: hồi đầu lượt rồi trừ cost, cộng thưởng grade Good
            Assert.Less(hero.Sp, spBefore + 20);
            Assert.AreEqual(3, hero.GetSkill(0).CooldownLeft);
        }

        [Test]
        public void MultiHitSkill_DealsDamageMultipleTimes()
        {
            var single = TestFactory.BasicAttack(power: 1.0f, hits: 1);
            var multi = TestFactory.BasicAttack(power: 1.0f, hits: 4);

            int DamageOf(SkillData s)
            {
                var sim = TestFactory.Duel(out var hero, out var enemy, s, seed: 88UL);
                int hpBefore = enemy.Hp;
                sim.Advance();
                sim.SubmitIntent(new ActionIntent(hero.Id, 0, enemy.Id, CommandGrade.Good));
                return hpBefore - enemy.Hp;
            }

            Assert.Greater(DamageOf(multi), DamageOf(single) * 2);
        }
    }

    /// <summary>CombatSimulation.OnEnemyWaveCleared — Tháp Vô Tận (task-endgame.md, plan.md
    /// §8.3): nhiều đợt địch trong 1 trận liên tục, HP phe Player KHÔNG hồi giữa các đợt.</summary>
    public class MultiWaveTests
    {
        [Test]
        public void OnEnemyWaveCleared_ReturnsTrue_KeepsBattleRunning_DoesNotResetPlayerHp()
        {
            var sim = new CombatSimulation(TestFactory.SEED);
            var hero = TestFactory.Unit("hero", TeamSide.Player);
            hero.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(dmgType: DamageType.Magical, power: 999f), 0));
            sim.AddUnit(hero);
            hero.SetHp(hero.MaxHp / 2); // giả lập hero đã mất nửa máu từ tầng trước

            var enemy1 = TestFactory.Unit("enemy1", TeamSide.Enemy, stats: TestFactory.Stats(con: 1));
            enemy1.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(power: 0f), 0));
            sim.AddUnit(enemy1, TestFactory.SimpleAi());
            enemy1.SetHp(1); // chết ngay ở đòn đầu tiên (đúng pattern các test khác trong file này)

            bool waveCalled = false;
            sim.OnEnemyWaveCleared = s =>
            {
                waveCalled = true;
                var enemy2 = TestFactory.Unit("enemy2", TeamSide.Enemy, stats: TestFactory.Stats(con: 999));
                enemy2.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(power: 0f), 0));
                s.AddUnit(enemy2, TestFactory.SimpleAi());
                return true;
            };

            sim.Start();
            int hpBeforeKill = hero.Hp;
            Assert.IsTrue(sim.Advance(), "phải dừng ở AwaitInput cho hero (SPD hoà, Id thấp hơn đi trước)");
            Assert.AreEqual(hero.Id, sim.CurrentActor.Id);

            sim.SubmitIntent(new ActionIntent(hero.Id, 0, enemy1.Id));

            Assert.IsTrue(waveCalled, "OnEnemyWaveCleared phải được gọi khi enemy1 chết");
            Assert.IsFalse(sim.IsFinished, "Trận KHÔNG được kết thúc — còn đợt địch mới");
            Assert.AreEqual(hpBeforeKill, hero.Hp, "HP hero KHÔNG được hồi/reset khi sang đợt địch mới");
            Assert.AreEqual(3, sim.State.Units.Count, "hero + enemy1 (đã chết, còn xác) + enemy2");
        }

        [Test]
        public void OnEnemyWaveCleared_ReturnsFalse_BattleFinishesAsVictory()
        {
            var sim = TestFactory.Duel(out var hero, out var enemy,
                heroSkill: TestFactory.BasicAttack(dmgType: DamageType.Magical, power: 999f));
            enemy.SetHp(1); // chết ngay ở đòn đầu tiên
            sim.OnEnemyWaveCleared = _ => false; // VD Tháp đã hết tầng — không spawn nữa

            Assert.IsTrue(sim.Advance());
            sim.SubmitIntent(new ActionIntent(hero.Id, 0, enemy.Id));

            Assert.IsTrue(sim.IsFinished);
            Assert.AreEqual(BattleResult.Victory, sim.State.Result);
        }

        [Test]
        public void OnEnemyWaveCleared_Null_BehavesLikeBeforeThisFeature()
        {
            // Mặc định null — mọi trận khác (node map/Dungeon/Trial Boss) không đổi hành vi.
            var sim = TestFactory.Duel(out var hero, out var enemy,
                heroSkill: TestFactory.BasicAttack(dmgType: DamageType.Magical, power: 999f));
            enemy.SetHp(1); // chết ngay ở đòn đầu tiên
            Assert.IsNull(sim.OnEnemyWaveCleared);

            Assert.IsTrue(sim.Advance());
            sim.SubmitIntent(new ActionIntent(hero.Id, 0, enemy.Id));

            Assert.IsTrue(sim.IsFinished);
            Assert.AreEqual(BattleResult.Victory, sim.State.Result);
        }
    }

    internal static class TestExtensions
    {
        public static T[] ToArrayCompat<T>(this HashSet<T> set)
        {
            var arr = new T[set.Count];
            set.CopyTo(arr);
            return arr;
        }
    }
}
