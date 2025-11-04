using System;
using System.Threading;

namespace IoTPowerShellAgent.Utilities
{




    public sealed class ProcessPriorityManager : IDisposable
    {
        private static readonly object _lock = new object();
        private static int _activeExecutionCount = 0;
        private static uint _originalPriority = WindowsApiInterop.NORMAL_PRIORITY_CLASS;
        private static bool _originalPrioritySet = false;
        private bool _disposed = false;





        public ProcessPriorityManager()
        {
            lock (_lock)
            {
                _activeExecutionCount++;


                if (_activeExecutionCount == 1)
                {

                    if (!_originalPrioritySet)
                    {


                        _originalPriority = WindowsApiInterop.NORMAL_PRIORITY_CLASS;
                        _originalPrioritySet = true;
                    }


                    ProcessUtil.SetProcessPriority(WindowsApiInterop.HIGH_PRIORITY_CLASS);
                }
            }
        }




        public void Dispose()
        {
            if (_disposed)
                return;

            lock (_lock)
            {
                _activeExecutionCount--;


                if (_activeExecutionCount == 0)
                {
                    ProcessUtil.SetProcessPriority(_originalPriority);
                }
                else if (_activeExecutionCount < 0)
                {

                    _activeExecutionCount = 0;
                    ProcessUtil.SetProcessPriority(_originalPriority);
                }

                _disposed = true;
            }
        }




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

