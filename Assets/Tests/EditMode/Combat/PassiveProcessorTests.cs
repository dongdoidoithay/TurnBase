using Game.Combat.Model;
using Game.Combat.Systems;
using Game.Core.Random;
using Game.Data;
using Game.Meta.Equipment;
using Game.Meta.Hero;
using NUnit.Framework;

namespace Game.Tests.Combat
{
    /// <summary>PassiveProcessor + AwakeningCatalog — task-ascend.md §7 mục A.</summary>
    public class PassiveProcessorTests
    {
        private (BattleState state, PassiveProcessor passive, CombatUnit a, CombatUnit b) Setup()
        {
            var state = new BattleState { Rng = new XorShiftRandom(TestFactory.SEED) };
            var events = new Game.Combat.Events.CombatEventQueue();
            var status = new StatusProcessor(state, events);
            var passive = new PassiveProcessor(state, events, status);
            var a = TestFactory.Unit("a", TeamSide.Player);
            var b = TestFactory.Unit("b", TeamSide.Enemy);
            state.AddUnit(a); state.AddUnit(b);
            a.FillResources(); b.FillResources();
            return (state, passive, a, b);
        }

        [Test]
        public void OnBattleStart_AppliesModifiers_AndDoesNotStack_OnReFire()
        {
            var (state, passive, a, _) = Setup();
            a.Passive = new PassiveData
            {
                Trigger = PassiveTrigger.OnBattleStart,
                Modifiers = new[] { new StatModifier(StatType.DefPct, 15f) }
            };

            passive.TriggerBattleStart(state.Rng);
            Assert.AreEqual(1, a.PassiveModifiers.Count);
            Assert.IsTrue(a.Passive.Consumed, "OnBattleStart cấp stat vĩnh viễn phải khoá Consumed");

            passive.TriggerBattleStart(state.Rng);
            Assert.AreEqual(1, a.PassiveModifiers.Count, "Gọi lại không được cộng dồn modifier");
        }

        [Test]
        public void OnKill_AppliesSelfBuff_ToKiller_AndKeepsRepeating()
        {
            var (state, passive, a, b) = Setup();
            a.Passive = new PassiveData
            {
                Trigger = PassiveTrigger.OnKill,
                Applies = new[] { new StatusApplication(StatusId.AtkUp, 1f, duration: 2, stacks: 2, targetSelf: true) }
            };

            passive.TriggerOnKill(a, state.Rng);
            Assert.IsTrue(a.HasStatus(StatusId.AtkUp));
            Assert.IsFalse(a.Passive.Consumed, "Passive dạng proc (Applies) không bị khoá sau 1 lần — đó là cơ chế snowball");

            a.Statuses.Clear();
            passive.TriggerOnKill(a, state.Rng);
            Assert.IsTrue(a.HasStatus(StatusId.AtkUp), "Phải áp lại được lần thứ 2 vì không bị Consumed");
        }

        [Test]
        public void OnHitDealt_TargetSelfFalse_AppliesToContextTarget_NotOwner()
        {
            var (state, passive, a, b) = Setup();
            a.Passive = new PassiveData
            {
                Trigger = PassiveTrigger.OnHitDealt,
                Applies = new[] { new StatusApplication(StatusId.SpdDown, 1f, duration: 2, stacks: 1, targetSelf: false) }
            };

            passive.TriggerOnHitDealt(a, b, state.Rng);

            Assert.IsTrue(b.HasStatus(StatusId.SpdDown), "targetSelf=false phải áp lên đối tượng ngữ cảnh (b), không phải owner (a)");
            Assert.IsFalse(a.HasStatus(StatusId.SpdDown));
        }

        [Test]
        public void CheckHpThreshold_FiresOnce_WhenBelowThreshold()
        {
            var (state, passive, a, _) = Setup();
            a.Awakening = new PassiveData
            {
                Trigger = PassiveTrigger.OnHpBelowThreshold,
                Threshold = 0.3f,
                Applies = new[] { new StatusApplication(StatusId.DefUp, 1f, duration: 2, stacks: 1, targetSelf: true) }
            };
            a.SetHp((int)(a.MaxHp * 0.2f));

            passive.CheckHpThreshold(a, state.Rng);
            Assert.IsTrue(a.HasStatus(StatusId.DefUp));
            Assert.IsTrue(a.Awakening.Consumed, "Ngưỡng HP là cảnh báo 1 lần/trận, không phải proc mỗi lượt HP còn thấp");

            a.Statuses.Clear();
            passive.CheckHpThreshold(a, state.Rng);
            Assert.IsFalse(a.HasStatus(StatusId.DefUp), "Đã Consumed thì không được áp lại dù HP vẫn thấp");
        }

