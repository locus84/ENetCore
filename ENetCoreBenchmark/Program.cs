using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace ENetCoreBenchmark
{
    [MemoryDiagnoser]
    public class MyBenchmark
    {
        TestClassBase normalObj;
        TestClassBase reflectedObj;

        public MyBenchmark()
        {

        }

        [Benchmark]
        public void NormalCall()
        {
            normalObj = new TestClass<int>() as TestClassBase;
            for (int i = 0; i < 10000; i++)
            {
                var tr = __makeref(i);
                normalObj.Foo(tr);
            }
        }

        [Benchmark]
        public void GenericCall()
        {
            var t = typeof(TestClass<>).MakeGenericType(typeof(int));
            reflectedObj = System.Activator.CreateInstance(t) as TestClassBase;
            for (int i = 0; i < 10000; i++)
            {
                var tr = __makeref(i);
                reflectedObj.Foo(tr);
            }
        }


        System.Net.IPEndPoint ep = new System.Net.IPEndPoint(System.Net.IPAddress.Any, 5555);
        System.Net.IPEndPoint ep2 = new System.Net.IPEndPoint(System.Net.IPAddress.Any, 5555);
        [Benchmark]
        public void IpEndPointRelated()
        {
            System.Net.Sockets.Socket soc = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetworkV6, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
            soc.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.IPv6Any, 0));
            for(int i = 0; i < 100000; i++)
            {
                var haha = ep2.Address;
            }
        }
    }

    public class TestClassBase
    {
        public virtual void Foo(TypedReference tr)
        {

        }
    }
    public class TestClass<T> : TestClassBase
    {

        public override void Foo(TypedReference tr)
        {
            T inVal = __refvalue(tr, T);
        }

        public void MakeGeneric(MethodInfo info)
        {
        }


    }

    class Program
    {
        static void Main(string[] args)
        {
            var summery = BenchmarkRunner.Run<MyBenchmark>();
            Console.Write(summery);
            Console.ReadLine();
        }
    }
}
