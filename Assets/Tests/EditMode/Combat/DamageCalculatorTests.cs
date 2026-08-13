using Game.Combat.Model;
using Game.Combat.Systems;
using Game.Core.Random;
using Game.Data;
using NUnit.Framework;

namespace Game.Tests.Combat
{
    /// <summary>Công thức sát thương — plan.md §4.6. Đổi công thức thì các test này phải đỏ.</summary>
    public class DamageCalculatorTests
    {
        private IRandomSource Rng() => new XorShiftRandom(TestFactory.SEED);

        /// <summary>
        /// DEX đủ cao để hitChance = (Acc − Eva)/100 vượt 1.0 → không bao giờ trượt.
        /// Cần thiết vì test tạo RNG mới cùng seed mỗi lần, nên lần roll đầu là cố định:
        /// nếu để ACC mặc định thì mọi đòn vật lý trong test đều trượt giống hệt nhau.
        /// </summary>
        private const float ALWAYS_HIT_DEX = 60f;

        // ---------- Nền tảng ----------

        [Test]
        public void Calculate_BasicHit_DealsPositiveDamage()
        {
            var a = TestFactory.Unit("a", stats: TestFactory.Stats(dex: ALWAYS_HIT_DEX));
            var d = TestFactory.Unit("d", TeamSide.Enemy);
            a.FillResources(); d.FillResources();
            var skill = new SkillRuntime(TestFactory.BasicAttack(), 0);

            var r = DamageCalculator.Calculate(a, d, skill, CommandGrade.Good, Rng());

            Assert.IsFalse(r.IsMiss);
            Assert.Greater(r.Amount, 0);
        }

        [Test]
        public void Calculate_AlwaysAtLeastOne_EvenWithHugeDefense()
        {
            var a = TestFactory.Unit("a", stats: TestFactory.Stats(str: 1, @int: 1));
            var d = TestFactory.Unit("d", TeamSide.Enemy, stats: TestFactory.Stats(con: 9999));
            a.FillResources(); d.FillResources();
            var skill = new SkillRuntime(
                TestFactory.BasicAttack(dmgType: DamageType.Magical, power: 0.01f), 0);

            var r = DamageCalculator.Calculate(a, d, skill, CommandGrade.Miss, Rng());

            Assert.GreaterOrEqual(r.Amount, 1, "Sát thương phải luôn >= 1 (edge case E24)");
        }

        [Test]
        public void Calculate_HigherDefense_ReducesDamage()
        {
            var a = TestFactory.Unit("a", stats: TestFactory.Stats(@int: 50));
            var weak = TestFactory.Unit("w", TeamSide.Enemy, stats: TestFactory.Stats(con: 5));
            var tanky = TestFactory.Unit("t", TeamSide.Enemy, stats: TestFactory.Stats(con: 100));
            a.FillResources(); weak.FillResources(); tanky.FillResources();
            var skill = new SkillRuntime(TestFactory.BasicAttack(dmgType: DamageType.Magical), 0);

            int dw = DamageCalculator.Calculate(a, weak, skill, CommandGrade.Good, Rng()).Amount;
            int dt = DamageCalculator.Calculate(a, tanky, skill, CommandGrade.Good, Rng()).Amount;

            Assert.Greater(dw, dt);
        }

        // ---------- AoE ----------

