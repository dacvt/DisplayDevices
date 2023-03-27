using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

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

        private  int numDeviceColumn = DEFAULT_COLUMN;
        public int NumDeviceColumn
        {
            get { return numDeviceColumn; }
            set { this.numDeviceColumn = value; }
        }

        SettingForm settingForm;
        private GlassyPanel panel;
        private List<Device> devices = new List<Device>();

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

        [DllImport("User32")]
        private static extern int ShowWindow(int hwnd, int nCmdShow);
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
            this.DisplayScreen();
            this.InitDevices();
        }

        public void DisplayDevices()
        {
            int numDevice = this.devices.Count;
            int columnDisp = this.numDeviceColumn;
            if (columnDisp < DEFAULT_COLUMN || numDevice < DEFAULT_COLUMN)
            {
                columnDisp = DEFAULT_COLUMN;
            } else if (numDevice < this.numDeviceColumn)
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
            foreach (Device device in this.devices)
            {
                SetParent(device.IntPtr, this.Handle);
                if (columnDisp == indexCol)
                {
                    indexCol = 0;
                    indexRow += 1;
                }
                MoveWindow(device.IntPtr, indexCol * DEVICE_WIDTH, indexRow * DEVICE_HEIGHT + DEVICE_MARGIN_TOP, DEVICE_WIDTH, DEVICE_HEIGHT, true);
                ShowWindow(device.IntPtr.ToInt32(), 5);
                indexCol++;
            }
            int numNullDevice = numDevice - devices.Count;
            for (int i = 0; i < numNullDevice; i++)
            {
                IntPtr emptyDevice = IntPtr.Zero;
                SetParent(emptyDevice, this.Handle);
                MoveWindow(emptyDevice, i * DEVICE_WIDTH, DEVICE_MARGIN_TOP, DEVICE_WIDTH, DEVICE_HEIGHT, true);
            }
        }

        private void DisplayScreen()
        {
            List<string> deviceSerials = this.GetDeviceSerials();
            int numDevice = deviceSerials.Count;
            int columnDisp = this.numDeviceColumn;
            if (columnDisp < DEFAULT_COLUMN || numDevice < DEFAULT_COLUMN)
            {
                columnDisp = DEFAULT_COLUMN;
            }
            else if (numDevice < this.numDeviceColumn)
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
            for (int i = 0; i < numDevice; i++)
            {
                IntPtr emptyIntPtr = IntPtr.Zero;
                SetParent(emptyIntPtr, this.Handle);
                if (columnDisp == indexCol)
                {
                    indexCol = 0;
                    indexRow += 1;
                }
                MoveWindow(emptyIntPtr, indexCol * DEVICE_WIDTH, indexRow * DEVICE_HEIGHT + DEVICE_MARGIN_TOP, DEVICE_WIDTH, DEVICE_HEIGHT, true);
                indexCol++;
            }
            int numNullDevice = numDevice - devices.Count;
            for (int i = 0; i < numNullDevice; i++)
            {
                IntPtr emptyDevice = IntPtr.Zero;
                SetParent(emptyDevice, this.Handle);
                MoveWindow(emptyDevice, i * DEVICE_WIDTH, DEVICE_MARGIN_TOP, DEVICE_WIDTH, DEVICE_HEIGHT, true);
            }
        }

        private string RunCmd(String command, bool isGetOutput)
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

        private List<string> GetDeviceSerials()
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
                    continue;
                }
                if (output == "List of devices attached")
                {
                    startGetUId = true;
                }
            }
            return deviceUids;
        }

        public void AddDeviceScreen(int col, int row, IntPtr deviceDisp)
        {
            SetParent(deviceDisp, this.Handle);
            MoveWindow(deviceDisp, col * DEVICE_WIDTH, row * DEVICE_HEIGHT + DEVICE_MARGIN_TOP, DEVICE_WIDTH, DEVICE_HEIGHT, true);
        }

        private void ApplyNewDevice(string serial, int col, int row)
        {
            Process process = new Process();
            ProcessStartInfo processStartInfo = new ProcessStartInfo("scrcpy.exe");
            string directoryName = Path.GetDirectoryName("scrcpy.exe");
            processStartInfo.WorkingDirectory = directoryName;
            processStartInfo.Arguments = "-s " + serial + " --window-title " + serial;
            processStartInfo.CreateNoWindow = true;
            processStartInfo.UseShellExecute = false;
            processStartInfo.RedirectStandardError = false;
            processStartInfo.RedirectStandardOutput = true;
            process.StartInfo = processStartInfo;
            process.Start();
            Thread subThread = new Thread(() =>
            {
                while (true)
                {
                    List<Process> processlist = new List<Process>(Process.GetProcessesByName("scrcpy"));
                    if (processlist.Find(p => p.MainWindowTitle == serial) != null)
                    {
                        break;
                    }
                    Thread.Sleep(10);
                }
                this.AddDeviceScreen(col, row, process.MainWindowHandle);
                Device device = new Device();
                IntPtr windowHandle = process.MainWindowHandle;
                device.Serial = serial;
                device.IntPtr = windowHandle;
                device.Process = process;
                this.devices.Add(device);
            });
            subThread.Start();
        }

        private void ReApplyOldDevice(string serial)
        {
            this.RunCmd("scrcpy -s" + serial, false);
            int numTry = 3;
            while (0 < numTry)
            {
                numTry--;
                Thread.Sleep(1000);
                Process[] processes = Process.GetProcessesByName("scrcpy");
                int countDeviceNotExited = this.devices.FindAll(d => !d.Process.HasExited).Count;
                if (processes.Length - countDeviceNotExited == 1)
                {
                    Device deviceExited = devices.Find(d => d.Process.HasExited);
                    foreach (Process process in processes)
                    {
                        Device deviceExist = devices.Find(d => !d.Process.HasExited && d.Process.Id != process.Id);
                        if (deviceExist == null)
                        {
                            deviceExited.Process = process;
                            IntPtr windowHandle = process.MainWindowHandle;
                            deviceExited.IntPtr = windowHandle;
                            break;
                        }
                    }
                    break;
                }
            }
            this.DisplayDevices();
        }

        // Get devices serial from adb devices command and display
        private void InitDevices()
        {
            List<string> deviceSerials = this.GetDeviceSerials();
            int length = deviceSerials.Count;
            int col = 0;
            int row = 0;
            for (int i = 0; i < length; i++)
            {
                this.ApplyNewDevice(deviceSerials[i], col, row);
                col++;
                if (col == NumDeviceColumn)
                {
                    col = 0;
                    row++;
                }
            }
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
            this.ReloadScreen();
        }

        // Get all device process and display
        private void RefreshScreen(string serial)
        {
            DisplayDevices();
        }

        private void ReloadScreen()
        {
            List<string> deviceSerials = this.GetDeviceSerials();
            foreach(string serial in deviceSerials)
            {
                Device existDevice = devices.Find(d => d.Serial == serial);
                if (existDevice != null && !existDevice.Process.HasExited)
                {
                    continue;
                }
                if (existDevice == null)
                {
                    int col = this.devices.Count % NumDeviceColumn;
                    int row = this.devices.Count / NumDeviceColumn;
                    this.ApplyNewDevice(serial, col, row);
                    continue;
                }
                if (existDevice.Process.HasExited)
                {
                    this.ReApplyOldDevice(existDevice.Serial);
                    continue;
                }
            }
            
        }

        private void SettingForm_FormClosing(object sender, FormClosedEventArgs e)
        {
            foreach (Device device in this.devices)
            {
                Process process = device.Process;
                if (!process.HasExited)
                {
                    process.Kill();
                    process.Dispose();
                }
            }
        }

        private class Device
        {
            private string serial;
            public string Serial
            {
                get { return this.serial; }
                set { this.serial = value; }
            }

            private Process process;
            public Process Process
            {
                get { return this.process; }
                set { this.process = value; }
            }
            private IntPtr intPtr;
            public IntPtr IntPtr
            {
                get { return this.intPtr; }
                set { this.intPtr = value; }
            }
        }
    }
}
