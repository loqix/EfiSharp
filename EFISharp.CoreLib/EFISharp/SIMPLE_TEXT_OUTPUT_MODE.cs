﻿using System.Runtime.InteropServices;

namespace EFISharp
{
    [StructLayout(LayoutKind.Sequential)]
    public struct SIMPLE_TEXT_OUTPUT_MODE
    {
        public readonly int MaxMode;
        public readonly int Mode;
        public readonly int Attribute;
        public readonly int CursorColumn;
        public readonly int CursorRow;
        public readonly bool CursorVisible;

    }
}
