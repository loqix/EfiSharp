using System.Runtime.CompilerServices;
using EFISharp;

//TODO Use builtin c#9 nuint
#if TARGET_64BIT
using nuint = System.UInt64;
#else
using nuint = System.UInt32;
#endif

namespace System
{
    //TODO Add beep, https://github.com/fpmurphy/UEFI-Utilities-2019/blob/master/MyApps/Beep/Beep.c
    public static unsafe class Console
    {
        //Queue
        //TODO Move to separate class, this requires fixing new
        private static char* inputBuffer;
        private static int front;
        private static int rear = -1;
        private static int max = 4096;

        //These colours are used by efi at boot up without prompting the user and so are used here just to match
        private const ConsoleColor DefaultBackgroundColour = ConsoleColor.Black;
        private const ConsoleColor DefaultForegroundColour = ConsoleColor.Gray;

        public static bool KeyAvailable => front != rear + 1 && rear != max - 1;

        public static ConsoleKeyInfo ReadKey()
        {
            return ReadKey(false);
        }

        public static ConsoleKeyInfo ReadKey(bool intercept)
        {
            EFI_KEY_DATA input;
            uint ignore;

            UefiApplication.SystemTable->BootServices->WaitForEvent(1,
                &global::Console.In->_waitForKeyEx, &ignore);
            global::Console.In->ReadKeyStrokeEx(global::Console.In, &input);

            if (!intercept)
            {
                Write(input.Key.UnicodeChar);
            }

            ConsoleKey key;
            bool shift = (input.KeyState.KeyShiftState & KeyShiftState.EFI_LEFT_SHIFT_PRESSED) != 0 ||
                         (input.KeyState.KeyShiftState & KeyShiftState.EFI_RIGHT_SHIFT_PRESSED) != 0;
            bool alt = (input.KeyState.KeyShiftState & KeyShiftState.EFI_LEFT_ALT_PRESSED) != 0 ||
                       (input.KeyState.KeyShiftState & KeyShiftState.EFI_RIGHT_ALT_PRESSED) != 0;
            bool control = (input.KeyState.KeyShiftState & KeyShiftState.EFI_LEFT_CONTROL_PRESSED) != 0 ||
                           (input.KeyState.KeyShiftState & KeyShiftState.EFI_RIGHT_CONTROL_PRESSED) != 0;

            //TODO Replace with Array of ConsoleKeys where the index corresponds with the unicode char
            //and the value is the corresponding ConsoleKey. The conversion is too complex for the switch
            //statement given here. Also need to figure out how to deal with different layouts, is it even 
            //possible if keys outside Basic Latin are used. Are the supplements supported?
            switch (input.Key.UnicodeChar)
            {
                case (char)ConsoleKey.Backspace:
                case (char)ConsoleKey.Tab:
                case (char)ConsoleKey.Enter:
                case (char)ConsoleKey.Spacebar:
                //Numbers
                case >= (char)ConsoleKey.D0 and <= (char)ConsoleKey.D9:
                    key = (ConsoleKey)input.Key.UnicodeChar;
                    break;
                //Upper Case
                case >= (char)ConsoleKey.A and <= (char)ConsoleKey.Z:
                    shift = true;
                    key = (ConsoleKey)input.Key.UnicodeChar;
                    break;
                //Lower Case
                case >= (char)(ConsoleKey.A + 0x20) and <= (char)(ConsoleKey.Z + 0x20):
                    key = (ConsoleKey)input.Key.UnicodeChar - 0x20;
                    break;
                //Symbols
                case '!' or '#' or '$' or '%':
                    shift = true;
                    key = (ConsoleKey)(input.Key.UnicodeChar + 0x10);
                    break;
                case '&' or '(':
                    shift = true;
                    key = (ConsoleKey)(input.Key.UnicodeChar + 0x11);
                    break;
                case '@':
                    shift = true;
                    key = ConsoleKey.D2;
                    break;
                case '^':
                    shift = true;
                    key = ConsoleKey.D6;
                    break;
                case '*':
                    shift = true;
                    key = ConsoleKey.D8;
                    break;
                case ')':
                    shift = true;
                    key = ConsoleKey.D0;
                    break;
                case ';':
                    key = ConsoleKey.Oem1;
                    break;
                case ':':
                    shift = true;
                    key = ConsoleKey.Oem1;
                    break;
                case '=':
                    key = ConsoleKey.OemPlus;
                    break;
                case '+':
                    shift = true;
                    key = ConsoleKey.OemPlus;
                    break;
                //Comma(,), Hyphen(-), Full Stop(.) and Forward Slash(/)
                case >= (char)(ConsoleKey.OemComma - 0x90) and <= (char)(ConsoleKey.Oem2 - 0x90):
                    key = (ConsoleKey)(input.Key.UnicodeChar + 0x90);
                    break;
                case '<' or '>' or '?':
                    shift = true;
                    key = (ConsoleKey)(input.Key.UnicodeChar + 0x80);
                    break;
                case '_':
                    shift = true;
                    key = ConsoleKey.OemMinus;
                    break;
                case '`':
                    key = ConsoleKey.Oem3;
                    break;
                case '~':
                    shift = true;
                    key = ConsoleKey.Oem3;
                    break;
                //Left([) and Right(]) Square Bracket and Back Slash(\)
                case >= (char)(ConsoleKey.Oem4 - 0x80) and <= (char)(ConsoleKey.Oem6 - 0x80):
                    key = (ConsoleKey) (input.Key.UnicodeChar + 0x80);
                    break;
                //Left({) and Right(}) Curly Bracket and Vertical Line(|)
                case >= (char)(ConsoleKey.Oem4 - 0x60) and <= (char)(ConsoleKey.Oem6 - 0x60):
                    shift = true;
                    key = (ConsoleKey)(input.Key.UnicodeChar + 0x60);
                    break;
                //Quote(')
                case '\'':
                    key = ConsoleKey.Oem7;
                    break;
                case '"':
                    shift = true;
                    key = ConsoleKey.Oem7;
                    break;
                default:
                    key = 0;
                    break;
            }

            return new ConsoleKeyInfo(input.Key.UnicodeChar, key, shift, alt, control);
        }