        [Test]
        public void Passive_And_Awakening_AreIndependentSlots_BothFire()
        {
            var (state, passive, a, _) = Setup();
            a.Passive = new PassiveData
            {
                Trigger = PassiveTrigger.OnBattleStart,
                Modifiers = new[] { new StatModifier(StatType.AtkPct, 5f) }
            };
            a.Awakening = new PassiveData
            {
                Trigger = PassiveTrigger.OnBattleStart,
                Modifiers = new[] { new StatModifier(StatType.DefPct, 5f) }
            };

            passive.TriggerBattleStart(state.Rng);

            Assert.AreEqual(2, a.PassiveModifiers.Count, "Cả Passive lẫn Awakening đều phải kích hoạt độc lập");
        }

        [Test]
        public void AwakeningCatalog_Get_ReturnsIndependentInstances_NotShared()
        {
            var first = AwakeningCatalog.Get("hero_ember_knight");
            var second = AwakeningCatalog.Get("hero_ember_knight");

            Assert.IsNotNull(first);
            Assert.IsNotNull(second);
            first.Consumed = true;

            Assert.IsFalse(second.Consumed, "Mỗi lần Get() phải trả về instance mới — Consumed không được rò rỉ giữa các trận");
        }

        [Test]
        public void AwakeningCatalog_UnknownHero_ReturnsNull()
        {
            Assert.IsNull(AwakeningCatalog.Get("hero_does_not_exist"));
        }

        [TestCase("hero_ember_knight")]
        [TestCase("hero_shadow_fang")]
        [TestCase("hero_frost_sage")]
        [TestCase("hero_dawn_cleric")]
        [TestCase("hero_gale_thief")]
        [TestCase("hero_bone_caller")]
        // 18 hero mở rộng — task-hero-roster.md
        [TestCase("hero_iron_bastion")]
        [TestCase("hero_tide_warden")]
        [TestCase("hero_stormguard")]
        [TestCase("hero_blade_dancer")]
        [TestCase("hero_crimson_reaver")]
        [TestCase("hero_stone_breaker")]
        [TestCase("hero_pyromancer")]
        [TestCase("hero_terra_seer")]
        [TestCase("hero_void_scholar")]
        [TestCase("hero_grove_keeper")]
        [TestCase("hero_moon_priestess")]
        [TestCase("hero_spring_medic")]
        [TestCase("hero_night_stalker")]
        [TestCase("hero_spark_runner")]
        [TestCase("hero_mirage_fox")]
        [TestCase("hero_beast_tamer")]
        [TestCase("hero_flame_binder")]
        [TestCase("hero_star_weaver")]
        public void AwakeningCatalog_AllTwentyFourHeroes_HaveAPassive(string heroDefId)
        {
            var data = AwakeningCatalog.Get(heroDefId);
            Assert.IsNotNull(data, $"{heroDefId} phải có Awakening thật (task-ascend.md §7 mục A.4 / task-hero-roster.md)");
            Assert.AreNotEqual(PassiveTrigger.None, data.Trigger);
        }

        // =====================================================================
        // InnatePassiveCatalog — task-innate-passive.md
        // =====================================================================

        [Test]
        public void InnatePassiveCatalog_Get_ReturnsIndependentInstances_NotShared()
        {
            var first = InnatePassiveCatalog.Get("hero_ember_knight");
            var second = InnatePassiveCatalog.Get("hero_ember_knight");

            Assert.IsNotNull(first);
            Assert.IsNotNull(second);
            first.Consumed = true;

            Assert.IsFalse(second.Consumed, "Mỗi lần Get() phải trả về instance mới");
        }

        [Test]
        public void InnatePassiveCatalog_UnknownHero_ReturnsNull()
        {
            Assert.IsNull(InnatePassiveCatalog.Get("hero_does_not_exist"));
        }

