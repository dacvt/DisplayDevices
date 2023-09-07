using System;
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
        private const int DEVICE_WIDTH = 370;
        private const int DEVICE_HEIGHT = 600;
        private const int DEVICE_MARGIN_TOP = 25;
        private const int DEVICE_WIDTH_FORM = 345;
        private const int DEVICE_HEIGHT_FORM = 637;
        private const int PADDING_FROM_RIGHT = 5; // padding increase if has more device
        private const int DEFAULT_COLUMN = 3;
        private ManagementEventWatcher processStartEvent = new ManagementEventWatcher("SELECT * FROM Win32_ProcessStartTrace");
        private ManagementEventWatcher processStopEvent = new ManagementEventWatcher("SELECT * FROM Win32_ProcessStopTrace");

        public int NumDeviceColumn { get; set; } = DEFAULT_COLUMN;

        private GlassyPanel panel;
        private readonly List<Device> devices = new List<Device>();
        private readonly List<MyThread> myThreads = new List<MyThread>();

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

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        //[DllImport("kernel32.dll", SetLastError = true)]
        //[return: MarshalAs(UnmanagedType.Bool)]
        //static extern bool AllocConsole();

        public DisplayForm(int numDeviceColumn)
        {
            InitializeComponent();
            this.NumDeviceColumn = numDeviceColumn;
            this.WindowState = FormWindowState.Maximized;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.KillAllProcess();
            InitDevices();
            Initialize();
        }

        protected override void WndProc(ref Message message)
        {
            const int WM_SYSCOMMAND = 0x0112;
            const int SC_MOVE = 0xF010;

            switch (message.Msg)
            {
                case WM_SYSCOMMAND:
                    int command = message.WParam.ToInt32() & 0xfff0;
                    if (command == SC_MOVE)
                        return;
                    break;
            }
            base.WndProc(ref message);
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
            int processID = 0;
            if (processName == "MEmu.exe")
            {
                Thread subThread = new Thread(() =>
                {
                    processID = Convert.ToInt32(e.NewEvent.Properties["ProcessID"].Value);
                    string deviceName;
                    Process memuProcess;
                    while (true)
                    {
                        memuProcess = Process.GetProcessById(processID);
                        // ShowWindow(memuProcess.MainWindowHandle.ToInt32(), 0);
                        deviceName = memuProcess.MainWindowTitle.Replace(")", "").Replace("(", "");
                        Console.WriteLine("Device Start. Name: " + deviceName + " | ID: " + processID);
                        if (deviceName == "")
                        {
                            Thread.Sleep(1000);
                            continue;
                        }
                        break;
                    }
                    string[] names = deviceName.Split('_');
                    string name = names[0];
                    int deviceNum = Int32.Parse(names[1]) - 1;
                    int col = deviceNum % NumDeviceColumn;
                    int row = deviceNum / NumDeviceColumn;
                    this.ApplyNewDevice(memuProcess, col, row);
                    while (true)
                    {
                        Thread.Sleep(3000);
                        string[] deviceInfo = this.GetVirtualDeviceInfo(deviceName);
                        if (deviceInfo == null)
                        {
                            continue;
                        }
                        if (this.IsDeviceStarted(deviceInfo[0]))
                        {
                            break;
                        }
                    }
                    SetWindowPos(memuProcess.MainWindowHandle, this.Handle, col * DEVICE_WIDTH, row * DEVICE_HEIGHT + DEVICE_MARGIN_TOP, DEVICE_WIDTH, DEVICE_HEIGHT, 0x0001);
                    this.AddDeviceScreen(col, row, memuProcess.MainWindowHandle);
                });
                subThread.Start();
                if (processID != 0)
                {
                    MyThread myThread = new MyThread
                    {
                        ProcessId = processID,
                        SubThread = subThread
                    };
                    this.myThreads.Add(myThread);
                }
            }
        }
        private void ProcessStopEvent_EventArrived(object sender, EventArrivedEventArgs e)
        {
            string processName = e.NewEvent.Properties["ProcessName"].Value.ToString();
            if (processName == "MEmu.exe")
            {
                string processID = Convert.ToInt32(e.NewEvent.Properties["ProcessID"].Value).ToString();
                Console.WriteLine("Process stopped. Name: " + processName + " | ID: " + processID);
                MyThread myThread = this.myThreads.Find(thread => thread.ProcessId == Int32.Parse(processID));
                myThread.SubThread.Abort();
            }
        }
        private bool IsDeviceStarted(string vmindex)
        {
            string output = this.RunCmd(String.Format("memuc -i {0} execcmd \"getprop sys.boot_completed\"", vmindex), true);
            return output.Contains("\n1");
        }

        private string[] GetVirtualDeviceInfo(string deviceName)
        {
            string output = this.RunCmd("memuc listvms", true);
            string[] outputStrArr = output.Split('\n');
            for (int i = 0; i < outputStrArr.Length; i++)
            {
                string[] deviceInfo = outputStrArr[i].Split(',');
                if (deviceInfo.Length != 5)
                {
                    continue;
                }
                if (deviceInfo[1] == deviceName)
                {
                    return deviceInfo;
                }
            }
            return null;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            //AllocConsole();
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
            MoveWindow(deviceDisp, col * DEVICE_WIDTH, row * DEVICE_HEIGHT + DEVICE_MARGIN_TOP, DEVICE_WIDTH, DEVICE_HEIGHT, false);
        }

        private void ApplyNewDevice(Process process, int col, int row)
        {
            IntPtr windowHandle = process.MainWindowHandle;
            this.AddDeviceScreen(col, row, process.MainWindowHandle);
            Device device = new Device
            {
                IntPtr = windowHandle,
                Process = process
            };
            this.devices.Add(device);
        }

        private void InitDevices()
        {
            List<Process> processlist = new List<Process>(Process.GetProcessesByName("MEmu"));
            foreach (Process memuProcess in processlist)
            {
                Thread subThread = new Thread(() =>
                {
                    string deviceName;
                    while (true)
                    {
                        deviceName = memuProcess.MainWindowTitle.Replace(")", "").Replace("(", "");
                        if (deviceName == "")
                        {
                            Thread.Sleep(1000);
                            continue;
                        }
                        break;
                    }
                    string[] names = deviceName.Split('_');
                    string name = names[0];
                    int deviceNum = Int32.Parse(names[1]) - 1;
                    int col = deviceNum % NumDeviceColumn;
                    int row = deviceNum / NumDeviceColumn;
                    this.ApplyNewDevice(memuProcess, col, row);
                    while (true)
                    {
                        Thread.Sleep(3000);
                        string[] deviceInfo = this.GetVirtualDeviceInfo(deviceName);
                        if (deviceInfo == null)
                        {
                            continue;
                        }
                        if (this.IsDeviceStarted(deviceInfo[0]))
                        {
                            break;
                        }
                    }
                    SetWindowPos(memuProcess.MainWindowHandle, this.Handle, col * DEVICE_WIDTH, row * DEVICE_HEIGHT + DEVICE_MARGIN_TOP, DEVICE_WIDTH, DEVICE_HEIGHT, 0x0001);
                    this.AddDeviceScreen(col, row, memuProcess.MainWindowHandle);
                });
                subThread.Start();
                MyThread myThread = new MyThread
                {
                    ProcessId = memuProcess.Id,
                    SubThread = subThread
                };
                this.myThreads.Add(myThread);
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

        private void KillAllProcess()
        {
            foreach (Device device in devices)
            {
                if (!device.Process.HasExited)
                {
                    device.Process.Kill();
                    device.Process.Dispose();
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

        private class MyThread
        {
            private int processId;
            public int ProcessId
            {
                get { return this.processId; }
                set { this.processId = value; }
            }

            private Thread subThread;
            public Thread SubThread
            {
                get { return this.subThread; }
                set { this.subThread = value; }
            }
        }
    }
}
