// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace System.Reflection
{
    public static class WrapDelegate
    {
        public static Action CreateDelegate(this Type type, object target, MethodInfo m)
        {
            MethodInfo methodInfo = m; // ?? target.GetType().GetMethod(nameof(FooMethod));

            // Delegate CreateDelegate(Type delegateType);
            Action action = (Action) methodInfo.CreateDelegate(type ?? typeof(Action), target);
            return action;
        }

        public static Func<T, Result> CreateDelegateFunc<T, Result>(this Type type, object target, MethodInfo m)
        {
            var d = m.CreateDelegate(type, target);
            return d as Func<T, Result>;
        }
        // delegate TResult Func<in T, out TResult>(T arg);
    }

    public static class TypeInfoMethods 
    {
        public static bool IsGenericType(this Type type)
        {
          TypeInfo info = type.GetTypeInfo();
          return info.IsGenericType; // .IsGenericType();
        }

        public static bool IsValueType(this Type type) => type.GetTypeInfo().IsValueType;
        
        public static bool IsGenericTypeDefinition(this Type type)
        {
          TypeInfo info = type.GetTypeInfo();
          return info.IsGenericTypeDefinition; // .IsGenericType();
        }
        
        public static Type[] GetGenericArguments(this Type type)
        {
          TypeInfo info = type.GetTypeInfo();
          return info.GenericTypeArguments;
        }

        // public abstract Assembly Assembly { get; }
        // public bool IsAutoClass { get; }
        // public abstract Type[] GenericTypeArguments { get; }
        // public override MemberTypes MemberType { get; }
        // public virtual Type UnderlyingSystemType { get; }
        // public ConstructorInfo TypeInitializer { get; }

        public static Type BaseType(this Type type) => type.GetTypeInfo().BaseType;
        
        public static Type UnderlyingSystemType (this Type type) => type.GetTypeInfo().UnderlyingSystemType;
        public static TypeInfo GetDeclaredNestedType(this Type type, string name) => type.GetTypeInfo().GetDeclaredNestedType(name);
        public static object Method(this Type type, string name)
        {
          TypeInfo info = type.GetTypeInfo();
          return info.GetDeclaredMethod(name);
          // Assembly System.Reflection, Version=4.1.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a : System.Reflection.dll
        }

    }

}

namespace IQToolkit
{
    using System.Reflection;
    /// <summary>
    /// Type related helper methods
    /// </summary>
    public static class TypeHelper
    {
        public static Type FindIEnumerable(Type seqType)
        {
            if (seqType == null || seqType == typeof(string))
                return null;
            if (seqType.IsArray)
                return typeof(IEnumerable<>).MakeGenericType(seqType.GetElementType());

            var seqTypeInfo = seqType.GetTypeInfo();
            if (seqTypeInfo.IsGenericType)
            {
                foreach (Type arg in seqTypeInfo.GetGenericArguments())
                {
                    Type ienum = typeof(IEnumerable<>).MakeGenericType(arg);
                    if (ienum.GetTypeInfo().IsAssignableFrom(seqType))
                    {
                        return ienum;
                    }
                }
            }
            Type[] ifaces = seqType.GetTypeInfo().GetInterfaces();
            if (ifaces != null && ifaces.Length > 0)
            {
                foreach (Type iface in ifaces)
                {
                    Type ienum = FindIEnumerable(iface);
                    if (ienum != null) return ienum;
                }
            }
            var BaseType  = seqType.GetTypeInfo().BaseType;
            if (BaseType != null && BaseType != typeof(object))
            {
                return FindIEnumerable(BaseType);
            }
            return null;
        }

        public static Type GetSequenceType(Type elementType)
        {
            return typeof(IEnumerable<>).MakeGenericType(elementType);
        }

        public static Type GetElementType(Type seqType)
        {
            Type ienum = FindIEnumerable(seqType);
            if (ienum == null) return seqType;
            return ienum.GetTypeInfo().GetGenericArguments()[0];
        }

        public static bool IsNullableType(Type type)
        {
            return type != null && type.GetTypeInfo().IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
        }

        public static bool IsNullAssignable(Type type)
        {
            return !type.GetTypeInfo().IsValueType || IsNullableType(type);
        }

        public static Type GetNonNullableType(Type type)
        {
            if (IsNullableType(type))
            {
                return type.GetGenericArguments()[0];
            }
            return type;
        }

        public static Type GetNullAssignableType(Type type)
        {
            if (!IsNullAssignable(type))
            {
                return typeof(Nullable<>).MakeGenericType(type);
            }
            return type;
        }

        public static ConstantExpression GetNullConstant(Type type)
        {
            return Expression.Constant(null, GetNullAssignableType(type));
        }

        public static Type GetMemberType(MemberInfo mi)
        {
            FieldInfo fi = mi as FieldInfo;
            if (fi != null) return fi.FieldType;
            PropertyInfo pi = mi as PropertyInfo;
            if (pi != null) return pi.PropertyType;
            EventInfo ei = mi as EventInfo;
            if (ei != null) return ei.EventHandlerType;
            MethodInfo meth = mi as MethodInfo;  // property getters really
            if (meth != null) return meth.ReturnType;
            return null;
        }

        public static object GetDefault(Type type)
        {
            bool isNullable = !type.GetTypeInfo().IsValueType || TypeHelper.IsNullableType(type);
            if (!isNullable)
                return Activator.CreateInstance(type);
            return null;
        }

        public static bool IsReadOnly(MemberInfo member)
        {
            switch (member.MemberType)
            {
                case MemberTypes.Field:
                    return (((FieldInfo)member).Attributes & FieldAttributes.InitOnly) != 0;
                case MemberTypes.Property:
                    PropertyInfo pi = (PropertyInfo)member;
                    return !pi.CanWrite || pi.GetSetMethod() == null;
                default:
                    return true;
            }
        }

        public static bool IsInteger(Type type)
        {
            Type nnType = GetNonNullableType(type);
            switch (Type.GetTypeCode(nnType))
            {
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.Byte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    return true;
                default:
                    return false;
            }
        }
    }
}
