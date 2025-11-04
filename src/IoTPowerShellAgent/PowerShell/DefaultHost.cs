using System;
using System.Globalization;
using System.Management.Automation.Host;
using System.Runtime.CompilerServices;

namespace IoTPowerShellAgent.PowerShell
{
    public class DefaultHost : PSHost
    {
        private CultureInfo _currentCulture;
        private CultureInfo _currentUICulture;
        private InformationDelegate? _onInformation;

        public event InformationDelegate OnInformation
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            add
            {
                _onInformation += value;
                GC.KeepAlive(this);
            }
            [MethodImpl(MethodImplOptions.Synchronized)]
            remove
            {
                _onInformation -= value;
                GC.KeepAlive(this);
            }
        }

        private void HandleInformation(string information) => _onInformation?.Invoke(information);

        public DefaultHost(CultureInfo currentCulture, CultureInfo currentUICulture)
        {
            this._currentCulture = currentCulture;
            this._currentUICulture = currentUICulture;
        }

        public override string Name => "Default Host";

        public override Version Version => new Version(1, 0);

        public override Guid InstanceId
        {
            get
            {
                Guid instanceId = Guid.NewGuid();
                GC.KeepAlive(this);
                return instanceId;
            }
        }

        public override PSHostUserInterface UI
        {
            get
            {
                DefaultHostUserInterface ui = new DefaultHostUserInterface();
                ui.OnInformation += new DefaultHostUserInterface.InformationDelegate(this.HandleInformation);
                GC.KeepAlive(this);
                return ui;
            }
        }

        public override CultureInfo CurrentCulture
        {
            get => this._currentCulture;
        }

        public override CultureInfo CurrentUICulture
        {
            get => this._currentUICulture;
        }

        public override void SetShouldExit(int exitCode)
        {

        }

        public override void EnterNestedPrompt()
        {
            throw new NotSupportedException("Nested prompt is not supported");
        }

        public override void ExitNestedPrompt()
        {
            throw new NotSupportedException("Exit nested prompt is not supported");
        }

        public override void NotifyBeginApplication()
        {
        }

        public override void NotifyEndApplication()
        {
        }

        public delegate void InformationDelegate(string information);
    }
}