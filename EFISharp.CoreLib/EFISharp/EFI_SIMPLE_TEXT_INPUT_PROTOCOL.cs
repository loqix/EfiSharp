﻿using System;
using System.Runtime.InteropServices;

namespace EfiSharp
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly unsafe struct EFI_SIMPLE_TEXT_INPUT_PROTOCOL
    {
        private readonly IntPtr _pad;
        private readonly delegate*<EFI_SIMPLE_TEXT_INPUT_PROTOCOL*, EFI_INPUT_KEY*, void> _readKeyStroke;
        public readonly IntPtr _waitForKey;

        public void ReadKeyStroke(EFI_INPUT_KEY* key)
        {
            fixed (EFI_SIMPLE_TEXT_INPUT_PROTOCOL* _this = &this)
            {
                _readKeyStroke(_this, key);
            }
        }
    }
}
