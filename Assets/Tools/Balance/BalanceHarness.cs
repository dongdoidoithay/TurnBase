using System.Collections.Generic;
using System.Text;
using Game.Core.Random;
using Game.Data;
using Game.Data.Dto;
using Game.Meta.Dungeon;
using Game.Meta.Gacha;
using Game.Meta.Hero;
using UnityEditor;
using UnityEngine;

namespace Game.Tools.Balance
{
    /// <summary>
    /// Đo tỉ lệ gacha/vật liệu để cân bằng — task-ascend.md §7 mục D. Tool thủ công cho dev
    /// (không phải logic gameplay), theo đúng style <c>Assets/Tools/DataImport/DataValidator.cs</c>.
    /// Log ra Console, không ghi file — xem kết quả qua Unity Console/unityMCP read_console.
    /// </summary>
    public static class BalanceHarness
    {
        private const int SIMULATED_OWNED_HEROES = 6; // giả định đội hình đầy 6/6 hero hiện có

        [MenuItem("Tools/Balance Harness/Gacha Pity Report")]
        public static void GachaPityReport()
        {
            var rng = new XorShiftRandom((ulong)System.DateTime.UtcNow.Ticks);

            // 1) Tỉ lệ GỐC (mỗi roll dùng state mới — không cộng dồn pity) — so trực tiếp bảng §9.3.
            const int SAMPLES = 100_000;
            int legendary = 0, epic = 0, rare = 0, common = 0;
            for (int i = 0; i < SAMPLES; i++)
            {
                switch (GachaSystem.RollRarity(new GachaStateDto(), rng))
                {
                    case Rarity.Legendary: legendary++; break;
                    case Rarity.Epic: epic++; break;
                    case Rarity.Rare: rare++; break;
                    default: common++; break;
                }
            }

            // 2) Tỉ lệ THỰC TẾ trên 1 chuỗi pull liên tục dài — có pity cộng dồn, cao hơn base rate.
            var chainState = new GachaStateDto();
            int chainLegendary = 0, chainEpic = 0;
            const int CHAIN_PULLS = 100_000;
            for (int i = 0; i < CHAIN_PULLS; i++)
            {
                var r = GachaSystem.RollRarity(chainState, rng);
                if (r == Rarity.Legendary) chainLegendary++;
                else if (r == Rarity.Epic) chainEpic++;
            }

            Debug.Log(
                $"[BalanceHarness] Gacha Pity Report\n" +
                $"— Tỉ lệ GỐC ({SAMPLES:N0} roll, state mới mỗi lần, không pity):\n" +
                $"    Legendary {(float)legendary / SAMPLES:P3}  (mục tiêu 1.500%)\n" +
                $"    Epic      {(float)epic / SAMPLES:P3}  (mục tiêu 12.000%)\n" +
                $"    Rare      {(float)rare / SAMPLES:P3}  (mục tiêu 36.500%)\n" +
                $"    Common    {(float)common / SAMPLES:P3}  (mục tiêu 50.000%)\n" +
                $"— Tỉ lệ THỰC TẾ ({CHAIN_PULLS:N0} pull liên tục, có pity cộng dồn):\n" +
                $"    Legendary {(float)chainLegendary / CHAIN_PULLS:P3}  (~1 mỗi {(float)CHAIN_PULLS / System.Math.Max(1, chainLegendary):F1} pull)\n" +
                $"    Epic      {(float)chainEpic / CHAIN_PULLS:P3}  (~1 mỗi {(float)CHAIN_PULLS / System.Math.Max(1, chainEpic):F1} pull)");
        }

        private readonly struct ChapterStats
        {
            public readonly double AvgTreasureVisited, AvgGold, AvgShards;
            public readonly Dictionary<CurrencyType, double> AvgMaterials;
            public ChapterStats(double avgTreasureVisited, double avgGold, double avgShards,
                Dictionary<CurrencyType, double> avgMaterials)
            {
                AvgTreasureVisited = avgTreasureVisited; AvgGold = avgGold; AvgShards = avgShards;
                AvgMaterials = avgMaterials;
            }
        }

