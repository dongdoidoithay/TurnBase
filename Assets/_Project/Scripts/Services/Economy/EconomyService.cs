using System.Collections.Generic;
using Game.Data;
using Game.Data.Dto;

namespace Game.Services.Economy
{
    /// <summary>Cổng duy nhất cho mọi thay đổi tiền tệ (plan.md §9.1, object-map.md §6.2) —
    /// không nơi nào khác được sửa <see cref="WalletDto"/> trực tiếp. Gold/Gem là field
    /// riêng; các <see cref="CurrencyType"/> vật liệu khác (Essence I/II/III, Core...) đọc/ghi
    /// qua <see cref="WalletDto.Materials"/> (khoá = tên enum). Mảnh hero không thuộc
    /// <see cref="CurrencyType"/> (khoá theo defId từng hero) nên có bộ hàm riêng.</summary>
    public interface IEconomyService
    {
        long Get(WalletDto wallet, CurrencyType type);

        /// <summary>Cộng/trừ tự do, luôn áp dụng, kẹp tại 0 — dùng cho thưởng hoặc phạt
        /// (event may/rủi) không cần chặn giao dịch.</summary>
        void Grant(WalletDto wallet, CurrencyType type, long delta);

        /// <summary>Trừ có điều kiện — đủ số dư mới trừ, dùng cho mọi giao dịch mua/nâng cấp.</summary>
        bool TryConsume(WalletDto wallet, CurrencyType type, long amount);

        long GetShards(WalletDto wallet, string heroDefId);
        void GrantShards(WalletDto wallet, string heroDefId, long delta);
        bool TryConsumeShards(WalletDto wallet, string heroDefId, long amount);

        /// <summary>task-consumable-items.md — vật phẩm tiêu hao nằm ở <see cref="InventoryDto"/>
        /// (không phải Wallet, khác Materials/HeroShards), cùng khuôn khoá=string qua
        /// <see cref="ItemType"/>.ToString().</summary>
        long GetItemCount(InventoryDto inventory, ItemType type);
        void GrantItem(InventoryDto inventory, ItemType type, long delta);
        bool TryConsumeItem(InventoryDto inventory, ItemType type, long amount);
    }

    public sealed class EconomyService : IEconomyService
    {
        public long Get(WalletDto wallet, CurrencyType type) => type switch
        {
            CurrencyType.Gold => wallet.Gold,
            CurrencyType.Gem => wallet.Gem,
            _ => GetEntry(wallet.Materials, type.ToString())
        };

        public void Grant(WalletDto wallet, CurrencyType type, long delta)
        {
            switch (type)
            {
                case CurrencyType.Gold: wallet.Gold = System.Math.Max(0, wallet.Gold + delta); break;
                case CurrencyType.Gem: wallet.Gem = System.Math.Max(0, wallet.Gem + delta); break;
                default: ApplyEntry(wallet.Materials, type.ToString(), delta); break;
            }
        }

        public bool TryConsume(WalletDto wallet, CurrencyType type, long amount)
        {
            if (amount < 0) throw new System.ArgumentOutOfRangeException(nameof(amount));
            if (amount == 0) return true;
            if (Get(wallet, type) < amount) return false;
            Grant(wallet, type, -amount);
            return true;
        }

        public long GetShards(WalletDto wallet, string heroDefId) => GetEntry(wallet.HeroShards, heroDefId);

        public void GrantShards(WalletDto wallet, string heroDefId, long delta)
            => ApplyEntry(wallet.HeroShards, heroDefId, delta);

        public bool TryConsumeShards(WalletDto wallet, string heroDefId, long amount)
        {
            if (amount < 0) throw new System.ArgumentOutOfRangeException(nameof(amount));
            if (amount == 0) return true;
            if (GetShards(wallet, heroDefId) < amount) return false;
            GrantShards(wallet, heroDefId, -amount);
            return true;
        }

        public long GetItemCount(InventoryDto inventory, ItemType type) => GetEntry(inventory.Items, type.ToString());

        public void GrantItem(InventoryDto inventory, ItemType type, long delta)
            => ApplyEntry(inventory.Items, type.ToString(), delta);

        public bool TryConsumeItem(InventoryDto inventory, ItemType type, long amount)
        {
            if (amount < 0) throw new System.ArgumentOutOfRangeException(nameof(amount));
            if (amount == 0) return true;
            if (GetItemCount(inventory, type) < amount) return false;
            GrantItem(inventory, type, -amount);
            return true;
        }

        // ---------- Materials/HeroShards/Items đều là List<CurrencyEntryDto> — dùng chung 2 hàm này ----------

        private static long GetEntry(List<CurrencyEntryDto> list, string key)
        {
            foreach (var e in list)
                if (e.Key == key) return e.Value;
            return 0;
        }

        private static void ApplyEntry(List<CurrencyEntryDto> list, string key, long delta)
        {
            foreach (var e in list)
            {
                if (e.Key != key) continue;
                e.Value = System.Math.Max(0, e.Value + delta);
                return;
            }
            // Chưa có entry: chỉ tạo mới khi cộng — trừ vào số dư vốn dĩ đã 0 thì không cần lưu gì.
            if (delta > 0) list.Add(new CurrencyEntryDto(key, delta));
        }
    }
}
