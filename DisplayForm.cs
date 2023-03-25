using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Management;

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
        private const int DEFAULT_COLUMN = 3;

        SettingForm settingForm;
        private GlassyPanel panel;
        private readonly List<Process> processList = new List<Process>();
        private List<IntPtr> deviceDisps;

        private ManagementEventWatcher processStartEvent = new ManagementEventWatcher("SELECT * FROM Win32_ProcessStartTrace");
        private ManagementEventWatcher processStopEvent = new ManagementEventWatcher("SELECT * FROM Win32_ProcessStopTrace");

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
            panel = new GlassyPanel
            {
                Width = this.Width,
                Height = this.Height,
                Dock = DockStyle.Fill
            };
            this.Controls.Add(panel);
            panel.Hide();
            panel.SendToBack();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.OpenDeviceScreen();
            deviceDisps = GetAllDeviceDisps();
            DisplayDevices(DEFAULT_COLUMN);
        }

        public void DisplayDevices (int column)
        {
            int numDevice = deviceDisps.Count;
            int columnDisp = column;
            if (columnDisp < DEFAULT_COLUMN || numDevice < DEFAULT_COLUMN)
            {
                columnDisp = DEFAULT_COLUMN;
            } else if (numDevice < column)
            {
                columnDisp = numDevice;
            }
            int rowDisp = numDevice / columnDisp;
            if (numDevice % columnDisp != 0)
            {
                rowDisp += 1;
            }
            int paddingFromRight = 0;
            if (1 < columnDisp)
            {
                paddingFromRight = columnDisp * (PADDING_FROM_RIGHT + columnDisp);
            }
            if (numDevice != 0)
            {
                this.Size = new Size(columnDisp * DEVICE_WIDTH_FORM - paddingFromRight, rowDisp * DEVICE_HEIGHT_FORM + DEVICE_MARGIN_TOP);
            }
            int indexCol = 0;
            int indexRow = 0;
            foreach (IntPtr deviceDisp in deviceDisps)
            {
                SetParent(deviceDisp, this.Handle);
                if (columnDisp == indexCol)
                {
                    indexCol = 0;
                    indexRow += 1;
                }
                MoveWindow(deviceDisp, indexCol * DEVICE_WIDTH, indexRow * DEVICE_HEIGHT + DEVICE_MARGIN_TOP, DEVICE_WIDTH, DEVICE_HEIGHT, true);
                indexCol++;
            }
            int numNullDevice = numDevice - deviceDisps.Count;
            for (int i = 0; i < numNullDevice; i++)
            {
                IntPtr emptyDevice = IntPtr.Zero;
                SetParent(emptyDevice, this.Handle);
                MoveWindow(emptyDevice, i * DEVICE_WIDTH, DEVICE_MARGIN_TOP, DEVICE_WIDTH, DEVICE_HEIGHT, true);
            }
        }

        private string RunCmd (String command, bool isGetOutput)
        {
            Process cmd = new Process();
            cmd.StartInfo.FileName = "cmd.exe";
            cmd.StartInfo.RedirectStandardInput = true;
            cmd.StartInfo.RedirectStandardOutput = true;
            cmd.StartInfo.CreateNoWindow = true;
            cmd.StartInfo.UseShellExecute = false;
            cmd.Start();
            cmd.StandardInput.WriteLine(command);
            cmd.StandardInput.Flush();
            cmd.StandardInput.Close();
            string output = "";
            if (isGetOutput)
            {
                output = cmd.StandardOutput.ReadToEnd();
            }
            return output;
        }

        private void OpenDeviceScreen ()
        {
            string ouputRunCmdGetDeviceUids = this.RunCmd("adb devices", true);
            string[] outputStrArr = Regex.Split(ouputRunCmdGetDeviceUids, "\r\n");
            List<string> deviceUids = new List<string>();
            bool startGetUId = false;
            foreach (string output in outputStrArr)
            {
                if (startGetUId)
                {
                    string uid = output.Replace("\tdevice", "");
                    if (uid == "")
                    {
                        break;
                    }
                    deviceUids.Add(uid);
                    this.RunCmd("scrcpy -s" + uid, false);
                    Thread.Sleep(1500);
                    continue;
                }
                if (output == "List of devices attached")
                {
                    startGetUId = true;
                }
                
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
                    if (process.ProcessName == "scrcpy")
                    {
                        IntPtr windowHandle = process.MainWindowHandle;
                        result.Add(windowHandle);
                        processList.Add(process);
                        NumDevice++;
                    }
                }
            }
            return result;
        }

        private void SettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (settingForm == null || settingForm.IsDisposed)
            {
                settingForm = new SettingForm(this, this.panel);
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

        private void RefreshToolStripMenuItem_Click(object sender, EventArgs e)
        {
            deviceDisps = GetAllDeviceDisps();
            this.DisplayDevices(DEFAULT_COLUMN);
        }

        private void SettingForm_FormClosing(object sender, FormClosedEventArgs e)
        {
            foreach (Process process in processList)
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    process.Dispose();
                }
            }
        }
    }
}
