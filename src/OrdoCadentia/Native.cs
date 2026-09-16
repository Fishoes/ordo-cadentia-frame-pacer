// Native.cs — the P/Invoke layer. Everything this program asks of Windows goes through here.
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace OrdoCadentia
{
    internal static class Native
    {
        // ---------------------------------------------------------------- windows
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr hWnd, uint flags);
        [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT pt);
        [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT pt);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
        [DllImport("user32.dll")] public static extern int GetWindowTextLengthW(IntPtr hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowTextW(IntPtr hWnd, StringBuilder txt, int max);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int idx);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        static extern int GetWindowLong32(IntPtr hWnd, int idx);

        public const uint GA_ROOT = 2;
        public const int GWL_EXSTYLE = -20;
        public const int GWL_STYLE = -16;
        public const long WS_EX_TOOLWINDOW = 0x00000080;
        public const long WS_CHILD = 0x40000000;

        public static long GetWindowLongSafe(IntPtr hWnd, int idx)
        {
            return IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, idx).ToInt64() : GetWindowLong32(hWnd, idx);
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X, Y; public POINT(int x, int y) { X = x; Y = y; } }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left, Top, Right, Bottom;
            public int Width { get { return Right - Left; } }
            public int Height { get { return Bottom - Top; } }
        }

        // ---------------------------------------------------------------- monitors
        [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint flags);
        [DllImport("user32.dll")] public static extern IntPtr MonitorFromPoint(POINT pt, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool GetMonitorInfoW(IntPtr hMon, ref MONITORINFOEX mi);

        public const uint MONITOR_DEFAULTTONEAREST = 2;
        public const uint MONITOR_DEFAULTTOPRIMARY = 1;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szDevice;
            public static MONITORINFOEX Novo()
            {
                var m = new MONITORINFOEX();
                m.cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));
                m.szDevice = string.Empty;
                return m;
            }
        }

        // ---------------------------------------------------------------- video modes
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool EnumDisplaySettingsExW(string dispositivo, int modo, ref DEVMODE dm, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int ChangeDisplaySettingsExW(string dispositivo, ref DEVMODE dm,
                                                          IntPtr hwnd, uint flags, IntPtr param);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int ChangeDisplaySettingsExW(string dispositivo, IntPtr dmNulo,
                                                          IntPtr hwnd, uint flags, IntPtr param);

        public const int ENUM_CURRENT_SETTINGS = -1;
        public const int ENUM_REGISTRY_SETTINGS = -2;

        public const uint CDS_UPDATEREGISTRY = 0x00000001;
        public const uint CDS_TEST = 0x00000002;
        public const uint CDS_FULLSCREEN = 0x00000004;   // temporario: nao grava no registro
        public const uint CDS_GLOBAL = 0x00000008;
        public const uint CDS_RESET = 0x40000000;

        public const int DISP_CHANGE_SUCCESSFUL = 0;
        public const int DISP_CHANGE_RESTART = 1;
        public const int DISP_CHANGE_FAILED = -1;
        public const int DISP_CHANGE_BADMODE = -2;
        public const int DISP_CHANGE_NOTUPDATED = -3;
        public const int DISP_CHANGE_BADFLAGS = -4;
        public const int DISP_CHANGE_BADPARAM = -5;
        public const int DISP_CHANGE_BADDUALVIEW = -6;

        public const uint DM_PELSWIDTH = 0x00080000;
        public const uint DM_PELSHEIGHT = 0x00100000;
        public const uint DM_DISPLAYFREQUENCY = 0x00400000;
        public const uint DM_BITSPERPEL = 0x00040000;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
            public ushort dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
            public uint dmFields;
            public int dmPositionX, dmPositionY;
            public uint dmDisplayOrientation, dmDisplayFixedOutput;
            public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
            public ushort dmLogPixels;
            public uint dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
            public uint dmICMMethod, dmICMIntent, dmMediaType, dmDitherType;
            public uint dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;

            public static DEVMODE Novo()
            {
                var d = new DEVMODE();
                d.dmDeviceName = string.Empty;
                d.dmFormName = string.Empty;
                d.dmSize = (ushort)Marshal.SizeOf(typeof(DEVMODE));
                return d;
            }
        }

        // ---------------------------------------------------------------- DWM (real refresh rate)
        // DwmGetCompositionTimingInfo fails on some machines (and its struct has 40
        // fields, full of alignment traps). DwmFlush blocks until the next
        // composition: timing N calls gives the real rate, and always works.
        [DllImport("dwmapi.dll")] public static extern int DwmFlush();

        [DllImport("kernel32.dll")] public static extern bool QueryPerformanceFrequency(out long freq);
        [DllImport("kernel32.dll")] public static extern bool QueryPerformanceCounter(out long contador);

        // ---------------------------------------------------------------- timer resolution
        [DllImport("ntdll.dll", SetLastError = true)]
        public static extern int NtSetTimerResolution(uint desejada100ns, bool definir, out uint atual100ns);

        [DllImport("ntdll.dll", SetLastError = true)]
        public static extern int NtQueryTimerResolution(out uint min100ns, out uint max100ns, out uint atual100ns);

        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")] public static extern uint TimeBeginPeriod(uint ms);
        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")] public static extern uint TimeEndPeriod(uint ms);

        // ---------------------------------------------------------------- processes
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(uint acesso, bool herdar, uint pid);
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr h);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool QueryFullProcessImageNameW(IntPtr h, uint flags, StringBuilder nome, ref uint tam);

        public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        public const uint PROCESS_SET_INFORMATION = 0x0200;
        public const uint PROCESS_QUERY_INFORMATION = 0x0400;

        // ---------------------------------------------------------------- CPU topology
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetLogicalProcessorInformationEx(int relacao, IntPtr buffer, ref uint tam);

        public const int RelationProcessorCore = 0;
        public const int RelationAll = 0xffff;

        // ---------------------------------------------------------------- elevation
        [DllImport("shell32.dll")] public static extern bool IsUserAnAdmin();
    }
}
