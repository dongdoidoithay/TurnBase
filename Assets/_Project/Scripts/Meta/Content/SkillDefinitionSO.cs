using Game.Data;
using UnityEngine;

namespace Game.Meta.Content
{
    /// <summary>
    /// Bọc <see cref="SkillData"/> (thuần C#) thành asset để chỉnh trong Inspector
    /// và nạp qua Addressables ở P2 — plan.md §11.1, structure.md §4.
    /// Sinh ra từ <c>Tools/skills.csv</c> qua menu Tools/Import Game Data.
    /// KHÔNG chứa logic — Game.Combat chỉ dùng <see cref="Data"/> (SkillData thuần).
    /// </summary>
    [CreateAssetMenu(menuName = "TurnBase/Content/Skill Definition", fileName = "skill_new")]
    public sealed class SkillDefinitionSO : ScriptableObject
    {
        public SkillData Data = new();

        public string Id => Data.Id;

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(Data.Id)) Data.Id = name;
        }
    }
}
