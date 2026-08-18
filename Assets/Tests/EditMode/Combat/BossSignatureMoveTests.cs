using Game.Combat.Ai;
using Game.Combat.Model;
using Game.Combat.Systems;
using Game.Core.Random;
using Game.Data;
using NUnit.Framework;

namespace Game.Tests.Combat
{
    /// <summary>AIController SignatureMove — task-boss-phase-enrage.md, plan.md §4.13.3. Test
    /// trực tiếp <see cref="AIController.Choose"/> (không cần dựng cả CombatSimulation), cùng
    /// mẫu đã dùng để verify AI diversity trước đó.</summary>
    public class BossSignatureMoveTests
    {
        private CombatUnit _boss;
        private CombatUnit _target;
        private AIController _ai;
        private AIProfile _profile;
        private IRandomSource _rng;

        [SetUp]
        public void SetUp()
        {
            _boss = TestFactory.Unit("boss", TeamSide.Enemy);
            _boss.IsBoss = true;
            _boss.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(), 0)); // skill_test, slot 0
            var signatureData = TestFactory.BasicAttack(power: 3f);
            signatureData.Id = "skill_signature";
            _boss.Skills.Add(new SkillRuntime(signatureData, 1));

            _target = TestFactory.Unit("hero", TeamSide.Player);

            var state = new BattleState { Rng = new XorShiftRandom(1UL) };
            state.AddUnit(_boss);
            state.AddUnit(_target);
            _boss.FillResources();
            _target.FillResources();

            var targeting = new TargetSelector(state);
            _ai = new AIController(state, targeting);
            _rng = new XorShiftRandom(1UL);

            _profile = new AIProfile { Id = "ai_test_boss", Noise = 0f };
            _profile.Rules.Add(new AIRule
            {
                When = new AICondition(AIConditionType.Always),
                SkillSlot = 1, Weight = 70f, RuleCooldown = 2, IsSignatureMove = true
            });
            _profile.Rules.Add(new AIRule
            {
                When = new AICondition(AIConditionType.Always),
                SkillSlot = 0, Weight = 30f
            });
        }

        [Test]
        public void FirstChoose_SignatureRuleWins_BeginsTelegraph_ReturnsFallbackNotSignature()
        {
            var decision = _ai.Choose(_boss, _profile, phase: 1, rng: _rng);

            Assert.AreEqual("skill_test", decision.Skill.Data.Id, "phải rơi về fallback, chưa đánh signature ngay");
            Assert.AreEqual(3, _boss.SignatureMoveTurnsLeft);
            Assert.AreEqual("skill_signature", _boss.PendingSignatureMoveSkillId);
        }

        [Test]
        public void SubsequentChooses_CountDownWithoutExecuting_ThenExecuteOnZero()
        {
            _ai.Choose(_boss, _profile, phase: 1, rng: _rng); // turn 1: bắt đầu đếm, 3

            var d2 = _ai.Choose(_boss, _profile, phase: 1, rng: _rng); // turn 2: 3→2
            Assert.AreEqual(2, _boss.SignatureMoveTurnsLeft);
            Assert.AreNotEqual("skill_signature", d2.Skill.Data.Id);

            var d3 = _ai.Choose(_boss, _profile, phase: 1, rng: _rng); // turn 3: 2→1
            Assert.AreEqual(1, _boss.SignatureMoveTurnsLeft);
            Assert.AreNotEqual("skill_signature", d3.Skill.Data.Id);

            var d4 = _ai.Choose(_boss, _profile, phase: 1, rng: _rng); // turn 4: 1→0, thực thi
            Assert.AreEqual(0, _boss.SignatureMoveTurnsLeft);
            Assert.AreEqual("skill_signature", d4.Skill.Data.Id);
            Assert.IsNull(_boss.PendingSignatureMoveSkillId);
        }

        [Test]
        public void Broken_DuringTelegraph_CancelsSignatureMove()
        {
            _ai.Choose(_boss, _profile, phase: 1, rng: _rng); // bắt đầu đếm
            _boss.BrokenTurnsLeft = 1; // Break — counterplay bắt buộc theo plan.md §4.13.3

            _ai.Choose(_boss, _profile, phase: 1, rng: _rng);

            Assert.AreEqual(0, _boss.SignatureMoveTurnsLeft);
            Assert.IsNull(_boss.PendingSignatureMoveSkillId);
        }

        [Test]
        public void Peek_DuringTelegraph_DoesNotMutateState()
        {
            _ai.Choose(_boss, _profile, phase: 1, rng: _rng); // bắt đầu đếm, 3
            int before = _boss.SignatureMoveTurnsLeft;

            _ai.Choose(_boss, _profile, phase: 1, rng: _rng, peek: true);

            Assert.AreEqual(before, _boss.SignatureMoveTurnsLeft, "peek (Intent Preview) không được tiêu trạng thái thật");
        }

        [Test]
        public void NonBossProfile_WithoutSignatureFlag_ExecutesImmediately_UnaffectedByFeature()
        {
            var basic = new AIProfile { Id = "ai_basic_test", Noise = 0f };
            basic.Rules.Add(new AIRule { When = new AICondition(AIConditionType.Always), SkillSlot = 0, Weight = 50f });

            var decision = _ai.Choose(_boss, basic, phase: 1, rng: _rng);

            Assert.AreEqual("skill_test", decision.Skill.Data.Id);
            Assert.AreEqual(0, _boss.SignatureMoveTurnsLeft, "không có rule IsSignatureMove thì không kích hoạt gì cả");
        }
    }
}
