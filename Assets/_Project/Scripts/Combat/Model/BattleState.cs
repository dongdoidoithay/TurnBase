using System.Collections.Generic;
using Game.Core.Random;
using Game.Data;

namespace Game.Combat.Model
{
    /// <summary>Toàn bộ trạng thái một trận đấu. C# thuần, serialize được để snapshot (edge case E17).</summary>
    public sealed class BattleState
    {
        public readonly List<CombatUnit> Units = new(16);
        public IRandomSource Rng;

        public int RoundNumber;
        public int TurnCounter;
        public int CurrentActorId = -1;
        public BattleResult Result = BattleResult.InProgress;

        /// <summary>Ultimate gauge dùng chung cả đội người chơi, 0..100 (plan.md §4.10).</summary>
        public int UltimateGauge;
        public const int ULTIMATE_MAX = 100;

        public int TurnLimit;             // 0 = không giới hạn
        public int EnrageRound = 12;
        public bool AllowEscape = true;

        /// <summary>task-consumable-items.md — số lượng vật phẩm phe Player MANG vào trận này
        /// (khoá = ItemType), giảm dần khi dùng. Rỗng mặc định = trận không có item (mọi trận
        /// khác không ảnh hưởng, khớp cách <see cref="AllowEscape"/> mặc định true cho mọi trận
        /// chưa set riêng).</summary>
        public readonly Dictionary<ItemType, int> ItemLoadout = new();

        /// <summary>Số vật phẩm ĐÃ DÙNG THẬT trong trận (khác đã MANG) — dùng để trừ vĩnh viễn
        /// khỏi <c>profile.Inventory.Items</c> sau trận, xem <see cref="ItemLoadout"/>.</summary>
        public readonly Dictionary<ItemType, int> ItemsUsed = new();

        // Thống kê để tính thưởng và Damage Meter
        public readonly Dictionary<int, long> DamageByUnit = new();

        /// <summary>task-analyze-tactic.md — ID địch đã bị Analyze, tồn tại suốt trận.
        /// HUD đọc trực tiếp mỗi frame (cùng pattern DamageByUnit/Damage Meter).</summary>
        public readonly System.Collections.Generic.HashSet<int> AnalyzedEnemyIds = new();
        public int PerfectCount;
        public int BreakCount;

        private int _nextUnitId;

        public int AllocateUnitId() => _nextUnitId++;

        public CombatUnit GetUnit(int id)
        {
            for (int i = 0; i < Units.Count; i++)
                if (Units[i].Id == id) return Units[i];
            return null;
        }

        public void AddUnit(CombatUnit u)
        {
            u.Id = AllocateUnitId();
            Units.Add(u);
        }

        // ===== Truy vấn (dùng buffer để tránh cấp phát trong vòng lặp trận) =====
        private readonly List<CombatUnit> _queryBuffer = new(16);

        /// <summary>Danh sách unit còn sống của một phe. Kết quả DÙNG NGAY, không giữ lại.</summary>
        public List<CombatUnit> AliveOf(TeamSide side)
        {
            _queryBuffer.Clear();
            for (int i = 0; i < Units.Count; i++)
                if (Units[i].Side == side && Units[i].IsAlive) _queryBuffer.Add(Units[i]);
            return _queryBuffer;
        }

        public int CountAlive(TeamSide side)
        {
            int n = 0;
            for (int i = 0; i < Units.Count; i++)
                if (Units[i].Side == side && Units[i].IsAlive) n++;
            return n;
        }

        public bool IsTeamWiped(TeamSide side) => CountAlive(side) == 0;

        public TeamSide Opposite(TeamSide side)
            => side == TeamSide.Player ? TeamSide.Enemy : TeamSide.Player;

        public void RecordDamage(int unitId, int amount)
        {
            if (amount <= 0) return;
            DamageByUnit.TryGetValue(unitId, out var cur);
            DamageByUnit[unitId] = cur + amount;
        }

        /// <summary>
        /// Kiểm tra kết thúc trận. Quy ước edge case E04: cả hai phe chết cùng lúc → người chơi THUA.
        /// </summary>
        public BattleResult EvaluateResult()
        {
            bool playerWiped = IsTeamWiped(TeamSide.Player);
            bool enemyWiped = IsTeamWiped(TeamSide.Enemy);

            if (playerWiped) return BattleResult.Defeat;   // kiểm tra trước → hòa cũng là thua
            if (enemyWiped) return BattleResult.Victory;
            if (TurnLimit > 0 && TurnCounter >= TurnLimit) return BattleResult.Timeout;
            return BattleResult.InProgress;
        }

        public void AddUltimate(int amount)
        {
            UltimateGauge = Core.Maths.GameMath.Clamp(UltimateGauge + amount, 0, ULTIMATE_MAX);
        }

        public bool IsUltimateReady => UltimateGauge >= ULTIMATE_MAX;

        /// <summary>Tiêu hết gauge sau khi dùng Ultimate thành công — edge case E15 (plan.md
        /// §4.14): caller (CombatSimulation.ExecuteIntent) chỉ gọi hàm này khi actor CÒN SỐNG sau
        /// khi resolve xong, để nếu actor tự chết giữa lúc dùng Ultimate (VD phản đòn/reflect),
        /// gauge KHÔNG bị tiêu — giữ nguyên cho hero khác dùng sau.</summary>
        public void ConsumeUltimate() => UltimateGauge = 0;
    }
}
