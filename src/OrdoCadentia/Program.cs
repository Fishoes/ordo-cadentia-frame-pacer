// Program.cs — entry point.
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace OrdoCadentia
{
    internal static class Program
    {
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
        [DllImport("shcore.dll")] static extern int SetProcessDpiAwareness(int valor);

        static Mutex _singleInstance;

        [STAThread]
        static void Main()
        {
            // One session at a time: two instances fighting over the same display is a recipe
            // for a stuck video mode.
            // The language has to be in effect before any text appears — including
            // the "already running" message just below.
            Txt.LoadFromDisk(Settings.Folder);

            bool inedito;
            _singleInstance = new Mutex(true, "OrdoCadentia_SessaoUnica_7f3a", out inedito);
            if (!inedito)
            {
                MessageBox.Show(Txt.JaAberto,
                    "Ordo Cadentia", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            MakeDpiAware();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Safety net: whatever happens, the display goes back to normal.
            AppDomain.CurrentDomain.UnhandledException += (s, e) => UndoAll();
            AppDomain.CurrentDomain.ProcessExit += (s, e) => UndoAll();
            Application.ThreadException += (s, e) =>
            {
                UndoAll();
                MessageBox.Show(string.Format(Txt.DeuErrado, e.Exception.Message),
                    "Ordo Cadentia", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            };

            using (var g = Graphics.FromHwnd(IntPtr.Zero))
                Theme.Init(g.DpiX / 96f);

            Application.Run(new MainWindow());

            GC.KeepAlive(_singleInstance);
        }

        static void MakeDpiAware()
        {
            // 2 = per monitor. If shcore is missing (Windows 7/8), fall back to the old mode.
            try { SetProcessDpiAwareness(2); }
            catch { try { SetProcessDPIAware(); } catch { } }
        }

        static void UndoAll()
        {
            try { SysTimer.Restore(); } catch { }
            try { Display.Restore(Display.DeviceForWindow(IntPtr.Zero)); } catch { }
        }
    }
}
