using System.Threading.Tasks;
using Game.Data.Dto;

namespace Game.Services.Save
{
    /// <summary>
    /// CỔNG SERVER-READY (plan.md §11.5).
    /// v1: LocalPlayerRepository (JSON + AES + HMAC + ghi atomic).
    /// v2: RemotePlayerRepository (HTTPS API) — đổi đúng 1 dòng ở ServiceInstaller,
    ///     không đụng bất kỳ gameplay code nào.
    /// </summary>
    public interface IPlayerRepository
    {
        /// <summary>Nạp profile. Nếu chưa có save thì tạo profile mới.</summary>
        Task<PlayerProfileDto> LoadAsync();

        Task SaveAsync(PlayerProfileDto profile);

        /// <summary>Có save hợp lệ trên máy không.</summary>
        bool HasSave { get; }

        /// <summary>Xoá save (nút "Chơi lại từ đầu"). Vẫn giữ bản backup.</summary>
        Task DeleteAsync();
    }
}
