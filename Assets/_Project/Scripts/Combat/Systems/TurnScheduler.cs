using System;
using System.Collections.Generic;
using Game.Combat.Model;
using Game.Data;

namespace Game.Combat.Systems
{
    /// <summary>Ném khi ATB không bao giờ đạt ngưỡng — chống treo vô hạn.</summary>
    public sealed class BattleStalemateException : Exception
    {
        public BattleStalemateException(string message) : base(message) { }
    }

    /// <summary>
    /// ATB rời rạc (tick-based) — plan.md §4.4.
    /// Deterministic tuyệt đối: tie-break theo ATB → SPD → UnitId.
    /// </summary>
    public sealed class TurnScheduler
    {
        public const int ATB_THRESHOLD = 1000;
        private const int MAX_TICKS_PER_TURN = 100_000;

        private readonly BattleState _state;
        private readonly List<CombatUnit> _readyBuffer = new(16);

        public TurnScheduler(BattleState state) => _state = state;

        /// <summary>
        /// Tick ATB tới khi có unit đạt ngưỡng, trả về unit đó.
        /// Trả null nếu không còn unit sống ở một trong hai phe.
        /// </summary>
        public CombatUnit AdvanceToNextActor()
        {
            int ticks = 0;

            while (true)
            {
                _readyBuffer.Clear();
                for (int i = 0; i < _state.Units.Count; i++)
                {
                    var u = _state.Units[i];
                    if (u.IsAlive && u.Atb >= ATB_THRESHOLD) _readyBuffer.Add(u);
                }

                if (_readyBuffer.Count > 0)
                {
                    SortStable(_readyBuffer);
                    return _readyBuffer[0];
                }

                bool anyAlive = false;
                for (int i = 0; i < _state.Units.Count; i++)
                {
                    var u = _state.Units[i];
                    if (!u.IsAlive) continue;
                    anyAlive = true;
                    // SpdEffective đã kẹp sàn 10 → luôn tiến tới ngưỡng (edge case E22)
                    u.Atb += (int)MathF.Round(u.SpdEffective);
                }

                if (!anyAlive) return null;

                if (++ticks > MAX_TICKS_PER_TURN)
                    throw new BattleStalemateException(
                        $"ATB không đạt ngưỡng sau {MAX_TICKS_PER_TURN} tick — kiểm tra SPD bị 0 hoặc âm.");
            }
        }

        /// <summary>Trừ ngưỡng sau khi unit hành động xong (pipeline bước 13).</summary>
        public void ConsumeTurn(CombatUnit actor, int extraAtbCost = 0)
        {
            actor.Atb -= ATB_THRESHOLD + extraAtbCost;
            if (actor.Atb < 0) actor.Atb = 0;
        }

        /// <summary>
        /// Preview n lượt kế tiếp cho TurnOrderBar — chạy mô phỏng trên BẢN SAO ATB,
        /// không side effect lên trạng thái thật.
        /// </summary>
        public List<int> PreviewOrder(int count)
        {
            var result = new List<int>(count);
            if (count <= 0) return result;

            // Sao chép ATB
            int n = _state.Units.Count;
            var ids = new int[n];
            var atb = new int[n];
            var spd = new float[n];
            var alive = new bool[n];
            for (int i = 0; i < n; i++)
            {
                var u = _state.Units[i];
                ids[i] = u.Id; atb[i] = u.Atb; spd[i] = u.SpdEffective; alive[i] = u.IsAlive;
            }

            int guard = 0;
            while (result.Count < count)
            {
                int best = -1;
                for (int i = 0; i < n; i++)
                {
                    if (!alive[i] || atb[i] < ATB_THRESHOLD) continue;
                    if (best < 0) { best = i; continue; }
                    if (atb[i] > atb[best]) { best = i; continue; }
                    if (atb[i] == atb[best])
                    {
                        if (spd[i] > spd[best]) { best = i; continue; }
                        if (Math.Abs(spd[i] - spd[best]) < 0.001f && ids[i] < ids[best]) best = i;
                    }
                }

                if (best >= 0)
                {
                    result.Add(ids[best]);
                    atb[best] -= ATB_THRESHOLD;
                    continue;
                }

                for (int i = 0; i < n; i++)
                    if (alive[i]) atb[i] += (int)MathF.Round(spd[i]);

                if (++guard > MAX_TICKS_PER_TURN) break;
            }

            return result;
        }

        /// <summary>Sắp xếp ổn định: ATB giảm dần → SPD giảm dần → Id tăng dần.</summary>
        private static void SortStable(List<CombatUnit> list)
        {
            list.Sort(static (a, b) =>
            {
                int c = b.Atb.CompareTo(a.Atb);
                if (c != 0) return c;
                c = b.SpdEffective.CompareTo(a.SpdEffective);
                if (c != 0) return c;
                return a.Id.CompareTo(b.Id);
            });
        }

        /// <summary>Reset ATB về 0 — dùng khi unit bị Break (mất lượt kế tiếp).</summary>
        public static void ResetAtb(CombatUnit u) => u.Atb = 0;
    }
}