        /// <summary>Edge case E16 — skill AoE vẫn được tính là AoE (bỏ qua penalty hàng sau) dù
        /// lúc resolve chỉ còn đúng 1 địch; single-target vẫn bị phạt bình thường.</summary>
        [Test]
        public void E16_AoeSkill_SkipsBackRowPenalty_EvenWithOnlyOneEnemyLeft()
        {
            var a = TestFactory.Unit("a", stats: TestFactory.Stats(dex: ALWAYS_HIT_DEX));
            var backRowEnemy = TestFactory.Unit("d", TeamSide.Enemy, row: Row.Back);
            a.FillResources(); backRowEnemy.FillResources();

            var single = new SkillRuntime(TestFactory.BasicAttack(), 0); // IsAoe=false mặc định
            var aoe = new SkillRuntime(new SkillData
            {
                Id = "skill_aoe_test",
                Type = SkillType.Physical,
                DamageType = DamageType.Physical,
                Target = TargetMode.AllEnemies,
                PowerMultiplier = 1f,
                IsAoe = true,
                CommandType = ActionCommandType.SingleTap
            }, 0);

            int singleDmg = DamageCalculator.Calculate(a, backRowEnemy, single, CommandGrade.Good, Rng()).Amount;
            int aoeDmg = DamageCalculator.Calculate(a, backRowEnemy, aoe, CommandGrade.Good, Rng()).Amount;

            Assert.Greater(aoeDmg, singleDmg,
                "Edge case E16: AoE bỏ qua penalty hàng sau dù chỉ còn đúng 1 địch, single-target vẫn bị phạt BACK_ROW_PHYS_MULT");
        }

        // ---------- Né tránh ----------

        [Test]
        public void Calculate_PhysicalCanMiss_WhenAccuracyLow()
        {
            var a = TestFactory.Unit("a", stats: TestFactory.Stats(dex: 0));
            var d = TestFactory.Unit("d", TeamSide.Enemy);
            a.FillResources(); d.FillResources();
            d.EquipmentModifiers.Add(new StatModifier(StatType.Eva, 999f)); // kẹp về EVA_MAX
            d.MarkStatsDirty();
            var skill = new SkillRuntime(TestFactory.BasicAttack(), 0);

            var rng = Rng();
            int misses = 0;
            for (int i = 0; i < 200; i++)
                if (DamageCalculator.Calculate(a, d, skill, CommandGrade.Good, rng).IsMiss) misses++;

            Assert.Greater(misses, 0, "EVA cao phải gây trượt");
            Assert.Less(misses, 200, "hitChance có sàn 10%, không được trượt 100%");
        }

        // ---------- Nguyên tố ----------

        [Test]
        public void Calculate_ElementAdvantage_Multiplies1_3()
        {
            var a = TestFactory.Unit("a", element: Element.Fire, stats: TestFactory.Stats(@int: 40));
            var weakTo = TestFactory.Unit("w", TeamSide.Enemy, element: Element.Wind);
            var neutral = TestFactory.Unit("n", TeamSide.Enemy, element: Element.Neutral);
            a.FillResources(); weakTo.FillResources(); neutral.FillResources();
            var skill = new SkillRuntime(
                TestFactory.BasicAttack(Element.Fire, DamageType.Magical), 0);

            var adv = DamageCalculator.Calculate(a, weakTo, skill, CommandGrade.Good, Rng());
            var neu = DamageCalculator.Calculate(a, neutral, skill, CommandGrade.Good, Rng());

            Assert.AreEqual(1.3f, adv.ElementMultiplier, 0.001f);
            Assert.AreEqual(1.0f, neu.ElementMultiplier, 0.001f);
            Assert.Greater(adv.Amount, neu.Amount);
        }

        [Test]
        public void Calculate_LightVsDark_Multiplies1_4()
        {
            var a = TestFactory.Unit("a", element: Element.Light);
            var d = TestFactory.Unit("d", TeamSide.Enemy, element: Element.Dark);
            a.FillResources(); d.FillResources();
            var skill = new SkillRuntime(
                TestFactory.BasicAttack(Element.Light, DamageType.Magical), 0);

            var r = DamageCalculator.Calculate(a, d, skill, CommandGrade.Good, Rng());

            Assert.AreEqual(1.4f, r.ElementMultiplier, 0.001f);
        }

        // ---------- Action Command grade ----------

