// CatTimer - keeps the PC awake and shuts it down at a target time.
// Build with build.bat (uses the C# compiler that ships with Windows).
using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace CatTimer
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool isNew;
            using (Mutex m = new Mutex(true, "CatTimer_SingleInstance", out isNew))
            {
                if (!isNew)
                {
                    MessageBox.Show("CatTimer is already running (look in the system tray).",
                        "CatTimer", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayApp());
            }
        }
    }

    class TrayApp : ApplicationContext
    {
        // ---- Settings ----
        static readonly TimeSpan DefaultTarget = new TimeSpan(17, 0, 0); // 17:00
        static readonly TimeSpan KeepAwakeEvery = TimeSpan.FromMinutes(5);
        static readonly TimeSpan WarnBefore = TimeSpan.FromMinutes(5);
        static readonly TimeSpan SnoozeStep = TimeSpan.FromMinutes(5);

        // ---- Win32 ----
        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
        [DllImport("kernel32.dll")]
        static extern uint SetThreadExecutionState(uint esFlags);
        const byte VK_CAPITAL = 0x14;
        const uint KEYEVENTF_KEYUP = 0x0002;
        const uint ES_CONTINUOUS = 0x80000000;
        const uint ES_SYSTEM_REQUIRED = 0x00000001;
        const uint ES_DISPLAY_REQUIRED = 0x00000002;

        NotifyIcon tray;
        ToolStripMenuItem miTarget, miSnooze;
        System.Windows.Forms.Timer tick;

        DateTime baseTarget;     // target before snoozes
        int snoozeCount;
        DateTime nextKeepAwake;
        bool warned;
        bool shuttingDown;
        AlertForm alert;

        DateTime Target { get { return baseTarget.Add(TimeSpan.FromMinutes(SnoozeStep.TotalMinutes * snoozeCount)); } }

        public TrayApp()
        {
            SetTarget(DefaultTarget);
            nextKeepAwake = DateTime.Now.Add(KeepAwakeEvery);
            SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED);

            ContextMenuStrip menu = new ContextMenuStrip();
            miTarget = new ToolStripMenuItem();
            miTarget.Enabled = false;
            miSnooze = new ToolStripMenuItem();
            miSnooze.Enabled = false;
            ToolStripMenuItem miChange = new ToolStripMenuItem("Change target time...", null, delegate { ChangeTarget(); });
            ToolStripMenuItem miExit = new ToolStripMenuItem("Exit", null, delegate { ExitApp(); });
            menu.Items.Add(miTarget);
            menu.Items.Add(miSnooze);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(miChange);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(miExit);
            menu.Opening += delegate { RefreshText(); };

            tray = new NotifyIcon();
            tray.Icon = LoadIcon();
            tray.ContextMenuStrip = menu;
            tray.Visible = true;
            tray.DoubleClick += delegate { ChangeTarget(); };
            RefreshText();
            tray.ShowBalloonTip(3000, "CatTimer is running",
                "Shutdown at " + Target.ToString("HH:mm") + ". Right-click the tray icon for options.", ToolTipIcon.Info);

            tick = new System.Windows.Forms.Timer();
            tick.Interval = 1000;
            tick.Tick += OnTick;
            tick.Start();
        }

        static Icon LoadIcon()
        {
            try { return Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { return SystemIcons.Application; }
        }

        // Sets the target to the given time of day: today if still ahead, otherwise tomorrow.
        void SetTarget(TimeSpan timeOfDay)
        {
            DateTime t = DateTime.Today.Add(timeOfDay);
            if (t <= DateTime.Now) t = t.AddDays(1);
            baseTarget = t;
            snoozeCount = 0;
            warned = false;
            CloseAlert();
        }

        void OnTick(object sender, EventArgs e)
        {
            if (shuttingDown) return;
            DateTime now = DateTime.Now;

            // 1. Keep awake: tap Caps Lock twice (state ends up unchanged).
            if (now >= nextKeepAwake)
            {
                TapCapsLock();
                TapCapsLock();
                nextKeepAwake = now.Add(KeepAwakeEvery);
            }

            // 2. Warn 5 minutes before, then shut down at target.
            if (!warned && now >= Target.Subtract(WarnBefore) && now < Target)
            {
                warned = true;
                ShowAlert();
            }
            if (now >= Target)
            {
                DoShutdown();
                return;
            }
            if (alert != null && !alert.IsDisposed) alert.UpdateCountdown(Target - now);
            UpdateTooltip();
        }

        static void TapCapsLock()
        {
            keybd_event(VK_CAPITAL, 0x45, 0, UIntPtr.Zero);
            keybd_event(VK_CAPITAL, 0x45, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        void ShowAlert()
        {
            CloseAlert();
            alert = new AlertForm(Target);
            alert.SnoozeClicked += delegate { Snooze(); };
            alert.ShutdownNowClicked += delegate { DoShutdown(); };
            alert.Show();
            alert.Activate();
            System.Media.SystemSounds.Exclamation.Play();
        }

        void CloseAlert()
        {
            if (alert != null && !alert.IsDisposed) alert.Close();
            alert = null;
        }

        void Snooze()
        {
            snoozeCount++;
            warned = false; // will warn again 5 min before the new target
            CloseAlert();
            RefreshText();
            tray.ShowBalloonTip(3000, "Snoozed +5 min", "New shutdown time: " + Target.ToString("HH:mm"), ToolTipIcon.Info);
        }

        void ChangeTarget()
        {
            using (ChangeTimeForm f = new ChangeTimeForm(Target))
            {
                if (f.ShowDialog() == DialogResult.OK)
                {
                    SetTarget(f.SelectedTime);
                    RefreshText();
                    tray.ShowBalloonTip(3000, "Target changed",
                        "Shutdown at " + Target.ToString("HH:mm") + DayNote(Target), ToolTipIcon.Info);
                }
            }
        }

        static string DayNote(DateTime t)
        {
            return t.Date > DateTime.Today ? " (tomorrow)" : "";
        }

        void RefreshText()
        {
            miTarget.Text = "Shutdown at: " + Target.ToString("HH:mm") + DayNote(Target);
            miSnooze.Text = snoozeCount == 0
                ? "Snooze: none"
                : "Snooze: " + snoozeCount + "x (+" + (snoozeCount * (int)SnoozeStep.TotalMinutes) + " min, was " + baseTarget.ToString("HH:mm") + ")";
            UpdateTooltip();
        }

        void UpdateTooltip()
        {
            TimeSpan left = Target - DateTime.Now;
            string s = "CatTimer - shutdown " + Target.ToString("HH:mm") + " (in " + FormatSpan(left) + ")";
            if (s.Length > 63) s = s.Substring(0, 63); // NotifyIcon limit
            tray.Text = s;
        }

        internal static string FormatSpan(TimeSpan t)
        {
            if (t < TimeSpan.Zero) t = TimeSpan.Zero;
            if (t.TotalHours >= 1) return (int)t.TotalHours + "h " + t.Minutes + "m";
            return t.Minutes + ":" + t.Seconds.ToString("00");
        }

        void DoShutdown()
        {
            if (shuttingDown) return;
            shuttingDown = true;
            tick.Stop();
            CloseAlert();
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("shutdown", "/s /t 0");
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not shut down: " + ex.Message, "CatTimer");
                shuttingDown = false;
                tick.Start();
            }
        }

        void ExitApp()
        {
            tick.Stop();
            CloseAlert();
            SetThreadExecutionState(ES_CONTINUOUS);
            tray.Visible = false;
            tray.Dispose();
            ExitThread();
        }
    }

    // Popup shown 5 minutes before shutdown.
    class AlertForm : Form
    {
        public event EventHandler SnoozeClicked;
        public event EventHandler ShutdownNowClicked;
        Label lblCountdown;

        public AlertForm(DateTime target)
        {
            Text = "CatTimer - Shutdown soon";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            TopMost = true;
            ShowInTaskbar = true;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(360, 160);
            Font = new Font("Segoe UI", 10f);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            Label lbl = new Label();
            lbl.Text = "The computer will shut down at " + target.ToString("HH:mm") + ".\nSave your work!";
            lbl.SetBounds(16, 12, 330, 44);
            Controls.Add(lbl);

            lblCountdown = new Label();
            lblCountdown.Font = new Font("Segoe UI", 18f, FontStyle.Bold);
            lblCountdown.TextAlign = ContentAlignment.MiddleCenter;
            lblCountdown.SetBounds(16, 56, 330, 40);
            Controls.Add(lblCountdown);

            Button bSnooze = new Button();
            bSnooze.Text = "Snooze +5 min";
            bSnooze.SetBounds(16, 108, 150, 36);
            bSnooze.Click += delegate { if (SnoozeClicked != null) SnoozeClicked(this, EventArgs.Empty); };
            Controls.Add(bSnooze);

            Button bNow = new Button();
            bNow.Text = "Shut down now";
            bNow.SetBounds(194, 108, 150, 36);
            bNow.Click += delegate { if (ShutdownNowClicked != null) ShutdownNowClicked(this, EventArgs.Empty); };
            Controls.Add(bNow);

            AcceptButton = bSnooze;
            UpdateCountdown(target - DateTime.Now);
        }

        public void UpdateCountdown(TimeSpan left)
        {
            lblCountdown.Text = TrayApp.FormatSpan(left);
        }
    }

    // Dialog to pick a new target time.
    class ChangeTimeForm : Form
    {
        DateTimePicker picker;
        public TimeSpan SelectedTime { get { return new TimeSpan(picker.Value.Hour, picker.Value.Minute, 0); } }

        public ChangeTimeForm(DateTime current)
        {
            Text = "CatTimer - Change shutdown time";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            TopMost = true;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(300, 120);
            Font = new Font("Segoe UI", 10f);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            Label lbl = new Label();
            lbl.Text = "Shutdown time (24h):";
            lbl.SetBounds(16, 18, 150, 24);
            Controls.Add(lbl);

            picker = new DateTimePicker();
            picker.Format = DateTimePickerFormat.Custom;
            picker.CustomFormat = "HH:mm";
            picker.ShowUpDown = true;
            picker.Value = current;
            picker.SetBounds(170, 14, 110, 28);
            Controls.Add(picker);

            Button ok = new Button();
            ok.Text = "OK";
            ok.DialogResult = DialogResult.OK;
            ok.SetBounds(100, 70, 85, 32);
            Controls.Add(ok);

            Button cancel = new Button();
            cancel.Text = "Cancel";
            cancel.DialogResult = DialogResult.Cancel;
            cancel.SetBounds(195, 70, 85, 32);
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
        }
    }
}