        [TestCase("hero_ember_knight")]
        [TestCase("hero_shadow_fang")]
        [TestCase("hero_frost_sage")]
        [TestCase("hero_dawn_cleric")]
        [TestCase("hero_gale_thief")]
        [TestCase("hero_bone_caller")]
        // 18 hero mở rộng — task-hero-roster.md
        [TestCase("hero_iron_bastion")]
        [TestCase("hero_tide_warden")]
        [TestCase("hero_stormguard")]
        [TestCase("hero_blade_dancer")]
        [TestCase("hero_crimson_reaver")]
        [TestCase("hero_stone_breaker")]
        [TestCase("hero_pyromancer")]
        [TestCase("hero_terra_seer")]
        [TestCase("hero_void_scholar")]
        [TestCase("hero_grove_keeper")]
        [TestCase("hero_moon_priestess")]
        [TestCase("hero_spring_medic")]
        [TestCase("hero_night_stalker")]
        [TestCase("hero_spark_runner")]
        [TestCase("hero_mirage_fox")]
        [TestCase("hero_beast_tamer")]
        [TestCase("hero_flame_binder")]
        [TestCase("hero_star_weaver")]
        public void InnatePassiveCatalog_AllTwentyFourHeroes_HaveAPassive(string heroDefId)
        {
            var data = InnatePassiveCatalog.Get(heroDefId);
            Assert.IsNotNull(data, $"{heroDefId} phải có passive bẩm sinh thật (task-innate-passive.md §1 / task-hero-roster.md)");
            Assert.AreNotEqual(PassiveTrigger.None, data.Trigger);
        }

        /// <summary>4 trigger audit ở task-ascend.md §10 phát hiện có hook thật nhưng chưa nội
        /// dung nào dùng — InnatePassiveCatalog phải lấp đủ, không để hook nằm không dùng mãi.</summary>
        [Test]
        public void InnatePassiveCatalog_UsesAllFourPreviouslyUnexercisedTriggers()
        {
            var usedTriggers = new System.Collections.Generic.HashSet<PassiveTrigger>();
            foreach (var defId in new[]
            {
                "hero_ember_knight", "hero_shadow_fang", "hero_frost_sage",
                "hero_dawn_cleric", "hero_gale_thief", "hero_bone_caller"
            })
                usedTriggers.Add(InnatePassiveCatalog.Get(defId).Trigger);

            Assert.IsTrue(usedTriggers.Contains(PassiveTrigger.OnTurnStart));
            Assert.IsTrue(usedTriggers.Contains(PassiveTrigger.OnDamageTaken));
            Assert.IsTrue(usedTriggers.Contains(PassiveTrigger.OnHpBelowThreshold));
            Assert.IsTrue(usedTriggers.Contains(PassiveTrigger.OnBreakTriggered));
        }

        [Test]
        public void InnatePassiveCatalog_FrostWard_TriggersViaCheckHpThreshold()
        {
            var (state, passive, a, _) = Setup();
            a.Passive = InnatePassiveCatalog.Get("hero_frost_sage");
            a.SetHp((int)(a.MaxHp * 0.1f)); // dưới ngưỡng 0.3

            passive.CheckHpThreshold(a, state.Rng);

            Assert.IsTrue(a.HasStatus(StatusId.DefUp), "Frost Ward phải tự áp DefUp khi HP dưới ngưỡng");
        }

        [Test]
        public void InnatePassiveCatalog_QuickReflexes_TriggersViaOnTurnStart()
        {
            var (state, passive, a, _) = Setup();
            a.Passive = InnatePassiveCatalog.Get("hero_shadow_fang");

            passive.TriggerTurnStart(a, state.Rng);

            Assert.IsTrue(a.HasStatus(StatusId.SpdUp));
        }

        // =====================================================================
        // Set Bonus 4-món — task-setbonus.md, plan.md §7.4
        // =====================================================================

        [Test]
        public void SetBonus_Ember_PerfectHit_AppliesBurnToTarget_NotSelf()
        {
            var (state, passive, a, b) = Setup();
            a.SetBonus = SetBonusCatalog.FourPiece("ember");

            passive.TriggerOnHitDealt(a, b, state.Rng, isCrit: false, isPerfect: true);

            Assert.IsTrue(b.HasStatus(StatusId.Burn), "Burn phải rơi lên địch (contextTarget), không phải actor");
            Assert.IsFalse(a.HasStatus(StatusId.Burn));
        }

