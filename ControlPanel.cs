using System;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Diagnostics;


namespace SharpTouch
{
    // Tested on the TM-P3017 PS2 board without pointerstick.
    // MPN 92000252301REVA
    public partial class ControlPanel : Form
    {
        readonly SYNCOMLib.SynAPI m_api = new SYNCOMLib.SynAPIClass();
        readonly SYNCOMLib.SynDevice m_dev = new SYNCOMLib.SynDeviceClass();

        readonly Assembly asm = Assembly.GetExecutingAssembly();
        const string cvrun = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string settingsKeyName = @"Software\SharpTouch";
        public static bool usbmouse = false;

        public delegate void SettingsChangedEventHandler();
        public event SettingsChangedEventHandler SettingsChanged;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

        [DllImport("USER32.DLL")]
        public static extern int GetDoubleClickTime();

        public int ScrollSpeedX
        {
            get { return m_scrollSpeed.Value; }
        }
        public int ScrollSpeedY
        {
            get { return m_scrollSpeed.Value; }
        }

        public Gesture UpAction
        {
            get { return (Gesture)m_cbUpAction.SelectedItem; }
        }
        public Gesture DownAction
        {
            get { return (Gesture)m_cbDownAction.SelectedItem; }
        }
        public Gesture LeftAction
        {
            get { return (Gesture)m_cbLeftAction.SelectedItem; }
        }
        public Gesture RightAction
        {
            get { return (Gesture)m_cbRightAction.SelectedItem; }
        }

