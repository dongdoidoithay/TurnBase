using Game.Data;
using UnityEngine;

namespace Game.Meta.Content
{
    /// <summary>
    /// Định nghĩa TĨNH 1 bảng loot theo (Chương, Loại node) — task-loottable.md, thay
    /// <c>PlaceholderLootTable</c> (hằng số cố định). Khác <c>AwakeningCatalog</c>/
    /// <c>AscendSystem.COSTS</c> (hard-code vì chứa field readonly Unity không serialize được),
    /// ở đây chỉ có field thường nên dùng ScriptableObject thật — đúng khuôn
    /// <see cref="HeroDefinitionSO"/>/<see cref="EnemyDefinitionSO"/>.
    ///
    /// plan.md không có bảng tỉ lệ vật liệu cụ thể theo chương — schema này tự thiết kế, bám theo
    /// <see cref="NodeType"/> và <c>RunStateDto.ChapterId</c> làm trục khoá.
    /// </summary>
    [CreateAssetMenu(menuName = "TurnBase/Content/Loot Table", fileName = "loottable_new")]
    public sealed class LootTableDefinitionSO : ScriptableObject
    {
        public string DefId = "loottable_unknown";

        /// <summary>0 = áp dụng mọi chương (wildcard, fallback nếu không có bảng riêng cho
        /// chương đó). Số cụ thể (1-5) ưu tiên hơn wildcard khi <see cref="LootRoller"/> tìm.</summary>
        public int Chapter;

        public NodeType NodeType = NodeType.Treasure;

        public int GoldMin = 80;
        public int GoldMax = 160;

        [System.Serializable]
        public struct MaterialDrop
        {
            public CurrencyType Type;
            public int MinAmount;
            public int MaxAmount;
            /// <summary>0..1 — mỗi dòng roll ĐỘC LẬP, không loại trừ lẫn nhau (khác
            /// PlaceholderLootTable cũ chỉ có 1 nhánh 50/50 loại trừ — cải thiện có chủ đích).</summary>
            [Range(0f, 1f)] public float Chance;
        }

        public MaterialDrop[] Materials = System.Array.Empty<MaterialDrop>();

        /// <summary>Mảnh cho 1 hero NGẪU NHIÊN trong số đang sở hữu (không phải mỗi hero — dùng
        /// cho Treasure). Boss "1 mảnh/hero ra trận" là logic riêng, xử lý trong
        /// MetaSceneInstaller, không nằm ở field này.</summary>
        [Range(0f, 1f)] public float HeroShardChance;
        public int HeroShardMin = 1;
        public int HeroShardMax = 1;

        /// <summary>Trang bị ngẫu nhiên — task-equipment.md, plan.md §8.1 (Treasure "đảm bảo ≥1
        /// trang bị ≥ Rare"). Không dùng <c>EquipSlot?</c> — Unity không serialize
        /// <c>Nullable&lt;enum&gt;</c> tốt trong Inspector, nên tách bool + enum.</summary>
        [Range(0f, 1f)] public float EquipmentChance;
        public Rarity EquipmentMinRarity = Rarity.Rare;
        public bool EquipmentAnySlot = true;
        public EquipSlot EquipmentSlot;

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(DefId)) DefId = name;
        }
    }
}
