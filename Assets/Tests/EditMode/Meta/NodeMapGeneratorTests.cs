using System.Collections.Generic;
using Game.Data;
using Game.Data.Dto;
using Game.Meta.Dungeon;
using NUnit.Framework;

namespace Game.Tests.Meta
{
    /// <summary>Node map — plan.md §8.1. Bất biến quan trọng nhất: LUÔN có đường Start→Boss.</summary>
    public class NodeMapGeneratorTests
    {
        [Test]
        public void Generate_AlwaysHasPathFromStartToBoss()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                var run = NodeMapGenerator.Generate(seed);
                Assert.IsTrue(HasPath(run), $"Không có đường Start→Boss với seed {seed}");
            }
        }

        [Test]
        public void Generate_IsDeterministic_ForSameSeed()
        {
            var a = NodeMapGenerator.Generate(20260807);
            var b = NodeMapGenerator.Generate(20260807);

            Assert.AreEqual(a.MapNodes.Count, b.MapNodes.Count);
            for (int i = 0; i < a.MapNodes.Count; i++)
            {
                Assert.AreEqual(a.MapNodes[i].Type, b.MapNodes[i].Type);
                Assert.AreEqual(a.MapNodes[i].NextIds.Count, b.MapNodes[i].NextIds.Count);
            }
        }

        [Test]
        public void Generate_HasExactlyOneBoss_InLastRow()
        {
            var run = NodeMapGenerator.Generate(1);
            int maxRow = 0, bossCount = 0, bossRow = -1;

            foreach (var n in run.MapNodes) maxRow = System.Math.Max(maxRow, n.RowIndex);
            foreach (var n in run.MapNodes)
            {
                if (n.Type != (int)NodeType.Boss) continue;
                bossCount++;
                bossRow = n.RowIndex;
            }

            Assert.AreEqual(1, bossCount, "Phải có đúng 1 node Boss");
            Assert.AreEqual(maxRow, bossRow, "Boss phải nằm ở hàng cuối chương");
        }

        [Test]
        public void Generate_FirstRow_IsAllBattle()
        {
            var run = NodeMapGenerator.Generate(2);
            foreach (var n in run.MapNodes)
                if (n.RowIndex == 0)
                    Assert.AreEqual((int)NodeType.Battle, n.Type, "Hàng đầu chương phải toàn Battle (dạy nhịp)");
        }

        [Test]
        public void Generate_NoTwoConsecutiveElites_OnAnyEdge()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                var run = NodeMapGenerator.Generate(seed);
                var byId = new Dictionary<int, MapNodeDto>();
                foreach (var n in run.MapNodes) byId[n.Id] = n;

                foreach (var n in run.MapNodes)
                {
                    if (n.Type != (int)NodeType.Elite) continue;
                    foreach (var nextId in n.NextIds)
                        Assert.AreNotEqual((int)NodeType.Elite, byId[nextId].Type,
                            $"2 Elite liên tiếp ở seed {seed}: {n.Id} → {nextId}");
                }
            }
        }

        [Test]
        public void Generate_EveryNonLastNode_HasAtLeastOneOutgoingEdge()
        {
            var run = NodeMapGenerator.Generate(3);
            int maxRow = 0;
            foreach (var n in run.MapNodes) maxRow = System.Math.Max(maxRow, n.RowIndex);

            foreach (var n in run.MapNodes)
                if (n.RowIndex < maxRow)
                    Assert.Greater(n.NextIds.Count, 0, $"Node {n.Id} (hàng {n.RowIndex}) không có đường ra");
        }

        [Test]
        public void Generate_EveryNonFirstNode_HasAtLeastOneIncomingEdge()
        {
            var run = NodeMapGenerator.Generate(4);
            var hasIncoming = new HashSet<int>();
            foreach (var n in run.MapNodes)
                foreach (var next in n.NextIds)
                    hasIncoming.Add(next);

            foreach (var n in run.MapNodes)
                if (n.RowIndex > 0)
                    Assert.IsTrue(hasIncoming.Contains(n.Id), $"Node {n.Id} (hàng {n.RowIndex}) không có đường vào");
        }

        [Test]
        public void Generate_NodeCount_IsWithinExpectedRange()
        {
            var run = NodeMapGenerator.Generate(5);
            // 3 tầng × 2 hàng × 2-3 node + 1 boss = 13..19
            Assert.GreaterOrEqual(run.MapNodes.Count, 12);
            Assert.LessOrEqual(run.MapNodes.Count, 20);
        }

        [Test]
        public void Generate_FirstRowNodes_AreAvailable_RestAreLocked()
        {
            var run = NodeMapGenerator.Generate(6);
            foreach (var n in run.MapNodes)
            {
                if (n.RowIndex == 0) Assert.AreEqual((int)NodeState.Available, n.State);
                else Assert.AreEqual((int)NodeState.Locked, n.State);
            }
        }

        // =====================================================================

        private static bool HasPath(RunStateDto run)
        {
            var byId = new Dictionary<int, MapNodeDto>();
            foreach (var n in run.MapNodes) byId[n.Id] = n;

            int bossId = -1;
            foreach (var n in run.MapNodes) if (n.Type == (int)NodeType.Boss) bossId = n.Id;
            if (bossId < 0) return false;

            var starts = new List<int>();
            foreach (var n in run.MapNodes) if (n.RowIndex == 0) starts.Add(n.Id);

            var visited = new HashSet<int>(starts);
            var queue = new Queue<int>(starts);
            while (queue.Count > 0)
            {
                int cur = queue.Dequeue();
                if (cur == bossId) return true;
                foreach (var next in byId[cur].NextIds)
                    if (visited.Add(next)) queue.Enqueue(next);
            }
            return false;
        }
    }
}
