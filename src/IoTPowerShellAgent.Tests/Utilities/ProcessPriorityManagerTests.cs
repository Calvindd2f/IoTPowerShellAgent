using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using IoTPowerShellAgent.Utilities;
using Xunit;

namespace IoTPowerShellAgent.Tests.Utilities
{
    /// <summary>
    /// Unit tests for ProcessPriorityManager
    /// </summary>
    public class ProcessPriorityManagerTests
    {
        [Fact]
        public void ProcessPriorityManager_SingleExecution_ElevatesAndRestores()
        {
            // Arrange
            int initialCount = ProcessPriorityManager.ActiveExecutionCount;

            // Act
            using (var manager = new ProcessPriorityManager())
            {
                // Assert
                ProcessPriorityManager.ActiveExecutionCount.Should().Be(initialCount + 1);
            }

            // Assert - priority should be restored
            ProcessPriorityManager.ActiveExecutionCount.Should().Be(initialCount);
        }

        [Fact]
        public void ProcessPriorityManager_ConcurrentExecutions_MaintainsHighPriority()
        {
            // Arrange
            int initialCount = ProcessPriorityManager.ActiveExecutionCount;

            // Act
            using (var manager1 = new ProcessPriorityManager())
            {
                ProcessPriorityManager.ActiveExecutionCount.Should().Be(initialCount + 1);

                using (var manager2 = new ProcessPriorityManager())
                {
                    ProcessPriorityManager.ActiveExecutionCount.Should().Be(initialCount + 2);
                }

                // After manager2 is disposed, count should decrease but priority stays HIGH
                ProcessPriorityManager.ActiveExecutionCount.Should().Be(initialCount + 1);
            }

            // Assert - all executions complete, priority restored
            ProcessPriorityManager.ActiveExecutionCount.Should().Be(initialCount);
        }

        [Fact]
        public void ProcessPriorityManager_Dispose_IsIdempotent()
        {
            // Arrange
            var manager = new ProcessPriorityManager();
            int countAfterCreate = ProcessPriorityManager.ActiveExecutionCount;

            // Act
            manager.Dispose();
            int countAfterFirstDispose = ProcessPriorityManager.ActiveExecutionCount;
            manager.Dispose(); // Second dispose should be safe
            int countAfterSecondDispose = ProcessPriorityManager.ActiveExecutionCount;

            // Assert
            countAfterFirstDispose.Should().BeLessThan(countAfterCreate);
            countAfterSecondDispose.Should().Be(countAfterFirstDispose);
        }

        [Fact]
        public void ProcessPriorityManager_Reset_RestoresToInitialState()
        {
            // Arrange
            using (var manager = new ProcessPriorityManager())
            {
                ProcessPriorityManager.ActiveExecutionCount.Should().BeGreaterThan(0);
            }

            // Act
            ProcessPriorityManager.Reset();

            // Assert
            ProcessPriorityManager.ActiveExecutionCount.Should().Be(0);
        }

        [Fact]
        public async Task ProcessPriorityManager_AsyncExecution_WorksCorrectly()
        {
            // Arrange
            int initialCount = ProcessPriorityManager.ActiveExecutionCount;

            // Act
            await Task.Run(() =>
            {
                using (var manager = new ProcessPriorityManager())
                {
                    ProcessPriorityManager.ActiveExecutionCount.Should().Be(initialCount + 1);
                    Thread.Sleep(10); // Simulate work
                }
            });

            // Assert
            ProcessPriorityManager.ActiveExecutionCount.Should().Be(initialCount);
        }
    }
}

