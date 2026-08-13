using Game.Data.Dto;

namespace Game.Services.Save
{
    /// <summary>
    /// Giữ profile đang hoạt động trong phiên chơi. Game.Bootstrap ghi 1 lần sau khi
    /// <see cref="IPlayerRepository.LoadAsync"/> xong; các tầng khác (Meta, UI, CombatView)
    /// đọc qua đây thay vì tham chiếu ngược lại Game.Bootstrap (asmdef Bootstrap phụ thuộc
    /// TẤT CẢ layer khác nên không layer nào được phép phụ thuộc ngược — object-map.md §6).
    /// </summary>
    public static class ProfileContext
    {
        public static PlayerProfileDto Current { get; set; }
        public static bool IsReady => Current != null;
    }
}
