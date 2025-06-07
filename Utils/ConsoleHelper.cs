using System;
using System.Runtime.InteropServices;

namespace Moyu.Utils
{
    public static class ConsoleHelper
    {
        private const int STD_OUTPUT_HANDLE = -11;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetConsoleScreenBufferInfo(IntPtr hConsoleOutput, out CONSOLE_SCREEN_BUFFER_INFO lpConsoleScreenBufferInfo);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FillConsoleOutputCharacter(IntPtr hConsoleOutput, char character, int length, COORD writeCoord, out int numberOfCharsWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FillConsoleOutputAttribute(IntPtr hConsoleOutput, short attribute, int length, COORD writeCoord, out int numberOfAttrsWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCursorPosition(IntPtr hConsoleOutput, COORD cursorPosition);

        [StructLayout(LayoutKind.Sequential)]
        private struct COORD
        {
            public short X;
            public short Y;

            public COORD(short x, short y)
            {
                X = x;
                Y = y;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CONSOLE_SCREEN_BUFFER_INFO
        {
            public COORD dwSize;
            public COORD dwCursorPosition;
            public short wAttributes;
            public COORD srWindowTopLeft;
            public COORD srWindowBottomRight;
            public COORD dwMaximumWindowSize;
        }

        /// <summary>
        /// 彻底清空控制台缓冲区，包括历史滚动区域
        /// </summary>
        public static void ClearAll()
        {
            IntPtr consoleHandle = GetStdHandle(STD_OUTPUT_HANDLE);

            if (!GetConsoleScreenBufferInfo(consoleHandle, out CONSOLE_SCREEN_BUFFER_INFO csbi))
                return;

            int bufferSize = csbi.dwSize.X * csbi.dwSize.Y;
            COORD topLeft = new COORD(0, 0);

            FillConsoleOutputCharacter(consoleHandle, ' ', bufferSize, topLeft, out _);
            FillConsoleOutputAttribute(consoleHandle, csbi.wAttributes, bufferSize, topLeft, out _);
            SetConsoleCursorPosition(consoleHandle, topLeft);
        }
    }
}
