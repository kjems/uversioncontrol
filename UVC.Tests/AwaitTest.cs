using System.Threading;
using NUnit.Framework;

namespace UVC.UnitTests
{
    public class AwaitTest
    {
        [Test]
        public async void TaskPoolRoundtrip()
        {
            var mainThreadID = Thread.CurrentThread.ManagedThreadId;
            Assert.AreEqual(mainThreadID, Thread.CurrentThread.ManagedThreadId);
            
            await Awaiters.TaskPool;
            Assert.AreNotEqual(mainThreadID, Thread.CurrentThread.ManagedThreadId);
            
            await Awaiters.MainThread;
            Assert.AreEqual(mainThreadID, Thread.CurrentThread.ManagedThreadId);
        }
    }
}