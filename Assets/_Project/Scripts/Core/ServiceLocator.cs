using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// Điểm truy cập service duy nhất được phép "toàn cục".
    /// Đăng ký TẤT CẢ ở composition root (ServiceInstaller trong scene Boot) — plan.md §11.7.
    /// Cấm tạo singleton rải rác trong code.
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> _services = new();

        public static void Register<T>(T service) where T : class
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            var type = typeof(T);
            if (_services.ContainsKey(type))
                throw new InvalidOperationException($"Service {type.Name} đã được đăng ký. Gọi Replace() nếu cố ý ghi đè.");
            _services[type] = service;
        }

        public static void Replace<T>(T service) where T : class
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            _services[typeof(T)] = service;
        }

        public static T Get<T>() where T : class
        {
            if (_services.TryGetValue(typeof(T), out var s)) return (T)s;
            throw new InvalidOperationException(
                $"Service {typeof(T).Name} chưa đăng ký. Kiểm tra ServiceInstaller (object-map.md §3.1).");
        }

        public static bool TryGet<T>(out T service) where T : class
        {
            if (_services.TryGetValue(typeof(T), out var s)) { service = (T)s; return true; }
            service = null;
            return false;
        }

        public static bool IsRegistered<T>() where T : class => _services.ContainsKey(typeof(T));

        /// <summary>Dispose mọi service có IDisposable rồi xoá sạch. Gọi khi thoát app hoặc giữa các test.</summary>
        public static void Clear()
        {
            foreach (var s in _services.Values)
                if (s is IDisposable d) d.Dispose();
            _services.Clear();
        }
    }
}
