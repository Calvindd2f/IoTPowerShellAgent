using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Runtime.CompilerServices;
using System.Security;

namespace IoTPowerShellAgent.PowerShell
{
    public class DefaultHostUserInterface : PSHostUserInterface, IHostUISupportsMultipleChoiceSelection
    {
        private InformationDelegate? _onInformation;
        public PSHostRawUserInterface _psRawUserInterface = new DefaultHostRawUserInterface();

        private static string[] GetHotkeyAndLabel(string input)
        {
            string[] hotkeyAndLabel = new string[2]
            {
                string.Empty,
                string.Empty
            };
            char[] chArray = new char[1] { '&' };
            string[] strArray = input.Split(chArray);
            if (strArray.Length == 2)
            {
                if (strArray[1].Length > 0)
                {
                    string upper = strArray[1][0].ToString().ToUpper(CultureInfo.CurrentCulture);
                    hotkeyAndLabel[0] = upper;
                }
                string str = (strArray[0] + strArray[1]).Trim();
                hotkeyAndLabel[1] = str;
            }
            else
                hotkeyAndLabel[1] = input;
            return hotkeyAndLabel;
        }

        private static string[,] BuildHotkeysAndPlainLabels(Collection<ChoiceDescription> choices)
        {
            string[,] strArray = new string[2, choices.Count];
            int index = 0;
            if (0 < choices.Count)
            {
                do
                {
                    string[] hotkeyAndLabel = DefaultHostUserInterface.GetHotkeyAndLabel(choices[index].Label);
                    strArray[0, index] = hotkeyAndLabel[0];
                    strArray[1, index] = hotkeyAndLabel[1];
                    ++index;
                }
                while (index < choices.Count);
            }
            return strArray;
        }

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

        private void raise_OnInformation(string value0)
        {
            _onInformation?.Invoke(value0);
        }

        public override PSHostRawUserInterface RawUI => this._psRawUserInterface;

        public override string ReadLine() => throw new NotImplementedException("Interactive input is not supported");

        public override SecureString ReadLineAsSecureString() => throw new NotImplementedException("Interactive input is not supported");

        public override void Write(
          ConsoleColor foregroundColor,
          ConsoleColor backgroundColor,
          string value)
        {
            this.raise_OnInformation(value);
        }

        public override void Write(string value) => this.raise_OnInformation(value);

        public override void WriteLine(string value) => this.raise_OnInformation(value);

        public override void WriteErrorLine(string value)
        {

        }

        public override void WriteDebugLine(string message)
        {

        }

        public override void WriteProgress(long sourceId, ProgressRecord record)
        {

        }

        public override void WriteVerboseLine(string message)
        {

        }

        public override void WriteWarningLine(string message)
        {

        }

        public override Dictionary<string, PSObject> Prompt(
          string caption,
          string message,
          Collection<FieldDescription> descriptions)
        {
            throw new NotImplementedException("Interactive prompts are not supported");
        }

        public override PSCredential PromptForCredential(
            string caption,
            string message,
            string userName,
            string targetName,
            PSCredentialTypes allowedCredentialTypes,
            PSCredentialUIOptions options)
        {
            throw new NotImplementedException("Interactive credential prompts are not supported");
        }

        public override PSCredential PromptForCredential(
          string caption,
          string message,
          string userName,
          string targetName)
        {
            throw new NotImplementedException("Interactive credential prompts are not supported");
        }

        public virtual Collection<int> PromptForChoice(
          string? caption,
          string? message,
          Collection<ChoiceDescription> choices,
          IEnumerable<int>? defaultChoices)
        {
            throw new NotImplementedException("Interactive choice selection is not supported");
        }

        public override int PromptForChoice(
          string caption,
          string message,
          Collection<ChoiceDescription> choices,
          int defaultChoice)
        {
            throw new NotImplementedException("Interactive choice selection is not supported");
        }

        public delegate void InformationDelegate(string information);
    }
}