        //TODO Check if this is possible on efi
        /*public static int CursorSize
        {
            [UnsupportedOSPlatform("browser")]
            get { return ConsolePal.CursorSize; }
            [SupportedOSPlatform("windows")]
            set { ConsolePal.CursorSize = value; }
        }*/

        //[SupportedOSPlatform("windows")]
        public static bool NumberLock
        {
            get
            {
                EFI_KEY_DATA key = new EFI_KEY_DATA();
                return global::Console.In->ReadKeyStrokeEx(global::Console.In, &key) ==
                       EFI_STATUS.EFI_SUCCESS &&
                       (key.KeyState.KeyToggleState & EFI_KEY_TOGGLE_STATE.EFI_NUM_LOCK_ACTIVE) != 0;
            }
        }

        //[SupportedOSPlatform("windows")]
        public static bool CapsLock
        {
            get
            {
                EFI_KEY_DATA key = new EFI_KEY_DATA();
                return global::Console.In->ReadKeyStrokeEx(global::Console.In, &key) ==
                       EFI_STATUS.EFI_SUCCESS &&
                       (key.KeyState.KeyToggleState & EFI_KEY_TOGGLE_STATE.EFI_CAPS_LOCK_ACTIVE) != 0;
            }
        }

        //[UnsupportedOSPlatform("browser")]
        public static ConsoleColor BackgroundColor
        {
            get => (ConsoleColor)((byte)global::Console.Out->Mode->Attribute >> 4);
            set
            {
                //Only lower nibble colours are supported by efi
                if ((uint)value >= 8) return;
                global::Console.Out->SetAttribute(global::Console.Out, ((nuint)value << 4) + (uint)ForegroundColor);
            }
        }

