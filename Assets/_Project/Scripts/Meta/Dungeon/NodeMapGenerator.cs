using System.Collections.Generic;
using Game.Core.Random;
using Game.Data;
using Game.Data.Dto;

namespace Game.Meta.Dungeon
{
    /// <summary>
    /// Sinh node map phân nhánh — plan.md §8.1. Thuần C# (chỉ dùng Game.Core/Game.Data)
    /// nên test được không cần Unity.
    ///
    /// Diễn giải cụ thể hoá từ plan.md (vì bảng gốc có chỗ mơ hồ giữa "tầng" và "hàng"):
    ///   - Mỗi chương có 3 TẦNG hiển thị `[1/3]`..`[3/3]`.
    ///   - Mỗi tầng có <see cref="RowsPerTier"/> HÀNG lựa chọn (mặc định 2).
    ///   - Mỗi hàng có 2–3 node song song (người chơi chọn 1 trong số đó).
    ///   - Hàng cuối cùng của toàn chương luôn là 1 node BOSS duy nhất.
    ///   - Tổng node ≈ 12–15, khớp bảng 5 chương ở plan.md §8.2.
    /// </summary>
    public static class NodeMapGenerator
    {
        public const int RowsPerTier = 2;
        private const int MaxRetries = 20;

        /// <summary>Sinh map hợp lệ (luôn có đường Start→Boss). Tăng seed và thử lại nếu cần.</summary>
        public static RunStateDto Generate(long seed, int chapterId = 1)
        {
            for (int attempt = 0; attempt < MaxRetries; attempt++)
            {
                var nodes = TryGenerate((ulong)(seed + attempt));
                if (HasPathToBoss(nodes))
                {
                    return new RunStateDto
                    {
                        Active = true,
                        ChapterId = chapterId,
                        Seed = seed,
                        CurrentNodeId = -1,
                        MapNodes = nodes
                    };
                }
            }

            // Không nên xảy ra với thuật toán nối "đảm bảo liên thông" bên dưới,
            // nhưng nếu có thì trả về lần thử cuối thay vì crash — Balance Harness sẽ bắt được ở CI.
            return new RunStateDto
            {
                Active = true,
                ChapterId = chapterId,
                Seed = seed,
                CurrentNodeId = -1,
                MapNodes = TryGenerate((ulong)seed)
            };
        }

        private static List<MapNodeDto> TryGenerate(ulong seed)
        {
            var rng = new XorShiftRandom(seed);
            var nodes = new List<MapNodeDto>();
            var rows = new List<List<MapNodeDto>>();
            int nextId = 0;

            for (int tier = 0; tier < 3; tier++)
            {
                for (int r = 0; r < RowsPerTier; r++)
                {
                    bool isFirstRowOfChapter = tier == 0 && r == 0;
                    bool isLastRowOfTier = r == RowsPerTier - 1;

                    int count = rng.NextInt(2, 4); // 2 hoặc 3
                    var row = new List<MapNodeDto>(count);

                    for (int c = 0; c < count; c++)
                    {
                        var node = new MapNodeDto
                        {
                            Id = nextId++,
                            Tier = tier,
                            RowIndex = rows.Count,
                            ColIndex = c,
                            State = isFirstRowOfChapter ? (int)NodeState.Available : (int)NodeState.Locked,
                            Type = (int)PickType(rng, isFirstRowOfChapter, isLastRowOfTier)
                        };
                        row.Add(node);
                        nodes.Add(node);
                    }

                    rows.Add(row);
                }
            }

            // Hàng cuối chương: 1 node Boss duy nhất
            var bossNode = new MapNodeDto
            {
                Id = nextId,
                Tier = 2,
                RowIndex = rows.Count,
                ColIndex = 0,
                State = (int)NodeState.Locked,
                Type = (int)NodeType.Boss
            };
            nodes.Add(bossNode);
            rows.Add(new List<MapNodeDto> { bossNode });

            ConnectRows(rows, rng);
            EnsureTreasurePerTier(rows, rng);
            BreakConsecutiveElites(rows);

            return nodes;
        }

        // =====================================================================

        private static NodeType PickType(XorShiftRandom rng, bool isFirstRow, bool isLastRowOfTier)
        {
            if (isFirstRow) return NodeType.Battle;                 // dạy nhịp — luật cứng
            if (isLastRowOfTier) return rng.Chance(0.5f) ? NodeType.Rest : NodeType.Shop;

            float r = rng.NextFloat();
            // Tỉ lệ theo plan.md §8.1 (bỏ Boss — dành riêng cho hàng cuối chương)
            if (r < 0.45f) return NodeType.Battle;
            if (r < 0.60f) return NodeType.Elite;
            if (r < 0.70f) return NodeType.Treasure;
            if (r < 0.82f) return NodeType.Event;
            if (r < 0.90f) return NodeType.Shop;
            if (r < 0.98f) return NodeType.Rest;
            return NodeType.Mystery;
        }

