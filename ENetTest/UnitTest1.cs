using System;
using Xunit;
using Xunit.Abstractions;
using ENetCore;
using System.Runtime.InteropServices;
using System.Reflection;

namespace ENetTest
{
    public class UnitTest1
    {

        private readonly ITestOutputHelper output;
        public UnitTest1(ITestOutputHelper output)
        {
            this.output = output;
        }


        [Fact]
        public void Test1()
        {
            unsafe
            {
                output.WriteLine(sizeof(ENetProtocol).ToString());
            }

            var curVersion = ENetVersion.Current;
            output.WriteLine(curVersion.ToString());
            output.WriteLine(ENetVersion.GetPatch(curVersion).ToString());
            output.WriteLine(ENetVersion.GetMajor(curVersion).ToString());
            output.WriteLine(ENetVersion.GetMiner(curVersion).ToString());
            output.WriteLine(0xFF.ToString());

            output.WriteLine(ENetTime.Less(0, 1).ToString());

            for(int i = 0; i < 10; i++)
            {
                output.WriteLine(ENetTime.GetTime().ToString());
                System.Threading.Thread.Sleep(10);
            }

            ENetAddress Address = new ENetAddress();

            var ipEndpoint = Address.ToIPEndPoint();

            output.WriteLine(ipEndpoint.ToString());

            var method =typeof(DelegateTestClass).GetMethod("Foo", BindingFlags.Public | BindingFlags.Instance);
            var openDelegate = (TestDelegate)method.CreateDelegate(typeof(TestDelegate));
            openDelegate.Invoke(new DelegateTestClass("haha"), output);

            ENetProtocolSendFragment frag = new ENetProtocolSendFragment();
            frag.fragmentCount = 1;
            frag.fragmentOffset = 2;
            frag.startSequenceNumber = 3;
            var bytes = new byte[100];
            ENetUtil.WriteStructure(frag, bytes);

            var readFrag = ENetUtil.ReadStructure<ENetProtocolSendFragment>(bytes);


            var size1 = SizeOf<SizeTestStruct1>.Value;
            var size2 = SizeOf<SizeTestStruct2>.Value;

            output.WriteLine(0x10000.ToString());
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SizeTestStruct1
        {
            public short value1;
            public int value2;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct SizeTestStruct2
        {
            public short value1;
            public int value2;
        }

        public static HahaDelegage Haha;
        public delegate void HahaDelegage(int inVal);
        public delegate void TestDelegate(DelegateTestClass target, ITestOutputHelper output);
        public static class SizeOf<T> where T : struct
        {
            static SizeOf()
            {
                T[] m_Array = new T[2];

                unsafe
                {
                    TypedReference elem1 = __makeref(m_Array[0]), elem2 = __makeref(m_Array[1]);
                    Value = (int)((byte*)*(IntPtr*)(&elem2) - (byte*)*(IntPtr*)(&elem1));
                }
            }
            public static readonly int Value;
        }

        public class DelegateTestClass
        {
            string m_InnerString;
            public DelegateTestClass(string str)
            {
                m_InnerString = str;
            }
            public void Foo(ITestOutputHelper output)
            {
                output.WriteLine(m_InnerString);
            }
        }



        [Fact]
        public void UnSequencedTest()
        {
            //unsequencedgroup is just incremental number of remote peer
            ushort incomingUnsequencedGroup = 0;
            ushort seq = 0;
            while(true)
            {
                if(seq == uint.MaxValue)
                {
                    output.WriteLine("haha");
                }
                CalculateUnsequenced(seq++, ref incomingUnsequencedGroup);
            }
        }

        public void CalculateUnsequenced(ushort unsequencedGroupShort,ref ushort incomingUnsequencedGroup)
        {
            uint unsequencedGroup = unsequencedGroupShort;
            //index is under 1024 = 100 0000 0000 = 2^10
            var index = unsequencedGroup % 1024;

            // if a message is late, unsequencedGroup is lower than incommingunsequencedgroup
            //this is for loop
            if (unsequencedGroup < incomingUnsequencedGroup)
                // 1 0000 0000 0000 0000 add 
                unsequencedGroup += 0x10000;

            //window size * window is entire number viable for unsequenced windows
            //because of peer.incomingUnsequencedGroup = (ushort)(unsequencedGroup - index); 
            //and it's masked by 0xFFFF, it reperesents number of groups
            if (unsequencedGroup >= incomingUnsequencedGroup + 1024 * 32)
            {
                output.WriteLine("overflowerror");
                return;
            }

            //mask it 1111 1111 1111 1111
            unsequencedGroup &= 0xFFFF;


            if (unsequencedGroup - index != incomingUnsequencedGroup)
            {
                incomingUnsequencedGroup = (ushort)(unsequencedGroup - index);
                output.WriteLine("Clear");
                //clear
            }
            else
            {
                output.WriteLine("CheckReceive");
                //already received? ok, not? receive and mark
            }
        }
    }
}
