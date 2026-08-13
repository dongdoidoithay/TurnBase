using System.Collections.Generic;
using Game.Data;

namespace Game.Combat.Events
{
    /// <summary>27 loại event — bảng đầy đủ ở plan.md §11.4 và object-map.md §5.1.</summary>
    public enum CombatEventType
    {
        BattleInitialized = 0,
        BattleStarted = 1,
        RoundStarted = 2,
        TurnStarted = 3,
        SpChanged = 4,
        CooldownChanged = 5,
        ActionRequested = 6,
        ActionDeclared = 7,
        CommandWindowOpened = 8,
        CommandGraded = 9,
        DamageDealt = 10,
        ShieldAbsorbed = 11,
        ShieldBroken = 12,
        HealApplied = 13,
        StatusApplied = 14,
        StatusResisted = 15,
        StatusTicked = 16,
        StatusExpired = 17,
        PoiseDamaged = 18,
        PoiseBroken = 19,
        UnitDied = 20,
        UnitRevived = 21,
        MinionSummoned = 22,
        PhaseChanged = 23,
        UltimateCharged = 24,
        IntentChanged = 25,
        TurnEnded = 26,
        BattleEnded = 27,
        SpRestored = 28
    }

    /// <summary>
    /// Struct đơn (không cấp phát) mang mọi payload. Dùng 1 struct thay vì kế thừa
    /// để tránh boxing và giữ ngân sách 0 GC (plan.md §11.11).
    /// </summary>
    public readonly struct CombatEvent
    {
        public readonly CombatEventType Type;
        public readonly int SourceUnitId;
        public readonly int TargetUnitId;
        public readonly int IntValue;      // damage, heal, sp, cooldown, round, stacks...
        public readonly int IntValue2;     // duration, remaining, phase...
        public readonly float FloatValue;  // multiplier, %...
        public readonly bool Flag;         // isCrit, isCleanse...
        public readonly StatusId Status;
        public readonly Element Element;
        public readonly CommandGrade Grade;
        public readonly BattleResult Result;
        public readonly string StringValue; // skillId, vfxKey...

        public CombatEvent(
            CombatEventType type,
            int source = -1, int target = -1,
            int intValue = 0, int intValue2 = 0,
            float floatValue = 0f, bool flag = false,
            StatusId status = StatusId.None,
            Element element = Element.Neutral,
            CommandGrade grade = CommandGrade.Good,
            BattleResult result = BattleResult.InProgress,
            string stringValue = null)
        {
            Type = type;
            SourceUnitId = source;
            TargetUnitId = target;
            IntValue = intValue;
            IntValue2 = intValue2;
            FloatValue = floatValue;
            Flag = flag;
            Status = status;
            Element = element;
            Grade = grade;
            Result = result;
            StringValue = stringValue;
        }

        public bool IsCrit => Flag && Type == CombatEventType.DamageDealt;
        public bool IsMiss => Type == CombatEventType.DamageDealt && IntValue < 0;

        public override string ToString() => Type switch
        {
            CombatEventType.DamageDealt => $"{Type}: #{SourceUnitId}→#{TargetUnitId} {IntValue}{(Flag ? " CRIT" : "")}",
            CombatEventType.StatusApplied => $"{Type}: #{TargetUnitId} {Status} x{IntValue} ({IntValue2} lượt)",
            CombatEventType.TurnStarted => $"{Type}: #{SourceUnitId}",
            CombatEventType.BattleEnded => $"{Type}: {Result}",
            _ => $"{Type}: #{SourceUnitId}→#{TargetUnitId} v={IntValue}"
        };
    }

    /// <summary>Hàng đợi event của simulation. Presenter đọc và diễn hoạt tuần tự.</summary>
    public sealed class CombatEventQueue
    {
        private readonly List<CombatEvent> _events = new(128);
        private int _readIndex;

        public int Count => _events.Count;
        public int Pending => _events.Count - _readIndex;
        public IReadOnlyList<CombatEvent> All => _events;

        public void Emit(in CombatEvent evt) => _events.Add(evt);

        public void Emit(CombatEventType type, int source = -1, int target = -1,
                         int intValue = 0, int intValue2 = 0, float floatValue = 0f,
                         bool flag = false, StatusId status = StatusId.None,
                         Element element = Element.Neutral,
                         CommandGrade grade = CommandGrade.Good,
                         BattleResult result = BattleResult.InProgress,
                         string stringValue = null)
            => _events.Add(new CombatEvent(type, source, target, intValue, intValue2,
                                           floatValue, flag, status, element, grade, result, stringValue));

        public bool TryDequeue(out CombatEvent evt)
        {
            if (_readIndex < _events.Count) { evt = _events[_readIndex++]; return true; }
            evt = default;
            return false;
        }

        /// <summary>Xoá toàn bộ lịch sử (dùng khi bắt đầu trận mới).</summary>
        public void Clear() { _events.Clear(); _readIndex = 0; }

        /// <summary>Chuỗi rút gọn để so sánh trong golden test / determinism test.</summary>
        public string ToSignature()
        {
            var sb = new System.Text.StringBuilder(_events.Count * 24);
            for (int i = 0; i < _events.Count; i++)
            {
                var e = _events[i];
                sb.Append((int)e.Type).Append(',')
                  .Append(e.SourceUnitId).Append(',')
                  .Append(e.TargetUnitId).Append(',')
                  .Append(e.IntValue).Append(';');
            }
            return sb.ToString();
        }
    }
}
