using Game.Data;
using UnityEngine;

namespace Game.Meta.Content
{
    /// <summary>
    /// Định nghĩa TĨNH của một hero (khác <c>HeroInstanceDto</c> — trạng thái của người chơi).
    /// Sinh ra từ <c>Tools/heroes.csv</c> qua menu Tools/Import Game Data.
    /// </summary>
    [CreateAssetMenu(menuName = "TurnBase/Content/Hero Definition", fileName = "hero_new")]
    public sealed class HeroDefinitionSO : ScriptableObject
    {
        public string DefId = "hero_unknown";
        public string NameKey = "";
        public HeroClass Class = HeroClass.Vanguard;
        public Rarity Rarity = Rarity.Common;
        public Element Element = Element.Neutral;
        public int PoiseMax = 60;
        public PrimaryStats BasePrimary = new(10, 10, 10, 10, 10, 10);

        /// <summary>Id các skill sở hữu ban đầu — khớp <see cref="SkillDefinitionSO.Id"/>.</summary>
        public string[] SkillIds = System.Array.Empty<string>();

        /// <summary>Thư mục sprite dưới Art/Characters/Heroes — dùng khi nạp Addressables ở P2.</summary>
        public string SpriteFolder = "";

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(DefId)) DefId = name;
        }
    }
}
