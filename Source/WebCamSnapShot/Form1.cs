// WebCamSnapShot

// Jeff Godfrey
// August 2011

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;

namespace WebCamSnapShot
{
    public partial class Form1 : Form
    {
        private bool _connected = false;

        private StreamWriter _sw;
        private string _logFile;

        private string _outfile = "WebCamSnapShot.jpg";
        private int _delay = 150;
        private bool _auto = false;
        private bool _log = false;
        private bool _debug = false;
        private int _imageWidth = 640;
        private int _imageHeight = 480;

        [DllImport("user32", EntryPoint = "SendMessage")]
        public static extern int SendMessage(int hWnd, uint Msg, int wParam, int lParam);

        [DllImport("avicap32.dll", EntryPoint = "capCreateCaptureWindowA")]
        public static extern int capCreateCaptureWindowA(string lpszWindowName, int dwStyle, int X, int Y, int nWidth, int nHeight, int hwndParent, int nID);

        public const int WM_USER = 1024;
        public const int WM_CAP_CONNECT = 1034;
        public const int WM_CAP_DISCONNECT = 1035;
        public const int WM_CAP_GET_FRAME = 1084;
        public const int WM_CAP_COPY = 1054;

        public const int WS_CHILD = 0x40000000;
        public const int WS_VISIBLE = 0x10000000;

        public const int WM_CAP_START = WM_USER;
        public const int WM_CAP_SET_SCALE = WM_CAP_START + 53;
        public const int WM_CAP_SET_PREVIEWRATE = WM_CAP_START + 52;
        public const int WM_CAP_SET_PREVIEW = WM_CAP_START + 50;

        private int mCapHwnd = 0;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            ParseCmdLine();
            if (_log) { CreateLogFile(); }
            Log("Starting up");

            if (_auto) 
            { 
                this.WindowState = FormWindowState.Minimized;
                this.ShowInTaskbar = false;
                this.Visible = false;
                if (Connect())
                {
                    timerSnap.Interval = _delay;
                    timerSnap.Enabled = true;
                }
                else
                {
                    AppExit();
                }
            }
        }

        private Image GetSnapShot()
        {
            Log("Getting snapshot from webcam");
            SendMessage(mCapHwnd, WM_CAP_GET_FRAME, 0, 0);
            SendMessage(mCapHwnd, WM_CAP_COPY, 0, 0);

            Image img = Clipboard.GetImage();
            Clipboard.Clear();
            return img;
        }

        private bool Connect()
        {
            bool retVal = false;

            mCapHwnd = capCreateCaptureWindowA("WebCap", WS_VISIBLE | WS_CHILD, 0, 0, _imageWidth, _imageHeight, this.pictureBox.Handle.ToInt32(), 0);
            Application.DoEvents();
            if (SendMessage(mCapHwnd, WM_CAP_CONNECT, 0, 0) > 0) //This will make sure the device is connected to the System
            {
                // Set the preview rate
                SendMessage(mCapHwnd, WM_CAP_SET_PREVIEWRATE, 30, 0);
                // Set the preview flag to true
                SendMessage(mCapHwnd, WM_CAP_SET_PREVIEW, 1, 0);
                _connected = true;
                retVal = true;
                Log("Successfully connected to webcam");
            }
            else
            {
                Log("Failed to connect to webcam");
            }

            return retVal;
        }

        private void Disconnect()
        {
            Log("Disconnecting from webcam");
            _connected = false;

            try
            {
                Application.DoEvents();
                SendMessage(mCapHwnd, WM_CAP_DISCONNECT, mCapHwnd, 0);
            }
            catch
            { // don't throw an error here...
            }
        }

