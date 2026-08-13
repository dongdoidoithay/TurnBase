using Game.Data;
using UnityEngine;

namespace Game.Meta.Content
{
    /// <summary>
    /// Định nghĩa TĨNH của 1 món trang bị — khác <c>EquipmentInstanceDto</c> (item cụ thể
    /// người chơi sở hữu). Sinh ra từ <c>Tools/equipment.csv</c> qua menu Tools/Import Game Data.
    ///
    /// MVP: mỗi món chỉ cộng 1 stat gốc (StatType phải là Str/Con/Int/Dex/Aur/Luk — khớp trực
    /// tiếp field của <see cref="PrimaryStats"/>) — chưa làm sub-stat/set-bonus như plan.md §7
    /// đầy đủ, để có hệ thống trang bị CHẠY ĐƯỢC trước, mở rộng sau.
    /// </summary>
    [CreateAssetMenu(menuName = "TurnBase/Content/Equipment Definition", fileName = "eq_new")]
    public sealed class EquipmentDefinitionSO : ScriptableObject
    {
        public string DefId = "eq_unknown";
        public string NameKey = "";
        public EquipSlot Slot = EquipSlot.Weapon;
        public int Rarity = 1;
        public StatType StatType = StatType.Str;
        public float StatValue = 5f;

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(DefId)) DefId = name;
        }
    }
}
