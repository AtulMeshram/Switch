using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Media;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.IO;
using SwitchRemoteServices;
using SwitchServerApplication;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Management;
using Blue.Windows;

namespace SwitchPlus
{
    public partial class Switch : Form, SwitchServerApplication.ISwitchRemote
    {
        bool isStartRemot = false;
        DateTime SysBootDT = new DateTime();
        ///
        /// Implementation of IAppRemote
        /// 
        public void doExecute(int task, bool brut)
        {
            this.Power(task, brut);
        }

        public int getNumber()
        {
            return (this.GetHashCode());
        }

        // Variable for Image Streaming
        private ImageStreamer _VServer;

        //Public Variables ------        
        public static bool pnlHhMmSs = true;
        //-----------------------
        [DllImport("user32.dll")]
        static extern bool AnimateWindow(IntPtr hWnd, int time, AnimateWindowFlags flags);

        [Flags]
        enum AnimateWindowFlags
        {
            AW_HOR_POSITIVE = 0x00000001,
            AW_HOR_NEGATIVE = 0x00000002,
            AW_VER_POSITIVE = 0x00000004,
            AW_VER_NEGATIVE = 0x00000008,
            AW_CENTER = 0x00000010,
            AW_HIDE = 0x00010000,
            AW_ACTIVATE = 0x00020000,
            AW_SLIDE = 0x00040000,
            AW_BLEND = 0x00080000
        }