        public ControlPanel(SYNCOMLib.SynAPI api, SYNCOMLib.SynDevice device)
        {
            InitializeComponent();

            m_api = api;
            m_dev = device;

            // auto start checkbox
            using (RegistryKey run = Registry.CurrentUser.OpenSubKey(cvrun))
            {
                m_autoStart.Checked = (run.GetValue(asm.GetName().Name) != null);
            }

            // speed
            using (RegistryKey mySettings = Registry.CurrentUser.CreateSubKey(settingsKeyName))
            {
                m_scrollSpeed.Value = (int)mySettings.GetValue("ScrollSpeed", 1000);
                m_speedLabel.Text = string.Format("{0}%", m_scrollSpeed.Value / 10);
            }

            FillComboBox(m_cbUpAction);
            FillComboBox(m_cbDownAction);
            FillComboBox(m_cbLeftAction);
            FillComboBox(m_cbRightAction);

            // gestures
            using (RegistryKey mySettings = Registry.CurrentUser.CreateSubKey(settingsKeyName))
            {
                m_cbUpAction.SelectedIndex = (int)mySettings.GetValue("UpAction", (int)GestureAction.Flip3D);
                m_cbDownAction.SelectedIndex = (int)mySettings.GetValue("DownAction", (int)GestureAction.ShowDesktop);
                m_cbLeftAction.SelectedIndex = (int)mySettings.GetValue("LeftAction", (int)GestureAction.DockLeft);
                m_cbRightAction.SelectedIndex = (int)mySettings.GetValue("RightAction", (int)GestureAction.DockRight);
            }

            m_cbUpAction.SelectedIndexChanged += gestureActionChanged;
            m_cbDownAction.SelectedIndexChanged += gestureActionChanged;
            m_cbLeftAction.SelectedIndexChanged += gestureActionChanged;
            m_cbRightAction.SelectedIndexChanged += gestureActionChanged;

            // api version and device version
            m_apiVer.Text = GetSynapticAPIStringProperty(api, SYNCTRLLib.SynAPIStringProperty.SP_VersionString, 100);
            m_devName.Text = GetSynapticDeviceStringProperty(device, SYNCTRLLib.SynDeviceStringProperty.SP_ModelString, 100);
            lbldriverversion.Text = GetVersion();

            ShowDebugElements();

            RegistryKey synTPCplKey = Registry.LocalMachine.OpenSubKey("Software\\Synaptics\\SynTPCpl", false);
            int awUI = Convert.ToInt32(synTPCplKey.GetValue("AlienwareUI")); // dell alienware theme..
            if(awUI == 1)
            {
                cbSkin.Checked = true;
            } else if(awUI == 0) {
                cbSkin.Checked = false;
            }
            int uiStyle = Convert.ToInt32(synTPCplKey.GetValue("UIStyle")); // win8ui
            if (uiStyle == 14)
            {
                cbUnlockHiddenScroll.Checked = false;
            } else if(uiStyle == 15) {
                cbUnlockHiddenScroll.Checked = true;
            }

            int m_touchspeed = 0;
            m_dev.GetProperty(16777884, ref m_touchspeed);
            m_PointerSpeed.Value = m_touchspeed;

            int m_touchpressure = 0;
            m_dev.GetProperty(268435752, ref m_touchpressure);
            m_Touchpressure.Value = m_touchpressure;

            int m_tappingspeed = 0;
            m_dev.GetProperty(16777531, ref m_tappingspeed);
            m_Tappingspeed.Value = m_tappingspeed;

            // 0 = none, 3 = tick to click, 7 = tick to click & touchguard
            int iGestures = 0;
            m_dev.GetProperty(268435726, ref iGestures);
            switch (iGestures)
            {
                case 0:
                    m_Ticktoclick.Checked = false;
                    m_Touchgaurd.Checked = false;
                    m_Tappingspeed.Enabled = false;
                    break;
                case 3:
                    m_Ticktoclick.Checked = true;
                    m_Touchgaurd.Checked = false;
                    m_Tappingspeed.Enabled = true;
                    break;
                case 7:
                    m_Ticktoclick.Checked = true;
                    m_Touchgaurd.Checked = true;
                    m_Tappingspeed.Enabled = true;
                    break;
                default:
                    break;
            }

            int m_palmdetection = 0;
            m_dev.GetProperty(16777876, ref m_palmdetection);
            switch (m_palmdetection)
            {
                case 1114255:
                    m_Palmdetection.Checked = true;
                    m_Touchguard.Enabled = true;
                    break;
                case 1114254:
                    m_Palmdetection.Checked = false;
                    m_Touchguard.Enabled = false;
                    break;
                default:
                    break;
            }

            //Palm sensetivity
            int m_touchguard = 0;
            m_dev.GetProperty(268435843, ref m_touchguard);
            m_Touchguard.Value = m_touchguard;

            //zigzag 16778072
            int m_zigzag = 0;
            m_dev.GetProperty(16778072, ref m_zigzag);
            m_Zigzag.Checked = Convert.ToBoolean(m_zigzag);

            int m_circularscrolling = 0;
            m_dev.GetProperty((int)SYNCTRLLib.SynDeviceProperty.SP_ChiralScrolling, ref m_circularscrolling);
            m_Circularscrolling.Checked = Convert.ToBoolean(m_circularscrolling);

            int m_disablenavinbuttuonzone = 0;
            m_dev.GetProperty((int)SYNCTRLLib.SynDeviceProperty.SP_DisableNavigationInButtonZone, ref m_disablenavinbuttuonzone);
            m_DisableNavInButtuonZone.Checked = Convert.ToBoolean(m_disablenavinbuttuonzone);

            // 0 disable, 5 slow scroll, 17 circular scroll
            int m_verticalscrollingflags = 0;
            m_dev.GetProperty((int)SYNCTRLLib.SynDeviceProperty.SP_VerticalScrollingFlags, ref m_verticalscrollingflags);
            switch (m_verticalscrollingflags)
            {
                case 0:
                    m_Enableedgesrolling.Checked = false;
                    //GUI controls
                    m_Slowscrolling.Enabled = false;
                    m_Circularscrolling.Enabled = false;
                    m_Zoomscroll.Enabled = false;
                    m_Edgescrollspeed.Enabled = false;
                    m_Virtualscrollregionnorrow.Enabled = false;
                    m_Virtualscrollregionnormal.Enabled = false;
                    m_Virtualscrollregionwide.Enabled = false;
                    break;
                case 1:
                    m_Enableedgesrolling.Checked = true;
                    //GUI controls
                    m_Slowscrolling.Enabled = true;
                    m_Circularscrolling.Enabled = true;
                    m_Zoomscroll.Enabled = true;
                    m_Edgescrollspeed.Enabled = true;
                    m_Virtualscrollregionnorrow.Enabled = true;
                    m_Virtualscrollregionnormal.Enabled = true;
                    m_Virtualscrollregionwide.Enabled = true;
                    break;
                case 5:
                    m_Enableedgesrolling.Checked = true;
                    m_Circularscrolling.Checked = false;
                    m_Slowscrolling.Checked = true;
                    break;
                case 17:
                    m_Enableedgesrolling.Checked = true;
                    m_Circularscrolling.Checked = true;
                    m_Slowscrolling.Checked = true;
                    break;
                default:
                    break;
            }

            // 32 - 256
            int m_edgescrollspeed = 0;
            m_dev.GetProperty((int)SYNCTRLLib.SynDeviceProperty.SP_VerticalScrollSpeed, ref m_edgescrollspeed);
            m_Edgescrollspeed.Value = m_edgescrollspeed;

            // 178 narrow, 268 normal, 357 wide
            int m_edgescrollarea = 0;
            m_dev.GetProperty((int)SYNCTRLLib.SynDeviceProperty.SP_VerticalScrollRegionWidth, ref m_edgescrollarea);
            switch(m_edgescrollarea)
            {
                case 178:
                    m_Virtualscrollregionnorrow.Checked = true;
                    break;
                case 268:
                    m_Virtualscrollregionnormal.Checked = true;
                    break;
                case 357:
                    m_Virtualscrollregionwide.Checked = true;
                    break;
                default:
                    break;
            }

            // 5120 off 5121 on
            int m_zoomscroll = 0;
            m_dev.GetProperty(2424832, ref m_zoomscroll);
            switch (m_zoomscroll)
            {
                case 5120:
                    m_Zoomscroll.Checked = false;
                    break;
                case 5121:
                    m_Zoomscroll.Checked = true;
                    break;
                default:
                    break;
            }

            m_Doubleclickspeed.Value = GetDoubleClickTime();

            int m_multifingergesture = 0;
            m_dev.GetProperty(589824, ref m_multifingergesture);
            switch (m_multifingergesture)
            {
                case 1:
                    m_Enable2Fgestures.Checked = false;
                    m_2FPanning.Checked = false;
                    m_2Fslowscrolling.Checked = false;
                    m_2Fsueezezoom.Checked = false;
                    m_2Fturning.Checked = false;
                    break;
                case 4097:
                    m_Enable2Fgestures.Checked = false;
                    m_2FPanning.Checked = false;
                    m_2Fsueezezoom.Checked = false;
                    m_2Fturning.Checked = false;
                    break;
                case 4099:
                    m_Enable2Fgestures.Checked = true;
                    m_2FPanning.Checked = true;
                    m_2Fsueezezoom.Checked = false;
                    m_2Fturning.Checked = false;
                    //=============================
                    m_2FPanning.Checked = true;
                    break;
                case 4115:
                    m_Enable2Fgestures.Checked = true;
                    m_2FPanning.Checked = true;
                    m_2Fsueezezoom.Checked = true;
                    m_2Fturning.Checked = false;
                    //=============================
                    m_2FPanning.Checked = true;
                    m_2Fsueezezoom.Checked = true;
                    break;
                case 4107:
                    m_Enable2Fgestures.Checked = true;
                    m_2FPanning.Checked = true;
                    m_2Fsueezezoom.Checked = false;
                    m_2Fturning.Checked = true;
                    //=============================
                    m_2FPanning.Checked = true;
                    m_2Fturning.Checked = true;
                    break;
                case 4123:
                    m_Enable2Fgestures.Checked = true;
                    m_2FPanning.Checked = true;
                    m_2Fsueezezoom.Checked = true;
                    m_2Fturning.Checked = true;
                    //=============================
                    m_2FPanning.Checked = true;
                    m_2Fsueezezoom.Checked = true;
                    m_2Fturning.Checked = true;
                    break;

                case 4113:
                    m_Enable2Fgestures.Checked = true;
                    m_2FPanning.Checked = false;
                    m_2Fsueezezoom.Checked = true;
                    m_2Fturning.Checked = false;
                    //=============================
                    m_2Fsueezezoom.Checked = true;
                    break;
                case 4105:
                    m_Enable2Fgestures.Checked = true;
                    m_2FPanning.Checked = false;
                    m_2Fsueezezoom.Checked = false;
                    m_2Fturning.Checked = true;
                    //=============================
                    m_2Fturning.Checked = true;
                    break;
                case 4121:
                    m_Enable2Fgestures.Checked = true;
                    m_2FPanning.Checked = false;
                    m_2Fsueezezoom.Checked = true;
                    m_2Fturning.Checked = true;
                    //=============================
                    m_2Fsueezezoom.Checked = true;
                    m_2Fturning.Checked = true;
                    break;
                default:
                    break;
            }

            // 32 - 256 
            int m_panscrollspeed = 0;
            m_dev.GetProperty(268435847, ref m_panscrollspeed);
            m_Panscrollspeed.Value = m_panscrollspeed;

            //1 = off, 5 = on spid 268435845
            int m_2fslowscrolling = 0; 
            m_dev.GetProperty((int)SYNCTRLLib.SynDeviceProperty.SP_2FHorizontalScrollingFlags, ref m_2fslowscrolling);
            switch (m_2fslowscrolling)
            {
                case 1:
                    m_2Fslowscrolling.Checked = false;
                    break;
                case 5:
                    m_2Fslowscrolling.Checked = true;
                    break;
                default:
                    break;
            }            
        }