        /// <summary>Mô phỏng ĐÚNG 1 lần chơi qua chương (Treasure dọc đường ngẫu nhiên + 1 Boss
        /// cuối), lặp <paramref name="runs"/> lần chỉ để lấy trung bình Monte Carlo chính xác — KHÔNG
        /// đại diện cho việc "cày lại chương N nhiều lần" (chương không replay được, xem
        /// task-balance-loottable.md §0 mục 4: mỗi chương chỉ chơi 1 lần trong 1 playthrough thật).</summary>
        private static ChapterStats SimulateChapter(int chapter, int runs, IRandomSource rng)
        {
            var treasureTable = LootRoller.Resolve(chapter, NodeType.Treasure);
            var bossTable = LootRoller.Resolve(chapter, NodeType.Boss);

            long totalGold = 0, totalShards = 0, totalTreasureVisited = 0;
            var totalMaterials = new Dictionary<CurrencyType, long>();
            void Add(CurrencyType type, long amount)
                => totalMaterials[type] = totalMaterials.GetValueOrDefault(type) + amount;

            for (int i = 0; i < runs; i++)
            {
                var run = NodeMapGenerator.Generate(rng.NextInt(int.MaxValue), chapter);
                var path = WalkRandomPath(run, rng);

                foreach (var node in path)
                {
                    if ((NodeType)node.Type != NodeType.Treasure) continue;
                    totalTreasureVisited++;

                    if (treasureTable != null)
                    {
                        var roll = LootRoller.Roll(treasureTable, rng, SIMULATED_OWNED_HEROES);
                        totalGold += roll.Gold;
                        foreach (var m in roll.Materials) Add(m.Type, m.Amount);
                        if (roll.ShardHeroIndex >= 0) totalShards += 1;
                    }
                    else
                    {
                        var roll = PlaceholderLootTable.RollTreasure(rng, SIMULATED_OWNED_HEROES);
                        totalGold += roll.Gold;
                        if (roll.EssenceIAmount > 0) Add(CurrencyType.EssenceI, roll.EssenceIAmount);
                        else if (roll.ShardHeroIndex >= 0) totalShards += 1;
                    }
                }

                // NodeMapGenerator đảm bảo luôn đúng 1 Boss cuối chương — cộng thưởng Boss 1 lần/run.
                if (bossTable != null)
                {
                    var roll = LootRoller.Roll(bossTable, rng, SIMULATED_OWNED_HEROES);
                    foreach (var m in roll.Materials) Add(m.Type, m.Amount);
                    // Đúng với MetaSceneInstaller.GrantBossAscendMaterials thật: 1 roll duy nhất,
                    // mảnh hero chỉ ra nếu trúng HeroShardChance (Boss ch1-5 hiện đều =0 — xem
                    // task-balance-loottable.md §0 mục 2, KHÔNG cộng cứng SIMULATED_OWNED_HEROES).
                    if (roll.ShardHeroIndex >= 0) totalShards += 1;
                }
                else
                {
                    Add(CurrencyType.EssenceII, PlaceholderLootTable.BOSS_REWARD_ESSENCE_II);
                    Add(CurrencyType.EssenceIII, PlaceholderLootTable.BOSS_REWARD_ESSENCE_III);
                    Add(CurrencyType.Core, PlaceholderLootTable.BOSS_REWARD_CORE);
                }
            }

            var avgMaterials = new Dictionary<CurrencyType, double>();
            foreach (var kv in totalMaterials) avgMaterials[kv.Key] = kv.Value / (double)runs;

            return new ChapterStats(
                (double)totalTreasureVisited / runs,
                (double)totalGold / runs,
                (double)totalShards / runs,
                avgMaterials);
        }

        [MenuItem("Tools/Balance Harness/Material Drop Report")]
        public static void MaterialDropReport() => Debug.Log(BuildMaterialDropReport());

