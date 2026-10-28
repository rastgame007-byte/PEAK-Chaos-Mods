// Мост к игре через рефлексию: мод не ссылается на Assembly-CSharp при сборке,
// поэтому DLL можно собрать без установленной игры и она подхватывает типы PEAK уже в рантайме.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace PeakFun
{
    internal static class R
    {
        const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        static readonly Dictionary<string, MemberInfo> cache = new Dictionary<string, MemberInfo>();

        public static Type FindType(string fullName)
        {
            Type fallback = null;
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = null;
                try { t = a.GetType(fullName, false); } catch { }
                if (t == null) continue;
                if (a.GetName().Name.StartsWith("Assembly-CSharp")) return t;
                if (fallback == null) fallback = t;
            }
            return fallback;
        }

        static MemberInfo Find(Type t, string name)
        {
            string key = t.FullName + "::" + name;
            if (cache.TryGetValue(key, out var m)) return m;
            for (var c = t; c != null && m == null; c = c.BaseType)
                m = (MemberInfo)c.GetField(name, All) ?? c.GetProperty(name, All);
            cache[key] = m;
            return m;
        }

        public static object Get(object o, string name)
        {
            if (o == null) return null;
            var m = Find(o.GetType(), name);
            if (m is FieldInfo f) return f.GetValue(o);
            if (m is PropertyInfo p && p.GetIndexParameters().Length == 0) return p.GetValue(o, null);
            return null;
        }

        public static T Get<T>(object o, string name, T def = default(T))
        {
            var v = Get(o, name);
            return v is T t ? t : def;
        }

        static object Conv(object value, Type t)
        {
            if (value == null || t.IsInstanceOfType(value)) return value;
            if (t.IsPrimitive && IsNum(value)) return Convert.ChangeType(value, t);
            if (t.IsEnum && IsNum(value)) return Enum.ToObject(t, value);
            return value;
        }

        public static bool Set(object o, string name, object value)
        {
            if (o == null) return false;
            var m = Find(o.GetType(), name);
            try
            {
                if (m is FieldInfo f) { f.SetValue(o, Conv(value, f.FieldType)); return true; }
                if (m is PropertyInfo p && p.CanWrite && p.GetIndexParameters().Length == 0) { p.SetValue(o, Conv(value, p.PropertyType), null); return true; }
            }
            catch { }
            return false;
        }

        public static object GetStatic(Type t, string name)
        {
            if (t == null) return null;
            var m = Find(t, name);
            if (m is FieldInfo f) return f.GetValue(null);
            if (m is PropertyInfo p) return p.GetValue(null, null);
            return null;
        }

        public static bool SetStatic(Type t, string name, object value)
        {
            if (t == null) return false;
            var m = Find(t, name);
            try
            {
                if (m is FieldInfo f) { f.SetValue(null, Conv(value, f.FieldType)); return true; }
                if (m is PropertyInfo p && p.CanWrite) { p.SetValue(null, Conv(value, p.PropertyType), null); return true; }
            }
            catch { }
            return false;
        }

        public static bool CallStatic(Type t, string name)
        {
            if (t == null) return false;
            for (var c = t; c != null; c = c.BaseType)
            {
                var m = c.GetMethod(name, All, null, Type.EmptyTypes, null);
                if (m != null && m.IsStatic) { m.Invoke(null, null); return true; }
            }
            return false;
        }

        public static bool CallNoArgs(object o, params string[] names)
        {
            if (o == null) return false;
            foreach (var n in names)
                for (var c = o.GetType(); c != null; c = c.BaseType)
                {
                    var m = c.GetMethod(n, All, null, Type.EmptyTypes, null);
                    if (m != null && !m.IsStatic) { m.Invoke(o, null); return true; }
                }
            return false;
        }

        // ---------- универсальный вызов методов (подбор перегрузки, значения по умолчанию) ----------
        static bool IsNum(object a) { return a is int || a is float || a is double || a is long || a is short || a is byte || a is uint || a is ushort; }

        static bool Bind(MethodBase m, object[] args, out object[] fin)
        {
            var ps = m.GetParameters();
            fin = null;
            if (args.Length > ps.Length) return false;
            var res = new object[ps.Length];
            for (int i = 0; i < ps.Length; i++)
            {
                var pt = ps[i].ParameterType;
                if (pt.IsByRef) return false;
                if (i < args.Length)
                {
                    var a = args[i];
                    if (a == null)
                    {
                        if (pt.IsValueType && Nullable.GetUnderlyingType(pt) == null) return false;
                        res[i] = null;
                    }
                    else if (pt.IsInstanceOfType(a)) res[i] = a;
                    else if (pt.IsEnum && a is string es) { try { res[i] = Enum.Parse(pt, es, true); } catch { return false; } }
                    else if (pt.IsEnum && IsNum(a)) res[i] = Enum.ToObject(pt, a);
                    else if (pt.IsPrimitive && IsNum(a)) { try { res[i] = Convert.ChangeType(a, pt); } catch { return false; } }
                    else return false;
                }
                else if (ps[i].HasDefaultValue)
                {
                    var d = ps[i].DefaultValue;
                    if (d == null || d is DBNull || d == Type.Missing)
                        d = pt.IsValueType ? Activator.CreateInstance(pt) : null;
                    else if (pt.IsEnum && !pt.IsInstanceOfType(d)) d = Enum.ToObject(pt, d);
                    res[i] = d;
                }
                else return false;
            }
            fin = res;
            return true;
        }

        const BindingFlags AnyInst = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        const BindingFlags AnyStat = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly;

        public static bool TryInvoke(object o, string name, out object result, params object[] args)
        {
            result = null;
            if (o == null) return false;
            for (var c = o.GetType(); c != null; c = c.BaseType)
                foreach (var m in c.GetMethods(AnyInst))
                {
                    if (m.Name != name) continue;
                    if (!Bind(m, args, out var fin)) continue;
                    try { result = m.Invoke(o, fin); return true; }
                    catch (TargetInvocationException e) { throw e.InnerException ?? e; }
                }
            return false;
        }

        public static bool TryInvokeStatic(Type t, string name, out object result, params object[] args)
        {
            result = null;
            if (t == null) return false;
            for (var c = t; c != null; c = c.BaseType)
                foreach (var m in c.GetMethods(AnyStat))
                {
                    if (m.Name != name) continue;
                    if (!Bind(m, args, out var fin)) continue;
                    try { result = m.Invoke(null, fin); return true; }
                    catch (TargetInvocationException e) { throw e.InnerException ?? e; }
                }
            return false;
        }

        public static bool Has(object o, string method)
        {
            if (o == null) return false;
            for (var c = o.GetType(); c != null; c = c.BaseType)
                foreach (var m in c.GetMethods(AnyInst)) if (m.Name == method) return true;
            return false;
        }

        /// <summary>Создать объект: пробуем конструктор с данными аргументами, потом без аргументов.</summary>
        public static object New(Type t, params object[] ctorArgs)
        {
            if (t == null) return null;
            foreach (var ctor in t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (Bind(ctor, ctorArgs, out var fin)) { try { return ctor.Invoke(fin); } catch { } }
            }
            if (ctorArgs.Length > 0)
                foreach (var ctor in t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    if (ctor.GetParameters().Length == 0) { try { return ctor.Invoke(null); } catch { } }
            return null;
        }
    }
}
