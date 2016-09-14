﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;

namespace YoutubeWallpaper
{
    public partial class Form_Wallpaper : Form
    {
        public Form_Wallpaper()
        {
            InitializeComponent();
        }

        //#############################################################################################

        protected bool m_isFixed = false;
        public bool IsFixed
        { get { return m_isFixed; } }

        public string Uri
        {
            get { return this.webBrowser_page.Url.ToString(); }
            set { this.webBrowser_page.Navigate(value); }
        }

        public int Volume
        {
            get
            {
                uint temp = 0;
                WinApi.waveOutGetVolume(IntPtr.Zero, out temp);
                return (int)((double)(temp & 0xFFFF) * 100 / 0xFFFF);
            }
            set
            {
                uint vol = (uint)((double)0xFFFF * value / 100) & 0xFFFF;
                WinApi.waveOutSetVolume(IntPtr.Zero, (vol << 16) | vol);
            }
        }

        public Screen OwnerScreen
        { get; set; } = Screen.PrimaryScreen;

        protected Task m_checkParent = null;
        protected bool m_onRunning = false;
        protected EventWaitHandle m_waitHandle = null;

        protected readonly object m_lockFlag = new object();
        protected bool m_needUpdateParent = false;

        //#############################################################################################

        public void ShowCursor(bool bShow)
        {
            this.panel_cursor.Visible = bShow;
        }

        public void MoveCursor(int x, int y)
        {
            this.panel_cursor.Location = new Point(x - this.panel_cursor.Width / 2,
                y - this.panel_cursor.Height / 2);
        }

        public void PerformClickWallpaper(int x, int y)
        {
            MoveCursor(x, y);


            IntPtr flash;
            flash = WinApi.FindWindowEx(this.webBrowser_page.Handle, IntPtr.Zero, "Shell Embedding", IntPtr.Zero);
            flash = WinApi.FindWindowEx(flash, IntPtr.Zero, "Shell DocObject View", IntPtr.Zero);
            flash = WinApi.FindWindowEx(flash, IntPtr.Zero, "Internet Explorer_Server", IntPtr.Zero);
            flash = WinApi.FindWindowEx(flash, IntPtr.Zero, "MacromediaFlashPlayerActiveX", IntPtr.Zero);

            if (flash != IntPtr.Zero)
            {
                IntPtr result = IntPtr.Zero;
                WinApi.SendMessageTimeout(flash, 0x201/*DOWN*/, new IntPtr(0), new IntPtr(WinApi.MakeParam(y, x)),
                    WinApi.SendMessageTimeoutFlags.SMTO_NORMAL, 0, out result);
                WinApi.SendMessageTimeout(flash, 0x202/*UP*/, new IntPtr(0), new IntPtr(WinApi.MakeParam(y, x)),
                    WinApi.SendMessageTimeoutFlags.SMTO_NORMAL, 0, out result);
            }
        }

        protected bool PinToBackground()
        {
            m_isFixed = BehindDesktopIcon.FixBehindDesktopIcon(this.Handle);

            if (m_isFixed)
            {
                ScreenUtility.FillScreen(this, OwnerScreen);
            }


            return m_isFixed;
        }

        protected void CheckParent(object thisHandle)
        {
            IntPtr me = (IntPtr)thisHandle;


            while (m_onRunning)
            {
                bool isChildOfProgman = false;


                var progman = WinApi.FindWindow("Progman", null);

                WinApi.EnumChildWindows(progman, new WinApi.EnumWindowsProc((handle, lparam) =>
                {
                    if (handle == me)
                    {
                        isChildOfProgman = true;
                        return false;
                    }

                    return true;
                }), IntPtr.Zero);


                if (isChildOfProgman == false)
                {
                    lock (m_lockFlag)
                    {
                        m_needUpdateParent = true;
                    }
                }


                m_waitHandle.WaitOne(2000);
            }
        }

        //#############################################################################################

        private void Form_Wallpaper_Load(object sender, EventArgs e)
        {
            if (PinToBackground())
            {
                m_waitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
                m_onRunning = true;
                m_checkParent = Task.Factory.StartNew(CheckParent, this.Handle);

                this.timer_check.Start();
            }
            else
            {
                this.Close();
            }
        }

        private void Form_Wallpaper_FormClosing(object sender, FormClosingEventArgs e)
        {
            this.timer_check.Stop();

            if (m_checkParent != null)
            {
                m_onRunning = false;
                m_waitHandle.Set();
                m_checkParent.Wait(TimeSpan.FromSeconds(10.0));
                m_checkParent = null;

                m_waitHandle.Dispose();
            }
        }

        private void timer_check_Tick(object sender, EventArgs e)
        {
            bool needUpdate = false;
            lock (m_lockFlag)
            {
                needUpdate = m_needUpdateParent;
                m_needUpdateParent = false;
            }

            if (needUpdate)
            {
                PinToBackground();
            }
        }
    }
}
