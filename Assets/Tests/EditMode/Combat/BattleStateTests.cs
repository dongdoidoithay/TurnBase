using Game.Combat.Model;
using NUnit.Framework;

namespace Game.Tests.Combat
{
    /// <summary>task-damage-meter.md — <see cref="BattleState.RecordDamage"/>/
    /// <see cref="BattleState.DamageByUnit"/> chưa từng có test riêng dù được gọi thật từ
    /// ActionResolver/StatusProcessor (chỉ có coverage gián tiếp qua kết quả HP) — thêm test trực
    /// tiếp trước khi UI (DamageMeter panel) bắt đầu hiển thị dữ liệu này cho người chơi.</summary>
    public class BattleStateTests
    {
        [Test]
        public void RecordDamage_AccumulatesPerUnit()
        {
            var state = new BattleState();

            state.RecordDamage(1, 50);
            state.RecordDamage(1, 30);
            state.RecordDamage(2, 100);

            Assert.AreEqual(80, state.DamageByUnit[1]);
            Assert.AreEqual(100, state.DamageByUnit[2]);
        }

        [Test]
        public void RecordDamage_IgnoresNonPositiveAmounts()
        {
            var state = new BattleState();

            state.RecordDamage(1, 0);
            state.RecordDamage(1, -10);

            Assert.IsFalse(state.DamageByUnit.ContainsKey(1),
                "amount <= 0 không được tạo entry — tránh liệt kê '0 dmg' cho unit chưa từng gây sát thương.");
        }
    }
}
