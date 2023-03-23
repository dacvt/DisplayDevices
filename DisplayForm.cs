using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Diagnostics;
using System.Windows;

namespace DisplayDevices
{
    public partial class DisplayForm : Form
    {
        private const int DEVICE_WIDTH = 331;
        private const int DEVICE_HEIGHT = 600;
        private const int DEVICE_MARGIN_TOP = 25;
        private const int DEVICE_WIDTH_FORM = 345;
        private const int DEVICE_HEIGHT_FORM = 637;
        private const int PADDING_FROM_RIGHT = 5; // padding increase if has more device

        SettingForm settingForm;
        private GlassyPanel panel;

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("user32.dll")]
        static extern int GetWindowText(IntPtr hwnd, StringBuilder ss, int count);

        [DllImport("User32.dll")]
        static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("User32.dll")]
        static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);

        [DllImport("gdi32.dll")]
        static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        [DllImport("user32.dll")]
        static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
        public DisplayForm()
        {
            InitializeComponent();
            panel = new GlassyPanel();
            panel.Width = this.Width;
            panel.Height = this.Height;
            panel.Dock = DockStyle.Fill;
            this.Controls.Add(panel);
            panel.Hide();
            panel.SendToBack();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            List<IntPtr> deviceDisps = GetAllDeviceDisps();
            int numDevice = deviceDisps.Count;
            int paddingFromRight = 0;
            if (1 < numDevice)
            {
                paddingFromRight = numDevice * (PADDING_FROM_RIGHT + numDevice);
            }
            if (numDevice != 0)
            {
                this.Size = new Size(numDevice * DEVICE_WIDTH_FORM - paddingFromRight, 1 * DEVICE_HEIGHT_FORM + DEVICE_MARGIN_TOP);
            }
            int index = 0;
            foreach (IntPtr deviceDisp in deviceDisps)
            {
                SetParent(deviceDisp, this.Handle);
                MoveWindow(deviceDisp, index * DEVICE_WIDTH, DEVICE_MARGIN_TOP, DEVICE_WIDTH, DEVICE_HEIGHT, true);
                index++;
            }
        }

        private List<IntPtr> GetAllDeviceDisps ()
        {
            Process[] processlist = Process.GetProcesses();
            IntPtr primary = GetDC(IntPtr.Zero);
            ReleaseDC(IntPtr.Zero, primary);
            int NumDevice = 0;
            List<IntPtr> result = new List<IntPtr>();
            foreach (Process process in processlist)
            {
                if (!String.IsNullOrEmpty(process.MainWindowTitle))
                {
                    Console.WriteLine("Process: {0} ID: {1} Window title: {2}", process.ProcessName, process.Id, process.MainWindowTitle);
                    if (process.ProcessName == "scrcpy")
                    {
                        IntPtr windowHandle = process.MainWindowHandle;
                        result.Add(windowHandle);
                        NumDevice++;
                    }
                }
            }
            return result;
        }

        private void settingBtn_Click(object sender, EventArgs e)
        {
            Point location = this.PointToScreen(new Point(this.Left, this.Bottom));
            if (settingForm == null || settingForm.IsDisposed)
            {
                settingForm = new SettingForm(location, this.panel);
            }
            panel.Show();
            panel.BringToFront();
            panel.MouseClick += delegate (object pSender, MouseEventArgs pe)
            {
                if (settingForm != null && !settingForm.IsDisposed)
                {
                    settingForm.Close();
                }
            };
            settingForm.Show();
        }
    }
}
