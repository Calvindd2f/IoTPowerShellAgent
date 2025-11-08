using System;
using System.Threading;

namespace IoTPowerShellAgent.Utilities
{
    /// <summary>
    /// Manages dynamic process priority adjustment for optimal performance during execution
    /// Uses reference counting to handle concurrent executions
    /// </summary>
    public sealed class ProcessPriorityManager : IDisposable
    {
        private static readonly object _lock = new object();
        private static int _activeExecutionCount = 0;
        private static uint _originalPriority = WindowsApiInterop.NORMAL_PRIORITY_CLASS;
        private static bool _originalPrioritySet = false;
        private bool _disposed = false;

        /// <summary>
        /// Creates a new priority manager that elevates process priority to HIGH during execution
        /// Priority is automatically restored when disposed
        /// </summary>
        public ProcessPriorityManager()
        {
            lock (_lock)
            {
                _activeExecutionCount++;

                // If this is the first active execution, elevate priority to HIGH
                if (_activeExecutionCount == 1)
                {
                    // Store original priority if not already stored
                    if (!_originalPrioritySet)
                    {
                        // Get current priority (we'll assume NORMAL if we can't determine)
                        // In practice, we start at NORMAL, so this is safe
                        _originalPriority = WindowsApiInterop.NORMAL_PRIORITY_CLASS;
                        _originalPrioritySet = true;
                    }

                    // Set to HIGH priority for execution
                    ProcessUtil.SetProcessPriority(WindowsApiInterop.HIGH_PRIORITY_CLASS);
                }
            }
        }

        /// <summary>
        /// Restores process priority to original (NORMAL) when execution completes
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            lock (_lock)
            {
                _activeExecutionCount--;

                // If no more active executions, restore to original priority
                if (_activeExecutionCount == 0)
                {
                    ProcessUtil.SetProcessPriority(_originalPriority);
                }
                else if (_activeExecutionCount < 0)
                {
                    // Safety check - shouldn't happen, but reset if it does
                    _activeExecutionCount = 0;
                    ProcessUtil.SetProcessPriority(_originalPriority);
                }

                _disposed = true;
            }
        }

        /// <summary>
        /// Gets the current number of active executions
        /// </summary>
        public static int ActiveExecutionCount
        {
            get
            {
                lock (_lock)
                {
                    return _activeExecutionCount;
                }
            }
        }

        /// <summary>
        /// Resets the priority manager (for testing or service restart scenarios)
        /// </summary>
        public static void Reset()
        {
            lock (_lock)
            {
                _activeExecutionCount = 0;
                _originalPriority = WindowsApiInterop.NORMAL_PRIORITY_CLASS;
                _originalPrioritySet = false;
                ProcessUtil.SetProcessPriority(_originalPriority);
            }
        }
    }
}