        [Test]
        public void GradeMultiplier_MatchesSpec()
        {
            Assert.AreEqual(1.30f, DamageCalculator.GradeMultiplier(CommandGrade.Perfect), 0.001f);
            Assert.AreEqual(1.10f, DamageCalculator.GradeMultiplier(CommandGrade.Good), 0.001f);
            Assert.AreEqual(0.80f, DamageCalculator.GradeMultiplier(CommandGrade.Miss), 0.001f);
        }

        [Test]
        public void Calculate_PerfectGrade_BeatsGoodBeatsMiss()
        {
            var a = TestFactory.Unit("a", stats: TestFactory.Stats(str: 40, luk: 0));
            var d = TestFactory.Unit("d", TeamSide.Enemy);
            a.FillResources(); d.FillResources();
            var skill = new SkillRuntime(TestFactory.BasicAttack(dmgType: DamageType.Magical), 0);

            int p = DamageCalculator.Calculate(a, d, skill, CommandGrade.Perfect, Rng()).Amount;
            int g = DamageCalculator.Calculate(a, d, skill, CommandGrade.Good, Rng()).Amount;
            int m = DamageCalculator.Calculate(a, d, skill, CommandGrade.Miss, Rng()).Amount;

            Assert.Greater(p, g);
            Assert.Greater(g, m);
        }

        // ---------- Break / hàng trận ----------

        [Test]
        public void Calculate_BrokenTarget_Multiplies1_5()
        {
            var a = TestFactory.Unit("a", stats: TestFactory.Stats(str: 40, luk: 0));
            var normal = TestFactory.Unit("n", TeamSide.Enemy);
            var broken = TestFactory.Unit("b", TeamSide.Enemy);
            a.FillResources(); normal.FillResources(); broken.FillResources();
            broken.BrokenTurnsLeft = 1;
            var skill = new SkillRuntime(TestFactory.BasicAttack(dmgType: DamageType.Magical), 0);

            int dn = DamageCalculator.Calculate(a, normal, skill, CommandGrade.Good, Rng()).Amount;
            int db = DamageCalculator.Calculate(a, broken, skill, CommandGrade.Good, Rng()).Amount;

            Assert.AreEqual(dn * 1.5f, db, dn * 0.06f, "Break phải nhân 1.5×");
        }

        [Test]
        public void Calculate_BackRowPhysical_Reduced()
        {
            var a = TestFactory.Unit("a", stats: TestFactory.Stats(str: 40, dex: ALWAYS_HIT_DEX, luk: 0));
            var front = TestFactory.Unit("f", TeamSide.Enemy, Row.Front);
            var back = TestFactory.Unit("b", TeamSide.Enemy, Row.Back);
            a.FillResources(); front.FillResources(); back.FillResources();
            var skill = new SkillRuntime(TestFactory.BasicAttack(), 0);

            int df = DamageCalculator.Calculate(a, front, skill, CommandGrade.Good, Rng()).Amount;
            int db = DamageCalculator.Calculate(a, back, skill, CommandGrade.Good, Rng()).Amount;

            Assert.Less(db, df, "Hàng sau nhận 70% sát thương vật lý đơn mục tiêu");
        }

        [Test]
        public void Calculate_BackRowMagical_NotReduced()
        {
            var a = TestFactory.Unit("a", stats: TestFactory.Stats(@int: 40, luk: 0));
            var front = TestFactory.Unit("f", TeamSide.Enemy, Row.Front);
            var back = TestFactory.Unit("b", TeamSide.Enemy, Row.Back);
            a.FillResources(); front.FillResources(); back.FillResources();
            var skill = new SkillRuntime(TestFactory.BasicAttack(dmgType: DamageType.Magical), 0);

            int df = DamageCalculator.Calculate(a, front, skill, CommandGrade.Good, Rng()).Amount;
            int db = DamageCalculator.Calculate(a, back, skill, CommandGrade.Good, Rng()).Amount;

            Assert.AreEqual(df, db, df * 0.12f, "Phép không bị giảm theo hàng");
        }

        // ---------- Né / crit ----------