        /// <summary>Tách riêng khỏi <see cref="MaterialDropReport"/> để gọi trực tiếp lấy chuỗi đầy
        /// đủ (VD qua execute_code khi verify) — Unity Console chỉ hiện DÒNG ĐẦU của 1 Debug.Log
        /// nhiều dòng trong danh sách tóm tắt, dễ tưởng nhầm là báo cáo bị cắt cụt.</summary>
        internal static string BuildMaterialDropReport()
        {
            const int RUNS_PER_CHAPTER = 1_000;
            var rng = new XorShiftRandom((ulong)System.DateTime.UtcNow.Ticks);

            // task-loottable-chapters.md — mỗi chương giờ có bảng Treasure/Boss riêng, phải đo
            // từng chương chứ không hardcode chương 1 như bản cũ (task-balance-loottable.md §0 mục 1).
            LootRoller.ClearCache();

            var sb = new StringBuilder();
            sb.AppendLine($"[BalanceHarness] Material Drop Report ({RUNS_PER_CHAPTER:N0} run/chương, " +
                          $"mỗi run đi 1 đường ngẫu nhiên tới Boss, giả định {SIMULATED_OWNED_HEROES} hero sở hữu):");

            double totalShardsAllChapters = 0;
            var totalMaterialsAllChapters = new Dictionary<CurrencyType, double>();

            for (int chapter = 1; chapter <= 5; chapter++)
            {
                var stats = SimulateChapter(chapter, RUNS_PER_CHAPTER, rng);

                sb.AppendLine($"  — Chương {chapter}:");
                sb.AppendLine($"      Treasure ghé được: {stats.AvgTreasureVisited:F2} node");
                sb.AppendLine($"      Gold từ Treasure:  {stats.AvgGold:F0}");
                sb.AppendLine($"      Mảnh hero:         {stats.AvgShards:F2}");
                foreach (var kv in stats.AvgMaterials)
                    sb.AppendLine($"      {kv.Key,-11}:      {kv.Value:F2}");

                totalShardsAllChapters += stats.AvgShards;
                foreach (var kv in stats.AvgMaterials)
                    totalMaterialsAllChapters[kv.Key] = totalMaterialsAllChapters.GetValueOrDefault(kv.Key) + kv.Value;
            }

            // TỔNG cả 5 chương = cộng dồn kỳ vọng từng chương (mỗi chương chỉ chơi ĐÚNG 1 lần trong
            // 1 playthrough thật — không có chapter replay, xem task-balance-loottable.md §0 mục 4),
            // KHÔNG nhân RUNS_PER_CHAPTER lên.
            double totalEssenceI = totalMaterialsAllChapters.GetValueOrDefault(CurrencyType.EssenceI);
            double totalEssenceII = totalMaterialsAllChapters.GetValueOrDefault(CurrencyType.EssenceII);
            double totalEssenceIII = totalMaterialsAllChapters.GetValueOrDefault(CurrencyType.EssenceIII);
            double totalCore = totalMaterialsAllChapters.GetValueOrDefault(CurrencyType.Core);

            sb.AppendLine("  — TỔNG cả 5 chương (1 playthrough trọn vẹn, chương không replay được):");
            sb.AppendLine($"      Mảnh hero:   {totalShardsAllChapters:F2}");
            sb.AppendLine($"      Essence I:   {totalEssenceI:F2}");
            sb.AppendLine($"      Essence II:  {totalEssenceII:F2}");
            sb.AppendLine($"      Essence III: {totalEssenceIII:F2}");
            sb.AppendLine($"      Core:        {totalCore:F2}");
            sb.Append(AscendPacingReport(totalShardsAllChapters, totalEssenceI, totalEssenceII, totalEssenceIII, totalCore));
            sb.AppendLine("    (Chỉ tính nguồn Treasure/Boss theo chương truyện (1 lần/chương, không " +
                          "replay được) — KHÔNG tính Material Dungeon (DungeonKind.Material, task-" +
                          "endgame.md, cày lại được nhưng không có Core), KHÔNG tính Shop mua bằng Gem " +
                          "(ShopScreen — Core/Essence I-III bán bằng Gem, Gem tái tạo qua QuestSystem " +
                          "hằng ngày), KHÔNG tính Mảnh hero từ trùng Gacha (GachaSystem.DuplicateShards " +
                          "— nguồn Mảnh CHÍNH thật sự, Treasure/Boss chỉ là phụ). % thấp/0% ở trên là " +
                          "DỰ KIẾN vì báo cáo này chỉ đo 1 trong 4 nguồn — không tự suy ra là mất cân " +
                          "bằng nếu chưa đối chiếu cả 4.)");
            sb.Append(GemPacingReport());

            return sb.ToString();
        }

        /// <summary>Gem giờ đến từ QuestSystem (task-quest.md), không còn tuyến tính theo số Boss
        /// — pacing tính theo NGÀY (Daily quest reset mỗi ngày), không theo run nữa.</summary>
        private static string GemPacingReport()
        {
            long maxDailyGem = 0;
            foreach (var q in Game.Meta.Quest.QuestSystem.DailyQuests) maxDailyGem += q.GemReward;

            var sb = new StringBuilder();
            sb.AppendLine($"    Gem tối đa/ngày (làm hết {Game.Meta.Quest.QuestSystem.DailyQuests.Count} Daily Quest): {maxDailyGem} Gem");
            sb.AppendLine("    Số ngày ước lượng để đủ Gem Summon (chưa tính Achievement 1 lần, Shop rẻ hơn):");
            sb.AppendLine($"      Summon x1 ({GachaSystem.SINGLE_PULL_COST} Gem): ~{GachaSystem.SINGLE_PULL_COST / (double)maxDailyGem:F1} ngày");
            sb.AppendLine($"      Summon x10 ({GachaSystem.TEN_PULL_COST} Gem): ~{GachaSystem.TEN_PULL_COST / (double)maxDailyGem:F1} ngày");
            return sb.ToString();
        }

