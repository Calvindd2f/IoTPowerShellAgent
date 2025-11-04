using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using IoTPowerShellAgent.Utilities;
using Xunit;

namespace IoTPowerShellAgent.Tests.Utilities
{



    public class ProcessPriorityManagerTests
    {
        [Fact]
        public void ProcessPriorityManager_SingleExecution_ElevatesAndRestores()
        {

            int initialCount = ProcessPriorityManager.ActiveExecutionCount;


            using (var manager = new ProcessPriorityManager())
            {

                ProcessPriorityManager.ActiveExecutionCount.Should().Be(initialCount + 1);
            }


            ProcessPriorityManager.ActiveExecutionCount.Should().Be(initialCount);
        }

        [Fact]
        public void ProcessPriorityManager_ConcurrentExecutions_MaintainsHighPriority()
        {

            int initialCount = ProcessPriorityManager.ActiveExecutionCount;


            using (var manager1 = new ProcessPriorityManager())
            {
                ProcessPriorityManager.ActiveExecutionCount.Should().Be(initialCount + 1);

                using (var manager2 = new ProcessPriorityManager())
                {
                    ProcessPriorityManager.ActiveExecutionCount.Should().Be(initialCount + 2);
                }


                ProcessPriorityManager.ActiveExecutionCount.Should().Be(initialCount + 1);
            }


            ProcessPriorityManager.ActiveExecutionCount.Should().Be(initialCount);
        }

        [Fact]
        public void ProcessPriorityManager_Dispose_IsIdempotent()
        {

            var manager = new ProcessPriorityManager();
            int countAfterCreate = ProcessPriorityManager.ActiveExecutionCount;


            manager.Dispose();
            int countAfterFirstDispose = ProcessPriorityManager.ActiveExecutionCount;
            manager.Dispose();
            int countAfterSecondDispose = ProcessPriorityManager.ActiveExecutionCount;


            countAfterFirstDispose.Should().BeLessThan(countAfterCreate);
            countAfterSecondDispose.Should().Be(countAfterFirstDispose);
        }

        [Fact]
        public void ProcessPriorityManager_Reset_RestoresToInitialState()
        {

            using (var manager = new ProcessPriorityManager())
            {
                ProcessPriorityManager.ActiveExecutionCount.Should().BeGreaterThan(0);
            }


            ProcessPriorityManager.Reset();


            ProcessPriorityManager.ActiveExecutionCount.Should().Be(0);
        }

        [Fact]
        public async Task ProcessPriorityManager_AsyncExecution_WorksCorrectly()
        {

            int initialCount = ProcessPriorityManager.ActiveExecutionCount;


            await Task.Run(() =>
            {
                using (var manager = new ProcessPriorityManager())
                {
                    ProcessPriorityManager.ActiveExecutionCount.Should().Be(initialCount + 1);
                    Thread.Sleep(10);
                }
            });


            ProcessPriorityManager.ActiveExecutionCount.Should().Be(initialCount);
        }
    }
}