        [Test]
        public void Calculate_MagicalNeverMisses()
        {
            var a = TestFactory.Unit("a", stats: TestFactory.Stats(dex: 0));
            var d = TestFactory.Unit("d", TeamSide.Enemy);
            a.FillResources(); d.FillResources();
            d.EquipmentModifiers.Add(new StatModifier(StatType.Eva, 999f));
            d.MarkStatsDirty();
            var skill = new SkillRuntime(TestFactory.BasicAttack(dmgType: DamageType.Magical), 0);

            var rng = Rng();
            for (int i = 0; i < 50; i++)
                Assert.IsFalse(DamageCalculator.Calculate(a, d, skill, CommandGrade.Good, rng).IsMiss);
        }

        [Test]
        public void Calculate_HighCrit_ProducesCrits()
        {
            var a = TestFactory.Unit("a", stats: TestFactory.Stats(str: 30, luk: 200));
            var d = TestFactory.Unit("d", TeamSide.Enemy);
            a.FillResources(); d.FillResources();
            var skill = new SkillRuntime(TestFactory.BasicAttack(dmgType: DamageType.Magical), 0);

            var rng = Rng();
            int crits = 0;
            for (int i = 0; i < 200; i++)
                if (DamageCalculator.Calculate(a, d, skill, CommandGrade.Good, rng).IsCrit) crits++;

            Assert.Greater(crits, 150, "CRIT bị kẹp 95%, 200 lần phải ra >150 crit");
        }

        // ---------- DoT ----------

        [Test]
        public void CalculateDot_Burn_ScalesWithMaxHpAndStacks()
        {
            var t = TestFactory.Unit("t", stats: TestFactory.Stats(con: 100));
            t.FillResources();

            var one = new StatusInstance(StatusId.Burn, 1, 3, -1);
            var three = new StatusInstance(StatusId.Burn, 3, 3, -1);

            int d1 = DamageCalculator.CalculateDot(t, one, null);
            int d3 = DamageCalculator.CalculateDot(t, three, null);

            Assert.AreEqual(d1 * 3, d3, 2);
            Assert.AreEqual(t.MaxHp * 0.05f, d1, 2f);
        }

        [Test]
        public void CalculateHeal_BlockedByCurse()
        {
            var healer = TestFactory.Unit("h");
            var target = TestFactory.Unit("t");
            healer.FillResources(); target.FillResources();
            target.Statuses.Add(new StatusInstance(StatusId.Curse, 1, 3, -1));

            var skill = new SkillRuntime(TestFactory.HealSkill(), 0);
            Assert.AreEqual(0, DamageCalculator.CalculateHeal(healer, target, skill));
        }

        // ---------- Poise ----------

        [Test]
        public void CalculatePoiseDamage_ElementAdvantage_AddsFifteen()
        {
            var a = TestFactory.Unit("a", element: Element.Fire);
            var weak = TestFactory.Unit("w", TeamSide.Enemy, element: Element.Wind);
            var neu = TestFactory.Unit("n", TeamSide.Enemy, element: Element.Neutral);
            a.FillResources(); weak.FillResources(); neu.FillResources();
            var skill = new SkillRuntime(TestFactory.BasicAttack(Element.Fire, poiseDmg: 3), 0);

            int pw = DamageCalculator.CalculatePoiseDamage(a, weak, skill, CommandGrade.Good);
            int pn = DamageCalculator.CalculatePoiseDamage(a, neu, skill, CommandGrade.Good);

            Assert.AreEqual(pn + 15, pw);
        }

        [Test]
        public void CalculatePoiseDamage_Perfect_AddsEight()
        {
            var a = TestFactory.Unit("a");
            var d = TestFactory.Unit("d", TeamSide.Enemy);
            a.FillResources(); d.FillResources();
            var skill = new SkillRuntime(TestFactory.BasicAttack(poiseDmg: 3), 0);

            int good = DamageCalculator.CalculatePoiseDamage(a, d, skill, CommandGrade.Good);
            int perfect = DamageCalculator.CalculatePoiseDamage(a, d, skill, CommandGrade.Perfect);

            Assert.AreEqual(good + 8, perfect);
        }

