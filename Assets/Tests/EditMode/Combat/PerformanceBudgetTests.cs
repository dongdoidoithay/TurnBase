using System.Collections.Generic;
using System.Diagnostics;
using Game.Combat;
using Game.Combat.Model;
using Game.Core.Random;
using Game.Data;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Game.Tests.Combat
{
    /// <summary>
    /// plan.md §11.11 "Ngân sách hiệu năng" — P7 chưa từng có test nào trước phiên này (roadmap.md
    /// §0.1 P7 tự ghi "chưa có test hiệu năng/GC/soak/build size"). Chỉ xây được phần THẬT SỰ đo
    /// được trong EditMode (không có thiết bị thật/Play mode ổn định — MCP hay bị frame-stall, xem
    /// feedback_unity_mcp_ui_gotchas):
    /// - GC alloc/frame khi chiến đấu = 0 B (dòng DUY NHẤT trong bảng §11.11 có số liệu tuyệt đối,
    ///   test được bằng <see cref="Is.Not.AllocatingGCMemory"/> — hạ tầng chuẩn của
    ///   UnityEngine.TestRunner, không cần package rời).
    /// - "Soak" rút gọn: KHÔNG chạy được 2 giờ thật trong 1 lượt CI — thay bằng ngưỡng thời lượng
    ///   ví dụ (2000 trận ngẫu nhiên, giống <c>FuzzBattleTests</c> nhưng thêm ràng buộc thời gian)
    ///   để bắt hồi quy hiệu năng rõ rệt (VD lỡ tay đổi O(n) thành O(n²)), KHÔNG phải benchmark
    ///   tuyệt đối (máy CI/máy dev khác tốc độ) — ngưỡng cố ý RỘNG, chỉ bắt regression thật sự lớn.
    /// FPS/draw call/RAM/Boot time/build size AAB (nốt còn lại của §11.11) đều cần thiết bị thật/
    /// build thật — KHÔNG giả lập ở đây, ghi rõ là giới hạn thay vì bỏ qua âm thầm.
    /// </summary>
    public class PerformanceBudgetTests
    {
        /// <summary>
        /// Cô lập ĐÚNG vòng lặp lượt (Advance/SubmitIntent) khỏi chi phí dựng trận 1 lần
        /// (BuildRandomBattle/Start — hợp lệ, không phải "mỗi frame khi chiến đấu"). Gọi
        /// <see cref="CombatSimulation.Start"/> trước khi đo — <see cref="CombatSimulation.
        /// RunToCompletion"/> tự bỏ qua Start() nếu Phase đã khác Init, nên phần đo chỉ còn lại
        /// đúng vòng lặp turn-by-turn thật.
        /// </summary>
        [Test]
        public void SteadyStateTurnLoop_DoesNotAllocateGC()
        {
            var sim = BuildBattle(seed: 90210);
            sim.Start();

            // Warm-up 1 lần NGOÀI phép đo — lần gọi đầu tiên sau domain reload có thể JIT/cache
            // nội bộ (Dictionary tĩnh, v.v.) hợp lệ không tính là "alloc mỗi frame khi chiến đấu".
            var warmup = BuildBattle(seed: 1);
            warmup.Start();
            warmup.RunToCompletion();

            Assert.That(() => { sim.RunToCompletion(); }, Is.Not.AllocatingGCMemory(),
                "CombatSimulation.RunToCompletion() (vòng lặp Advance/SubmitIntent ổn định) cấp " +
                "phát GC — vi phạm plan.md §11.11 'GC alloc/frame khi chiến đấu = 0 B'.");
        }

        /// <summary>
        /// "Soak" rút gọn — KHÔNG thay thế test 2 giờ thật trên thiết bị thật (roadmap.md P7 vẫn
        /// còn thiếu đúng phần đó). Ngưỡng thời gian CỐ Ý rộng (10s cho 2000 trận ~ 5ms/trận trung
        /// bình, máy CI chậm nhất cũng dư sức) — mục đích bắt hồi quy độ phức tạp thuật toán rõ
        /// rệt, không phải đo hiệu năng tuyệt đối (khác máy khác số).
        /// </summary>
        [Test]
        public void Batch2000RandomBattles_CompletesWithinTimeBudget()
        {
            const int RUNS = 2000;
            const int BUDGET_MS = 10_000;

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < RUNS; i++)
            {
                var sim = BuildBattle(seed: (ulong)(i * 7919 + 13));
                sim.RunToCompletion();
            }
            sw.Stop();

            TestContext.WriteLine($"{RUNS} trận trong {sw.ElapsedMilliseconds}ms " +
                                   $"({sw.ElapsedMilliseconds / (double)RUNS:F2}ms/trận)");
            Assert.Less(sw.ElapsedMilliseconds, BUDGET_MS,
                $"{RUNS} trận mất {sw.ElapsedMilliseconds}ms, vượt ngưỡng {BUDGET_MS}ms — " +
                "nghi ngờ hồi quy độ phức tạp thuật toán (không phải benchmark tuyệt đối).");
        }

        private static CombatSimulation BuildBattle(ulong seed)
        {
            var rng = new XorShiftRandom(seed);
            var sim = new CombatSimulation(seed);

            var elements = new[]
            {
                Element.Fire, Element.Water, Element.Earth, Element.Wind, Element.Light, Element.Dark,
            };

            var units = new List<CombatUnit>(6);
            for (int i = 0; i < 3; i++)
            {
                units.Add(TestFactory.Unit($"p{i}", TeamSide.Player,
                    i == 0 ? Row.Front : Row.Back, elements[rng.NextInt(elements.Length)]));
                units.Add(TestFactory.Unit($"e{i}", TeamSide.Enemy,
                    i == 0 ? Row.Front : Row.Back, elements[rng.NextInt(elements.Length)]));
            }

            foreach (var u in units)
            {
                u.Skills.Add(new SkillRuntime(TestFactory.BasicAttack(u.Element), 0));
                sim.State.Units.Add(u);
            }

            return sim;
        }
    }
}
