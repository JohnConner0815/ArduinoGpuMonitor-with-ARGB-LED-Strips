using System;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Windows.Forms;
using LibreHardwareMonitor.Hardware;

namespace ArduinoGpuMonitor
{
    public class MainForm : Form
    {
        private Computer computer;
        private SerialPort serialPort;
        private System.Windows.Forms.Timer pollTimer;

        // UI Controls
        private ComboBox cmbComPorts;
        private Button btnRefresh;
        private ComboBox cmbGpu;
        private ComboBox cmbPollingRate;
        private Button btnConnect;
        private Button btnDisconnect;
        private DarkGreenProgressBar progressBar;
        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;

        // Tray Icons
        private Icon iconGreen;
        private Icon iconRed;
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        public MainForm()
        {
            InitializeHardwareMonitor();
            InitializeUI();
            LoadComPorts();
            SetupTrayIcon();
        }

        private void InitializeHardwareMonitor()
        {
            computer = new Computer
            {
                IsGpuEnabled = true
            };
            computer.Open();
            computer.Accept(new UpdateVisitor());
        }

        private void InitializeUI()
        {
            // Form Settings
            this.Text = "Arduino GPU Monitor LED strip";
            this.Size = new Size(480, 220);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimizeBox = true;

            int y = 20;
            int dropdownWidth = 200;
            int buttonWidth = 80;
            int spacing = 10;
            int x = 15;

            // Row 1: COM Port & Refresh
            cmbComPorts = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = dropdownWidth, Location = new Point(x, y) };
            btnRefresh = new Button { Text = "Refresh", Width = buttonWidth, Location = new Point(x + dropdownWidth + spacing, y) };
            btnRefresh.Click += (s, e) => LoadComPorts();

            // Row 2: GPU Selection
            y += 35;
            cmbGpu = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = dropdownWidth, Location = new Point(x, y) };
            cmbGpu.Items.Add("No GPUs detected");
            cmbGpu.SelectedIndex = 0;
            PopulateGpuList();

