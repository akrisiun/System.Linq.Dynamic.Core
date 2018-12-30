// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace IQToolkit
{
    /// <summary>
    /// Make a strongly-typed delegate to a weakly typed method (one that takes single object[] argument)
    /// (up to 8 arguments)
    /// </summary>
    public class StrongDelegate
    {
        Func<object[], object> fn;

        private StrongDelegate(Func<object[], object> fn)
        {
            this.fn = fn;
        }

        private static MethodInfo[] _meths;

        static StrongDelegate()
        {
            _meths = new MethodInfo[9];

            var meths = typeof(StrongDelegate).GetTypeInfo().GetMethods();
            for (int i = 0, n = meths.Length; i < n; i++)
            {
                var gm = meths[i];
                if (gm.Name.StartsWith("M"))
                {
                    var tas = gm.GetGenericArguments();
                    _meths[tas.Length - 1] = gm;
                }
            }
        }

        /// <summary>
        /// Create a strongly typed delegate over a method with a weak signature
        /// </summary>
        /// <param name="delegateType">The strongly typed delegate's type</param>
        /// <param name="target"></param>
        /// <param name="method">Any method that takes a single array of objects and returns an object.</param>
        /// <returns></returns>
        public static Delegate CreateDelegate(Type delegateType, object target, MethodInfo method)
        {
            Func<object[], object> func = WrapDelegate.CreateDelegateFunc<object[], object>
                   (typeof(Func<object[], object>), target, method);
            return CreateDelegate(delegateType, func);
        }

        /// <summary>
        /// Create a strongly typed delegate over a Func delegate with weak signature
        /// </summary>
        /// <param name="delegateType"></param>
        /// <param name="fn"></param>
        /// <returns></returns>
        public static Delegate CreateDelegate(Type delegateType, Func<object[], object> fn)
        {
            MethodInfo invoke = delegateType.GetTypeInfo().GetMethod("Invoke");

            var parameters = invoke.GetParameters();
            Type[] typeArgs = new Type[1 + parameters.Length];
            for (int i = 0, n = parameters.Length; i < n; i++)
            {
                typeArgs[i] = parameters[i].ParameterType;
            }
            typeArgs[typeArgs.Length - 1] = invoke.ReturnType;
            if (typeArgs.Length <= _meths.Length)
            {
                var gm = _meths[typeArgs.Length - 1];
                MethodInfo m = gm.MakeGenericMethod(typeArgs);

                // System.Runtime, Version=4.1.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a // System.Runtime.dll
                return WrapDelegate.CreateDelegate(delegateType, new StrongDelegate(fn), m);
            }
            throw new NotSupportedException("Delegate has too many arguments");
        }

        // Use MethodInfo.CreateDelegate instead.
        

        public R M<R>()
        {
            return (R)fn(null);
        }

        public R M<A1, R>(A1 a1)
        {
            return (R)fn(new object[] { a1 });
        }

        public R M<A1, A2, R>(A1 a1, A2 a2)
        {
            return (R)fn(new object[] { a1, a2 });
        }

        public R M<A1, A2, A3, R>(A1 a1, A2 a2, A3 a3)
        {
            return (R)fn(new object[] { a1, a2, a3 });
        }

        public R M<A1, A2, A3, A4, R>(A1 a1, A2 a2, A3 a3, A4 a4)
        {
            return (R)fn(new object[] { a1, a2, a3, a4 });
        }

        public R M<A1, A2, A3, A4, A5, R>(A1 a1, A2 a2, A3 a3, A4 a4, A5 a5)
        {
            return (R)fn(new object[] { a1, a2, a3, a4, a5 });
        }

        public R M<A1, A2, A3, A4, A5, A6, R>(A1 a1, A2 a2, A3 a3, A4 a4, A5 a5, A6 a6)
        {
            return (R)fn(new object[] { a1, a2, a3, a4, a5, a6 });
        }

        public R M<A1, A2, A3, A4, A5, A6, A7, R>(A1 a1, A2 a2, A3 a3, A4 a4, A5 a5, A6 a6, A7 a7)
        {
            return (R)fn(new object[] { a1, a2, a3, a4, a5, a6, a7 });
        }

        public R M<A1, A2, A3, A4, A5, A6, A7, A8, R>(A1 a1, A2 a2, A3 a3, A4 a4, A5 a5, A6 a6, A7 a7, A8 a8)
        {
            return (R)fn(new object[] { a1, a2, a3, a4, a5, a6, a7, a8 });
        }
    }
}