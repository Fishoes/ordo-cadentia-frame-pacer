// DummyTarget.cs — a plain Win32 window used as the target in the end-to-end test.
// Its own process, with no intermediate launcher like Windows 11's Notepad has.
using System;
using System.Drawing;
using System.Windows.Forms;

static class AlvoFalso
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.Run(new Form
        {
            Text = "Ordo Cadentia Test Target",
            ClientSize = new Size(640, 400),
            StartPosition = FormStartPosition.CenterScreen,
            BackColor = Color.FromArgb(18, 20, 18)
        });
    }
}