        //[UnsupportedOSPlatform("browser")]
        public static ConsoleColor ForegroundColor
        {
            get => (ConsoleColor)(global::Console.Out->Mode->Attribute & 0b1111);
            set => global::Console.Out->SetAttribute(global::Console.Out, ((nuint)BackgroundColor << 4) + (uint)value);
        }

        public static int BufferWidth
        {
            //[UnsupportedOSPlatform("browser")]
            get
            {
                nuint width, height;
                global::Console.Out->QueryMode(global::Console.Out, (nuint)global::Console.Out->Mode->Mode, &width, &height);
                return (int)width;
            }
            //[SupportedOSPlatform("windows")]
            //set { }
        }

        public static int BufferHeight
        {
            //[UnsupportedOSPlatform("browser")]
            get
            {
                nuint width, height;
                global::Console.Out->QueryMode(global::Console.Out, (nuint)global::Console.Out->Mode->Mode, &width, &height);
                return (int)height;
            }
            //[SupportedOSPlatform("windows")]
            //set { }
        }

        //[UnsupportedOSPlatform("browser")]
        public static void ResetColor()
        {
            global::Console.Out->SetAttribute(global::Console.Out, ((nuint)DefaultBackgroundColour << 4) + (nuint)DefaultForegroundColour);
        }

        public static bool CursorVisible
        {
            //[SupportedOSPlatform("windows")]
            get => global::Console.Out->Mode->CursorVisible;
            //[UnsupportedOSPlatform("browser")]
            set => global::Console.Out->EnableCursor(global::Console.Out, value);
        }

        //TODO Enforce maximum, EFI_SIMPLE_TEXT_OUTPUT_PROTOCOL.QueryMode(...)
        //[UnsupportedOSPlatform("browser")]
        public static int CursorLeft
        {
            //TODO Fix get cursor column
            get => global::Console.Out->Mode->CursorColumn;
            set
            {
                if (value >= 0)
                {
                    global::Console.Out->SetCursorPosition(global::Console.Out, (nuint)value, (nuint)CursorTop);
                }
            }
        }

        //TODO Enforce maximum, EFI_SIMPLE_TEXT_OUTPUT_PROTOCOL.QueryMode(...)
        //[UnsupportedOSPlatform("browser")]
        public static int CursorTop
        {
            get => global::Console.Out->Mode->CursorRow;
            set
            {
                if (value >= 0)
                {
                    global::Console.Out->SetCursorPosition(global::Console.Out, (nuint)CursorLeft, (nuint)value);
                }
            }
        }

        //TODO Add ValueTuple?
        /// <summary>Gets the position of the cursor.</summary>
        /// <returns>The column and row position of the cursor.</returns>
        /// <remarks>
        /// Columns are numbered from left to right starting at 0. Rows are numbered from top to bottom starting at 0.
        /// </remarks>
        //[UnsupportedOSPlatform("browser")]
        /*public static (int Left, int Top) GetCursorPosition()
        {
            return (CursorLeft, CursorTop);
        }*/

        public static void Clear()
        {
            global::Console.Out->ClearScreen(global::Console.Out);
        }

        //[UnsupportedOSPlatform("browser")]
        //TODO Enforce maximum, EFI_SIMPLE_TEXT_OUTPUT_PROTOCOL.QueryMode(...)
        public static void SetCursorPosition(int left, int top)
        {
            if (left >= 0 && top >= 0)
            {
                global::Console.Out->SetCursorPosition(global::Console.Out, (nuint)left, (nuint)top);
            }
        }