        // ---------- ExtraDamagePercent (task-extra-damage.md) ----------

        [Test]
        public void Calculate_PassiveOnHitDealt_ExtraDamagePercent_IncreasesDamage()
        {
            var a = TestFactory.Unit("a", stats: TestFactory.Stats(dex: ALWAYS_HIT_DEX));
            var d = TestFactory.Unit("d", TeamSide.Enemy);
            a.FillResources(); d.FillResources();
            var skill = new SkillRuntime(TestFactory.BasicAttack(), 0);

            int baseline = DamageCalculator.Calculate(a, d, skill, CommandGrade.Good, Rng()).Amount;

            a.Passive = new PassiveData { Trigger = PassiveTrigger.OnHitDealt, ExtraDamagePercent = 20f };
            int boosted = DamageCalculator.Calculate(a, d, skill, CommandGrade.Good, Rng()).Amount;

            Assert.AreEqual(baseline * 1.2f, boosted, baseline * 0.05f,
                "Passive Trigger=OnHitDealt, ExtraDamagePercent=20 phải tăng damage ~20%");
        }

        [Test]
        public void Calculate_PassiveWrongTrigger_ExtraDamagePercent_DoesNotApply()
        {
            var a = TestFactory.Unit("a", stats: TestFactory.Stats(dex: ALWAYS_HIT_DEX));
            var d = TestFactory.Unit("d", TeamSide.Enemy);
            a.FillResources(); d.FillResources();
            var skill = new SkillRuntime(TestFactory.BasicAttack(), 0);

            int baseline = DamageCalculator.Calculate(a, d, skill, CommandGrade.Good, Rng()).Amount;

            // Trigger=OnKill (không phải OnHitDealt) — ExtraDamagePercent KHÔNG được áp dụng dù có giá trị.
            a.Passive = new PassiveData { Trigger = PassiveTrigger.OnKill, ExtraDamagePercent = 50f };
            int withWrongTrigger = DamageCalculator.Calculate(a, d, skill, CommandGrade.Good, Rng()).Amount;

            Assert.AreEqual(baseline, withWrongTrigger, baseline * 0.02f,
                "ExtraDamagePercent chỉ áp dụng khi Trigger=OnHitDealt, không phải mọi trigger");
        }

        [Test]
        public void Calculate_NoPassiveOrAwakening_DoesNotChangeDamage()
        {
            var a = TestFactory.Unit("a", stats: TestFactory.Stats(dex: ALWAYS_HIT_DEX));
            var d = TestFactory.Unit("d", TeamSide.Enemy);
            a.FillResources(); d.FillResources();
            Assert.IsNull(a.Passive);
            Assert.IsNull(a.Awakening);
            var skill = new SkillRuntime(TestFactory.BasicAttack(), 0);

            // Không throw, không đổi hành vi — regression cho unit thường/enemy chưa có passive nào.
            var r = DamageCalculator.Calculate(a, d, skill, CommandGrade.Good, Rng());
            Assert.Greater(r.Amount, 0);
        }

        [Test]
        public void Calculate_BothPassiveAndAwakening_OnHitDealt_StackAdditively()
        {
            var a = TestFactory.Unit("a", stats: TestFactory.Stats(dex: ALWAYS_HIT_DEX));
            var d = TestFactory.Unit("d", TeamSide.Enemy);
            a.FillResources(); d.FillResources();
            var skill = new SkillRuntime(TestFactory.BasicAttack(), 0);

            int baseline = DamageCalculator.Calculate(a, d, skill, CommandGrade.Good, Rng()).Amount;

            a.Passive = new PassiveData { Trigger = PassiveTrigger.OnHitDealt, ExtraDamagePercent = 10f };
            a.Awakening = new PassiveData { Trigger = PassiveTrigger.OnHitDealt, ExtraDamagePercent = 10f };
            int boosted = DamageCalculator.Calculate(a, d, skill, CommandGrade.Good, Rng()).Amount;

            Assert.AreEqual(baseline * 1.2f, boosted, baseline * 0.05f,
                "Passive + Awakening cùng Trigger=OnHitDealt phải cộng dồn (10% + 10% = 20%)");
        }