        /// <summary>Đi theo 1 đường ngẫu nhiên từ hàng đầu (Battle bắt buộc) tới Boss cuối chương,
        /// mô phỏng đúng 1 lượt chơi thật (không đi hết mọi node trong map như node-count thô).</summary>
        private static List<MapNodeDto> WalkRandomPath(RunStateDto run, IRandomSource rng)
        {
            var byId = new Dictionary<int, MapNodeDto>();
            foreach (var n in run.MapNodes) byId[n.Id] = n;

            var path = new List<MapNodeDto>();
            var row0 = run.MapNodes.FindAll(n => n.RowIndex == 0);
            if (row0.Count == 0) return path;

            var current = row0[rng.NextInt(row0.Count)];
            path.Add(current);

            var guard = 0;
            while (current.NextIds != null && current.NextIds.Count > 0 && guard++ < 64)
            {
                int nextId = current.NextIds[rng.NextInt(current.NextIds.Count)];
                if (!byId.TryGetValue(nextId, out current)) break;
                path.Add(current);
            }
            return path;
        }

        /// <summary>Đối chiếu TỔNG mảnh + vật liệu cả 5 chương (1 playthrough trọn vẹn, KHÔNG phải
        /// tốc độ/run vì chương không replay được — task-balance-loottable.md §0 mục 4) với bảng chi
        /// phí AscendSystem — báo % chi phí mỗi bậc mà riêng câu chuyện chính cung cấp đủ, phần còn
        /// thiếu (nếu có) phải đến từ Material Dungeon (task-endgame.md, ngoài phạm vi báo cáo này).
        /// Không tính Gold (giả định luôn dư từ Battle/Elite thường, chưa mô phỏng ở đây).</summary>
        private static string AscendPacingReport(double totalShards, double totalEssenceI,
            double totalEssenceII, double totalEssenceIII, double totalCore)
        {
            double Total(CurrencyType type) => type switch
            {
                CurrencyType.EssenceI => totalEssenceI,
                CurrencyType.EssenceII => totalEssenceII,
                CurrencyType.EssenceIII => totalEssenceIII,
                CurrencyType.Core => totalCore,
                _ => 0
            };

            var hero = new HeroInstanceDto { DefId = "probe", Star = 1 };
            var sb = new StringBuilder();
            sb.AppendLine("    Cả 5 chương (1 playthrough) tự đủ bao nhiêu % chi phí mỗi bậc ★ (nút thắt = mục thiếu nhiều nhất):");

            double remainingShards = totalShards;
            var remainingMaterials = new Dictionary<CurrencyType, double>
            {
                [CurrencyType.EssenceI] = totalEssenceI,
                [CurrencyType.EssenceII] = totalEssenceII,
                [CurrencyType.EssenceIII] = totalEssenceIII,
                [CurrencyType.Core] = totalCore,
            };

            while (hero.Star < AscendSystem.MAX_STAR)
            {
                var cost = AscendSystem.CostForNextStar(hero);
                if (cost == null) break;

                double worstPct = cost.Value.Shards > 0 ? System.Math.Min(1.0, remainingShards / cost.Value.Shards) : 1.0;
                string bottleneck = "Mảnh";
                foreach (var m in cost.Value.Materials)
                {
                    double have = remainingMaterials.GetValueOrDefault(m.Type);
                    double pct = m.Amount > 0 ? System.Math.Min(1.0, have / m.Amount) : 1.0;
                    if (pct < worstPct) { worstPct = pct; bottleneck = m.Type.ToString(); }
                }

                // Giả định lên sao ngay khi đủ — trừ dần vào phần còn lại để mốc sau phản ánh đúng
                // phần đã "tiêu" ở mốc trước (không double-count cùng 1 nguồn tài nguyên hữu hạn).
                remainingShards = System.Math.Max(0, remainingShards - cost.Value.Shards);
                foreach (var m in cost.Value.Materials)
                    remainingMaterials[m.Type] = System.Math.Max(0, remainingMaterials.GetValueOrDefault(m.Type) - m.Amount);

                string pctText = worstPct >= 1.0 ? "ĐỦ 100%" : $"chỉ {worstPct:P0} (thiếu {bottleneck}, cần cày thêm Material Dungeon)";
                sb.AppendLine($"      ★{hero.Star}→★{hero.Star + 1}: {pctText}");
                hero.Star++;
            }
            return sb.ToString();
        }
    }
}