        //
        // Give a hint to the code generator to not inline the common console methods. The console methods are
        // not performance critical. It is unnecessary code bloat to have them inlined.
        //
        // Moreover, simple repros for codegen bugs are often console-based. It is tedious to manually filter out
        // the inlined console writelines from them.
        //
        [MethodImpl(MethodImplOptions.NoInlining)]
        //[UnsupportedOSPlatform("browser")]
        //TODO Check if EFI_SIMPLE_TEXT_INPUT_EX_PROTOCOL.RegisterKeyNotify() can be used instead of the queue
        //TODO handle control chars, enter, backspace, ...
        public static int Read()
        {
            if (inputBuffer == null)
            {
                char* newBuffer = stackalloc char[max];
                inputBuffer = newBuffer;
            }

            if (!KeyAvailable)
            {
                EFI_KEY_DATA input;
                uint ignore;

                do
                {
                    UefiApplication.SystemTable->BootServices->WaitForEvent(1,
                        &global::Console.In->_waitForKeyEx, &ignore);
                    global::Console.In->ReadKeyStrokeEx(global::Console.In, &input);

                    if (input.Key.UnicodeChar != (char)ConsoleKey.Enter)
                    {
                        Write(input.Key.UnicodeChar);
                        //Backspace needs to get this far since we need Write(backspace) to visually remove a key from the screen
                        if (input.Key.UnicodeChar == (char)ConsoleKey.Backspace && rear != front)
                        {
                            //TODO Rewrite to follow queue design or use a different array structure
                            rear--;
                        }
                        else if (rear != max - 1)
                        {
                            inputBuffer[++rear] = input.Key.UnicodeChar;
                        }

                    }
                } while (input.Key.UnicodeChar != (char)ConsoleKey.Enter);

                WriteLine();
            }

            return KeyAvailable ? inputBuffer[front++] : '\0';
        }

