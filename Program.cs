using System;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;

namespace InstantWindowLogic
{
    class Program
    {
        static void Main(string[] args)
        {
            ProcessMonitor monitor = new ProcessMonitor();
            monitor.Start();

            Console.WriteLine("Monitoring process starts. Press Enter to exit.");
            Console.ReadLine();

            monitor.Stop();
        }
    }

    public class ProcessMonitor
    {
        private ManagementEventWatcher startWatch;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private const int SW_MAXIMIZE = 3; // Constant to maximize the window

        public ProcessMonitor()
        {
            startWatch = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace"));
            startWatch.EventArrived += new EventArrivedEventHandler(ProcessStarted);
        }

        public void Start()
        {
            startWatch.Start();
        }

        public void Stop()
        {
            startWatch.Stop();
        }

        private void ProcessStarted(object sender, EventArrivedEventArgs e)
        {
            string processName = e.NewEvent.Properties["ProcessName"].Value.ToString();
            uint processId = (uint)e.NewEvent.Properties["ProcessID"].Value;

            // Run the process handling code in a separate thread
            ThreadPool.QueueUserWorkItem(HandleProcessStart, new ProcessInfo { ProcessId = processId, ProcessName = processName });
        }

        private void HandleProcessStart(object state)
        {
            var processInfo = (ProcessInfo)state;

            // Short delay to allow the process to create its main window
            Thread.Sleep(100);

            // Find the window handle by the process ID
            IntPtr hWnd = IntPtr.Zero;
            EnumWindows((wnd, param) =>
            {
                GetWindowThreadProcessId(wnd, out uint wndProcessId);
                if (wndProcessId == processInfo.ProcessId && IsWindowVisible(wnd))
                {
                    hWnd = wnd;
                    return false; // Stop enumeration
                }
                return true; // Continue enumeration
            }, IntPtr.Zero);

            if (hWnd != IntPtr.Zero)
            {
                // Log the window details
                bool isVisible = IsWindowVisible(hWnd);
                string visibility = isVisible ? "Visible" : "Not Visible";

                Console.WriteLine($"Window with handle {hWnd} for process {processInfo.ProcessName} (ID: {processInfo.ProcessId}) is {visibility}");

                // Maximize the window (turn it into full screen)
                ShowWindow(hWnd, SW_MAXIMIZE);
                Console.WriteLine($"Maximized window with handle {hWnd} for process {processInfo.ProcessName} (ID: {processInfo.ProcessId})");
            }
            else
            {
                Console.WriteLine($"No visible window found for process {processInfo.ProcessName} (ID: {processInfo.ProcessId})");
            }
        }

        private class ProcessInfo
        {
            public uint ProcessId { get; set; }
            public string ProcessName { get; set; }
        }
    }
}
