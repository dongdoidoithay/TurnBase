using System.Collections.Generic;
using Game.Combat;
using Game.Combat.Model;
using Game.Data;
using NUnit.Framework;

namespace Game.Tests.Combat
{
    /// <summary>BattleReplay — edge case E17 (plan.md §4.14/§4.17). Chỉ test phần LÕI thuần C#
    /// (đúng kỷ luật determinism của Game.Combat) — phần ghi/đọc BattleSnapshotDto qua save và
    /// hook OnApplicationPause thuộc Game.CombatView/Game.Meta, verify riêng qua execute_code
    /// Play mode (xem task-edgecases.md §9), không lặp lại ở EditMode vì phụ thuộc MonoBehaviour
    /// lifecycle/Resources.Load.</summary>
    public class BattleReplayTests
    {
        /// <summary>Chính sách chơi tối giản, xác định: luôn đánh thường (slot 0) vào địch đầu
        /// tiên còn sống của phe đối diện — đủ để lái trận tiến triển thật mà không cần AI/UI.</summary>
        private static int FirstEnemyId(BattleState state, CombatUnit actor)
        {
            foreach (var u in state.Units)
                if (u.Side != actor.Side && u.IsAlive) return u.Id;
            return -1;
        }

        /// <summary>Chạy <paramref name="playerTurns"/> lượt người chơi bằng chính sách trên,
        /// ghi lại từng ActionIntent đã submit — mô phỏng 1 trận LIVE thật trước khi "thoát app".</summary>
        private static List<ActionIntent> PlayAndRecord(CombatSimulation sim, int playerTurns)
        {
            var recorded = new List<ActionIntent>();
            int guard = 0;
            for (int i = 0; i < playerTurns && guard++ < 500;)
            {
                bool waiting = sim.Advance();
                if (!waiting || sim.IsFinished) break;

                var actor = sim.State.Units.Find(u => u.Id == sim.State.CurrentActorId);
                int targetId = FirstEnemyId(sim.State, actor);
                if (targetId < 0) break;

                var intent = new ActionIntent(actor.Id, 0, targetId, CommandGrade.Good);
                recorded.Add(intent);
                sim.SubmitIntent(intent);
                i++;
            }
            return recorded;
        }

        [Test]
        public void ReplayPlayerIntents_ReproducesIdenticalState_AtResumePoint()
        {
            var live = TestFactory.TeamBattle(out _, out _, seed: 777UL);
            var recorded = PlayAndRecord(live, playerTurns: 5);

            var fresh = TestFactory.TeamBattle(out _, out _, seed: 777UL);
            bool ok = BattleReplay.ReplayPlayerIntents(fresh, recorded);

            Assert.IsTrue(ok);
            Assert.AreEqual(live.State.CurrentActorId, fresh.State.CurrentActorId);
            Assert.AreEqual(live.State.RoundNumber, fresh.State.RoundNumber);
            Assert.AreEqual(live.State.TurnCounter, fresh.State.TurnCounter);
            Assert.AreEqual(live.State.UltimateGauge, fresh.State.UltimateGauge);
            for (int i = 0; i < live.State.Units.Count; i++)
                Assert.AreEqual(live.State.Units[i].Hp, fresh.State.Units[i].Hp,
                    $"Unit {i} HP phải khớp tuyệt đối sau replay — đây chính là edge case E17");
        }

        [Test]
        public void ReplayPlayerIntents_AllowsContinuingPlay_StaysInSyncAfterResume()
        {
            var live = TestFactory.TeamBattle(out _, out _, seed: 555UL);
            var recorded = PlayAndRecord(live, playerTurns: 4);

            var fresh = TestFactory.TeamBattle(out _, out _, seed: 555UL);
            Assert.IsTrue(BattleReplay.ReplayPlayerIntents(fresh, recorded));

            // Chơi tiếp CẢ HAI sim thêm 6 lượt bằng đúng chính sách — mô phỏng người chơi tiếp
            // tục sau khi app resume. Nếu BattleReplay đúng, 2 sim phải tiến triển giống hệt.
            PlayAndRecord(live, playerTurns: 6);
            PlayAndRecord(fresh, playerTurns: 6);

            Assert.AreEqual(live.State.Result, fresh.State.Result);
            for (int i = 0; i < live.State.Units.Count; i++)
                Assert.AreEqual(live.State.Units[i].Hp, fresh.State.Units[i].Hp,
                    $"Unit {i} HP phải vẫn khớp sau khi chơi tiếp — replay không chỉ đúng tại điểm dừng mà cả về sau");
        }

        [Test]
        public void ReplayPlayerIntents_ReturnsFalse_WhenActorIdDoesNotMatch()
        {
            var sim = TestFactory.TeamBattle(out _, out _, seed: 1UL);
            var badIntents = new List<ActionIntent> { new ActionIntent(actorId: 9999, 0, 0, CommandGrade.Good) };

            bool ok = BattleReplay.ReplayPlayerIntents(sim, badIntents);

            Assert.IsFalse(ok, "ActorId không khớp actor đang chờ input → replay phải báo lỗi, không cố chơi tiếp từ trạng thái sai");
        }

        [Test]
        public void ReplayPlayerIntents_EmptyList_IsNoOp()
        {
            var sim = TestFactory.TeamBattle(out _, out _, seed: 2UL);
            int actorBefore = sim.State.CurrentActorId;

            bool ok = BattleReplay.ReplayPlayerIntents(sim, new List<ActionIntent>());

            Assert.IsTrue(ok);
            Assert.AreEqual(actorBefore, sim.State.CurrentActorId, "Danh sách rỗng không được làm sim tiến triển");
        }
    }
}