        /*[MethodImpl(MethodImplOptions.NoInlining)]
        //[UnsupportedOSPlatform("browser")]
        public static string ReadLine() { }*/

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void WriteLine()
        {
            //TODO Make line terminator changeable
            char* pValue = stackalloc char[3];
            pValue[0] = '\r';
            pValue[1] = '\n';
            pValue[2] = '\0';

            global::Console.Out->OutputString(global::Console.Out, pValue);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void WriteLine(bool value)
        {
            Write(value);
            WriteLine();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void WriteLine(char value)
        {
            Write(value);
            WriteLine();
        }

        //TODO Add char[]
        /*[MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static void WriteLine(char[]? buffer) { }

        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static void WriteLine(char[] buffer, int index, int count) { }*/

        //TODO Add single and double Write
        /*[MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static void WriteLine(decimal value) { }

        //TODO Add decimal type
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static void WriteLine(double value) { }

        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static void WriteLine(float value) { }*/

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void WriteLine(int value)
        {
            Write(value);
            WriteLine();
        }

        //TODO Add CLSCompliantAttribute?
        //[CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WriteLine(uint value)
        {
            Write(value);
            WriteLine();
        }


        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void WriteLine(long value)
        {
            Write(value);
            WriteLine();
        }

        //TODO Add CLSCompliantAttribute?
        //[CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void WriteLine(ulong value)
        {
            Write(value);
            WriteLine();
        }

        //TODO Add .ToString(), Nullable?
        /*[MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static void WriteLine(object? value) { } */

        [MethodImpl(MethodImplOptions.NoInlining)]
        //TODO Add Nullable?
        public static void WriteLine(string value)
        {
            Write(value);
            WriteLine();
        }

        //TODO Add format string
        /*[MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static void WriteLine(string format, object? arg0) { }

        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static void WriteLine(string format, object? arg0, object? arg1) { }

        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static void WriteLine(string format, object? arg0, object? arg1, object? arg2) { }

        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static void WriteLine(string format, params object?[]? arg)
        {
            if (arg == null)                       // avoid ArgumentNullException from String.Format
                - // faster than Out.WriteLine(format, (Object)arg);
            else
                -
        }

        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static void Write(string format, object? arg0) { }

        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static void Write(string format, object? arg0, object? arg1) { }

        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static void Write(string format, object? arg0, object? arg1, object? arg2) { }

        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static void Write(string format, params object?[]? arg)
        {
            if (arg == null)                   // avoid ArgumentNullException from String.Format
                - // faster than Out.Write(format, (Object)arg);
            else
                -
        }*/

        //TODO make char arrays constant, use string instead?
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Write(bool value)
        {
            if (value)
            {
                char* pValue = stackalloc char[5];
                pValue[0] = 'T';
                pValue[1] = 'r';
                pValue[2] = 'u';
                pValue[3] = 'e';
                pValue[4] = '\0';

                global::Console.Out->OutputString(global::Console.Out, pValue);
            }
            else
            {
                char* pValue = stackalloc char[6];
                pValue[0] = 'F';
                pValue[1] = 'a';
                pValue[2] = 'l';
                pValue[3] = 's';
                pValue[4] = 'e';
                pValue[5] = '\0';

                global::Console.Out->OutputString(global::Console.Out, pValue);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Write(char value)
        {
            char* pValue = stackalloc char[2];
            pValue[0] = value;
            pValue[1] = '\0';

            global::Console.Out->OutputString(global::Console.Out, pValue);
        }

        //TODO Fix char[]
        /*[MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static void Write(char[]? buffer)
        {
        }

        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static void Write(char[] buffer, int index, int count)
        {
        }*/

        //TODO Add single and double Write
        /*[MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static void Write(double value) { }

        //TODO Add decimal Type
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static void Write(decimal value) { }

        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static void Write(float value) { }*/

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Write(int value)
        {
            if (value < 0)
            {
                Write('-');
                //TODO Add Math.Abs?
                value = -value;
            }

            Write((uint)value);
        }

        //TODO Add CLSCompliantAttribute?
        //[CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.NoInlining)]
        //TODO Rewrite to make a single pointer array for char of max uint length and use a single loop
        //TODO Add single integer to string function with variable int size
        public static void Write(uint value)
        {
            //TODO Figure out why using array here makes the vm crash on startup
            byte* digits = stackalloc byte[10];
            byte digitCount = 0;
            byte digitPosition = 9; //This is designed to wrap around for numbers with 10 digits

            //From https://stackoverflow.com/a/4808815
            do
            {
                digits[digitPosition] = (byte)(value % 10);
                value = value / 10;
                digitCount++;
                digitPosition--;
            } while (value > 0);

            byte charCount = (byte)(digitCount + 1);

            char* pValue = stackalloc char[charCount];
            pValue[charCount - 1] = '\0';

            digitPosition++;
            for (int i = 0; i < digitCount; i++, digitPosition++)
            {
                pValue[i] = (char)(digits[digitPosition] + '0');
            }

            global::Console.Out->OutputString(global::Console.Out, pValue);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Write(long value)
        {
            if (value < 0)
            {
                Write('-');
                //TODO Add Math.Abs?
                value = -value;
            }

            Write((ulong)value);
        }

        //TODO Add CLSCompliantAttribute? 
        //[CLSCompliant(false)]
        //TODO Rewrite to make a single pointer array for char of max ulong length and use a single loop
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Write(ulong value)
        {
            //TODO Figure out why using array here makes the vm crash on startup
            byte* digits = stackalloc byte[19];
            byte digitCount = 0;
            byte digitPosition = 18; //This is designed to wrap around for numbers with 19 digits

            //From https://stackoverflow.com/a/4808815
            do
            {
                digits[digitPosition] = (byte)(value % 10);
                value = value / 10;
                digitCount++;
                digitPosition--;
            } while (value > 0);


            byte charCount = (byte)(digitCount + 1);

            char* pValue = stackalloc char[charCount];
            pValue[charCount - 1] = '\0';

            digitPosition++;
            for (int i = 0; i < digitCount; i++, digitPosition++)
            {
                pValue[i] = (char)(digits[digitPosition] + '0');
            }

            global::Console.Out->OutputString(global::Console.Out, pValue);
        }

        //TODO Add .ToString(), Nullable?
        /*[MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static void Write(object? value) { }*/

        [MethodImpl(MethodImplOptions.NoInlining)]
        //TODO Add Nullable?
        public static void Write(string value)
        {
            fixed (char* pValue = value)
            {
                global::Console.Out->OutputString(global::Console.Out, pValue);
            }
        }
    }
}