        private void ParseCmdLine()
        {
            String[] args = Environment.GetCommandLineArgs();
            if (args.Length > 1 && args.Length % 2 == 0)
            {
                string appName = Path.GetFileName(args[0]);
                string msg =
                   @"Usage: {0} <options>
                     
                     where <options> =

                     -outfile ? (string, default = {1})
                     -delay ? (integer, default = {2} milliseconds)
                     -height ? (integer, default = {3} pixels)
                     -width ? (integer, default = {4} pixels)
                     -auto ? (boolean, default = {5})
                     -log ? (boolean, default = {6})
                     -debug ? (bool, default = {7})"; 
                msg = String.Format(msg, appName, _outfile,
                    _delay.ToString(), _imageHeight.ToString(), 
                    _imageWidth.ToString(), _auto.ToString(), 
                    _log.ToString(), _debug.ToString() );
                MessageBox.Show(msg, appName + " Usage");
                AppExit();
            }

            for (int i = 1; i < args.Length; i++)
            {
                string opt = args[i].ToLower();
                string val = args[i + 1];

                switch (opt)
                {
                    case "-outfile":
                        // Replace all forward slashes with back slashes
                        // Prey passes some malformed paths (a mix of slashes...)
                        _outfile = val.Replace('/', '\\');
                        // Add a .jpg extension if none was specified
                        if (_outfile == String.Empty) { _outfile += ".jpg"; }
                        break;
                    case "-delay":
                        int del;
                        if (Int32.TryParse(val, out del) && del > 0) { _delay = del; }
                        break;
                    case "-height":
                        int hgt;
                        if (Int32.TryParse(val, out hgt) && hgt > 0) { _imageHeight = hgt; }
                        break;
                    case "-width":
                        int wid;
                        if (Int32.TryParse(val, out wid) && wid > 0) { _imageWidth = wid; }
                        break;
                    case "-auto":
                        bool auto;
                        if (Boolean.TryParse(val, out auto)) { _auto = auto; }
                        break;
                    case "-debug":
                        bool debug;
                        if (Boolean.TryParse(val, out debug)) { _debug = debug; }
                        break;
                    case "-log":
                        bool log;
                        if (Boolean.TryParse(val, out log)) { _log = log; }
                        break;
                }
                i++;
            }

            if (_debug)
            {
                string msg = "outfile = " + _outfile + Environment.NewLine;
                msg += "delay = " + _delay.ToString() + Environment.NewLine;
                msg += "width = " + _imageWidth.ToString() + Environment.NewLine;
                msg += "height = " + _imageHeight.ToString() + Environment.NewLine;
                msg += "auto = " + _auto.ToString() + Environment.NewLine;
                msg += "log = " + _log.ToString() + Environment.NewLine;
                msg += "debug = " + _debug.ToString() + Environment.NewLine;
                MessageBox.Show(msg, "Parsed Command Line Values");
            }
        }

        private void btnStartMonitor_Click(object sender, EventArgs e)
        {
            if (!Connect())
            {
                MessageBox.Show("Unable to connect to camera.", "Error");
            }

        }

        private void btnTakePic_Click(object sender, EventArgs e)
        {
            if (_connected)
            {
                Image img = GetSnapShot();
                if (img != null)
                {
                    using (SaveFileDialog dlg = new SaveFileDialog())
                    {
                        dlg.Filter = "JPG Images|*.jpg";
                        dlg.OverwritePrompt = true;
                        dlg.ValidateNames = true;
                        dlg.DefaultExt = "JPG";
                        if (dlg.ShowDialog(this) == DialogResult.OK)
                        {
                            img.Save(dlg.FileName, ImageFormat.Jpeg);
                        }
                    }
                }
            }
            else
            {
                MessageBox.Show("Not connected to the camera.", "Error");
            }
            Disconnect();
            Connect();
        }

        private void timerSnap_Tick(object sender, EventArgs e)
        {
            Log("Timer expired - ready to take snapshot");
            timerSnap.Enabled = false;
            if (_connected)
            {
                Image img = GetSnapShot();
                if (img != null)
                {
                    try
                    {
                        img.Save(_outfile, ImageFormat.Jpeg);
                        Log("Successfully saved snapshot - " + _outfile);
                    }
                    catch (Exception ex)
                    {
                        Log("Error saving file " + _outfile);
                        Log("Error was: " + ex.Message);
                    }
                }
            }
            AppExit();
        }

        public void AppExit()
        {
            Disconnect();

            Log("Exiting...");

            CloseLogFile();

            if (System.Windows.Forms.Application.MessageLoop)
            {
                // Use this since we are a WinForms app
                System.Windows.Forms.Application.Exit();
            }
            else
            {
                // Use this since we are a console app
                System.Environment.Exit(1);
            }
        }

        public void CreateLogFile()
        {
            // Create a new, empty log file
            string appPath = Path.GetDirectoryName(Application.ExecutablePath);
            _logFile = Path.Combine(appPath, "WebCamSnapShot.log");
            _sw = new StreamWriter(_logFile, false);
        }

        public void CloseLogFile()
        {
            try
            {
                _sw.Close();
            }
            catch
            { // don't throw an error here...
            }
        }

        public void Log(string msg)
        {
            if (!_log) { return; }

            try
            {
                string timeStamp = DateTime.Now.ToString("yyyy-MM-dd_HH:mm:ss:ffff");
                _sw.WriteLine(timeStamp + " - " + msg);
            }
            catch
            { // don't throw an error here...
            }
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            AppExit();
        }
    }
}