        #region Droping Shadow
        private const int CS_DROPSHADOW = 0x0020000;
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= CS_DROPSHADOW;
                //cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_TOPMOST;
                cp.Parent = IntPtr.Zero;
                //cp.ExStyle |= 0x02000000; // Turn on WS_EX_COMPOSITED
                return cp;
            }

        }
        #endregion

        #region Rounding Window
        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn
        (
        int nLeftRect,
        int nTopRect,
        int nRightRect,
        int nBottomRect,
        int nWidthEllipse,
        int nHeightEllipse
        );
        #endregion

        #region Interop Definitions for top most window
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static public extern IntPtr GetDesktopWindow();
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public extern static IntPtr SetParent(IntPtr hChild, IntPtr hParent);
        [DllImport("user32.dll", EntryPoint = "SetWindowPos", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        public static extern int SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        private const int WS_EX_TOPMOST = unchecked((int)0x00000008L);
        private const int WS_EX_TOOLWINDOW = unchecked((int)0x00000080);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const int HWND_TOPMOST = -1;
        #endregion

        [DllImport("user32.dll")]
        private static extern
            bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern
            bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")]
        private static extern
            bool IsIconic(IntPtr hWnd);

        private const int SW_HIDE = 0;
        private const int SW_SHOWNORMAL = 1;
        private const int SW_SHOWMINIMIZED = 2;
        private const int SW_SHOWMAXIMIZED = 3;
        private const int SW_SHOWNOACTIVATE = 4;
        private const int SW_RESTORE = 9;
        private const int SW_SHOWDEFAULT = 10;
        //---------------------------------------

        /// <summary>
        /// Thumb Movement
        /// </summary>
        #region
        private const int WM_HSCROLL = 0x114;
        private const int WM_VSCROLL = 0x115;

        protected override void WndProc(ref Message m)
        {
            if ((m.Msg == WM_HSCROLL || m.Msg == WM_VSCROLL)
            && (((int)m.WParam & 0xFFFF) == 5))
            {
                // Change SB_THUMBTRACK to SB_THUMBPOSITION
                m.WParam = (IntPtr)(((int)m.WParam & ~0xFFFF) | 4);
            }
            base.WndProc(ref m);

            if (m.Msg == 163 && this.ClientRectangle.Contains(this.PointToClient(new Point(m.LParam.ToInt32()))) && m.WParam.ToInt32() == 2)
                m.WParam = (IntPtr)1;
            base.WndProc(ref m);
            if (m.Msg == 132 && m.Result.ToInt32() == 1)
                m.Result = (IntPtr)2;
        }
        #endregion

        //Action Code ---------------------------
        [DllImport("user32.dll")]       //Importing System DLL
        public static extern int ExitWindowsEx(int uFlags, int dwReason);

        [DllImport("user32.dll")]
        public static extern void LockWorkStation();

        [DllImport("user32.dll", SetLastError = true)]
        static extern int ExitWindowsEx(uint uFlags, uint dwReason);
        enum ExitFlags
        {
            Logoff = 0,
            Shutdown = 1,
            Reboot = 2,
            Force = 4,
            PowerOff = 8,
            ForceIfHung = 16
        }
        enum Reason : uint
        {
            ApplicationIssue = 0x00040000,
            HardwareIssue = 0x00010000,
            SoftwareIssue = 0x00030000,
            PlannedShutdown = 0x80000000
        }
        const int PrivilegeEnabled = 0x00000002;
        const int TokenQuery = 0x00000008;
        const int AdjustPrivileges = 0x00000020;
        const string ShutdownPrivilege = "SeShutdownPrivilege";

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct TokenPrivileges
        {
            public int PrivilegeCount;
            public long Luid;
            public int Attributes;
        }

        [DllImport("kernel32.dll")]
        internal static extern IntPtr GetCurrentProcess();

        [DllImport("advapi32.dll", SetLastError = true)]
        internal static extern int OpenProcessToken(IntPtr processHandle, int desiredAccess, ref IntPtr tokenHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        internal static extern int LookupPrivilegeValue(string systemName, string name, ref long luid);

        [DllImport("advapi32.dll", SetLastError = true)]
        internal static extern int AdjustTokenPrivileges(IntPtr tokenHandle, bool disableAllPrivileges, ref TokenPrivileges newState, int bufferLength, IntPtr previousState, IntPtr length);
        private void ElevatePrivileges()
        {
            IntPtr currentProcess = GetCurrentProcess();
            IntPtr tokenHandle = IntPtr.Zero;

            int result = OpenProcessToken(currentProcess, AdjustPrivileges | TokenQuery, ref tokenHandle);

            if (result == 0)
                throw new Win32Exception(Marshal.GetLastWin32Error());

            TokenPrivileges tokenPrivileges;
            tokenPrivileges.PrivilegeCount = 1;
            tokenPrivileges.Luid = 0;
            tokenPrivileges.Attributes = PrivilegeEnabled;

            result = LookupPrivilegeValue(null, ShutdownPrivilege, ref tokenPrivileges.Luid);

            if (result == 0)
                throw new Win32Exception(Marshal.GetLastWin32Error());

            result = AdjustTokenPrivileges(tokenHandle, false, ref tokenPrivileges, 0, IntPtr.Zero, IntPtr.Zero);

            if (result == 0)
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        public static void Standby()
        {
            Application.SetSuspendState(PowerState.Suspend, true, true);
        }
        //-------------------------------------------


        public Switch()
        {
            InitializeComponent();
            this.tmrSys.Start();

            //SetWindowPos(this.Handle, (System.IntPtr)HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);

            #region Sticky Window
            Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, Width - -1, Height - -1, 5, 5));
            Blue.Windows.StickyWindow SW = new StickyWindow(this);
            SW.StickToScreen = true;
            SW.StickToOther = true;
            SW.StickOnResize = true;
            SW.StickOnMove = true;
            int screenWidth = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea.Width;
            int screenHeight = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea.Height;
            this.Left = screenWidth - this.Width;
            this.Top = screenHeight - this.Height;
            AnimateWindow(this.Handle, 1500, AnimateWindowFlags.AW_BLEND);
            #endregion

            //Double Buffer the form controls
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            
        }

        private void Switch_Load(object sender, EventArgs e)
        {
            try
            {
            this.BusyBar.Start();
            startSF();
            startDL();
            startMirror();
            tbCountdown_Click(sender, e);
            SelectQuery Query = new SelectQuery("SELECT LastBootUpTime FROM Win32_OperatingSystem WHERE Primary='true'");
            ManagementObjectSearcher Searcher = new ManagementObjectSearcher(Query);
            foreach (ManagementObject MngObj in Searcher.Get())
            {
                SysBootDT = ManagementDateTimeConverter.ToDateTime(MngObj.Properties["LastBootUpTime"].Value.ToString());
                lblBoot.Text = "System Boot Time : "+ SysBootDT.ToString("T");
                lblBootDate.Text = "System Boot Date : " + SysBootDT.ToString("D");
            }
            }
            catch {
                MessageBox.Show("Error!", "Switch", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
            }

            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
                {
                    key.SetValue("Switch", "\"" + Application.ExecutablePath + "\"");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unable to add 'Switch' at Windows Start Up.\n" + ex.ToString(), "Switch", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
            }
        }

        private void startMirror()
        {
            try
            {
                _VServer = new ImageStreamer(SystemInformation.PrimaryMonitorSize.Width, SystemInformation.PrimaryMonitorSize.Height, 9999);
                _VServer.Start(9999);
            }
            catch
            {
                MessageBox.Show("Error!", "Switch", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
            }
        }

        private void tmrSys_Tick(object sender, EventArgs e)
        {
            lblSysHH.Text = DateTime.Now.ToString("hh") + " :";
            lblSysMM.Text = DateTime.Now.ToString("mm") + " :";
            lblSysSS.Text = DateTime.Now.ToString("ss"); 
            lblSysAP.Text = DateTime.Now.ToString("tt");
            lblYr.Text = DateTime.Now.Year.ToString();
            lblMon.Text = DateTime.Now.ToString("M").ToUpper().ToString();
            lblDay.Text = Convert.ToString(DateTime.Now.DayOfWeek).Substring(0, 3).ToUpper();

            if (tmrCD.Enabled == true || tmrDL.Enabled == true || tmrSF.Enabled == true)
            {
                BusyBar.ForeColor = Color.MediumSeaGreen;
            }
            else
                BusyBar.ForeColor = Color.Crimson;
        }

        private void pbClose_MouseEnter(object sender, EventArgs e)
        {
            this.pbClose.Image = SwitchPlus.Properties.Resources.CloseHover;
        }

        private void pbClose_MouseDown(object sender, MouseEventArgs e)
        {
            this.pbClose.Image = SwitchPlus.Properties.Resources.ClosePress;
        }

        private void pbClose_MouseUp(object sender, MouseEventArgs e)
        {
            this.pbClose.Image = SwitchPlus.Properties.Resources.CloseNormal;
        }

        private void pbClose_MouseLeave(object sender, EventArgs e)
        {
            this.pbClose.Image = SwitchPlus.Properties.Resources.CloseNormal;
        }

        private void pbClose_MouseHover(object sender, EventArgs e)
        {
            this.pbClose.Image = SwitchPlus.Properties.Resources.CloseHover;
        }

        private void pbMin_MouseEnter(object sender, EventArgs e)
        {
            this.pbMin.Image = SwitchPlus.Properties.Resources.MinHover;
        }

        private void pbMin_MouseHover(object sender, EventArgs e)
        {
            this.pbMin.Image = SwitchPlus.Properties.Resources.MinHover;
        }

        private void pbMin_MouseLeave(object sender, EventArgs e)
        {
            this.pbMin.Image = SwitchPlus.Properties.Resources.MinNormal;
        }

        private void pbMin_MouseDown(object sender, MouseEventArgs e)
        {
            this.pbMin.Image = SwitchPlus.Properties.Resources.MinPress;
        }

        private void pbMin_MouseUp(object sender, MouseEventArgs e)
        {
            this.pbMin.Image = SwitchPlus.Properties.Resources.MinNormal;
        }

        private void pbClose_Click(object sender, EventArgs e)
        {
            this.Hide();
            this.Visible = false;
        }

        private void pbMin_Click(object sender, EventArgs e)
        {
            this.Visible = false;
        }

        private void pbShutdown_MouseEnter(object sender, EventArgs e)
        {
            this.pbShutdown.Image = SwitchPlus.Properties.Resources.ShutdownHover;
        }

        private void pbShutdown_MouseHover(object sender, EventArgs e)
        {
            this.pbShutdown.Image = SwitchPlus.Properties.Resources.ShutdownHover;
        }

        private void pbShutdown_MouseDown(object sender, MouseEventArgs e)
        {
            this.pbShutdown.Image = SwitchPlus.Properties.Resources.ShutdownPress;
        }

        private void pbShutdown_MouseUp(object sender, MouseEventArgs e)
        {
            this.pbShutdown.Image = SwitchPlus.Properties.Resources.Shutdown;
        }

        private void pbShutdown_MouseLeave(object sender, EventArgs e)
        {
            if (SwitchPlus.Program.selected_CD)
            {
                if (SwitchPlus.Program.action_CD == 1)
                    this.pbShutdown.Image = SwitchPlus.Properties.Resources.Shutdown;
                else
                    this.pbShutdown.Image = SwitchPlus.Properties.Resources.ShutdownNormal;
            }
            else if (SwitchPlus.Program.selected_SF)
            {
                if (SwitchPlus.Program.action_SF == 1)
                    this.pbShutdown.Image = SwitchPlus.Properties.Resources.Shutdown;
                else
                    this.pbShutdown.Image = SwitchPlus.Properties.Resources.ShutdownNormal;
            }
            else if (SwitchPlus.Program.selected_DL)
            {
                if (SwitchPlus.Program.action_DL == 1)
                    this.pbShutdown.Image = SwitchPlus.Properties.Resources.Shutdown;
                else
                    this.pbShutdown.Image = SwitchPlus.Properties.Resources.ShutdownNormal;
            }
        }

        private void pbRestart_MouseDown(object sender, MouseEventArgs e)
        {
            this.pbRestart.Image = SwitchPlus.Properties.Resources.RestartPressed;

        }

        private void pbRestart_MouseEnter(object sender, EventArgs e)
        {
            this.pbRestart.Image = SwitchPlus.Properties.Resources.RestartHover;
        }

        private void pbRestart_MouseHover(object sender, EventArgs e)
        {
            this.pbRestart.Image = SwitchPlus.Properties.Resources.RestartHover;
        }

        private void pbRestart_MouseLeave(object sender, EventArgs e)
        {
            if (SwitchPlus.Program.selected_CD)
            {
                if (SwitchPlus.Program.action_CD == 2)
                    this.pbRestart.Image = SwitchPlus.Properties.Resources.Restart;
                else
                    this.pbRestart.Image = SwitchPlus.Properties.Resources.RestartNormal;
            }
            else if (SwitchPlus.Program.selected_SF)
            {
                if (SwitchPlus.Program.action_SF == 2)
                    this.pbRestart.Image = SwitchPlus.Properties.Resources.Restart;
                else
                    this.pbRestart.Image = SwitchPlus.Properties.Resources.RestartNormal;
            }
            else if (SwitchPlus.Program.selected_DL)
            {
                if (SwitchPlus.Program.action_DL == 2)
                    this.pbRestart.Image = SwitchPlus.Properties.Resources.Restart;
                else
                    this.pbRestart.Image = SwitchPlus.Properties.Resources.RestartNormal;
            }
        }

        private void pbRestart_MouseUp(object sender, MouseEventArgs e)
        {
            this.pbRestart.Image = SwitchPlus.Properties.Resources.Restart;
        }

        private void pbSleep_MouseDown(object sender, MouseEventArgs e)
        {
            this.pbSleep.Image = SwitchPlus.Properties.Resources.SleepPressed;
        }

        private void pbSleep_MouseEnter(object sender, EventArgs e)
        {
            this.pbSleep.Image = SwitchPlus.Properties.Resources.SleepHover;
        }

        private void pbSleep_MouseHover(object sender, EventArgs e)
        {
            this.pbSleep.Image = SwitchPlus.Properties.Resources.SleepHover;
        }

        private void pbSleep_MouseLeave(object sender, EventArgs e)
        {
            if (SwitchPlus.Program.selected_CD)
            {
                if (SwitchPlus.Program.action_CD == 3)
                    this.pbSleep.Image = SwitchPlus.Properties.Resources.Sleep;
                else
                    this.pbSleep.Image = SwitchPlus.Properties.Resources.SleepNormal;
            }
            else if (SwitchPlus.Program.selected_SF)
            {
                if (SwitchPlus.Program.action_SF == 3)
                    this.pbSleep.Image = SwitchPlus.Properties.Resources.Sleep;
                else
                    this.pbSleep.Image = SwitchPlus.Properties.Resources.SleepNormal;
            }
            else if (SwitchPlus.Program.selected_DL)
            {
                if (SwitchPlus.Program.action_DL == 3)
                    this.pbSleep.Image = SwitchPlus.Properties.Resources.Sleep;
                else
                    this.pbSleep.Image = SwitchPlus.Properties.Resources.SleepNormal;
            }
        }

        private void pbSleep_MouseUp(object sender, MouseEventArgs e)
        {
            this.pbSleep.Image = SwitchPlus.Properties.Resources.Sleep;
        }

        private void pbHibernate_MouseDown(object sender, MouseEventArgs e)
        {
            this.pbHibernate.Image = SwitchPlus.Properties.Resources.HibernatePressed;
        }

        private void pbHibernate_MouseEnter(object sender, EventArgs e)
        {
            this.pbHibernate.Image = SwitchPlus.Properties.Resources.HibernateHover;
        }

        private void pbHibernate_MouseHover(object sender, EventArgs e)
        {
            this.pbHibernate.Image = SwitchPlus.Properties.Resources.HibernateHover;
        }

        private void pbHibernate_MouseLeave(object sender, EventArgs e)
        {
            if (SwitchPlus.Program.selected_CD)
            {
                if (SwitchPlus.Program.action_CD == 4)
                    this.pbHibernate.Image = SwitchPlus.Properties.Resources.Hibernate;
                else
                    this.pbHibernate.Image = SwitchPlus.Properties.Resources.HibernateNormal;
            }
            else if (SwitchPlus.Program.selected_SF)
            {
                if (SwitchPlus.Program.action_SF == 4)
                    this.pbHibernate.Image = SwitchPlus.Properties.Resources.Hibernate;
                else
                    this.pbHibernate.Image = SwitchPlus.Properties.Resources.HibernateNormal;
            }
            else if (SwitchPlus.Program.selected_DL)
            {
                if (SwitchPlus.Program.action_DL == 4)
                    this.pbHibernate.Image = SwitchPlus.Properties.Resources.Hibernate;
                else
                    this.pbHibernate.Image = SwitchPlus.Properties.Resources.HibernateNormal;
            }
        }

        private void pbHibernate_MouseUp(object sender, MouseEventArgs e)
        {
            this.pbHibernate.Image = SwitchPlus.Properties.Resources.Hibernate;
        }

        private void pbLogoff_MouseDown(object sender, MouseEventArgs e)
        {
            this.pbLogoff.Image = SwitchPlus.Properties.Resources.LogoffPressed;
        }

        private void pbLogoff_MouseEnter(object sender, EventArgs e)
        {
            this.pbLogoff.Image = SwitchPlus.Properties.Resources.LogoffHover;
        }
        private void pbLogoff_MouseHover(object sender, EventArgs e)
        {
            this.pbLogoff.Image = SwitchPlus.Properties.Resources.LogoffHover;
        }

        private void pbLogoff_MouseUp(object sender, MouseEventArgs e)
        {
            this.pbLogoff.Image = SwitchPlus.Properties.Resources.Logoff;
        }

        private void pbLogoff_MouseLeave(object sender, EventArgs e)
        {
            if (SwitchPlus.Program.selected_CD)
            {
                if (SwitchPlus.Program.action_CD == 5)
                    this.pbLogoff.Image = SwitchPlus.Properties.Resources.Logoff;
                else
                    this.pbLogoff.Image = SwitchPlus.Properties.Resources.LogoffNormal;
            }
            else if (SwitchPlus.Program.selected_SF)
            {
                if (SwitchPlus.Program.action_SF == 5)
                    this.pbLogoff.Image = SwitchPlus.Properties.Resources.Logoff;
                else
                    this.pbLogoff.Image = SwitchPlus.Properties.Resources.LogoffNormal;
            }
            else if (SwitchPlus.Program.selected_DL)
            {
                if (SwitchPlus.Program.action_DL == 5)
                    this.pbLogoff.Image = SwitchPlus.Properties.Resources.Logoff;
                else
                    this.pbLogoff.Image = SwitchPlus.Properties.Resources.LogoffNormal;
            }
        }

        private void pbLock_MouseDown(object sender, MouseEventArgs e)
        {
            this.pbLock.Image = SwitchPlus.Properties.Resources.LockPressed;
        }

        private void pbLock_MouseEnter(object sender, EventArgs e)
        {
            this.pbLock.Image = SwitchPlus.Properties.Resources.LockHover;
        }

        private void pbLock_MouseHover(object sender, EventArgs e)
        {
            this.pbLock.Image = SwitchPlus.Properties.Resources.LockHover;
        }

        private void pbLock_MouseLeave(object sender, EventArgs e)
        {
            if (SwitchPlus.Program.selected_CD)
            {
                if (SwitchPlus.Program.action_CD == 6)
                    this.pbLock.Image = SwitchPlus.Properties.Resources.Lock;
                else
                    this.pbLock.Image = SwitchPlus.Properties.Resources.LockNormal;
            }
            else if (SwitchPlus.Program.selected_SF)
            {
                if (SwitchPlus.Program.action_SF == 6)
                    this.pbLock.Image = SwitchPlus.Properties.Resources.Lock;
                else
                    this.pbLock.Image = SwitchPlus.Properties.Resources.LockNormal;
            }
            else if (SwitchPlus.Program.selected_DL)
            {
                if (SwitchPlus.Program.action_DL == 6)
                    this.pbLock.Image = SwitchPlus.Properties.Resources.Lock;
                else
                    this.pbLock.Image = SwitchPlus.Properties.Resources.LockNormal;
            }
        }

        private void pbLock_MouseUp(object sender, MouseEventArgs e)
        {
            this.pbLock.Image = SwitchPlus.Properties.Resources.Lock;
        }

        private void rbCountDown_MouseHover(object sender, EventArgs e)
        {
            if (SwitchPlus.Program.brutally_CD == true)
                this.rbCountDown.Image = SwitchPlus.Properties.Resources.RadioButtonAH;
            else
                this.rbCountDown.Image = SwitchPlus.Properties.Resources.RadioButtonDH;
        }

        private void rbCountDown_MouseEnter(object sender, EventArgs e)
        {
            this.pbBruteInfo.Visible = true;
            if (SwitchPlus.Program.brutally_CD == true)
                this.rbCountDown.Image = SwitchPlus.Properties.Resources.RadioButtonAH;
            else
                this.rbCountDown.Image = SwitchPlus.Properties.Resources.RadioButtonDH;
        }

        private void rbCountDown_Click(object sender, EventArgs e)
        {
            if (SwitchPlus.Program.brutally_CD == false)
            {
                SwitchPlus.Program.brutally_CD = true;
                this.rbCountDown.Image = SwitchPlus.Properties.Resources.RadioButtonA;
            }
            else
            {
                SwitchPlus.Program.brutally_CD = false;
                this.rbCountDown.Image = SwitchPlus.Properties.Resources.RadioButtonA;
            }

        }

        private void btnCountReset_MouseDown(object sender, MouseEventArgs e)
        {
            this.btnCountReset.Image = SwitchPlus.Properties.Resources.BtnResetPressed;
        }

        private void btnCountReset_MouseEnter(object sender, EventArgs e)
        {
            this.btnCountReset.Image = SwitchPlus.Properties.Resources.BtnResetHover;
            pbResetInfo.Visible = true;
        }

        private void btnCountReset_MouseHover(object sender, EventArgs e)
        {
            this.btnCountReset.Image = SwitchPlus.Properties.Resources.BtnResetHover;
            pbResetInfo.Visible = true;
        }

        private void btnCountReset_MouseLeave(object sender, EventArgs e)
        {
            this.btnCountReset.Image = SwitchPlus.Properties.Resources.BtnReset;
            pbResetInfo.Visible = false;
        }

        private void btnCountReset_MouseUp(object sender, MouseEventArgs e)
        {
            this.btnCountReset.Image = SwitchPlus.Properties.Resources.BtnResetHover;
        }

        private void btnCountStart_MouseDown(object sender, MouseEventArgs e)
        {
            this.btnCountStart.Image = SwitchPlus.Properties.Resources.BtnStartPressed;
        }

        private void btnCountStart_MouseEnter(object sender, EventArgs e)
        {
            this.btnCountStart.Image = SwitchPlus.Properties.Resources.BtnStartFocus;
            this.pbCDStartInfo.Visible = true;
        }

        private void btnCountStart_MouseHover(object sender, EventArgs e)
        {
            this.btnCountStart.Image = SwitchPlus.Properties.Resources.BtnStartFocus;
            this.pbCDStartInfo.Visible = true;
        }

        private void btnCountStart_MouseLeave(object sender, EventArgs e)
        {
            this.btnCountStart.Image = SwitchPlus.Properties.Resources.BtnStart;
            this.pbCDStartInfo.Visible = false;
        }

        private void btnCountStart_MouseUp(object sender, MouseEventArgs e)
        {
            this.btnCountStart.Image = SwitchPlus.Properties.Resources.BtnStartFocus;
        }

        private void rbCountMode_Click(object sender, EventArgs e)
        {
            if (pnlHhMmSs == true)
            {
                pnlHhMmSs = false;
                this.pnlCount.BackgroundImage = SwitchPlus.Properties.Resources.Panel_New;
                this.pnlHHMMSS.Visible = false;
                this.dtpCountDown.Visible = true;
                this.rbCountMode.Image = SwitchPlus.Properties.Resources.rb2;
                this.pbCDIcon.Image = SwitchPlus.Properties.Resources.ClockSmall;

                // Resetting to Current DateTime
                this.dtpCountDown.Value = System.DateTime.Now;

            }
            else
            {
                pnlHhMmSs = true;
                this.pnlCount.BackgroundImage = SwitchPlus.Properties.Resources.PnlCountdown;
                this.pnlHHMMSS.Visible = true;
                this.dtpCountDown.Visible = false;
                this.rbCountMode.Image = SwitchPlus.Properties.Resources.rb2;
                this.pbCDIcon.Image = SwitchPlus.Properties.Resources.TimerSmall;
            }
        }

        private void rbCountMode_MouseEnter(object sender, EventArgs e)
        {
            if (pnlHhMmSs == true)
                this.rbCountMode.Image = SwitchPlus.Properties.Resources.rb1A;
            else
                this.rbCountMode.Image = SwitchPlus.Properties.Resources.rb2A;
            this.pbTimerMode.Visible = true;
        }

        private void rbCountMode_MouseHover(object sender, EventArgs e)
        {
            if (pnlHhMmSs == true)
                this.rbCountMode.Image = SwitchPlus.Properties.Resources.rb1A;
            else
                this.rbCountMode.Image = SwitchPlus.Properties.Resources.rb2A;
            this.pbTimerMode.Visible = true;
        }

        private void rbCountMode_MouseDown(object sender, MouseEventArgs e)
        {
            if (pnlHhMmSs == true)
                this.rbCountMode.Image = SwitchPlus.Properties.Resources.rb1A;
            else
                this.rbCountMode.Image = SwitchPlus.Properties.Resources.rb2A;
        }

        private void rbCountMode_MouseLeave(object sender, EventArgs e)
        {
            if (pnlHhMmSs == true)
                this.rbCountMode.Image = SwitchPlus.Properties.Resources.rb1;
            else
                this.rbCountMode.Image = SwitchPlus.Properties.Resources.rb2;
            this.pbTimerMode.Visible = false;
        }

        private void rbCountMode_MouseUp(object sender, MouseEventArgs e)
        {
            if (pnlHhMmSs == true)
                this.rbCountMode.Image = SwitchPlus.Properties.Resources.rb1;
            else
                this.rbCountMode.Image = SwitchPlus.Properties.Resources.rb2;
        }

        private void rbCountDown_MouseLeave(object sender, EventArgs e)
        {
            this.pbBruteInfo.Visible = false;
            if (SwitchPlus.Program.brutally_CD == true)
                this.rbCountDown.Image = SwitchPlus.Properties.Resources.RadioButtonA;
            else
                this.rbCountDown.Image = SwitchPlus.Properties.Resources.RadioButtonD;
        }

        private void rbCountDown_MouseHover_1(object sender, EventArgs e)
        {
            this.pbBruteInfo.Visible = true;
            if (SwitchPlus.Program.brutally_CD == true)
                this.rbCountDown.Image = SwitchPlus.Properties.Resources.RadioButtonAH;
            else
                this.rbCountDown.Image = SwitchPlus.Properties.Resources.RadioButtonDH;
        }

        private void rbCountDown_MouseDown_1(object sender, MouseEventArgs e)
        {
            if (SwitchPlus.Program.brutally_CD == true)
                this.rbCountDown.Image = SwitchPlus.Properties.Resources.RadioButtonAP;
            else
                this.rbCountDown.Image = SwitchPlus.Properties.Resources.RadioButtonDP;
        }

        private void rbCountDown_MouseUp(object sender, MouseEventArgs e)
        {
            if (SwitchPlus.Program.brutally_CD == true)
                this.rbCountDown.Image = SwitchPlus.Properties.Resources.RadioButtonA;
            else
                this.rbCountDown.Image = SwitchPlus.Properties.Resources.RadioButtonD;
        }

        private void rbDaily_MouseDown(object sender, MouseEventArgs e)
        {
            if (SwitchPlus.Program.brutally_DL == true)
                this.rbDaily.Image = SwitchPlus.Properties.Resources.RadioButtonAP;
            else
                this.rbDaily.Image = SwitchPlus.Properties.Resources.RadioButtonDP;
        }

        private void rbDaily_MouseEnter(object sender, EventArgs e)
        {
            this.pbBruteInfo.Visible = true;
            if (SwitchPlus.Program.brutally_DL == true)
                this.rbDaily.Image = SwitchPlus.Properties.Resources.RadioButtonAH;
            else
                this.rbDaily.Image = SwitchPlus.Properties.Resources.RadioButtonDH;
        }

        private void rbDaily_MouseHover(object sender, EventArgs e)
        {
            this.pbBruteInfo.Visible = true;
            if (SwitchPlus.Program.brutally_DL == true)
                this.rbDaily.Image = SwitchPlus.Properties.Resources.RadioButtonAH;
            else
                this.rbDaily.Image = SwitchPlus.Properties.Resources.RadioButtonDH;
        }

        private void rbDaily_MouseLeave(object sender, EventArgs e)
        {
            this.pbBruteInfo.Visible = false;
            if (SwitchPlus.Program.brutally_DL == true)
                this.rbDaily.Image = SwitchPlus.Properties.Resources.RadioButtonA;
            else
                this.rbDaily.Image = SwitchPlus.Properties.Resources.RadioButtonD;
        }

        private void rbDaily_MouseUp(object sender, MouseEventArgs e)
        {
            if (SwitchPlus.Program.brutally_DL == true)
                this.rbDaily.Image = SwitchPlus.Properties.Resources.RadioButtonA;
            else
                this.rbDaily.Image = SwitchPlus.Properties.Resources.RadioButtonD;
        }

        private void rbDaily_Click(object sender, EventArgs e)
        {
            if (SwitchPlus.Program.brutally_DL)
            {
                SwitchPlus.Program.brutally_DL = false;
                resetBrutallyDL();
                this.rbDaily.Image = SwitchPlus.Properties.Resources.RadioButtonD;
            }
            else
            {
                SwitchPlus.Program.brutally_DL = true;
                setBrutallyDL();
                this.rbDaily.Image = SwitchPlus.Properties.Resources.RadioButtonA;
            }
        }
        private void setBrutallyDL()
        {
            SwitchPlus.Program.DL.SetValue("isBrutally", "1", RegistryValueKind.String);
        }
        private void resetBrutallyDL()
        {
            SwitchPlus.Program.DL.SetValue("isBrutally", "0", RegistryValueKind.String);
        }

        private void btnDailyReset_MouseDown(object sender, MouseEventArgs e)
        {
            this.btnDailyReset.Image = SwitchPlus.Properties.Resources.BtnResetPressed;
        }

        private void btnDailyReset_MouseEnter(object sender, EventArgs e)
        {
            this.btnDailyReset.Image = SwitchPlus.Properties.Resources.BtnResetHover;
            pbResetInfo.Visible = true;
        }

        private void btnDailyReset_MouseHover(object sender, EventArgs e)
        {
            this.btnDailyReset.Image = SwitchPlus.Properties.Resources.BtnResetHover;
            pbResetInfo.Visible = true;
        }

        private void btnDailyReset_MouseLeave(object sender, EventArgs e)
        {
            this.btnDailyReset.Image = SwitchPlus.Properties.Resources.BtnReset;
            pbResetInfo.Visible = false;
        }

        private void btnDailyReset_MouseUp(object sender, MouseEventArgs e)
        {
            this.btnDailyReset.Image = SwitchPlus.Properties.Resources.BtnResetHover;
        }

        private void btnDailyStart_MouseDown(object sender, MouseEventArgs e)
        {
            this.btnDailyStart.Image = SwitchPlus.Properties.Resources.BtnStartPressed;
        }

        private void btnDailyStart_MouseEnter(object sender, EventArgs e)
        {
            this.btnDailyStart.Image = SwitchPlus.Properties.Resources.BtnStartFocus;
            this.pbCDStartInfo.Visible = true;
        }

        private void btnDailyStart_MouseHover(object sender, EventArgs e)
        {
            this.btnDailyStart.Image = SwitchPlus.Properties.Resources.BtnStartFocus;
            this.pbCDStartInfo.Visible = true;
        }

        private void btnDailyStart_MouseUp(object sender, MouseEventArgs e)
        {
            this.btnDailyStart.Image = SwitchPlus.Properties.Resources.BtnStartFocus;
        }

        private void btnDailyStart_MouseLeave(object sender, EventArgs e)
        {
            this.btnDailyStart.Image = SwitchPlus.Properties.Resources.BtnStart;
            this.pbCDStartInfo.Visible = false;
        }

        private void rbScheduleFor_Click(object sender, EventArgs e)
        {
            if (SwitchPlus.Program.brutally_SF)
            {
                SwitchPlus.Program.brutally_SF = false;
                resetBrutallySF();
                this.rbScheduleFor.Image = SwitchPlus.Properties.Resources.RadioButtonD;
            }
            else
            {
                SwitchPlus.Program.brutally_SF = true;
                setBrutallySF();
                this.rbScheduleFor.Image = SwitchPlus.Properties.Resources.RadioButtonA;
            }
        }

        private void rbScheduleFor_MouseDown(object sender, MouseEventArgs e)
        {
            if (SwitchPlus.Program.brutally_SF == true)
                this.rbScheduleFor.Image = SwitchPlus.Properties.Resources.RadioButtonAP;
            else
                this.rbScheduleFor.Image = SwitchPlus.Properties.Resources.RadioButtonDP;
        }

        private void rbScheduleFor_MouseEnter(object sender, EventArgs e)
        {
            this.pbBruteInfo.Visible = true;
            if (SwitchPlus.Program.brutally_SF == true)
                this.rbScheduleFor.Image = SwitchPlus.Properties.Resources.RadioButtonAH;
            else
                this.rbScheduleFor.Image = SwitchPlus.Properties.Resources.RadioButtonDH;
        }

        private void rbScheduleFor_MouseHover(object sender, EventArgs e)
        {
            this.pbBruteInfo.Visible = true;
            if (SwitchPlus.Program.brutally_SF == true)
                this.rbScheduleFor.Image = SwitchPlus.Properties.Resources.RadioButtonAH;
            else
                this.rbScheduleFor.Image = SwitchPlus.Properties.Resources.RadioButtonDH;
        }

        private void rbScheduleFor_MouseLeave(object sender, EventArgs e)
        {
            this.pbBruteInfo.Visible = false;
            if (SwitchPlus.Program.brutally_SF == true)
                this.rbScheduleFor.Image = SwitchPlus.Properties.Resources.RadioButtonA;
            else
                this.rbScheduleFor.Image = SwitchPlus.Properties.Resources.RadioButtonD;
        }

        private void rbScheduleFor_MouseUp(object sender, MouseEventArgs e)
        {
            if (SwitchPlus.Program.brutally_SF == true)
                this.rbScheduleFor.Image = SwitchPlus.Properties.Resources.RadioButtonA;
            else
                this.rbScheduleFor.Image = SwitchPlus.Properties.Resources.RadioButtonD;
        }

        private void btnScheduleReset_MouseDown(object sender, MouseEventArgs e)
        {
            this.btnScheduleReset.Image = SwitchPlus.Properties.Resources.BtnResetPressed;
        }

        private void btnScheduleReset_MouseEnter(object sender, EventArgs e)
        {
            this.btnScheduleReset.Image = SwitchPlus.Properties.Resources.BtnResetHover;
            pbResetInfo.Visible = true;
        }

        private void btnScheduleReset_MouseHover(object sender, EventArgs e)
        {
            this.btnScheduleReset.Image = SwitchPlus.Properties.Resources.BtnResetHover;
            pbResetInfo.Visible = true;
        }

        private void btnScheduleReset_MouseLeave(object sender, EventArgs e)
        {
            this.btnScheduleReset.Image = SwitchPlus.Properties.Resources.BtnReset;
            pbResetInfo.Visible = false;
        }

        private void btnScheduleReset_MouseUp(object sender, MouseEventArgs e)
        {
            this.btnScheduleReset.Image = SwitchPlus.Properties.Resources.BtnResetHover;
        }

        private void btnScheduleStart_MouseDown(object sender, MouseEventArgs e)
        {
            this.btnScheduleStart.Image = SwitchPlus.Properties.Resources.BtnStartPressed;
        }

        private void btnScheduleStart_MouseEnter(object sender, EventArgs e)
        {
            this.btnScheduleStart.Image = SwitchPlus.Properties.Resources.BtnStartFocus;
            this.pbCDStartInfo.Visible = true;
        }

        private void btnScheduleStart_MouseHover(object sender, EventArgs e)
        {
            this.btnScheduleStart.Image = SwitchPlus.Properties.Resources.BtnStartFocus;
            this.pbCDStartInfo.Visible = true;
        }

        private void btnScheduleStart_MouseLeave(object sender, EventArgs e)
        {
            this.btnScheduleStart.Image = SwitchPlus.Properties.Resources.BtnStart;
            this.pbCDStartInfo.Visible = false;
        }

        private void btnScheduleStart_MouseUp(object sender, MouseEventArgs e)
        {
            this.btnScheduleStart.Image = SwitchPlus.Properties.Resources.BtnStartFocus;
        }

        private void btnCountStart_Click(object sender, EventArgs e)
        {
            if (this.countDownDTP_Validate())
            {
                if (validateAction())
                {
                    this.tmrCD.Start();
                    pgrBrCD.Visible = true;
                    SwitchPlus.Program.isActive_CD = true;
                    this.lblCountdownAction.Text = SwitchPlus.Program.getTaskName(SwitchPlus.Program.action_CD).ToString();
                    this.pnlCDInfo.Visible = false;
                    this.pnlCDStatus.Visible = true;
                    this.pnlCount.Enabled = false;
                    this.pnlAction.Enabled = false;
                    this.pnlCountDown.BackgroundImage = SwitchPlus.Properties.Resources.PanelGreen;
                    this.pidCD.Image = SwitchPlus.Properties.Resources.G;
                    // Code For Progress Bar
                    long tm = SwitchPlus.Program.time_CD.Ticks - System.DateTime.Now.Ticks;
                    TimeSpan ts = new TimeSpan(tm);
                    pgrBrCD.Visible = true;
                    pgrBrCD.Value = 0;
                    pgrBrCD.MinValue = 0;
                    pgrBrCD.MaxValue = Convert.ToInt32(ts.TotalSeconds - 1);
                    //----------------------
                }
            }
        }

        public bool countDownDTP_Validate()
        {
            if (pnlHhMmSs == true)
            {
                if (this.numHH.Value == 0 && this.numMM.Value == 0 && this.numSS.Value == 0)
                {
                    MessageBox.Show("Invalid Time! Countdown Time can't be 00:00:00", "Switch", MessageBoxButtons.OK, MessageBoxIcon.Exclamation,
                         MessageBoxDefaultButton.Button1);
                    numHH.Focus();
                    return false;
                }
                else
                {
                    this.dtpCountDown.Value = DateTime.Now;
                    this.dtpCountDown.Value = DateTime.Now.AddHours(Convert.ToDouble(this.numHH.Value)).AddMinutes(Convert.ToDouble(this.numMM.Value)).AddSeconds(Convert.ToDouble(this.numSS.Value));
                    SwitchPlus.Program.time_CD = this.dtpCountDown.Value;
                }
            }
            else
            {
                SwitchPlus.Program.time_CD = this.dtpCountDown.Value;
            }

            if (SwitchPlus.Program.time_CD.CompareTo(System.DateTime.Now) < 0)
            {
                MessageBox.Show("Invalid Time! Please choose time greater than your current system time.", "Switch", MessageBoxButtons.OK, MessageBoxIcon.Exclamation,
                         MessageBoxDefaultButton.Button1);
                this.dtpCountDown.Value = System.DateTime.Now;
                return false;
            }
            else
                return true;
        }

        // Selected Action Code ---------
        public void select_Shutdown()
        {
            this.pbShutdown.Image = SwitchPlus.Properties.Resources.Shutdown;
            this.pbRestart.Image = SwitchPlus.Properties.Resources.RestartNormal;
            this.pbSleep.Image = SwitchPlus.Properties.Resources.SleepNormal;
            this.pbHibernate.Image = SwitchPlus.Properties.Resources.HibernateNormal;
            this.pbLogoff.Image = SwitchPlus.Properties.Resources.LogoffNormal;
            this.pbLock.Image = SwitchPlus.Properties.Resources.LockNormal;
        }

        public void select_Restart()
        {
            this.pbShutdown.Image = SwitchPlus.Properties.Resources.ShutdownNormal;
            this.pbRestart.Image = SwitchPlus.Properties.Resources.Restart;
            this.pbSleep.Image = SwitchPlus.Properties.Resources.SleepNormal;
            this.pbHibernate.Image = SwitchPlus.Properties.Resources.HibernateNormal;
            this.pbLogoff.Image = SwitchPlus.Properties.Resources.LogoffNormal;
            this.pbLock.Image = SwitchPlus.Properties.Resources.LockNormal;
        }

        public void select_Sleep()
        {
            this.pbShutdown.Image = SwitchPlus.Properties.Resources.ShutdownNormal;
            this.pbRestart.Image = SwitchPlus.Properties.Resources.RestartNormal;
            this.pbSleep.Image = SwitchPlus.Properties.Resources.Sleep;
            this.pbHibernate.Image = SwitchPlus.Properties.Resources.HibernateNormal;
            this.pbLogoff.Image = SwitchPlus.Properties.Resources.LogoffNormal;
            this.pbLock.Image = SwitchPlus.Properties.Resources.LockNormal;
        }

        public void select_Hibernate()
        {
            this.pbShutdown.Image = SwitchPlus.Properties.Resources.ShutdownNormal;
            this.pbRestart.Image = SwitchPlus.Properties.Resources.RestartNormal;
            this.pbSleep.Image = SwitchPlus.Properties.Resources.SleepNormal;
            this.pbHibernate.Image = SwitchPlus.Properties.Resources.Hibernate;
            this.pbLogoff.Image = SwitchPlus.Properties.Resources.LogoffNormal;
            this.pbLock.Image = SwitchPlus.Properties.Resources.LockNormal;
        }

        public void select_Logoff()
        {
            this.pbShutdown.Image = SwitchPlus.Properties.Resources.ShutdownNormal;
            this.pbRestart.Image = SwitchPlus.Properties.Resources.RestartNormal;
            this.pbSleep.Image = SwitchPlus.Properties.Resources.SleepNormal;
            this.pbHibernate.Image = SwitchPlus.Properties.Resources.HibernateNormal;
            this.pbLogoff.Image = SwitchPlus.Properties.Resources.Logoff;
            this.pbLock.Image = SwitchPlus.Properties.Resources.LockNormal;
        }

        public void select_Lock()
        {
            this.pbShutdown.Image = SwitchPlus.Properties.Resources.ShutdownNormal;
            this.pbRestart.Image = SwitchPlus.Properties.Resources.RestartNormal;
            this.pbSleep.Image = SwitchPlus.Properties.Resources.SleepNormal;
            this.pbHibernate.Image = SwitchPlus.Properties.Resources.HibernateNormal;
            this.pbLogoff.Image = SwitchPlus.Properties.Resources.LogoffNormal;
            this.pbLock.Image = SwitchPlus.Properties.Resources.Lock;
        }

        public void deselectAll()
        {
            this.pbShutdown.Image = SwitchPlus.Properties.Resources.ShutdownNormal;
            this.pbRestart.Image = SwitchPlus.Properties.Resources.RestartNormal;
            this.pbSleep.Image = SwitchPlus.Properties.Resources.SleepNormal;
            this.pbHibernate.Image = SwitchPlus.Properties.Resources.HibernateNormal;
            this.pbLogoff.Image = SwitchPlus.Properties.Resources.LogoffNormal;
            this.pbLock.Image = SwitchPlus.Properties.Resources.LockNormal;
        }

        private void tbCountdown_MouseEnter(object sender, EventArgs e)
        {
            if (SwitchPlus.Program.selected_CD)
                this.tbCD.Image = SwitchPlus.Properties.Resources.TabCDActive;
            else
                this.tbCD.Image = SwitchPlus.Properties.Resources.TabCD_H;
        }


        private void tbCountdown_MouseHover(object sender, EventArgs e)
        {
            if (SwitchPlus.Program.selected_CD)
                this.tbCD.Image = SwitchPlus.Properties.Resources.TabCDActive;
            else
                this.tbCD.Image = SwitchPlus.Properties.Resources.TabCD_H;
        }

        private void tbCountdown_MouseLeave(object sender, EventArgs e)
        {
            if (SwitchPlus.Program.selected_CD)
                this.tbCD.Image = SwitchPlus.Properties.Resources.TabCDActive;
            else
                this.tbCD.Image = SwitchPlus.Properties.Resources.TabCD;
        }


        private void tbCountdown_Click(object sender, EventArgs e)
        {
            SwitchPlus.Program.selected_CD = true;
            this.tbCD.Image = SwitchPlus.Properties.Resources.TabCDActive;
            SwitchPlus.Program.selected_SF = false;
            this.tbSF.Image = SwitchPlus.Properties.Resources.TabSF;
            SwitchPlus.Program.selected_DL = false;
            this.tbDL.Image = SwitchPlus.Properties.Resources.TabDL;
            this.pnlCountDown.Visible = true;
            this.pnlScheduled.Visible = false;
            this.pnlDaily.Visible = false;
            if (SwitchPlus.Program.isActive_CD)
            {
                this.pnlAction.Enabled = false;
                this.getAction(SwitchPlus.Program.action_CD);
                pgrBrCD.Visible = true;
            }
            else
            {
                Reset_CD();
            }
        }
        //-------------------------------


        //Get Action Details
        public void getAction(int opt)
        {
            switch (opt)
            {
                case 1:
                    this.select_Shutdown();
                    break;
                case 2:
                    this.select_Restart();
                    break;
                case 3:
                    this.select_Sleep();
                    break;
                case 4:
                    this.select_Hibernate();
                    break;
                case 5:
                    this.select_Logoff();
                    break;
                case 6:
                    this.select_Lock();
                    break;
                default:
                    this.deselectAll();
                    break;
            }
        }

        private void pbShutdown_Click(object sender, EventArgs e)
        {
            if (SwitchPlus.Program.selected_CD == true)
            {
                SwitchPlus.Program.action_CD = 1;
                this.select_Shutdown();
            }
            if (SwitchPlus.Program.selected_SF == true)
            {
                SwitchPlus.Program.action_SF = 1;
                this.select_Shutdown();
            }
            if (SwitchPlus.Program.selected_DL)
            {
                SwitchPlus.Program.action_DL = 1;
                this.select_Shutdown();
            }
        }

        private void pbRestart_Click(object sender, EventArgs e)
        {
            if (SwitchPlus.Program.selected_CD == true)
            {
                SwitchPlus.Program.action_CD = 2;
                this.select_Restart();
            }
            if (SwitchPlus.Program.selected_SF == true)
            {
                SwitchPlus.Program.action_SF = 2;
                this.select_Restart();
            }
            if (SwitchPlus.Program.selected_DL)
            {
                SwitchPlus.Program.action_DL = 2;
                this.select_Restart();
            }
        }

        private void pbSleep_Click(object sender, EventArgs e)
        {
            if (SwitchPlus.Program.selected_CD == true)
            {
                SwitchPlus.Program.action_CD = 3;
                this.select_Sleep();
            }
            if (SwitchPlus.Program.selected_SF == true)
            {
                SwitchPlus.Program.action_SF = 3;
                this.select_Sleep();
            }
            if (SwitchPlus.Program.selected_DL == true)
            {
                SwitchPlus.Program.action_DL = 3;
                this.select_Sleep();
            }
        }

        private void pbHibernate_Click(object sender, EventArgs e)
        {
            if (SwitchPlus.Program.selected_CD == true)
            {
                SwitchPlus.Program.action_CD = 4;
                this.select_Hibernate();
            }
            if (SwitchPlus.Program.selected_SF == true)
            {
                SwitchPlus.Program.action_SF = 4;
                this.select_Hibernate();
            }
            if (SwitchPlus.Program.selected_DL == true)
            {
                SwitchPlus.Program.action_DL = 4;
                this.select_Hibernate();
            }
        }

        private void pbLogoff_Click(object sender, EventArgs e)
        {
            if (SwitchPlus.Program.selected_CD == true)
            {
                SwitchPlus.Program.action_CD = 5;
                this.select_Logoff();
            }
            if (SwitchPlus.Program.selected_SF == true)
            {
                SwitchPlus.Program.action_SF = 5;
                this.select_Logoff();
            }
            if (SwitchPlus.Program.selected_DL == true)
            {
                SwitchPlus.Program.action_DL = 5;
                this.select_Logoff();
            }
        }

        private void pbLock_Click(object sender, EventArgs e)
        {
            if (SwitchPlus.Program.selected_CD == true)
            {
                SwitchPlus.Program.action_CD = 6;
                this.select_Lock();
            }
            if (SwitchPlus.Program.selected_SF == true)
            {
                SwitchPlus.Program.action_SF = 6;
                this.select_Lock();
            }
            if (SwitchPlus.Program.selected_DL == true)
            {
                SwitchPlus.Program.action_DL = 6;
                this.select_Lock();
            }
        }

        private void btnCountReset_Click(object sender, EventArgs e)
        {
            this.dtpCountDown.Value = System.DateTime.Now;
            this.numHH.Value = 0;
            this.numMM.Value = 0;
            this.numSS.Value = 0;
            this.pidCD.Image = SwitchPlus.Properties.Resources.R;
        }

        // Power Task Implementation
        private void brutally()
        {
            System.Diagnostics.Process[] processes = System.Diagnostics.Process.GetProcesses();

            foreach (System.Diagnostics.Process processParent in processes)
            {
                System.Diagnostics.Process[] processNames = System.Diagnostics.Process.GetProcessesByName(processParent.ProcessName);

                foreach (System.Diagnostics.Process processChild in processNames)
                {
                    try
                    {
                        System.IntPtr hWnd = processChild.MainWindowHandle;

                        if (IsIconic(hWnd))
                        {
                            ShowWindowAsync(hWnd, SW_RESTORE);
                        }

                        SetForegroundWindow(hWnd);

                        if (!(processChild.MainWindowTitle.Equals(this.Text)))
                        {
                            processChild.CloseMainWindow();
                            processChild.Kill();
                            processChild.WaitForExit();
                        }
                    }
                    catch (System.Exception exception)
                    {

                    }
                }
            }
        }

        public void Power(int task, bool brute)
        {
            int result;
            switch (task)
            {

                case 1:
                    if (brute)
                    {
                        brutally();
                    }
                    ElevatePrivileges();
                    result = ExitWindowsEx((uint)(ExitFlags.Shutdown | ExitFlags.PowerOff | ExitFlags.Force | ExitFlags.ForceIfHung), (uint)(Reason.HardwareIssue | Reason.PlannedShutdown));
                    if (result == 0)
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }
                    Application.Exit();
                    break;
                case 2:
                    if (brute)
                    {
                        brutally();
                    }
                    ElevatePrivileges();
                    result = ExitWindowsEx((uint)(ExitFlags.Reboot), (uint)(Reason.SoftwareIssue | Reason.PlannedShutdown));
                    Application.Exit();
                    break;
                case 3:
                    Standby();
                    Application.Restart();
                    break;
                case 4:
                    Application.SetSuspendState(PowerState.Hibernate, true, true);
                    break;
                case 5:
                    ExitWindowsEx(0, 0);
                    break;
                case 6:
                    LockWorkStation();
                    break;
            }
        }

        private void tmrCD_Tick(object sender, EventArgs e)
        {
            this.lblCountdownTime.Text = SwitchPlus.Program.time_CD.TimeOfDay.Subtract(System.DateTime.Now.TimeOfDay).ToString();
            pgrBrCD.Value++;
            //if((SwitchPlus.Program.time_CD.Hour == System.DateTime.Now.Hour) && (SwitchPlus.Program.time_CD.Minute == System.DateTime.Now.Minute)&&(SwitchPlus.Program.time_CD.Second == System.DateTime.Now.Second))
            if (SwitchPlus.Program.time_CD.CompareTo(System.DateTime.Now) <= 0)
            {
                this.lblCountdownAction.ResetText();
                this.lblCountdownTime.ResetText();
                //performing Task---------
                Power(SwitchPlus.Program.action_CD, SwitchPlus.Program.brutally_CD);
                //------------------------
                this.tmrCD.Stop();
                Reset_CD();

            }
        }

        private void Reset_CD()
        {
            SwitchPlus.Program.resetCD();
            this.pnlAction.Enabled = true;
            this.deselectAll();
            this.pnlCDInfo.Visible = true;
            this.pnlCDStatus.Visible = false;
            this.pnlCount.Enabled = true;
            pgrBrCD.Visible = false;
            pgrBrCD.Value = 0;
            this.lblCountdownAction.ResetText();
            this.lblCountdownTime.ResetText();
            this.dtpCountDown.Value = System.DateTime.Now;
            this.numHH.Value = 0;
            this.numMM.Value = 0;
            this.numSS.Value = 0;
            this.pnlCountDown.BackgroundImage = SwitchPlus.Properties.Resources.PanelGrey;
            this.pidCD.Image = SwitchPlus.Properties.Resources.R;
        }

        private void btnCountdownStop_Click(object sender, EventArgs e)
        {
            this.tmrCD.Stop();
            Reset_CD();
        }

        private void btnCountdownStop_MouseDown(object sender, MouseEventArgs e)
        {
            this.btnCountdownStop.Image = SwitchPlus.Properties.Resources.BtnStopPressed;
        }

        private void btnCountdownStop_MouseEnter(object sender, EventArgs e)
        {
            this.btnCountdownStop.Image = SwitchPlus.Properties.Resources.BtnStopHover;
            this.pbStopInfo_CD.Visible = true;
        }

        private void btnCountdownStop_MouseHover(object sender, EventArgs e)
        {
            this.btnCountdownStop.Image = SwitchPlus.Properties.Resources.BtnStopHover;
            this.pbStopInfo_CD.Visible = true;
        }

        private void btnCountdownStop_MouseLeave(object sender, EventArgs e)
        {
            this.btnCountdownStop.Image = SwitchPlus.Properties.Resources.BtnStop;
            this.pbStopInfo_CD.Visible = false;
        }

        private void btnCountdownStop_MouseUp(object sender, MouseEventArgs e)
        {
            this.btnCountdownStop.Image = SwitchPlus.Properties.Resources.BtnStopHover;
        }
        private void setBrutallySF()
        {
            SwitchPlus.Program.SF.SetValue("isbrutally", "1", RegistryValueKind.String);
        }
        private void resetBrutallySF()
        {
            SwitchPlus.Program.SF.SetValue("isbrutally", "0", RegistryValueKind.String);
        }

        private void reset_SF()
        {
            this.dtpScheduleCal.MinDate = System.DateTime.Now;
            this.dtpScheduleCal.Value = System.DateTime.Now;
            this.dtpSchedule.Value = System.DateTime.Now;
            SwitchPlus.Program.resetSF();
            this.rbScheduleFor.Image = SwitchPlus.Properties.Resources.RadioButtonD;
            deselectAll();
            this.pnlAction.Enabled = true;
        }
        public void setSF()
        {
            try
            {
                SwitchPlus.Program.SF.SetValue("Action", SwitchPlus.Program.action_SF.ToString(), RegistryValueKind.String);
                SwitchPlus.Program.DT_SF = DateTime.SpecifyKind(new DateTime(dtpScheduleCal.Value.Year, dtpScheduleCal.Value.Month, dtpScheduleCal.Value.Day, dtpSchedule.Value.Hour, dtpSchedule.Value.Minute, dtpSchedule.Value.Second), DateTimeKind.Local);
                SwitchPlus.Program.SF.SetValue("DT", SwitchPlus.Program.DT_SF.ToString("o"), RegistryValueKind.String);
                SwitchPlus.Program.isActive_SF = true;
                SwitchPlus.Program.SF.SetValue("isActive", "1", RegistryValueKind.String);
                if (SwitchPlus.Program.brutally_SF)
                    setBrutallySF();
                else
                    resetBrutallySF();
            }
            catch
            {
                MessageBox.Show("Error!!! Unable to Schedule. Please Reset and Continue.", "Switch", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
            }
        }
        private void btnScheduleStart_Click(object sender, EventArgs e)
        {
            if (validateAction())
            {
                if (validateSF())
                {
                    setSF(); //Recheck?????????????-----------------------
                    this.tmrSF.Start();
                    this.pnlSFInfo.Visible = false;
                    this.pnlSFStatus.Visible = true;
                    this.pnlSFSetting.Enabled = false;
                    lblSFAction.Text = SwitchPlus.Program.getTaskName(SwitchPlus.Program.action_SF);
                    lblSFDate.Text = SwitchPlus.Program.DT_SF.ToLongDateString();
                    lblSFTime.Text = SwitchPlus.Program.DT_SF.ToLongTimeString();
                    this.pnlScheduled.BackgroundImage = SwitchPlus.Properties.Resources.PanelGreen;
                    this.pnlAction.Enabled = false;
                    this.pidSF.Image = SwitchPlus.Properties.Resources.G;
                }
            }
        }

        private DateTime getDT_SF()
        {
            DateTime dt = DateTime.Parse(Convert.ToString(SwitchPlus.Program.SF.GetValue("DT_SF")), null, System.Globalization.DateTimeStyles.RoundtripKind);
            return dt;
        }
        private void retDT_SF() //Retrieving Date & Time From 'SF' Registry
        {
            SwitchPlus.Program.DT_SF = DateTime.Parse(Convert.ToString(SwitchPlus.Program.SF.GetValue("DT")), null, System.Globalization.DateTimeStyles.RoundtripKind);
        }

        private void btnScheduleReset_Click(object sender, EventArgs e)
        {
            reset_SF();
        }

        private bool validateSF()
        {
            SwitchPlus.Program.DT_SF = DateTime.SpecifyKind(new DateTime(dtpScheduleCal.Value.Year, dtpScheduleCal.Value.Month, dtpScheduleCal.Value.Day, dtpSchedule.Value.Hour, dtpSchedule.Value.Minute, dtpSchedule.Value.Second), DateTimeKind.Local);

            if (SwitchPlus.Program.DT_SF.CompareTo(System.DateTime.Now) < 0)
            {
                MessageBox.Show("Please check Date & Time! Scheduler Time must be greater than current system Date & Time.", "Switch", MessageBoxButtons.OK, MessageBoxIcon.Exclamation,
 MessageBoxDefaultButton.Button1);
                this.dtpScheduleCal.Value = System.DateTime.Now;
                this.dtpSchedule.Value = System.DateTime.Now;
                this.dtpScheduleCal.Focus();
                return false;
            }
            else
                return true;
        }
        private bool validateAction()
        {
            if (SwitchPlus.Program.selected_CD)
            {
                if (SwitchPlus.Program.action_CD == 9)
                {
                    MessageBox.Show("Please Select Power Option to continue.", "Switch", MessageBoxButtons.OK, MessageBoxIcon.Information,
     MessageBoxDefaultButton.Button1);
                    pnlAction.Enabled = true;
                    return false;
                }
            }
            else if (SwitchPlus.Program.action_SF == 9)
            {
                MessageBox.Show("Please Select Power Option to continue.", "Switch", MessageBoxButtons.OK, MessageBoxIcon.Information,
     MessageBoxDefaultButton.Button1);
                pnlAction.Enabled = true;
                return false;
            }
            return true;
        }

        private void tmrSF_Tick(object sender, EventArgs e)
        {
            if (SwitchPlus.Program.DT_SF.CompareTo(System.DateTime.Now) <= 0)
            //if ((SwitchPlus.Program.DT_SF.Year == System.DateTime.Now.Year) && (SwitchPlus.Program.DT_SF.Month == System.DateTime.Now.Month) && (SwitchPlus.Program.DT_SF.Day == System.DateTime.Now.Day)&&(SwitchPlus.Program.DT_SF.Hour == System.DateTime.Now.Hour) && (SwitchPlus.Program.DT_SF.Minute == System.DateTime.Now.Minute) && (SwitchPlus.Program.DT_SF.Second == System.DateTime.Now.Second))
            {
                this.lblSFAction.ResetText();
                this.lblSFDate.ResetText();
                this.lblSFTime.ResetText();
                //performing Task---------
                Power(SwitchPlus.Program.action_SF, SwitchPlus.Program.brutally_SF);
                //------------------------
                SwitchPlus.Program.resetSF();
                this.rbScheduleFor.Image = SwitchPlus.Properties.Resources.RadioButtonD;
                this.pnlSFSetting.Enabled = true;
                this.dtpScheduleCal.MinDate = System.DateTime.Now;
                this.dtpSchedule.Value = System.DateTime.Now;
                this.pnlSFInfo.Visible = true;
                this.pnlSFStatus.Visible = false;
                this.lblSFAction.ResetText();
                this.lblSFDate.ResetText();
                this.lblSFTime.ResetText();
                SwitchPlus.Program.isActive_SF = false;
                this.tmrSF.Stop();
            }
        }

        private void btnSFStop_Click(object sender, EventArgs e)
        {
            this.tmrSF.Stop();
            this.pnlSFInfo.Visible = true;
            this.pnlSFStatus.Visible = false;
            this.pnlSFSetting.Enabled = true;
            lblSFAction.ResetText();
            lblSFDate.ResetText();
            lblSFTime.ResetText();
            this.pnlScheduled.BackgroundImage = SwitchPlus.Properties.Resources.PanelGrey;
            this.pidSF.Image = SwitchPlus.Properties.Resources.R;
        }

        private bool isActive_SF()
        {
            if (getDT_SF() > System.DateTime.Now)
            {
                SwitchPlus.Program.resetSF();
                return false;
            }
            else
            {
                retDT_SF();
                return true;
            }

        }

        private void tbSF_MouseEnter(object sender, EventArgs e)
        {
            if (SwitchPlus.Program.selected_SF)
                this.tbSF.Image = SwitchPlus.Properties.Resources.TabSFActive;
            else
                this.tbSF.Image = SwitchPlus.Properties.Resources.TabSF_H;
        }

        private void tbSF_MouseHover(object sender, EventArgs e)
        {
            if (SwitchPlus.Program.selected_SF)
                this.tbSF.Image = SwitchPlus.Properties.Resources.TabSFActive;
            else
                this.tbSF.Image = SwitchPlus.Properties.Resources.TabSF_H;
        }

        private void tbSF_MouseLeave(object sender, EventArgs e)
        {
            if (SwitchPlus.Program.selected_SF)
                this.tbSF.Image = SwitchPlus.Properties.Resources.TabSFActive;
            else
                this.tbSF.Image = SwitchPlus.Properties.Resources.TabSF;
        }

        private void tbSF_Click(object sender, EventArgs e)
        {
            SwitchPlus.Program.selected_SF = true;
            this.tbSF.Image = SwitchPlus.Properties.Resources.TabSFActive;
            SwitchPlus.Program.selected_CD = false;
            this.tbCD.Image = SwitchPlus.Properties.Resources.TabCD;
            SwitchPlus.Program.selected_DL = false;
            this.tbDL.Image = SwitchPlus.Properties.Resources.TabDL;
            this.pnlCountDown.Visible = false;
            this.pnlDaily.Visible = false;
            this.pnlScheduled.Visible = true;
            pgrBrCD.Visible = false;
            // Checking All The SF Attribute
            if (SwitchPlus.Program.isActive_SF)
            {
                this.getAction(SwitchPlus.Program.action_SF);
                this.pnlAction.Enabled = false;
            }
            else
            {
                deselectAll();
                SwitchPlus.Program.resetSF();
                this.pnlAction.Enabled = true;
                this.dtpScheduleCal.MinDate = System.DateTime.Now;
                this.dtpSchedule.Value = System.DateTime.Now;
            }
        }

        public void startSF()
        {
            SwitchPlus.Program.retSF();
            if (SwitchPlus.Program.isRemDT_SF())
            {
                this.tmrSF.Start();
                if (SwitchPlus.Program.brutally_SF)
                    this.rbScheduleFor.Image = SwitchPlus.Properties.Resources.RadioButtonA;
                else
                    this.rbScheduleFor.Image = SwitchPlus.Properties.Resources.RadioButtonD;
                this.pnlSFSetting.Enabled = false;
                this.dtpScheduleCal.Value = SwitchPlus.Program.DT_SF;
                this.dtpSchedule.Value = SwitchPlus.Program.DT_SF;
                this.pnlSFInfo.Visible = false;
                this.pnlSFStatus.Visible = true;
                this.lblSFAction.Text = SwitchPlus.Program.getTaskName(SwitchPlus.Program.action_SF);
                this.lblSFDate.Text = SwitchPlus.Program.DT_SF.ToLongDateString();
                this.lblSFTime.Text = SwitchPlus.Program.DT_SF.ToLongTimeString();
                SwitchPlus.Program.isActive_SF = true;
                this.pidSF.Image = SwitchPlus.Properties.Resources.G;
            }
            else
            {
                SwitchPlus.Program.resetSF();
                this.rbScheduleFor.Image = SwitchPlus.Properties.Resources.RadioButtonD;
                this.pnlSFSetting.Enabled = true;
                this.dtpScheduleCal.MinDate = System.DateTime.Now;
                this.dtpSchedule.Value = System.DateTime.Now;
                this.pnlSFInfo.Visible = true;
                this.pnlSFStatus.Visible = false;
                this.lblSFAction.ResetText();
                this.lblSFDate.ResetText();
                this.lblSFTime.ResetText();
                SwitchPlus.Program.isActive_SF = false;
                this.pidSF.Image = SwitchPlus.Properties.Resources.R;
            }

        }


        private void btnDailyReset_Click(object sender, EventArgs e)
        {
            if (SwitchPlus.Program.DL_Reset == false)
            {
                DialogResult dr = MessageBox.Show("Daily Scheduler is Scheduled to '" + SwitchPlus.Program.getTaskName(SwitchPlus.Program.action_DL) + "' the system at " + SwitchPlus.Program.DT_DL.ToString("T") + "\nDo you want to Reset?", "Switch", MessageBoxButtons.YesNo, MessageBoxIcon.Information,
    MessageBoxDefaultButton.Button1);
                if (dr == System.Windows.Forms.DialogResult.Yes)
                {
                    SwitchPlus.Program.resetDL();
                    if (SwitchPlus.Program.brutally_DL)
                        this.rbDaily.Image = SwitchPlus.Properties.Resources.RadioButtonA;
                    else
                        this.rbDaily.Image = SwitchPlus.Properties.Resources.RadioButtonD;
                    this.pnlDailySetting.Enabled = true;
                    this.dtpDaily.Value = System.DateTime.Now;
                    this.dtpDaily.Enabled = true;
                    this.pnlDLInfo.Visible = true;
                    this.lblDLStatus.Text = "Daily Task Scheduler is Off. Please Select Task & Time to Start.";
                    this.pnlDLStatus.Visible = false;
                    this.lblDLAction.ResetText();
                    this.lblDLTime.ResetText();
                    SwitchPlus.Program.isActive_DL = false;
                    this.deselectAll();
                    this.pnlAction.Enabled = true;
                    this.dtpDaily.Enabled = true;
                }
            }
        }

        private void btnDailyStart_Click(object sender, EventArgs e)
        {
            if (SwitchPlus.Program.action_DL != 9)
            {
                setDL();
                this.tmrDL.Start();
                this.pnlDLInfo.Visible = false;
                this.pnlDLStatus.Visible = true;
                this.pnlDailySetting.Enabled = false;
                lblDLAction.Text = SwitchPlus.Program.getTaskName(SwitchPlus.Program.action_DL);
                lblDLTime.Text = SwitchPlus.Program.DT_DL.ToLongTimeString();
                this.pnlDaily.BackgroundImage = SwitchPlus.Properties.Resources.PanelGreen;
                this.pnlAction.Enabled = false;
                this.dtpDaily.Enabled = false;
                this.pidDL.Image = SwitchPlus.Properties.Resources.G;
                if (dtpDaily.Value.TimeOfDay < System.DateTime.Now.TimeOfDay)
                {
                    MessageBox.Show("Daily Action Schedule Time is elapsed for today! Task is scheduled for next time.", "Switch", MessageBoxButtons.OK, MessageBoxIcon.Exclamation,
     MessageBoxDefaultButton.Button1);
                }
            }
            else
            {
                MessageBox.Show("Action Not Selected! Please select Task to perform Task.", "Switch", MessageBoxButtons.OK, MessageBoxIcon.Exclamation,
     MessageBoxDefaultButton.Button1);
                this.pnlAction.Enabled = true;
                deselectAll();
            }
        }


        public void setDL()
        {
            try
            {
                SwitchPlus.Program.DL.SetValue("Action", SwitchPlus.Program.action_DL.ToString(), RegistryValueKind.String);
                TimeSpan TS = new TimeSpan(dtpDaily.Value.Hour, dtpDaily.Value.Minute, dtpDaily.Value.Second);
                SwitchPlus.Program.DT_DL = DateTime.SpecifyKind(new DateTime(System.DateTime.Now.Year, System.DateTime.Now.Month, System.DateTime.Now.Day, TS.Hours, TS.Minutes, TS.Seconds), DateTimeKind.Local);
                SwitchPlus.Program.DL.SetValue("DT", TS.ToString(), RegistryValueKind.String);
                SwitchPlus.Program.isActive_DL = true;
                SwitchPlus.Program.DL.SetValue("isActive", "1", RegistryValueKind.String);
                if (SwitchPlus.Program.brutally_DL)
                    setBrutallyDL();
                else
                    resetBrutallyDL();
            }
            catch
            {
                MessageBox.Show("Error!!! Unable to Schedule. Please Reset and Continue.", "Switch", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
            }
        }

        private void tbDL_MouseHover(object sender, EventArgs e)
        {
            if (SwitchPlus.Program.selected_DL)
                this.tbDL.Image = SwitchPlus.Properties.Resources.TabDLActive;
            else
                this.tbDL.Image = SwitchPlus.Properties.Resources.TabDL_H;
        }

        private void tbDL_MouseEnter(object sender, EventArgs e)
        {
            if (SwitchPlus.Program.selected_DL)
                this.tbDL.Image = SwitchPlus.Properties.Resources.TabDLActive;
            else
                this.tbDL.Image = SwitchPlus.Properties.Resources.TabDL_H;
        }

        private void tbDL_MouseLeave(object sender, EventArgs e)
        {
            if (SwitchPlus.Program.selected_DL)
                this.tbDL.Image = SwitchPlus.Properties.Resources.TabDLActive;
            else
                this.tbDL.Image = SwitchPlus.Properties.Resources.TabDL;
        }

        private void tbDL_Click(object sender, EventArgs e)
        {
            SwitchPlus.Program.selected_DL = true;
            this.tbDL.Image = SwitchPlus.Properties.Resources.TabDLActive;
            SwitchPlus.Program.selected_SF = false;
            this.tbSF.Image = SwitchPlus.Properties.Resources.TabSF;
            SwitchPlus.Program.selected_CD = false;
            this.tbCD.Image = SwitchPlus.Properties.Resources.TabCD;
            this.pnlDaily.Visible = true;
            this.pnlCountDown.Visible = false;
            this.pnlScheduled.Visible = false;
            if (SwitchPlus.Program.isActive_DL && tmrDL.Enabled)
            {
                this.getAction(SwitchPlus.Program.action_DL);
                this.pnlAction.Enabled = false;
                this.pnlDLStatus.Visible = true;
                this.pnlDLInfo.Visible = false;
                dtpDaily.Enabled = false;
            }
            else if (tmrDL.Enabled == false && SwitchPlus.Program.isActive_DL)
            {
                pnlDailySetting.Visible = true;
                pnlDailySetting.Enabled = true;
                pnlDLStatus.Visible = false;
                pnlDLInfo.Visible = true;
            }
            else
            {
                Reset_DL();
            }
        }

        private void Reset_DL()
        {
            this.pnlAction.Enabled = true;
            this.deselectAll();
            this.pnlDLInfo.Visible = true;
            this.lblDLStatus.Text = "Daily Task Scheduler is Off. Please Select Task, Day/Days and Time to Start.";
            this.pnlDLStatus.Visible = false;
            this.pnlDailySetting.Enabled = true;
            this.lblDLAction.ResetText();
            this.lblDLTime.ResetText();
            this.dtpDaily.Value = SwitchPlus.Program.DT_DL;
            this.dtpDaily.Enabled = true;
            this.pnlDaily.BackgroundImage = SwitchPlus.Properties.Resources.PanelGrey;
            if (SwitchPlus.Program.brutally_DL)
                this.rbDaily.Image = SwitchPlus.Properties.Resources.RadioButtonA;
            else
                this.rbDaily.Image = SwitchPlus.Properties.Resources.RadioButtonD;
            this.pidDL.Image = SwitchPlus.Properties.Resources.R;
        }

        private void tmrDL_Tick(object sender, EventArgs e)
        {
            if ((SwitchPlus.Program.DT_DL.Hour == System.DateTime.Now.Hour) && (SwitchPlus.Program.DT_DL.Minute == System.DateTime.Now.Minute) && (SwitchPlus.Program.DT_DL.Second == System.DateTime.Now.Second))
            {
                this.lblDLAction.ResetText();
                this.lblDLTime.ResetText();
                //performing Task---------
                Power(SwitchPlus.Program.action_DL, SwitchPlus.Program.brutally_DL);
                //------------------------
                Reset_DL();
                this.tmrDL.Stop();
            }
        }
        // ----------------------------------------------------- DAILY ------------------------------------
        public void startDL()
        {
            SwitchPlus.Program.retDL();

            if (SwitchPlus.Program.action_DL != 9)
            {
                this.tmrDL.Start();
                this.lblDLAction.Text = SwitchPlus.Program.getTaskName(SwitchPlus.Program.action_DL);
                this.lblDLTime.Text = SwitchPlus.Program.DT_DL.ToLongTimeString();
                if (SwitchPlus.Program.brutally_DL)
                    this.rbDaily.Image = SwitchPlus.Properties.Resources.RadioButtonA;
                else
                    this.rbDaily.Image = SwitchPlus.Properties.Resources.RadioButtonD;
                this.pnlDLInfo.Visible = false;
                this.pnlDLStatus.Visible = true;
                this.pnlDailySetting.Enabled = false;
                this.dtpDaily.Value = SwitchPlus.Program.DT_DL;
                SwitchPlus.Program.isActive_DL = true;
                this.getAction(SwitchPlus.Program.action_DL);
                this.pnlAction.Enabled = false;
                this.pidDL.Image = SwitchPlus.Properties.Resources.G;
            }
            else
            {
                Reset_DL();
                SwitchPlus.Program.isActive_DL = false;
            }

        }

        private void btnDLStop_Click(object sender, EventArgs e)
        {
            this.tmrDL.Stop();
            this.pnlAction.Enabled = false;
            this.pnlDLInfo.Visible = true;
            this.lblDLStatus.Text = "Daily Task Scheduler is Stopped. Click Start to continue.";
            this.pnlDLStatus.Visible = false;
            this.pnlDailySetting.Enabled = true;
            //this.lblDLAction.ResetText();
            //this.lblDLTime.ResetText();
            this.dtpDaily.Value = SwitchPlus.Program.DT_DL;
            this.dtpDaily.Enabled = false;
            this.pnlDaily.BackgroundImage = SwitchPlus.Properties.Resources.PanelGrey;
            this.pidDL.Image = SwitchPlus.Properties.Resources.R;
            if (SwitchPlus.Program.brutally_DL)
                this.rbDaily.Image = SwitchPlus.Properties.Resources.RadioButtonA;
            else
                this.rbDaily.Image = SwitchPlus.Properties.Resources.RadioButtonD;
        }


        private void NI_Switch_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (this.Visible == false)
            {
                this.Visible = true;
            }
            else
            {
                this.Visible = false;
            }
        }

        private void MnItm_Switch_Click(object sender, EventArgs e)
        {
            if (this.Visible == false)
            {
                this.Visible = true;
            }
            else
            {
                this.Visible = false;
            }
        }

        private void MnItm_Exit_Click(object sender, EventArgs e)
        {
            _VServer.Stop();
            ChannelServices.UnregisterChannel(SwitchPlus.Program.channel);
            Application.Exit();
        }

        private void MnItm_Shutdown_Click(object sender, EventArgs e)
        {
            DialogResult dr = MessageBox.Show("Do you want to Shutdown the System?", "Switch", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1);
            if(dr == System.Windows.Forms.DialogResult.Yes)
                this.Power(1, false);         
        }

        private void MnItm_Restart_Click(object sender, EventArgs e)
        {
            DialogResult dr = MessageBox.Show("Do you want to Restart the System?", "Switch", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1);
            if (dr == System.Windows.Forms.DialogResult.Yes)
            this.Power(2, false);
        }

        private void MnBtn_StandBy_Click(object sender, EventArgs e)
        {
            DialogResult dr = MessageBox.Show("Do you want to Sleep the System?", "Switch", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1);
            if (dr == System.Windows.Forms.DialogResult.Yes)
            this.Power(3, false);
        }

        private void MnItm_Hibernet_Click(object sender, EventArgs e)
        {
            DialogResult dr = MessageBox.Show("Do you want to Hibernet the System?", "Switch", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1);
            if (dr == System.Windows.Forms.DialogResult.Yes)
            this.Power(4, false);
        }

        private void MnItm_LogOff_Click(object sender, EventArgs e)
        {
            DialogResult dr = MessageBox.Show("Do you want to Logoff the System?", "Switch", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1);
            if (dr == System.Windows.Forms.DialogResult.Yes)
            this.Power(5, false);
        }

        private void MnItm_Lock_Click(object sender, EventArgs e)
        {
            DialogResult dr = MessageBox.Show("Do you want to Lock the System?", "Switch", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1);
            if (dr == System.Windows.Forms.DialogResult.Yes)
            this.Power(6, false);
        }


        private void btnSFStop_MouseDown(object sender, MouseEventArgs e)
        {
            this.btnSFStop.Image = SwitchPlus.Properties.Resources.BtnStopPressed;
        }

        private void btnSFStop_MouseEnter(object sender, EventArgs e)
        {
            this.btnSFStop.Image = SwitchPlus.Properties.Resources.BtnStopHover;
            this.pbStopInfo_CD.Visible = true;
        }

        private void btnSFStop_MouseHover(object sender, EventArgs e)
        {
            this.btnSFStop.Image = SwitchPlus.Properties.Resources.BtnStopHover;
            this.pbStopInfo_CD.Visible = true;
        }

        private void btnSFStop_MouseLeave(object sender, EventArgs e)
        {
            this.btnSFStop.Image = SwitchPlus.Properties.Resources.BtnStop;
            this.pbStopInfo_CD.Visible = false;
        }

        private void btnSFStop_MouseUp(object sender, MouseEventArgs e)
        {
            this.btnSFStop.Image = SwitchPlus.Properties.Resources.BtnStopHover;
        }

        private void btnDLStop_MouseDown(object sender, MouseEventArgs e)
        {
            this.btnDLStop.Image = SwitchPlus.Properties.Resources.BtnStopPressed;
        }

        private void btnDLStop_MouseEnter(object sender, EventArgs e)
        {
            this.btnDLStop.Image = SwitchPlus.Properties.Resources.BtnStopHover;
            this.pbStopInfo_CD.Visible = true;
        }

        private void btnDLStop_MouseHover(object sender, EventArgs e)
        {
            this.btnDLStop.Image = SwitchPlus.Properties.Resources.BtnStopHover;
            this.pbStopInfo_CD.Visible = true;
        }

        private void btnDLStop_MouseLeave(object sender, EventArgs e)
        {
            this.btnDLStop.Image = SwitchPlus.Properties.Resources.BtnStop;
            this.pbStopInfo_CD.Visible = false;
        }

        private void btnDLStop_MouseUp(object sender, MouseEventArgs e)
        {
            this.btnDLStop.Image = SwitchPlus.Properties.Resources.BtnStopHover;
        }

        private void mnAbout_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Developed By: \tAtul Meshram\nE-Mail: \t\tatul.meshram.akm@gmail.com\nContact: \t\t+91-8050606549\n\nDepartment of Mathematical And Computational Sciences\nNITK, Surathkal, INDIA", "Switch", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
        }
    }
}