        [Test]
        public void SetBonus_Ember_NonPerfectHit_DoesNotApplyBurn()
        {
            var (state, passive, a, b) = Setup();
            a.SetBonus = SetBonusCatalog.FourPiece("ember");

            passive.TriggerOnHitDealt(a, b, state.Rng, isCrit: false, isPerfect: false);

            Assert.IsFalse(b.HasStatus(StatusId.Burn), "RequiresPerfectGrade=true phải chặn đòn không Perfect");
        }

        [Test]
        public void SetBonus_Bastion_HpBelowThreshold_AppliesShieldWithCorrectAmount_ConsumedOnce()
        {
            var (state, passive, a, _) = Setup();
            a.SetBonus = SetBonusCatalog.FourPiece("bastion");
            a.SetHp((int)(a.MaxHp * 0.4f)); // dưới 50%

            passive.CheckHpThreshold(a, state.Rng);

            var shield = a.GetStatus(StatusId.Shield);
            Assert.IsNotNull(shield, "Bastion 4-món phải tự áp Shield khi HP < 50%");
            Assert.AreEqual(a.MaxHp * 0.20f, shield.Value, 0.01f, "Shield phải đúng 20% MaxHp");
            Assert.IsTrue(a.SetBonus.Consumed, "Chỉ 1 lần/trận");

            a.Statuses.Clear();
            passive.CheckHpThreshold(a, state.Rng);
            Assert.IsNull(a.GetStatus(StatusId.Shield), "Đã Consumed thì không được áp lại dù HP vẫn thấp");
        }

        [Test]
        public void SetBonus_Breaker_OnBreakTriggered_BuffsWholeTeam_NotJustOwner_NotEnemy()
        {
            var state = new BattleState { Rng = new XorShiftRandom(TestFactory.SEED) };
            var events = new Game.Combat.Events.CombatEventQueue();
            var status = new StatusProcessor(state, events);
            var passive = new PassiveProcessor(state, events, status);
            var a = TestFactory.Unit("a", TeamSide.Player);
            var ally = TestFactory.Unit("ally", TeamSide.Player);
            var enemy = TestFactory.Unit("enemy", TeamSide.Enemy);
            state.AddUnit(a); state.AddUnit(ally); state.AddUnit(enemy);
            a.FillResources(); ally.FillResources(); enemy.FillResources();
            a.SetBonus = SetBonusCatalog.FourPiece("breaker");

            passive.TriggerOnBreakTriggered(a, state.Rng);

            Assert.IsTrue(a.HasStatus(StatusId.AtkUp), "Owner (người phá Poise) cũng phải nhận buff");
            Assert.IsTrue(ally.HasStatus(StatusId.AtkUp), "TargetAllAllies phải buff luôn đồng minh khác");
            Assert.IsFalse(enemy.HasStatus(StatusId.AtkUp), "KHÔNG được buff nhầm sang phe địch");
        }

        [Test]
        public void SetBonus_Sage_OnPerfectCommand_RefundsPercentOfSpCost()
        {
            var (state, passive, a, _) = Setup();
            a.SetBonus = SetBonusCatalog.FourPiece("sage");
            a.SetSp(0);

            passive.TriggerOnPerfectCommand(a, state.Rng, spCost: 20);

            Assert.AreEqual(6, a.Sp, "Hoàn 30% của 20 SP cost = 6 (RoundToInt)");
        }

        [Test]
        public void SetBonus_Sage_NoSpCost_RefundsNothing()
        {
            var (state, passive, a, _) = Setup();
            a.SetBonus = SetBonusCatalog.FourPiece("sage");
            a.SetSp(0);

            passive.TriggerOnPerfectCommand(a, state.Rng); // spCost mặc định 0

            Assert.AreEqual(0, a.Sp);
        }

