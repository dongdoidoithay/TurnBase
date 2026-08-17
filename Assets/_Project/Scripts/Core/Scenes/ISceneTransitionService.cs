namespace Game.Core.Scenes
{
    /// <summary>plan.md §10.1 "Loading — Overlay, có mẹo chơi". Đăng ký ở ServiceInstaller (impl
    /// thật ở Game.Bootstrap.GameBootstrap, MonoBehaviour DontDestroyOnLoad — cần host coroutine
    /// sống sót qua chính lần đổi scene mà nó gây ra). Đặt interface ở Game.Core (không phải
    /// Game.Bootstrap) để Game.Meta/Game.CombatView gọi được qua ServiceLocator mà KHÔNG cần tham
    /// chiếu ngược lên Game.Bootstrap — Bootstrap đã tham chiếu CẢ 2 xuống (asmdef), tham chiếu
    /// ngược lại sẽ tạo vòng lặp assembly (thật sự chặn compile, không phải chỉ quy ước) — đúng
    /// mẫu <see cref="Game.Core.UI.IUiRootHost"/> đã dùng cho đúng lý do này.</summary>
    public interface ISceneTransitionService
    {
        /// <summary>Thay <c>SceneManager.LoadScene(sceneName)</c> trực tiếp — hiện overlay + 1 mẹo
        /// chơi ngẫu nhiên trong thời gian ngắn trước khi đổi scene (vẫn đồng bộ bên dưới, không
        /// phải progress bar thật).</summary>
        void LoadSceneWithOverlay(string sceneName);
    }
}
