using Game.Data;
using UnityEngine;

namespace Game.Meta.Content
{
    /// <summary>
    /// Định nghĩa TĨNH của một enemy/boss. Sinh ra từ <c>Tools/enemies.csv</c>
    /// qua menu Tools/Import Game Data.
    /// </summary>
    [CreateAssetMenu(menuName = "TurnBase/Content/Enemy Definition", fileName = "enemy_new")]
    public sealed class EnemyDefinitionSO : ScriptableObject
    {
        public string DefId = "enemy_unknown";
        public string NameKey = "";
        public Element Element = Element.Neutral;
        public ArchetypeId Archetype = ArchetypeId.Grunt;
        public int PoiseMax = 30;
        public PrimaryStats BasePrimary = new(8, 8, 8, 8, 8, 8);

        public string[] SkillIds = System.Array.Empty<string>();

        /// <summary>Id của AIProfile sẽ dùng — khớp dữ liệu ở Data/AIProfiles (P2).</summary>
        public string AiProfileId = "ai_basic";

        /// <summary>Chương xuất hiện — plan.md §8.2. Dùng để lọc hồ chứa địch theo Meta.</summary>
        public int Chapter = 1;

        public string SpriteFolder = "";

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(DefId)) DefId = name;
        }
    }
}
