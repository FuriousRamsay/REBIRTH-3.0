using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace RebirthVehicleInspector.Util
{
    internal static class Reflect
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        public static object Get(object obj, params string[] names)
        {
            if (obj == null) return null;
            Type t = obj.GetType();
            foreach (string name in names)
            {
                try
                {
                    FieldInfo f = t.GetField(name, Flags);
                    if (f != null) return f.GetValue(obj);
                    PropertyInfo p = t.GetProperty(name, Flags);
                    if (p != null && p.GetIndexParameters().Length == 0 && p.GetGetMethod(true) != null)
                        return p.GetValue(obj, null);
                }
                catch (Exception) { /* version-specific or invalid member; try next candidate */ }
            }
            return null;
        }

        public static object Call0(object obj, string name)
        {
            if (obj == null) return null;
            try
            {
                MethodInfo m = obj.GetType().GetMethod(name, Flags, null, Type.EmptyTypes, null);
                return m == null ? null : m.Invoke(obj, null);
            }
            catch (Exception) { return null; }
        }

        public static object Call1(object obj, string name, object arg)
        {
            if (obj == null || arg == null) return null;
            foreach (MethodInfo m in obj.GetType().GetMethods(Flags))
            {
                if (m.Name != name) continue;
                ParameterInfo[] p = m.GetParameters();
                if (p.Length != 1 || !p[0].ParameterType.IsInstanceOfType(arg) && p[0].ParameterType != arg.GetType()) continue;
                try { return m.Invoke(obj, new [] { arg }); }
                catch (Exception) { }
            }
            return null;
        }

        public static bool HasAncestorNamed(Type t, string name)
        {
            for (Type scan = t; scan != null; scan = scan.BaseType)
                if (scan.Name.Equals(name, StringComparison.Ordinal)) return true;
            return false;
        }

        public static int Id(object entity)
        {
            object val = Get(entity, "entityId", "EntityId", "EntityID");
            if (val == null) return -1;
            try { return Convert.ToInt32(val); } catch { return -1; }
        }

        public static Vector3? Position(object entity)
        {
            object val = Call0(entity, "GetPosition") ?? Get(entity, "position", "Position");
            if (val is Vector3) return (Vector3)val;
            Transform t = FindTransform(entity);
            if (t != null) return t.position;
            return null;
        }

        // Never traverse arbitrary references: only known, shallow geometry holders.
        private static readonly string[] GeometryMembers = {
            "transform", "Transform", "gameObject", "GameObject", "emodel", "EModel",
            "model", "Model", "vehicle", "Vehicle", "RootTransform", "ModelTransform", "MeshTransform"
        };
        public static Transform FindTransform(object instance)
        {
            return FindTransform(instance, 0, new HashSet<object>(ReferenceComparer.Instance));
        }
        private static Transform FindTransform(object instance, int depth, HashSet<object> visited)
        {
            if (instance == null || depth > 3 || !visited.Add(instance)) return null;
            if (instance is Transform) return (Transform)instance;
            if (instance is GameObject) return ((GameObject)instance).transform;
            if (instance is Component) return ((Component)instance).transform;
            foreach (string name in GeometryMembers)
            {
                object value = Get(instance, name);
                if (value == null || ReferenceEquals(value, instance)) continue;
                Transform candidate = FindTransform(value, depth + 1, visited);
                if (candidate != null) return candidate;
            }
            return null;
        }

        public static IEnumerable<object> Values(object container, int max = 8192)
        {
            if (container == null || container is string) yield break;
            var en = container as IEnumerable;
            if (en == null) yield break;
            int n = 0;
            foreach (object item in en)
            {
                if (++n > max) yield break;
                if (item is DictionaryEntry) { yield return ((DictionaryEntry)item).Value; continue; }
                object possibleValue = item == null ? null : Get(item, "Value");
                yield return possibleValue ?? item;
            }
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            internal static readonly ReferenceComparer Instance = new ReferenceComparer();
            public new bool Equals(object a, object b) { return ReferenceEquals(a,b); }
            public int GetHashCode(object o) { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o); }
        }
    }
}
