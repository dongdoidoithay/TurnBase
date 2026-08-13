using Game.Data;

namespace Game.Meta.Items
{
    /// <summary>1 dòng catalog vật phẩm tiêu hao — tên, giá Vàng, mô tả ngắn cho Shop/UI.</summary>
    public readonly struct ItemDef
    {
        public readonly ItemType Type;
        public readonly string Name;
        public readonly string Description;
        public readonly long PriceGold;

        public ItemDef(ItemType type, string name, string description, long priceGold)
        {
            Type = type; Name = name; Description = description; PriceGold = priceGold;
        }
    }

    /// <summary>task-consumable-items.md — 6 vật phẩm tiêu hao thật, plan.md §7.5. Hard-code
    /// (KHÔNG ScriptableObject) — chỉ 6 mục cố định, không cần pipeline CSV/SO cho ngần này dữ
    /// liệu, cùng lý do <see cref="Game.Meta.Hero.AwakeningCatalog"/>/<see
    /// cref="Game.Meta.Equipment.SetBonusCatalog"/> đã hard-code.</summary>
    public static class ItemCatalog
    {
        /// <summary>Thứ tự CỐ ĐỊNH — dùng để tự chọn "mang tối đa 5 loại" khi vào trận (không có
        /// UI chọn loadout thủ công, xem task-consumable-items.md §0.6).</summary>
        public static readonly ItemDef[] ALL =
        {
            new(ItemType.Potion,        "Potion",         "Hồi 35% MaxHP 1 hero",             200),
            new(ItemType.Ether,         "Ether",          "Hồi 40 SP",                         300),
            new(ItemType.Antidote,      "Antidote",       "Cleanse mọi DoT 1 hero",            250),
            new(ItemType.SmokeBomb,     "Smoke Bomb",     "Thoát trận 100%",                   500),
            new(ItemType.ReviveFeather, "Revive Feather", "Hồi sinh 40% HP",                 1_500),
            new(ItemType.ElementalBomb, "Elemental Bomb", "2.0x dmg hệ + -20 Poise, AoE",       800),
        };

        public static ItemDef Get(ItemType type)
        {
            foreach (var def in ALL)
                if (def.Type == type) return def;
            return default;
        }
    }
}