        [Conditional("DEBUG")]
        private void ShowDebugElements()
        {
            btndebug.Visible = true;
        }
    
        private void gestureActionChanged(object sender, EventArgs e)
        {
            ComboBox cb = (ComboBox)sender;
            using (RegistryKey mySettings = Registry.CurrentUser.CreateSubKey(settingsKeyName))
            {
                Gesture g = (Gesture)cb.SelectedItem;
                mySettings.SetValue((string)cb.Tag, (int)g.ActionCode);
                if (SettingsChanged != null)
                    SettingsChanged();
            }
        }

        void FillComboBox(ComboBox cb)
        {
            cb.Items.AddRange(Gesture.AllGestures);
        }

        private void ControlPanel_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                this.Hide();
                e.Cancel = true;
            }
        }

        private void m_exit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void m_autoStart_CheckedChanged(object sender, EventArgs e)
        {
            using (RegistryKey run = Registry.CurrentUser.OpenSubKey(cvrun, true))
            {
                if (m_autoStart.Checked)
                    run.SetValue(asm.GetName().Name, "\"" + asm.Location + "\"");
                else
                    run.DeleteValue(asm.GetName().Name);
            }
        }

        string GetSynapticAPIStringProperty(SYNCOMLib.SynAPI api, SYNCTRLLib.SynAPIStringProperty prop, int bufSize)
        {
            byte[] buf = new byte[bufSize];
            api.GetStringProperty((int)prop, ref buf[0], ref bufSize);
            return Encoding.ASCII.GetString(buf, 0, bufSize);
        }

        bool GetTouchPadStatus(SYNCOMLib.SynDevice dev, SYNCTRLLib.SynDeviceProperty prop)
        {
            int pValue = 0;
            try
            {
                dev.GetProperty((int)prop, ref pValue);
                return Convert.ToBoolean(pValue);
            }
            catch (Exception)
            {
                return Convert.ToBoolean(pValue);
            }
        }

        bool GetDisableTouchpadWhenUSBMouse(SYNCOMLib.SynDevice dev, SYNCTRLLib.SynDeviceProperty prop)
        {
            int pValue = 0;    
            try
            {                     
                dev.GetProperty((int)prop, ref pValue);
                return Convert.ToBoolean(pValue);
            }
            catch (Exception)
            {
                return Convert.ToBoolean(pValue);
            }
        }

        int GetProperty(SYNCOMLib.SynDevice dev, SYNCTRLLib.SynDeviceProperty prop)
        {
            int pValue = 0;
            dev.GetProperty((int)prop, ref pValue);
            return pValue;
        }

        string GetSynapticDeviceStringProperty(SYNCOMLib.SynDevice dev, SYNCTRLLib.SynDeviceStringProperty prop, int bufSize)
        {
            byte[] buf = new byte[bufSize];
            dev.GetStringProperty((int)prop, ref buf[0], ref bufSize);
            return Encoding.ASCII.GetString(buf, 0, bufSize);
        }

        private void m_scrollSpeed_Scroll(object sender, EventArgs e)
        {
            m_speedLabel.Text = string.Format("{0}%", m_scrollSpeed.Value / 10);
            using (RegistryKey mySettings = Registry.CurrentUser.CreateSubKey(settingsKeyName))
            {
                mySettings.SetValue("ScrollSpeed", m_scrollSpeed.Value);
                if (SettingsChanged != null)
                    SettingsChanged();
            }
        }

        private void TmrPoll_Tick(object sender, EventArgs e)
        {
            if (!GetTouchPadStatus(m_dev, SYNCTRLLib.SynDeviceProperty.SP_DeviceStatus))
            {
                lbltouchstatus.Text = @"Yes";               
                lblusbmouse.Text = @"No";
                cbTouchpadenabled.Checked = true;
                enableAllGUIControls(true);

            } else {
                lbltouchstatus.Text = @"No";
                lblusbmouse.Text = @"Yes";
                cbTouchpadenabled.Checked = false;
                enableAllGUIControls(false);
            }

            if (!GetDisableTouchpadWhenUSBMouse(m_dev, SYNCTRLLib.SynDeviceProperty.SP_DisablePDIfExtPresent))
            {
                cbDisableTP.Checked = false;
            } else {
                cbDisableTP.Checked = true;
            }

            //Synaptics tray state 17|49
            int pValue = 0;
            m_dev.GetProperty(524288, ref pValue);
            switch (pValue)
            {
                case 17:
                    cbTray.Checked = true;
                    break;
                case 49:
                    cbTray.Checked = false;
                    break;
                default:
                    break;
            }
        }

        private void enableAllGUIControls(bool action)
        { //GUI controls
            if (action == true)
            {
                m_PointerSpeed.Enabled = true;
                m_Touchpressure.Enabled = true;
                m_Tappingspeed.Enabled = true;
                m_Ticktoclick.Enabled = true;
                m_Palmdetection.Enabled = true;
                m_Zigzag.Enabled = true;
                m_Touchgaurd.Enabled = true;
                m_Enableedgesrolling.Enabled = true;
                m_Slowscrolling.Enabled = true;
                m_Circularscrolling.Enabled = true;
                m_Zoomscroll.Enabled = true;
                m_Edgescrollspeed.Enabled = true;
                m_Virtualscrollregionnorrow.Enabled = true;
                m_Virtualscrollregionnormal.Enabled = true;
                m_Virtualscrollregionwide.Enabled = true;
                m_2FPanning.Enabled = true;
                m_2Fsueezezoom.Enabled = true;
                m_2Fturning.Enabled = true;
                m_Panscrollspeed.Enabled = true;
                m_Enable2Fgestures.Enabled = true;
            } else {    
                m_PointerSpeed.Enabled = false;
                m_Touchpressure.Enabled = false;
                m_Tappingspeed.Enabled = false;
                m_Ticktoclick.Enabled = false;
                m_Palmdetection.Enabled = false;
                m_Zigzag.Enabled = false;
                m_Touchgaurd.Enabled = false;
                m_Enableedgesrolling.Enabled = false;
                m_Slowscrolling.Enabled = false;
                m_Circularscrolling.Enabled = false;
                m_Zoomscroll.Enabled = false;
                m_Edgescrollspeed.Enabled = false;
                m_Virtualscrollregionnorrow.Enabled = false;
                m_Virtualscrollregionnormal.Enabled = false;
                m_Virtualscrollregionwide.Enabled = false;
                m_2FPanning.Enabled = false;
                m_2Fsueezezoom.Enabled = false;
                m_2Fturning.Enabled = false;
                m_Panscrollspeed.Enabled = false;
                m_Enable2Fgestures.Enabled = false;
            }
        }

        private void cbDisableTP_MouseClick(object sender, MouseEventArgs e)
        {
            if (cbDisableTP.Checked == true)
            {
                m_dev.SetProperty((int)SYNCTRLLib.SynDeviceProperty.SP_DisablePDIfExtPresent, 1);
            } else {
                m_dev.SetProperty((int)SYNCTRLLib.SynDeviceProperty.SP_DisablePDIfExtPresent, 0);
            }
        }

        private void cbTray_MouseClick(object sender, MouseEventArgs e)
        {
            int pValue = 0;
            m_dev.GetProperty(524288, ref pValue);
            m_dev.SetProperty(1507328, !cbTray.Checked ? pValue | 32 : pValue & -33 | 17);
        }

        private void btnMoreInf_Click(object sender, EventArgs e)
        {
            string dvrFolder = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + "\\Synaptics\\SynTp";
            RegistryKey synTPKey = Registry.LocalMachine.OpenSubKey("Software\\Synaptics\\SynTP\\Install", false);
            if (synTPKey != null)
            {
                String progDir = (String)synTPKey.GetValue("ProgDir");
                if (string.IsNullOrEmpty(progDir))
                    progDir = dvrFolder;
                if (progDir.Length <= 0)
                    return;
                try
                {
                    Process.Start(new ProcessStartInfo(progDir + "\\SynTPEnh.exe")
                    {
                        Arguments = "/information"
                    });
                }
                catch (Exception)
                {
                    MessageBox.Show(@"Not able to show Dynamic Version.", @"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void cbSkin_MouseClick(object sender, MouseEventArgs e)
        {
            try // set AlienwareUI
            {
                RegistryKey SynTPEnhKey = Registry.LocalMachine.OpenSubKey("Software\\Synaptics\\SynTPEnh", false);
                RegistryKey synTPCplKey = Registry.LocalMachine.OpenSubKey("Software\\Synaptics\\SynTPCpl", true);
                if (synTPCplKey != null)
                {
                    int awUI = Convert.ToInt32(synTPCplKey.GetValue("AlienwareUI"));
                    int uiStyle = Convert.ToInt32(synTPCplKey.GetValue("UIStyle"));
                    if (SynTPEnhKey != null)
                    {
                        string SynTPEnhUI = (string)SynTPEnhKey.GetValue("DoubleClickTrayActionPath");
                        string[] sEnhUI = SynTPEnhUI.Split('\\');
                    }

                    if (awUI == 0)
                    {
                        synTPCplKey.SetValue("AlienwareUI", "1");
                        synTPCplKey.Flush();
                        awUI = Convert.ToInt32(synTPCplKey.GetValue("AlienwareUI"));
                        if (awUI != 1)
                        {
                            MessageBox.Show(@"Changes are not applied for some reason..", @"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        cbSkin.Checked = true;
                    }
                    else
                    {
                        synTPCplKey.SetValue("AlienwareUI", "0");
                        synTPCplKey.Flush();
                        awUI = Convert.ToInt32(synTPCplKey.GetValue("AlienwareUI"));
                        if (awUI != 0)
                        {
                            MessageBox.Show(@"Changes are not applied for some reason..", @"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        cbSkin.Checked = false;
                    }
                }

                synTPCplKey.Close();
                restartTrayAction();
            }
            catch (Exception ex)
            {
                MessageBox.Show(@"Insufficient permissions, try running as administrator!\n\nError:" + ex.Message, @"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void cbUnlockHiddenScroll_MouseClick(object sender, MouseEventArgs e)
        {           
            try // set UIStyle
            {
                using (RegistryKey synTPCplKey = Registry.LocalMachine.OpenSubKey("Software\\Synaptics\\SynTPCpl", true))
                {
                    if (synTPCplKey != null)
                    {
                        int uiStyle = Convert.ToInt32(synTPCplKey.GetValue("UIStyle"));
                        if (uiStyle == 14)
                        {
                            synTPCplKey.SetValue("UIStyle", "15");
                            synTPCplKey.Flush();
                            uiStyle = Convert.ToInt32(synTPCplKey.GetValue("UIStyle"));
                            if (uiStyle != 15)
                            {
                                MessageBox.Show(@"Changes are not applied for some reason..", @"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                            cbUnlockHiddenScroll.Checked = true;
                        }
                        else
                        {
                            synTPCplKey.SetValue("UIStyle", "14");
                            synTPCplKey.Flush();
                            uiStyle = Convert.ToInt32(synTPCplKey.GetValue("UIStyle"));
                            if (uiStyle != 14)
                            {
                                MessageBox.Show(@"Changes are not applied for some reason..", @"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                            cbUnlockHiddenScroll.Checked = false;
                        }
                    }

                    synTPCplKey.Close();
                }

                restartTrayAction();
            }
            catch (Exception ex)
            {
                MessageBox.Show(@"Insufficient permissions, try running as administrator!\n\nError:" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void restartTrayAction()
        {
            using (RegistryKey SynTPEnhKey = Registry.LocalMachine.OpenSubKey("Software\\Synaptics\\SynTPEnh", false))
            {
                if (SynTPEnhKey != null)
                {
                    string SynTPEnhUI = (string)SynTPEnhKey.GetValue("DoubleClickTrayActionPath");
                    string[] sEnhUI = SynTPEnhUI.Split('\\');
                    char[] trimext = { '.', 'e', 'x', 'e' };
                    string exeName = sEnhUI[sEnhUI.Length - 1].ToString().TrimEnd(trimext);
                    foreach (var process in Process.GetProcessesByName(exeName))
                    {
                        process.Kill();
                    }
                    Process.Start(new ProcessStartInfo(SynTPEnhUI) { });
                }
            }
        }

        public string GetVersion() // disassembled getversion
        {
            string str = "Version";
            int pValue = -1;
            uint num1 = 4278190080;
            int num2 = 16711680;
            int num3 = 65280;
            int maxValue = (int)byte.MaxValue;
            int num4 = 24;
            int num5 = 16;
            int num6 = 8;
            try
            {
                if (m_api != null)
                {
                    m_api.GetProperty(268435460, ref pValue);
                    if (pValue >= 0)
                    {
                        str =
                            $"v.{(object) (((long) pValue & (long) num1) >> num4)}.{(object) ((pValue & num2) >> num5)}.{(object) ((pValue & num3) >> num6)}";
                        if ((pValue & maxValue) >= 0)
                            str += $".{(object) (pValue & maxValue)}";
                    }
                }
            }
            catch (Exception)
            {
                str = "Version unknown";
            }
            return str;
        }

        private void m_PointerSpeed_ValueChanged(object sender, EventArgs e)
        {
            m_dev.SetProperty(16777884, m_PointerSpeed.Value);
        }

        private void m_Touchpressure_ValueChanged(object sender, EventArgs e)
        {
            m_dev.SetProperty(268435752, m_Touchpressure.Value);
        }

        private void m_Tappingspeed_ValueChanged(object sender, EventArgs e)
        {
            m_dev.SetProperty(16777531, m_Tappingspeed.Value);
        }

        private void m_Touchguard_ValueChanged(object sender, EventArgs e)
        {
            m_dev.SetProperty(268435843, m_Touchguard.Value);
        }

        private void m_Ticktoclick_MouseClick(object sender, MouseEventArgs e)
        {
            //// 0 = none, 3 = tick to click, 7 = tick to click & touchguard
            if (m_Ticktoclick.Checked == false || m_Touchgaurd.Checked == true)
            {
                m_dev.SetProperty(268435726, 0);
                m_Touchgaurd.Checked = false;
                m_Tappingspeed.Enabled = false;
            }
            else if(m_Ticktoclick.Checked == true && m_Touchgaurd.Checked == false)
            {
                m_dev.SetProperty(268435726, 3);
                m_Tappingspeed.Enabled = true;
            }
            else if(m_Ticktoclick.Checked == false && m_Touchgaurd.Checked == false)
            {
                m_dev.SetProperty(268435726, 0);
                m_Tappingspeed.Enabled = false;
            }
        }

        private void m_Palmdetection_MouseClick(object sender, MouseEventArgs e)
        {
            if(m_Palmdetection.Checked == true)
            {
                m_dev.SetProperty(16777876, 1114255);
                m_Touchguard.Enabled = true;
            } else {
                m_dev.SetProperty(16777876, 1114254);               
                m_Touchguard.Enabled = false;
            }
        }

        private void m_Touchgaurd_MouseClick(object sender, MouseEventArgs e)
        {
            if(m_Ticktoclick.Checked == false)
            {
                MessageBox.Show(@"Please first enable tick to click.", @"Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                m_Touchgaurd.Checked = false;
            }
            else if (m_Ticktoclick.Checked == false && m_Touchgaurd.Checked == true)
            {
                m_dev.SetProperty(268435726, 0);
                m_Touchgaurd.Checked = false;
            }
            else if(m_Ticktoclick.Checked == true && m_Touchgaurd.Checked == false)
            {  
                m_dev.SetProperty(268435726, 3);
            }
            else if (m_Ticktoclick.Checked == true && m_Touchgaurd.Checked == true)
            {
                m_dev.SetProperty(268435726, 7);
            }
        }

        private void m_Zigzag_MouseClick(object sender, MouseEventArgs e)
        {
            if(m_Zigzag.Checked == true)
            {
                m_dev.SetProperty(16778072, 1);
            } else {
                m_dev.SetProperty(16778072, 0);
            }
        }

        private void cbTouchpadenabled_MouseClick(object sender, MouseEventArgs e)
        {
            if(cbTouchpadenabled.Checked == true)
            {
                m_dev.SetProperty((int)SYNCTRLLib.SynDeviceProperty.SP_DisableState, 0);
            }
            else
            {
                m_dev.SetProperty((int)SYNCTRLLib.SynDeviceProperty.SP_DisableState, 1);
            }
        }

        private void m_circularScrolling_MouseClick(object sender, MouseEventArgs e)
        {
            // 0 disable, 5 slow scroll, 17 circular scroll
            if (m_Circularscrolling.Checked == false)
            {
                m_dev.SetProperty((int)SYNCTRLLib.SynDeviceProperty.SP_VerticalScrollingFlags, 1);
            } else {
                m_Slowscrolling.Checked = false;
                m_dev.SetProperty((int)SYNCTRLLib.SynDeviceProperty.SP_VerticalScrollingFlags, 17);
            }
        }

        private void m_Enableedgesrolling_MouseClick(object sender, MouseEventArgs e)
        {
            if (m_Enableedgesrolling.Checked == true)
            {
                m_dev.SetProperty((int)SYNCTRLLib.SynDeviceProperty.SP_VerticalScrollingFlags, 1);
            }
            else
            {
                m_dev.SetProperty((int)SYNCTRLLib.SynDeviceProperty.SP_VerticalScrollingFlags, 0);
            }
        }

        private void m_DisableNavInButtuonZone_MouseClick(object sender, MouseEventArgs e)
        {
            if (m_DisableNavInButtuonZone.Checked == true)
            {
                m_dev.SetProperty((int)SYNCTRLLib.SynDeviceProperty.SP_DisableNavigationInButtonZone, 1);
            }
            else
            {
                m_dev.SetProperty((int)SYNCTRLLib.SynDeviceProperty.SP_EnableNavigationInButtonZone, 1);
            }                          
        }

        private void m_Slowscrolling_MouseClick(object sender, MouseEventArgs e)
        {
            // 0 disable, 5 slow scroll, 17 circular scroll
            if (m_Slowscrolling.Checked == false)
            {
                m_dev.SetProperty((int)SYNCTRLLib.SynDeviceProperty.SP_VerticalScrollingFlags, 1);
            } else {               
                m_dev.SetProperty((int)SYNCTRLLib.SynDeviceProperty.SP_VerticalScrollingFlags, 5);
                m_Circularscrolling.Checked = false;
            }
        }

        private void m_edgescrollspeed_ValueChanged(object sender, EventArgs e)
        {
            m_dev.SetProperty((int)SYNCTRLLib.SynDeviceProperty.SP_VerticalScrollSpeed, m_Edgescrollspeed.Value);
        }

        private void btndebug_Click(object sender, EventArgs e)
        {
            // 32 - 256 268435847
            //m_dev.SetProperty((int)SYNCTRLLib.SynDeviceProperty.SP_IlluminationEnabledState, 1);
            //m_dev.SetProperty((int)SYNCTRLLib.SynDeviceProperty.SP_TouchPadLightingEnabled, 100);
            int debug = 0;
            m_dev.GetProperty((int)SYNCTRLLib.SynDeviceProperty.SP_2FHorizontalScrollingFlags, ref debug);
            //268435845
            MessageBox.Show(debug.ToString());

            //1 = off, 5 = on 268435844
        }

        private void m_Virtualscrollregionnorrow_MouseClick(object sender, MouseEventArgs e)
        {
            //SP_HorizontalScrollRegionHeight, SP_VerticalScrollRegionWidth
            m_dev.SetProperty(262144, (int)((double)268 * (2.0 / 3.0))); //DefaultHorizontalScrollRegionHeight
            m_dev.SetProperty(65536, (int)((double)268 * (2.0 / 3.0))); //DefaultVerticalScrollRegionWidth
            //m_dev.SetProperty(4128768, (int)((double)touchpadScrollAndZoom.iDefaultLeftSliderRegionWidth * (2.0 / 3.0)));
        }

        private void m_Virtualscrollregionnormal_MouseClick(object sender, MouseEventArgs e)
        {
            m_dev.SetProperty(262144, (int)((double)268));
            m_dev.SetProperty(65536, (int)((double)268));
        }

        private void m_Virtualscrollregionwide_MouseClick(object sender, MouseEventArgs e)
        {
            m_dev.SetProperty(262144, (int)((double)268 * (4.0 / 3.0))); //DefaultHorizontalScrollRegionHeight
            m_dev.SetProperty(65536, (int)((double)268 * (4.0 / 3.0))); //DefaultVerticalScrollRegionWidth
        }

        private void m_Zoomscroll_MouseClick(object sender, MouseEventArgs e)
        {
            if (m_Zoomscroll.Checked == false)
            {
                m_dev.SetProperty(2424832, 5120);
            } else {
                m_dev.SetProperty(2424832, 5121);
            }
        }

        private void checkGestureValuesAndSet()
        {
            /*
            iMultiFingerGesture
            1 = disable all
            4097 = all controls active but none enabled
            4099 = pan/scroll
            4115 = pan/scroll + squeeze zoom
            4107 = pan/scroll + turning
            4123 = everything
            -----------------
            4113 = only squeeze zoom
            4105 = only turning
            4121 = squeeze zoom and turning
            */
            bool pan = m_2FPanning.Checked;
            bool zoom = m_2Fsueezezoom.Checked;
            bool turn = m_2Fturning.Checked;

            //off
            if (pan == false && zoom == false && turn == false)
            {
                m_dev.SetProperty(589824, 4097);
            }
            //on
            if (pan == true && zoom == true && turn == true)
            {
                m_dev.SetProperty(589824, 4123);
            }
            //pan/scroll only
            if (pan == true && zoom == false && turn == false)
            {
                m_dev.SetProperty(589824, 4099);
            }
            //pan/scroll + squeeze zoom
            if (pan == true && zoom == true && turn == false)
            {
                m_dev.SetProperty(589824, 4115);
            }
            //pan/scroll + turning
            if (pan == true && zoom == false && turn == true)
            {
                m_dev.SetProperty(589824, 4107);
            }
            //squeeze zoom
            if (pan == false && zoom == true && turn == false)
            {
                m_dev.SetProperty(589824, 4113);
            }
            //turning
            if (pan == false && zoom == false && turn == true)
            {
                m_dev.SetProperty(589824, 4105);
            }
            //turning + squeeze zoom
            if (pan == false && zoom == true && turn == true)
            {
                m_dev.SetProperty(589824, 4121);
            }
        }

        private void m_Enable2Fgestures_MouseClick(object sender, MouseEventArgs e)
        {
            if (m_Enable2Fgestures.Checked == true)
            {
                m_dev.SetProperty(589824, 4097);
                m_2FPanning.Enabled = true;
                m_2Fsueezezoom.Enabled = true;
                m_2Fturning.Enabled = true;
                m_Panscrollspeed.Enabled = true;
            }
            else
            {
                m_dev.SetProperty(589824, 1);
                m_2FPanning.Enabled = false;
                m_2Fsueezezoom.Enabled = false;
                m_2Fturning.Enabled = false;
                m_Panscrollspeed.Enabled = false;
            }
        }

        private void m_Doubleclickspeed_ValueChanged(object sender, EventArgs e)
        {
            try
            {
                //NT 6.1 low 900 high 200
                int sense = m_Doubleclickspeed.Value;
                SystemParametersInfo(32, sense, string.Empty, 1);
            }
            catch {}
        }

        private void m_Panscrollspeed_ValueChanged(object sender, EventArgs e)
        {
            m_dev.SetProperty(268435847, m_Panscrollspeed.Value);
        }

        private void m_2FPanning_MouseClick(object sender, MouseEventArgs e)
        {
            checkGestureValuesAndSet();
        }

        private void m_2Fsueezezoom_MouseClick(object sender, MouseEventArgs e)
        {
            checkGestureValuesAndSet();
        }

        private void m_2Fturning_MouseClick(object sender, MouseEventArgs e)
        {
            checkGestureValuesAndSet();
        }

        private void m_Switchscreen_MouseClick(object sender, MouseEventArgs e)
        {
            checkGestureValuesAndSet();
        }

        private void m_2Fslowscrolling_MouseClick(object sender, MouseEventArgs e)
        {
            if(m_2Fslowscrolling.Checked == true)
            {
                m_dev.SetProperty((int)SYNCTRLLib.SynDeviceProperty.SP_2FHorizontalScrollingFlags, 5);               
            } else {
                m_dev.SetProperty((int)SYNCTRLLib.SynDeviceProperty.SP_2FHorizontalScrollingFlags, 1);
            }
        }
    }
}
