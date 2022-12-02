using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using DBOUtils;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;

namespace UserPermissionMapTests
{

    public class ECallbackFailed : Exception
    {
        public ECallbackFailed(string msg) : base(msg)
        {

        }
    }

    [TestClass]
    public class Tests
    {
        [TestMethod]
        public void GetOneUserPermissions()
        {
            var l = new List<int>();
            l.Add(42);

            var map = new UsersPermissionsMap((int u) => { return l; });
            Assert.AreEqual(map.GetUserPermission(1), l);
        }

        [TestMethod]
        public void GetPermissionsFor2Users()
        {
            var l1 = new List<int>();
            l1.Add(42);

            var l2 = new List<int>();
            l2.AddRange(new int[] { 1, 2, 3 });

            var map = new UsersPermissionsMap((int u) => { if (u == 1) return l1; else return l2; });
            Assert.AreEqual(map.GetUserPermission(1), l1);
            Assert.AreEqual(map.GetUserPermission(2), l2);
        }


        [TestMethod]
        public void RetrievePermissionOnlyOnceForUser()
        {
            int Counter = 0;
            var l1 = new List<int>();
            l1.Add(42);

            var map = new UsersPermissionsMap((int u) => { ++Counter; return l1; });
            Assert.AreEqual(map.GetUserPermission(1), l1);
            Assert.AreEqual(map.GetUserPermission(1), l1);
            Assert.AreEqual(Counter, 1);
        }

        public class TestThread
        {
            public static void Do(UsersPermissionsMap m, List<int> l, ref bool TestOK)
            {
                var lret = m.GetUserPermission(1);
                TestOK = lret == l;
            }

            public static void DoFail(UsersPermissionsMap m, ref bool TestOK)
            {
                TestOK = false;
                try
                {
                    m.GetUserPermission(1);
                }
                catch (ECallbackFailed)
                {
                    TestOK = true;
                }

            }

            public static void DoMany(UsersPermissionsMap m, int Size, Action ReportOK)
            {
                try
                {
                    for (int i = 0; i < Size; ++i)
                    {
                        var l = m.GetUserPermission(i);
                        if (l.Count == 0)
                            return;
                        if (l[0] != i)
                            return;
                    }

                    ReportOK();

                }
                catch (Exception e)
                {

                }

            }

        }

        [TestMethod]
        public void ThreadWaitsUntilDataAvailable()
        {
            int SleepTimeInMS = 300;
            int Counter = 0;
            bool ListsEqual = false;
            var l = new List<int>(new int[] { 2, 3, 4 });
            var map = new UsersPermissionsMap((int u) => { Interlocked.Increment(ref Counter); Thread.Sleep(SleepTimeInMS); return l; });
            Thread t = new Thread(() => TestThread.Do(map, l, ref ListsEqual));

            Stopwatch s = new Stopwatch();
            s.Start();
            t.Start();

            t.Join();
            s.Stop();
            Assert.IsTrue(s.ElapsedMilliseconds > SleepTimeInMS);
            Assert.AreEqual(Counter, 1);
            Assert.IsTrue(ListsEqual);          // assert that other thread saw the correct data
        }

        [TestMethod]
        public void ExceptionThrownIfCallbackThrows()
        {
            bool TestOK = false;
            var map = new UsersPermissionsMap((int u) => { throw new ECallbackFailed("Test"); });

            Assert.ThrowsException<ECallbackFailed>(() =>
            {
                map.GetUserPermission(1);
            });

            Thread t = new Thread(() => TestThread.DoFail(map, ref TestOK));

            t.Start();
            t.Join();

            Assert.IsTrue(TestOK);          // assert that other thread saw the correct data
        }

        [TestMethod]
        public void AssertEqualTest()
        {
            var l1 = new List<int>();
            var l2 = new List<int>(new int[] { 1, 2 });
            var l3 = new List<int>(new int[] { 1, 2 });
            Assert.AreNotEqual(l1, l2);
            Assert.AreNotEqual(l2, l3);     // assert compares references and not the actual contents of the list
        }

        [TestMethod]
        public void PerfTest()
        {
            int Size = 100;
            Thread[] Threads = new Thread[Size];
            bool[] TestsOK = new bool[Size];

            var map = new UsersPermissionsMap((int u) => { return new List<int>(new int[] { u }); });
            
            for (int i = 0; i < Size; ++i)
            {
                int idx = i;        
                Action a = () => { 
                    lock (TestsOK) { 
                        TestsOK[idx] = true;       // we have to capture idx and not i here - see
                                                   // https://unicorn-dev.medium.com/how-to-capture-a-variable-in-c-and-not-to-shoot-yourself-in-the-foot-d169aa161aa6
                    } 
                };
                Threads[i] = new Thread(() => TestThread.DoMany(map, Size, a));
                Threads[i].Start();
            }

            bool Result = true;

            for (int i = 0; i < Size; ++i)
            {
                Threads[i].Join();
            }

            lock (TestsOK)
            {
                for (int i = 0; i < Size; ++i)
                {
                    Result &= TestsOK[i];
                }

            }

            Assert.IsTrue(Result);
        }
    }
}
