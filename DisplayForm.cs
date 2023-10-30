using System;
using System.Timers;
using System.Management;
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

        private int numDeviceColumn = DEFAULT_COLUMN;
        private static System.Timers.Timer aTimer;

        public int NumDeviceColumn
        {
            get { return numDeviceColumn; }
            set { this.numDeviceColumn = value; }
        }

        SettingForm settingForm;
        private GlassyPanel panel;
        private readonly List<Device> devices = new List<Device>();
        private List<String> deviceSerials = new List<string>();

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

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllocConsole();

        private ManagementEventWatcher processStartEvent = new ManagementEventWatcher("SELECT * FROM Win32_ProcessStartTrace");
        private ManagementEventWatcher processStopEvent = new ManagementEventWatcher("SELECT * FROM Win32_ProcessStopTrace");

        public DisplayForm()
        {
            InitializeComponent();
            this.WindowState = FormWindowState.Maximized;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            this.MaximizeBox = false;
            this.KillAllScrcpyProcess();
            aTimer = new System.Timers.Timer();
            aTimer.Interval = 3000;
            aTimer.Elapsed += OnTimedEvent;
            aTimer.AutoReset = true;
            aTimer.Enabled = true;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.DisplayScreen();
            this.InitDevices();
            panel = new GlassyPanel
            {
                Width = this.Width,
                Height = this.Height,
                Dock = DockStyle.Fill
            };
            this.Controls.Add(panel);
            panel.Hide();
            panel.SendToBack();
            AllocConsole();
        }

        private void Initialize()
        {
            processStartEvent.EventArrived += new EventArrivedEventHandler(ProcessStartEvent_EventArrived);
            processStartEvent.Start();
            processStopEvent.EventArrived += new EventArrivedEventHandler(ProcessStopEvent_EventArrived);
            processStopEvent.Start();
        }

        private void ProcessStartEvent_EventArrived(object sender, EventArrivedEventArgs e)
        {
            string processName = e.NewEvent.Properties["ProcessName"].Value.ToString();
            if (processName == "scrcpy.exe")
            {
                //int processID = Convert.ToInt32(e.NewEvent.Properties["ProcessID"].Value);
                //Process scrcpyProcess = Process.GetProcessById(processID);
                //List<string> deviceSerials = this.GetDeviceSerials();
                //// Get all exist old devices
                //List<Device> oldDevices = this.devices.FindAll(d => deviceSerials.Exists(serial => d.Serial == serial));
                //string newSerialDevice = deviceSerials.Find(serial => oldDevices.Exists(d => d.Serial != serial));
                string processID = Convert.ToInt32(e.NewEvent.Properties["ProcessID"].Value).ToString();
                Console.WriteLine("Process started. Name: " + processName + " | ID: " + processID);
            }
        }
        private void ProcessStopEvent_EventArrived(object sender, EventArrivedEventArgs e)
        {
            string processName = e.NewEvent.Properties["ProcessName"].Value.ToString();
            if (processName == "scrcpy.exe")
            {
                string processID = Convert.ToInt32(e.NewEvent.Properties["ProcessID"].Value).ToString();
                Console.WriteLine("Process stopped. Name: " + processName + " | ID: " + processID);
            }
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
                if (2 < rowDisp)
                {
                    rowDisp = 2;
                }
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
            this.deviceSerials = this.GetDeviceSerials();
            int numDevice = this.deviceSerials.Count;
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
                this.AutoScrollMinSize = new Size(0, rowDisp * DEVICE_HEIGHT_FORM + DEVICE_MARGIN_TOP);
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
            ShowWindow(process.MainWindowHandle.ToInt32(), 0);
            Thread.Sleep(500);
            Thread subThread = new Thread(() =>
            {
                int timeSleep = 0;
                while (true)
                {
                    List<Process> processlist = new List<Process>(Process.GetProcessesByName("scrcpy"));
                    if (processlist.Find(p => p.MainWindowTitle == serial) != null)
                    {
                        break;
                    }
                    Thread.Sleep(10);
                    timeSleep += 10;
                    if (timeSleep == 3000)
                    {
                        break;
                    }
                }
                IntPtr windowHandle = IntPtr.Zero;
                if (timeSleep < 3000)
                {
                    windowHandle = process.MainWindowHandle;
                }
                this.AddDeviceScreen(col, row, windowHandle);
                Device device = new Device();
                device.Serial = serial;
                device.IntPtr = windowHandle;
                device.Process = process;
                this.devices.Add(device);
            });
            subThread.Start();
        }

        private void ReApplyOldDevice(string serial, int col, int row)
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
            Thread.Sleep(500);
            Thread subThread = new Thread(() =>
            {
                int timeSleep = 0;
                while (true)
                {
                    List<Process> processlist = new List<Process>(Process.GetProcessesByName("scrcpy"));
                    if (processlist.Find(p => p.MainWindowTitle == serial) != null)
                    {
                        break;
                    }
                    Thread.Sleep(10);
                    timeSleep += 10;
                    if (timeSleep == 3000)
                    {
                        break;
                    }
                }
                if (timeSleep < 3000)
                {
                    Device deviceExist = this.devices.Find(d => d.Serial == serial);
                    if (deviceExist != null)
                    {
                        deviceExist.Process = process;
                        deviceExist.IntPtr = process.MainWindowHandle;
                    }
                    this.AddDeviceScreen(col, row, process.MainWindowHandle);
                }
            });
            subThread.Start();
        }

        // Get devices serial from adb devices command and display
        private void InitDevices()
        {
            this.deviceSerials = this.GetDeviceSerials();
            int length = deviceSerials.Count;
            int col = 0;
            int row = 0;
            for (int i = 0; i < length; i++)
            {
                this.ApplyNewDevice(this.deviceSerials[i], col, row);
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

        private void ReloadScreen()
        {
            this.deviceSerials = this.GetDeviceSerials();
            // Get all exist old devices
            List<Device> oldDevices = this.devices.FindAll(d => this.deviceSerials.Exists(serial => d.Serial == serial));
            foreach (Device oldDevice in oldDevices)
            {
                int index = this.devices.FindIndex(d => d.Serial == oldDevice.Serial);
                // old device but not initialize IntPtr
                int col = index % this.NumDeviceColumn;
                int row = index / this.NumDeviceColumn;
                if (oldDevice.IntPtr.Equals(IntPtr.Zero))
                {
                    this.ApplyNewDevice(oldDevice.Serial, col, row);
                }
                else // if (oldDevice.Process.HasExited)
                {
                    this.ReApplyOldDevice(oldDevice.Serial, col, row);
                }
            }
            List<string> newDeviceSerials = this.deviceSerials.FindAll(serial => oldDevices.Exists(d => d.Serial != serial));
            foreach (string serial in newDeviceSerials)
            {
                int index = this.devices.FindIndex(d => d.Serial == serial);
                if (index == -1)
                {
                    int col = this.devices.Count % NumDeviceColumn;
                    int row = this.devices.Count / NumDeviceColumn;
                    this.ApplyNewDevice(serial, col, row);
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

        private void KillAllScrcpyProcess()
        {
            List<Process> processlist = new List<Process>(Process.GetProcessesByName("scrcpy"));
            foreach (Process process in processlist)
            {
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

        private void Form1_Show(object sender, EventArgs e)
        {
            Initialize();
        }

        private void OnTimedEvent(Object source, System.Timers.ElapsedEventArgs e)
        {
            // this.ReloadScreen();
            if (this.deviceSerials.Count != this.devices.Count)
            {
                return;
            }
            List<String> deviceSerials = this.GetDeviceSerials();
            // for new devices
            List<String> newSerials = deviceSerials.FindAll(s => !this.deviceSerials.Exists(serial => String.Equals(s, serial)));
            if (0 < newSerials.Count)
            {
                Console.WriteLine("device1: " + deviceSerials[0]);
                Console.WriteLine("device2: " + newSerials[0]);
                Console.WriteLine("device" + String.Equals(deviceSerials[0], newSerials[0]));
            }
            this.deviceSerials = deviceSerials;
            if (0 < newSerials.Count)
            {
                for (int i = 0; i < newSerials.Count; i++)
                {
                    int index = this.devices.FindIndex(d => d.Serial == newSerials[i]);
                    // this is new device
                    if (index == -1)
                    {
                        index = this.devices.Count + i;
                        int col = index % this.NumDeviceColumn;
                        int row = index / this.NumDeviceColumn;
                        Console.WriteLine("ApplyNewDevice!");
                        this.ApplyNewDevice(newSerials[i], col, row);
                    }
                    else
                    {
                        Device oldDevice = this.devices[index];
                        // old device but not initialize IntPtr
                        int col = index % this.NumDeviceColumn;
                        int row = index / this.NumDeviceColumn;
                        Console.WriteLine(oldDevice.Serial);
                        if (oldDevice.IntPtr.Equals(IntPtr.Zero))
                        {
                            Console.WriteLine("ApplyNewDevice!");
                            this.ApplyNewDevice(oldDevice.Serial, col, row);
                        }
                        else // if (oldDevice.Process.HasExited)
                        {
                            Console.WriteLine("ReApplyOldDevice!");
                            this.ReApplyOldDevice(oldDevice.Serial, col, row);
                        }
                    }
                }
            }
            else
            {
                for (int i = 0; i < deviceSerials.Count; i++)
                {
                    int index = this.devices.FindIndex(d => d.Serial == deviceSerials[i]);
                    Device oldDevice = this.devices[i];
                    // old device but not initialize IntPtr
                    int col = index % this.NumDeviceColumn;
                    int row = index / this.NumDeviceColumn;
                    if (oldDevice.IntPtr.Equals(IntPtr.Zero))
                    {
                        Console.WriteLine("ApplyNewDevice!");
                        this.ApplyNewDevice(oldDevice.Serial, col, row);
                    } else if (oldDevice.Process.HasExited)
                    {
                        Console.WriteLine("ReApplyOldDevice!");
                        this.ReApplyOldDevice(oldDevice.Serial, col, row);
                    }
                }
            }
            int numDevice = this.devices.Count;
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
                this.AutoScrollMinSize = new Size(0, rowDisp * DEVICE_HEIGHT_FORM + DEVICE_MARGIN_TOP);
            }
        }
    }
}