        // ---------- SetBonus — bộ Tempest (task-setbonus.md §1.2) ----------

        [Test]
        public void Calculate_SetBonusOnAttacker_NotIncludedBefore_NowIncluded()
        {
            // Phát hiện phụ khi làm Tempest: dòng cộng ExtraDamageFrom trước đó THIẾU attacker.SetBonus.
            var a = TestFactory.Unit("a", stats: TestFactory.Stats(dex: ALWAYS_HIT_DEX));
            var d = TestFactory.Unit("d", TeamSide.Enemy);
            a.FillResources(); d.FillResources();
            var skill = new SkillRuntime(TestFactory.BasicAttack(), 0);

            int baseline = DamageCalculator.Calculate(a, d, skill, CommandGrade.Good, Rng()).Amount;

            a.SetBonus = new PassiveData { Trigger = PassiveTrigger.OnHitDealt, ExtraDamagePercent = 20f };
            int boosted = DamageCalculator.Calculate(a, d, skill, CommandGrade.Good, Rng()).Amount;

            Assert.AreEqual(baseline * 1.2f, boosted, baseline * 0.05f,
                "CombatUnit.SetBonus phải được cộng vào ExtraDamagePercent giống Passive/Awakening");
        }

        [Test]
        public void Calculate_TempestBonus_IsFirstActorThisRoundTrue_IncreasesDamage()
        {
            var a = TestFactory.Unit("a", stats: TestFactory.Stats(dex: ALWAYS_HIT_DEX));
            var d = TestFactory.Unit("d", TeamSide.Enemy);
            a.FillResources(); d.FillResources();
            var skill = new SkillRuntime(TestFactory.BasicAttack(), 0);
            a.SetBonus = Game.Meta.Equipment.SetBonusCatalog.FourPiece("tempest");

            int baseline = DamageCalculator.Calculate(a, d, skill, CommandGrade.Good, Rng()).Amount;

            a.IsFirstActorThisRound = true;
            int boosted = DamageCalculator.Calculate(a, d, skill, CommandGrade.Good, Rng()).Amount;

            Assert.AreEqual(baseline * 1.2f, boosted, baseline * 0.05f,
                "Tempest 4-món: +20% dmg khi actor là người hành động đầu round");
        }

        [Test]
        public void Calculate_TempestBonus_IsFirstActorThisRoundFalse_NoBonus()
        {
            var a = TestFactory.Unit("a", stats: TestFactory.Stats(dex: ALWAYS_HIT_DEX));
            var d = TestFactory.Unit("d", TeamSide.Enemy);
            a.FillResources(); d.FillResources();
            var skill = new SkillRuntime(TestFactory.BasicAttack(), 0);
            a.SetBonus = Game.Meta.Equipment.SetBonusCatalog.FourPiece("tempest");
            a.IsFirstActorThisRound = false;

            int baseline = DamageCalculator.Calculate(a, d, skill, CommandGrade.Good, Rng()).Amount;

            // Không có Tempest hoàn toàn (baseline "sạch") để so sánh chính xác cùng seed.
            var clean = TestFactory.Unit("clean", stats: TestFactory.Stats(dex: ALWAYS_HIT_DEX));
            clean.FillResources();
            int withoutSetBonus = DamageCalculator.Calculate(clean, d, skill, CommandGrade.Good, Rng()).Amount;

            Assert.AreEqual(withoutSetBonus, baseline, withoutSetBonus * 0.02f,
                "RequiresFirstActionOfRound=true nhưng cờ IsFirstActorThisRound=false → KHÔNG cộng bonus");
        }
    }
}