        [Test]
        public void SetBonus_Guardian_OnTurnEnd_HealsPercentMaxHp()
        {
            var (state, passive, a, _) = Setup();
            a.SetBonus = SetBonusCatalog.FourPiece("guardian");
            a.SetHp(a.MaxHp / 2);
            int hpBefore = a.Hp;

            passive.TriggerOnTurnEnd(a, state.Rng);

            Assert.AreEqual(hpBefore + (int)(a.MaxHp * 0.08f), a.Hp, 1);
        }

        [Test]
        public void SetBonus_Vampire_OnKill_HealsPercentMaxHp()
        {
            var (state, passive, a, _) = Setup();
            a.SetBonus = SetBonusCatalog.FourPiece("vampire");
            a.SetHp(a.MaxHp / 2);
            int hpBefore = a.Hp;

            passive.TriggerOnKill(a, state.Rng);

            Assert.AreEqual(hpBefore + (int)(a.MaxHp * 0.15f), a.Hp, 1);
        }

        [Test]
        public void SetBonus_Assassin_RequiresCrit_DoesNotFireOnNonCritHit()
        {
            var (state, passive, a, b) = Setup();
            a.SetBonus = SetBonusCatalog.FourPiece("assassin");

            passive.TriggerOnHitDealt(a, b, state.Rng, isCrit: false);

            Assert.IsFalse(b.HasStatus(StatusId.Bleed), "RequiresCrit=true phải chặn đòn không Crit");
        }

        [Test]
        public void SetBonus_Assassin_RequiresCrit_FiresOnCritHit()
        {
            // Catalog thật dùng Chance=0.5f (số liệu cân bằng, không đảm bảo áp mỗi lần) — test
            // gate RequiresCrit cần Chance=1f riêng để xác định, không phụ thuộc seed.
            var (state, passive, a, b) = Setup();
            a.SetBonus = new PassiveData
            {
                Trigger = PassiveTrigger.OnHitDealt,
                RequiresCrit = true,
                Applies = new[] { new StatusApplication(StatusId.Bleed, 1f, duration: 2, stacks: 1, targetSelf: false) }
            };

            passive.TriggerOnHitDealt(a, b, state.Rng, isCrit: true);

            Assert.IsTrue(b.HasStatus(StatusId.Bleed));
        }

        [Test]
        public void SetBonus_RequiresCrit_DoesNotAffect_OtherOnHitDealtPassivesWithoutTheFlag()
        {
            // Passive OnHitDealt không set RequiresCrit (VD mọi Awakening OnHitDealt hiện có)
            // phải KHÔNG bị chặn dù isCrit=false — chứng minh field mới không đổi hành vi cũ.
            var (state, passive, a, b) = Setup();
            a.Passive = new PassiveData
            {
                Trigger = PassiveTrigger.OnHitDealt,
                Applies = new[] { new StatusApplication(StatusId.SpdDown, 1f, duration: 2, stacks: 1, targetSelf: false) }
            };

            passive.TriggerOnHitDealt(a, b, state.Rng, isCrit: false);

            Assert.IsTrue(b.HasStatus(StatusId.SpdDown), "Passive OnHitDealt không RequiresCrit phải vẫn nổ dù đòn không Crit");
        }

        [Test]
        public void SetBonusCatalog_TwoPiece_AllEightSets_ReturnAtLeastOneModifier()
        {
            foreach (var setId in SetBonusCatalog.SET_IDS)
            {
                var mods = SetBonusCatalog.TwoPiece(setId);
                Assert.Greater(mods.Length, 0, $"Bộ '{setId}' phải có ít nhất 1 StatModifier 2-món");
            }
        }

        [Test]
        public void SetBonusCatalog_FourPiece_AllEightSets_HaveARealPassive()
        {
            foreach (var setId in SetBonusCatalog.SET_IDS)
                Assert.IsNotNull(SetBonusCatalog.FourPiece(setId), $"Bộ '{setId}' phải có bonus 4-món thật (task-setbonus.md)");
        }

        [Test]
        public void SetBonusCatalog_FourPiece_ReturnsIndependentInstances_NotShared()
        {
            var first = SetBonusCatalog.FourPiece("ember");
            var second = SetBonusCatalog.FourPiece("ember");
            first.Consumed = true;

            Assert.IsFalse(second.Consumed, "Mỗi lần gọi phải trả về instance mới — Consumed không rò rỉ giữa các trận");
        }
    }
}