        /// <summary>Nối mỗi node hàng r tới 1–2 node hàng r+1 gần nhất, đảm bảo mọi node
        /// đều có ≥1 đường vào và ≥1 đường ra (trừ hàng đầu/cuối).</summary>
        private static void ConnectRows(List<List<MapNodeDto>> rows, XorShiftRandom rng)
        {
            for (int r = 0; r < rows.Count - 1; r++)
            {
                var from = rows[r];
                var to = rows[r + 1];
                var incoming = new bool[to.Count];

                for (int i = 0; i < from.Count; i++)
                {
                    // Vị trí tương ứng tỉ lệ ở hàng sau
                    float t = from.Count <= 1 ? 0.5f : (float)i / (from.Count - 1);
                    int primary = GameMath_RoundToInt(t * (to.Count - 1));
                    from[i].NextIds.Add(to[primary].Id);
                    incoming[primary] = true;

                    // 40% có thêm nhánh phụ sang node liền kề (đa dạng đường đi)
                    if (to.Count > 1 && rng.Chance(0.4f))
                    {
                        int alt = primary == 0 ? primary + 1
                                : primary == to.Count - 1 ? primary - 1
                                : primary + (rng.Chance(0.5f) ? 1 : -1);
                        alt = Clamp(alt, 0, to.Count - 1);
                        if (!from[i].NextIds.Contains(to[alt].Id))
                        {
                            from[i].NextIds.Add(to[alt].Id);
                            incoming[alt] = true;
                        }
                    }
                }

                // Đảm bảo mọi node hàng sau có ít nhất 1 đường vào
                for (int j = 0; j < to.Count; j++)
                {
                    if (incoming[j]) continue;
                    float tBack = to.Count <= 1 ? 0.5f : (float)j / (to.Count - 1);
                    int nearest = Clamp(GameMath_RoundToInt(tBack * (from.Count - 1)), 0, from.Count - 1);
                    from[nearest].NextIds.Add(to[j].Id);
                }
            }
        }

        private static void EnsureTreasurePerTier(List<List<MapNodeDto>> rows, XorShiftRandom rng)
        {
            for (int tier = 0; tier < 3; tier++)
            {
                var tierRows = GetTierRows(rows, tier);
                bool hasTreasure = false;
                foreach (var row in tierRows)
                    foreach (var n in row)
                        if (n.Type == (int)NodeType.Treasure) hasTreasure = true;

                if (hasTreasure) continue;

                // Ép 1 node không nằm ở hàng đầu chương / hàng cuối tầng thành Treasure
                foreach (var row in tierRows)
                {
                    bool isFirstOfChapter = tier == 0 && row == tierRows[0];
                    bool isLastOfTier = row == tierRows[^1];
                    if (isFirstOfChapter || isLastOfTier) continue;

                    row[rng.NextInt(0, row.Count)].Type = (int)NodeType.Treasure;
                    hasTreasure = true;
                    break;
                }
                // Nếu tầng chỉ có hàng đầu/cuối (không có hàng giữa) thì bỏ qua — chấp nhận không có Treasure.
            }
        }

        /// <summary>Không cho 2 Elite nối trực tiếp nhau trên cùng một đường đi.</summary>
        private static void BreakConsecutiveElites(List<List<MapNodeDto>> rows)
        {
            var byId = new Dictionary<int, MapNodeDto>();
            foreach (var row in rows) foreach (var n in row) byId[n.Id] = n;

            foreach (var row in rows)
            {
                foreach (var n in row)
                {
                    if (n.Type != (int)NodeType.Elite) continue;
                    foreach (var nextId in n.NextIds)
                    {
                        if (byId[nextId].Type == (int)NodeType.Elite)
                            byId[nextId].Type = (int)NodeType.Battle;
                    }
                }
            }
        }

        private static List<List<MapNodeDto>> GetTierRows(List<List<MapNodeDto>> rows, int tier)
        {
            var result = new List<List<MapNodeDto>>();
            foreach (var row in rows)
                if (row.Count > 0 && row[0].Tier == tier) result.Add(row);
            return result;
        }

        /// <summary>BFS: có tồn tại đường từ bất kỳ node hàng đầu tới node Boss không.</summary>
        private static bool HasPathToBoss(List<MapNodeDto> nodes)
        {
            if (nodes.Count == 0) return false;
            var byId = new Dictionary<int, MapNodeDto>();
            foreach (var n in nodes) byId[n.Id] = n;

            var bossId = -1;
            foreach (var n in nodes) if (n.Type == (int)NodeType.Boss) bossId = n.Id;
            if (bossId < 0) return false;

            var starts = new List<int>();
            int minRow = int.MaxValue;
            foreach (var n in nodes) minRow = System.Math.Min(minRow, n.RowIndex);
            foreach (var n in nodes) if (n.RowIndex == minRow) starts.Add(n.Id);

            var visited = new HashSet<int>();
            var queue = new Queue<int>(starts);
            foreach (var s in starts) visited.Add(s);

            while (queue.Count > 0)
            {
                int cur = queue.Dequeue();
                if (cur == bossId) return true;
                foreach (var next in byId[cur].NextIds)
                {
                    if (visited.Add(next)) queue.Enqueue(next);
                }
            }
            return visited.Contains(bossId);
        }

        // =====================================================================

        private static int Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;
        private static int GameMath_RoundToInt(float v) => (int)(v + 0.5f);
    }
}