            // Row 3: Polling Rate, Connect, Disconnect
            y += 35;
            cmbPollingRate = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = dropdownWidth, Location = new Point(x, y) };
            cmbPollingRate.Items.AddRange(new object[] { "1.0 s", "0.75 s", "0.5 s", "0.25 s", "0.1 s" });
            cmbPollingRate.SelectedIndex = 2; // Default 0.5s

            btnConnect = new Button { Text = "Connect", Width = buttonWidth, Location = new Point(x + dropdownWidth + spacing, y) };
            btnDisconnect = new Button { Text = "Disconnect", Width = buttonWidth + 10, Location = new Point(x + dropdownWidth + spacing + buttonWidth + spacing, y), Enabled = false };

            btnConnect.Click += BtnConnect_Click;
            btnDisconnect.Click += BtnDisconnect_Click;

            // Row 4: Progress Bar
            y += 45;
            progressBar = new DarkGreenProgressBar { Width = this.ClientSize.Width - 30, Location = new Point(x, y), Height = 25 };

            // Add controls to form
            this.Controls.Add(cmbComPorts);
            this.Controls.Add(btnRefresh);
            this.Controls.Add(cmbGpu);
            this.Controls.Add(cmbPollingRate);
            this.Controls.Add(btnConnect);
            this.Controls.Add(btnDisconnect);
            this.Controls.Add(progressBar);

            // Timer
            pollTimer = new System.Windows.Forms.Timer();
            pollTimer.Tick += PollTimer_Tick;

        }

        private void PopulateGpuList()
        {
            cmbGpu.Items.Clear();
            var gpus = computer.Hardware.Where(h =>
                h.HardwareType == HardwareType.GpuNvidia ||
                h.HardwareType == HardwareType.GpuAmd ||
                h.HardwareType == HardwareType.GpuIntel).ToList();

            if (gpus.Count == 0)
            {
                cmbGpu.Items.Add("No GPUs detected");
                cmbGpu.SelectedIndex = 0;
                return;
            }

            int dedicatedIndex = 0;
            for (int i = 0; i < gpus.Count; i++)
            {
                cmbGpu.Items.Add(gpus[i].Name);
                // Heuristic to pre-select dedicated GPU
                if (gpus[i].Name.Contains("NVIDIA") || gpus[i].Name.Contains("AMD") || gpus[i].Name.Contains("Radeon"))
                {
                    dedicatedIndex = i;
                }
            }
            cmbGpu.SelectedIndex = dedicatedIndex;
        }

        private void LoadComPorts()
        {
            string selectedPort = cmbComPorts.SelectedItem?.ToString();
            cmbComPorts.Items.Clear();
            cmbComPorts.Items.AddRange(SerialPort.GetPortNames());

            if (cmbComPorts.Items.Count == 0) cmbComPorts.Items.Add("No COM ports found");

            if (selectedPort != null && cmbComPorts.Items.Contains(selectedPort))
                cmbComPorts.SelectedItem = selectedPort;
            else
                cmbComPorts.SelectedIndex = 0;
        }

        private void SetupTrayIcon()
        {
            iconGreen = CreateSolidIcon(Color.Green);
            iconRed = CreateSolidIcon(Color.Red);

            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Show", null, (s, e) => { ShowWindow(); });
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("Close", null, (s, e) => { Application.Exit(); });

            trayIcon = new NotifyIcon
            {
                Icon = iconRed,
                ContextMenuStrip = trayMenu,
                Visible = true,
                Text = "GPU Monitor - Disconnected"
            };
            trayIcon.DoubleClick += (s, e) => { ShowWindow(); };
        }

        protected override void WndProc(ref Message m)
        {
            // Intercept the minimize command (WM_SYSCOMMAND = 0x0112, SC_MINIMIZE = 0xF020)
            if (m.Msg == 0x0112 && m.WParam.ToInt32() == 0xF020)
            {
                this.Hide(); // Just hide it, never let it enter the minimized state
                m.Result = IntPtr.Zero;
                return;
            }
            base.WndProc(ref m);
        }

        private void ShowWindow()
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke((MethodInvoker)delegate { ShowWindow(); });
                return;
            }

            this.Show();
            SetForegroundWindow(this.Handle); // Brute force Windows to pull the window to the front
            this.Activate();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide(); // Directly hide, do not minimize
                return;
            }

            // Actual cleanup when Application.Exit() is called from tray
            pollTimer?.Stop();
            if (serialPort != null && serialPort.IsOpen) serialPort.Close();
            computer?.Close();
            trayIcon?.Dispose();
            iconGreen?.Dispose();
            iconRed?.Dispose();
            base.OnFormClosing(e);
        }

        private Icon CreateSolidIcon(Color color)
        {
            Bitmap bmp = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(color);
            }
            return Icon.FromHandle(bmp.GetHicon());
        }

        private void BtnConnect_Click(object sender, EventArgs e)
        {
            if (cmbComPorts.SelectedItem == null || cmbComPorts.SelectedItem.ToString().StartsWith("No")) return;

            try
            {
                serialPort = new SerialPort(cmbComPorts.SelectedItem.ToString(), 19200);
                serialPort.Open();

                btnConnect.Enabled = false;
                btnDisconnect.Enabled = true;
                cmbComPorts.Enabled = false;
                btnRefresh.Enabled = false;

                trayIcon.Icon = iconGreen;
                trayIcon.Text = "GPU Monitor - Connected";

                UpdateTimerInterval();
                pollTimer.Start();
            }
            catch
            {
                MessageBox.Show("Could not open COM Port. Is it in use by another program?", "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                SilentDisconnect();
            }
        }

        private void BtnDisconnect_Click(object sender, EventArgs e)
        {
            SilentDisconnect();
        }

        private void SilentDisconnect()
        {
            pollTimer.Stop();
            if (serialPort != null && serialPort.IsOpen)
            {
                try { serialPort.Close(); } catch { }
            }
            serialPort = null;

            btnConnect.Enabled = true;
            btnDisconnect.Enabled = false;
            cmbComPorts.Enabled = true;
            btnRefresh.Enabled = true;

            progressBar.Value = 0;
            trayIcon.Icon = iconRed;
            trayIcon.Text = "GPU Monitor - Disconnected";
        }

        private void UpdateTimerInterval()
        {
            string selected = cmbPollingRate.SelectedItem.ToString();
            int ms = 500; // default
            if (selected.Contains("1.0")) ms = 1000;
            else if (selected.Contains("0.75")) ms = 750;
            else if (selected.Contains("0.5")) ms = 500;
            else if (selected.Contains("0.25")) ms = 250;
            else if (selected.Contains("0.1")) ms = 100;

            pollTimer.Interval = ms;
        }

        private void PollTimer_Tick(object sender, EventArgs e)
        {
            // If user changes polling rate on the fly
            UpdateTimerInterval();

            int gpuLoad = GetGpuLoad();

            progressBar.Value = gpuLoad;

            try
            {
                if (serialPort != null && serialPort.IsOpen)
                {
                    serialPort.WriteLine(gpuLoad.ToString());
                }
                else
                {
                    SilentDisconnect(); // Cable was unplugged
                }
            }
            catch
            {
                SilentDisconnect(); // Error writing (Cable unplugged)
            }
        }

        private int GetGpuLoad()
        {
            if (cmbGpu.SelectedItem == null || cmbGpu.SelectedItem.ToString().StartsWith("No")) return 0;

            string selectedGpuName = cmbGpu.SelectedItem.ToString();
            var gpu = computer.Hardware.FirstOrDefault(h =>
                (h.HardwareType == HardwareType.GpuNvidia ||
                 h.HardwareType == HardwareType.GpuAmd ||
                 h.HardwareType == HardwareType.GpuIntel) && h.Name == selectedGpuName);

            if (gpu != null)
            {
                gpu.Update();
                var sensor = gpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Load && s.Name.Contains("Core"));
                if (sensor != null && sensor.Value.HasValue)
                {
                    return (int)Math.Min(100, Math.Max(0, sensor.Value.Value));
                }
            }
            return 0;
        }

    }

    // Required for LibreHardwareMonitor to update sensors
    public class UpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer) { computer.Traverse(this); }
        public void VisitHardware(IHardware hardware) { hardware.Update(); foreach (IHardware subHardware in hardware.SubHardware) subHardware.Accept(this); }
        public void VisitSensor(ISensor sensor) { }
        public void VisitParameter(IParameter parameter) { }
    }
}