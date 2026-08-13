using UnityEngine;

namespace Game.Core.UI
{
    /// <summary>Điểm neo UI toàn cục — Canvas overlay sống xuyên suốt mọi scene
    /// (UIRoot ở Boot/__UI__, DontDestroyOnLoad). Đăng ký ở ServiceInstaller; các scene
    /// khác gắn Canvas riêng của mình vào đây thay vì để Canvas nổi lẻ ở scene root.</summary>
    public interface IUiRootHost
    {
        Transform Root { get; }
    }

    public sealed class UiRootHost : IUiRootHost
    {
        public Transform Root { get; }
        public UiRootHost(Transform root) => Root = root;
    }
}
