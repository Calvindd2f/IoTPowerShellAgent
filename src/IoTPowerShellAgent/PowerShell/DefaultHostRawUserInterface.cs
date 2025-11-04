using System;
using System.Management.Automation.Host;
using System.Runtime.InteropServices;

namespace IoTPowerShellAgent.PowerShell
{
    public class DefaultHostRawUserInterface : PSHostRawUserInterface
    {
        public override ConsoleColor ForegroundColor
        {
            get
            {
                int foregroundColor = (int)Console.ForegroundColor;
                GC.KeepAlive(this);
                return (ConsoleColor)foregroundColor;
            }
            set => Console.ForegroundColor = value;
        }

        public override ConsoleColor BackgroundColor
        {
            get
            {
                int backgroundColor = (int)Console.BackgroundColor;
                GC.KeepAlive(this);
                return (ConsoleColor)backgroundColor;
            }
            set => Console.BackgroundColor = value;
        }

        public override Coordinates CursorPosition
        {
            get
            {
                Coordinates cursorPosition = new Coordinates(Console.CursorLeft, Console.CursorTop);
                GC.KeepAlive(this);
                return cursorPosition;
            }
            set
            {
                int y = value.Y;
                Console.SetCursorPosition(value.X, y);
            }
        }

        public override Coordinates WindowPosition
        {
            get
            {
                Coordinates windowPosition = new Coordinates(Console.WindowLeft, Console.WindowTop);
                GC.KeepAlive(this);
                return windowPosition;
            }
            set
            {
                int y = value.Y;
                Console.SetWindowPosition(value.X, y);
            }
        }

        public override int CursorSize
        {
            get
            {
                int cursorSize = Console.CursorSize;
                GC.KeepAlive(this);
                return cursorSize;
            }
            set => Console.CursorSize = value;
        }

        public override Size BufferSize
        {
            get
            {
                Size bufferSize = new Size(Console.BufferWidth, Console.BufferHeight);
                GC.KeepAlive(this);
                return bufferSize;
            }
            set
            {
                int height = value.Height;
                Console.SetBufferSize(value.Width, height);
            }
        }

        public override Size WindowSize
        {
            get
            {
                Size windowSize = new Size(Console.LargestWindowWidth, Console.LargestWindowHeight);
                GC.KeepAlive(this);
                return windowSize;
            }
            set
            {
                int height = value.Height;
                Console.SetWindowSize(value.Width, height);
            }
        }

        public override Size MaxWindowSize
        {
            get
            {
                Size maxWindowSize = new Size(Console.LargestWindowWidth, Console.LargestWindowHeight);
                GC.KeepAlive(this);
                return maxWindowSize;
            }
        }

        public override Size MaxPhysicalWindowSize
        {
            get
            {
                Size physicalWindowSize = new Size(Console.LargestWindowWidth, Console.LargestWindowHeight);
                GC.KeepAlive(this);
                return physicalWindowSize;
            }
        }

        public override bool KeyAvailable
        {
            [return: MarshalAs(UnmanagedType.U1)]
            get => false;
        }

        public override string WindowTitle
        {
            get
            {
                string title = Console.Title;
                GC.KeepAlive(this);
                return title;
            }
            set => Console.Title = value;
        }

        public override KeyInfo ReadKey(ReadKeyOptions options) => throw new NotImplementedException("Interactive key reading is not supported");

        public override void FlushInputBuffer()
        {
        }

        public override void SetBufferContents(Rectangle rectangle, BufferCell fill)
        {
            throw new NotImplementedException("Buffer manipulation is not supported");
        }

        public override void SetBufferContents(Coordinates origin, BufferCell[,] contents)
        {
            throw new NotImplementedException("Buffer manipulation is not supported");
        }

        public override BufferCell[,] GetBufferContents(Rectangle rectangle)
        {
            throw new NotImplementedException("Buffer manipulation is not supported");
        }

        public override void ScrollBufferContents(
          Rectangle source,
          Coordinates destination,
          Rectangle clip,
          BufferCell fill)
        {
            throw new NotImplementedException("Buffer manipulation is not supported");
        }
    }
}