namespace Game.Data
{
    public enum EquipSlot { Weapon = 0, Armor = 1, Helm = 2, Boots = 3, Ring = 4, Amulet = 5 }

    public enum CurrencyType
    {
        Gold = 0, Gem = 1, Energy = 2, Ticket = 3, Honor = 4,
        EssenceI = 10, EssenceII = 11, EssenceIII = 12, Core = 13, EnhanceStone = 14
    }

    /// <summary>plan.md §8.1 — loại node trên bản đồ phân nhánh.</summary>
    public enum NodeType
    {
        Start = 0, Battle = 1, Elite = 2, Boss = 3,
        Shop = 4, Rest = 5, Treasure = 6, Event = 7, Mystery = 8
    }

    public enum NodeState { Locked = 0, Available = 1, Visited = 2, Current = 3 }

    /// <summary>Chỉ số dùng cho main/sub stat trang bị và modifier.</summary>
    public enum StatType
    {
        // Primary (plan.md §4.5)
        Str = 0, Con = 1, Int = 2, Dex = 3, Aur = 4, Luk = 5,
        // Derived flat
        MaxHp = 10, MaxSp = 11, Atk = 12, Def = 13, Spd = 14,
        Acc = 15, Eva = 16, Res = 17, EffAcc = 18,
        // Percent
        MaxHpPct = 30, AtkPct = 31, DefPct = 32, SpdPct = 33,
        CritPct = 34, CritDmgPct = 35, LifestealPct = 36,
        DmgBonusPct = 37, DmgReductPct = 38, PoiseDmgPct = 39,
        /// <summary>task-setbonus.md — thiếu tương đương % cho MaxSp (chỉ có MaxHpPct trước đó),
        /// cần cho bộ Sage. Mirror đúng cách MaxHpPct đã tính trong CombatUnit.ComputeStats.</summary>
        MaxSpPct = 40
    }

    public enum ItemCategory { Consumable = 0, Material = 1, HeroShard = 2, EquipmentBox = 3, SkillBook = 4 }

    /// <summary>task-consumable-items.md — 6 vật phẩm tiêu hao dùng trong trận, plan.md §7.5.</summary>
    public enum ItemType { Potion = 0, Ether = 1, Antidote = 2, SmokeBomb = 3, ReviveFeather = 4, ElementalBomb = 5 }

    public enum QuestKind { Daily = 0, Weekly = 1, Chain = 2, Event = 3 }

    public enum QuestConditionType
    {
        BattlesWon = 0, StagesCleared = 1, PerfectHits = 2, BreaksTriggered = 3,
        SummonsPerformed = 4, HeroLevelUps = 5, EquipEnhanced = 6, DungeonCleared = 7,
        GoldSpent = 8, LoginDays = 9
    }

    public enum DungeonKind { Gold = 0, Exp = 1, Material = 2, Stone = 3, Tower = 4, TrialBoss = 5, Arena = 6 }

    /// <summary>Cờ tách bản Mobile F2P và bản PC premium — plan.md §9.5.</summary>
    public enum MonetizationProfile { MobileF2P = 0, PremiumPC = 1 }

    /// <summary>Lý do biến động tiền tệ — bắt buộc cho analytics và TransactionLog.</summary>
    public enum CurrencyReason
    {
        Unknown = 0,
        BattleReward = 1, QuestReward = 2, AchievementReward = 3, DungeonReward = 4,
        LevelUp = 10, Ascend = 11, SkillUpgrade = 12, Enhance = 13, Reforge = 14,
        ShopPurchase = 20, Summon = 21, EnergyRefill = 22, Revive = 23,
        IapPurchase = 30, MailClaim = 31, EnergyRegen = 32, SellItem = 33,
        NodeEntry = 40, EventNode = 41, Debug = 99
    }
}
