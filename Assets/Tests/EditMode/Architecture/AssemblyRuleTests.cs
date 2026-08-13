using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Game.Tests.Architecture
{
    public class AssemblyRuleTests
    {
        static Assembly GetAssembly(string name)
        {
            var asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == name);
            Assert.IsNotNull(asm, $"Assembly '{name}' not found in AppDomain");
            return asm;
        }

        static HashSet<string> RefNames(Assembly asm) =>
            new HashSet<string>(asm.GetReferencedAssemblies().Select(r => r.Name));

        static List<string> ScanMembersForType(Assembly asm, Type forbidden)
        {
            var violations = new List<string>();
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            foreach (var type in asm.GetTypes())
            {
                foreach (var f in type.GetFields(flags))
                    if (f.FieldType == forbidden)
                        violations.Add($"{type.Name}.{f.Name} (field)");
                foreach (var p in type.GetProperties(flags))
                    if (p.PropertyType == forbidden)
                        violations.Add($"{type.Name}.{p.Name} (property)");
                foreach (var m in type.GetMethods(flags))
                {
                    if (m.ReturnType == forbidden)
                        violations.Add($"{type.Name}.{m.Name} (return)");
                    foreach (var param in m.GetParameters())
                        if (param.ParameterType == forbidden)
                            violations.Add($"{type.Name}.{m.Name}.{param.Name} (param)");
                }
            }
            return violations;
        }

        // ── Game.Combat isolation ────────────────────────────────────────────

        [Test]
        public void Combat_DoesNotReference_CombatView()
        {
            Assert.IsFalse(RefNames(GetAssembly("Game.Combat")).Contains("Game.CombatView"),
                "Game.Combat must not reference Game.CombatView");
        }

        [Test]
        public void Combat_DoesNotReference_UI()
        {
            Assert.IsFalse(RefNames(GetAssembly("Game.Combat")).Contains("Game.UI"),
                "Game.Combat must not reference Game.UI");
        }

        [Test]
        public void Combat_DoesNotReference_Meta()
        {
            Assert.IsFalse(RefNames(GetAssembly("Game.Combat")).Contains("Game.Meta"),
                "Game.Combat must not reference Game.Meta");
        }

        [Test]
        public void Combat_DoesNotReference_Services()
        {
            Assert.IsFalse(RefNames(GetAssembly("Game.Combat")).Contains("Game.Services"),
                "Game.Combat must not reference Game.Services");
        }

        [Test]
        public void Combat_NoTypeMember_UsesUnityEngineRandom()
        {
            var violations = ScanMembersForType(GetAssembly("Game.Combat"), typeof(UnityEngine.Random));
            Assert.IsEmpty(violations,
                $"Game.Combat must use IRandomSource, not UnityEngine.Random. Violations: {string.Join(", ", violations)}");
        }

        [Test]
        public void Combat_NoTypeMember_UsesUnityEngineTime()
        {
            var violations = ScanMembersForType(GetAssembly("Game.Combat"), typeof(UnityEngine.Time));
            Assert.IsEmpty(violations,
                $"Game.Combat must not use UnityEngine.Time. Violations: {string.Join(", ", violations)}");
        }

        // ── Foundation layers purity ─────────────────────────────────────────

        [Test]
        public void Core_DoesNotReference_AnyGameAssembly()
        {
            var forbidden = RefNames(GetAssembly("Game.Core"))
                .Where(n => n.StartsWith("Game.") && n != "Game.Core")
                .ToList();
            Assert.IsEmpty(forbidden,
                $"Game.Core must have no Game.* references, found: {string.Join(", ", forbidden)}");
        }

        [Test]
        public void Data_DoesNotReference_HigherLayers()
        {
            var refs = RefNames(GetAssembly("Game.Data"));
            var forbidden = new[] { "Game.Combat", "Game.Meta", "Game.Services", "Game.CombatView", "Game.UI", "Game.Bootstrap" };
            var violations = forbidden.Where(refs.Contains).ToList();
            Assert.IsEmpty(violations,
                $"Game.Data must not reference: {string.Join(", ", violations)}");
        }

        [Test]
        public void Services_DoesNotReference_UpperLayers()
        {
            var refs = RefNames(GetAssembly("Game.Services"));
            var forbidden = new[] { "Game.Combat", "Game.Meta", "Game.CombatView", "Game.UI", "Game.Bootstrap" };
            var violations = forbidden.Where(refs.Contains).ToList();
            Assert.IsEmpty(violations,
                $"Game.Services must not reference: {string.Join(", ", violations)}");
        }
    }
}
