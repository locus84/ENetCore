using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace ENetCore
{
    public abstract class FastInvokerBase
    {
        public abstract void Invoke<T>(T target, object parameter) where T : class;
    }

    public class FastInvoker<Target, T1> : FastInvokerBase where Target : class
    {
        delegate void InvokeDelegate(Target target, T1 parameter);
        InvokeDelegate m_InnerDelegate;

        public FastInvoker(MethodInfo methodInfo)
        {
            m_InnerDelegate = methodInfo.CreateDelegate(typeof(InvokeDelegate)) as InvokeDelegate;
        }

        public override void Invoke<T>(T target, object parameter)
        {
            if(target is Target)
                m_InnerDelegate.Invoke(target as Target, (T1)parameter);
        }
    }


    public class FastInvoker<Target, T1, T2> : FastInvokerBase where Target : class
    {
        delegate void InvokeDelegate(Target target, T1 parameter1, T2 parameter2);
        InvokeDelegate m_InnerDelegate;

        public FastInvoker(MethodInfo methodInfo)
        {
            m_InnerDelegate = methodInfo.CreateDelegate(typeof(InvokeDelegate)) as InvokeDelegate;
        }

        public override void Invoke<T>(T target, object parameter)
        {
            if (target is Target)
                m_InnerDelegate.Invoke(target as Target, (T1)parameter, (T2)parameter);
        }

    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class RPCAttribute : Attribute { }
}
