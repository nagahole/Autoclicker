using Gma.System.MouseKeyHook;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;

namespace Autoclicker {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        #region stolen
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool SetCursorPos(int x, int y);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern void mouse_event(int dwFlags, int dx, int dy, int cButtons, int dwExtraInfo);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(ref Win32Point pt);

        [StructLayout(LayoutKind.Sequential)]
        internal struct Win32Point {
            public Int32 X;
            public Int32 Y;
        };

        public static Point GetMousePosition() {
            var w32Mouse = new Win32Point();
            GetCursorPos(ref w32Mouse);

            return new Point(w32Mouse.X, w32Mouse.Y);
        }

        //This simulates a left mouse click
        public static void LeftMouseClick() {
            var point = GetMousePosition();

            mouse_event(MOUSEEVENTF_LEFTDOWN, (int) point.X, (int)point.Y, 0, 0);
            mouse_event(MOUSEEVENTF_LEFTUP, (int)point.X, (int)point.Y, 0, 0);
        }
        
        public const int MOUSEEVENTF_LEFTDOWN = 0x02;
        public const int MOUSEEVENTF_LEFTUP = 0x04;
        #endregion

        #region UI References
        private System.Windows.Controls.TextBox minCpsTextbox;
        private System.Windows.Controls.TextBox maxCpsTextbox;
        private TextBlock hotkeyTextblock;
        private System.Windows.Controls.CheckBox holdCheckbox;

        private const string
            minCpsTextboxName = "MinCpsTextBox",
            maxCpsTextboxName = "MaxCpsTextBox",
            hotkeyTextblockName = "HotkeyText",
            holdCheckboxName = "HoldCheckbox";
        #endregion

        private bool
            toggled = false,
            wait = true,
            listening = false;

        float maxCps, minCps;
        int min, max;

        #region Hotkeys
        private enum HotkeyType {
            mouseButton,
            key
        }

        HotkeyType hotkeyType = HotkeyType.key;
        Keys key = Keys.G;
        MouseButtons mouseButton;
        #endregion

        #region API / USEFUL CLASSES
        private IKeyboardMouseEvents m_GlobalHook;
        private Random rand = new Random();
        #endregion

        #region Input Listeners
        public void Subscribe() {
            m_GlobalHook = Hook.GlobalEvents();

            m_GlobalHook.KeyDown += GlobalHookKeyDown;
            m_GlobalHook.MouseClick += GlobalMousePress;
        }

        private void GlobalHookKeyDown(object sender, KeyEventArgs e) {
            if (listening) {
                listening = false;
                hotkeyType = HotkeyType.key;
                key = e.KeyCode;
                MatchHotkeyTextblockText();
                return;
            }

            if (e.KeyCode == key && hotkeyType == HotkeyType.key) {
                Toggle();
            }
        }

        private void GlobalMousePress(object sender, System.Windows.Forms.MouseEventArgs e) {
            if (listening) {
                listening = false;
                hotkeyType = HotkeyType.mouseButton;
                mouseButton = e.Button;
                MatchHotkeyTextblockText();
                return;
            }

            if (hotkeyType == HotkeyType.mouseButton && mouseButton == e.Button) {
                Toggle();
            }
        }

        #endregion

        #region Initializations / Fired once
        public MainWindow() {
            InitializeComponent();
            Subscribe();
            InitializeElements();

            this.Closed += (s, e) => {
                App.Current.Shutdown();
                Process.GetCurrentProcess().Kill();
            };


            MatchHotkeyTextblockText();

            Thread AC = new Thread(AutoClick);

            AC.Start();

            Thread.Sleep(1);
            wait = false;

            minCps = float.Parse(minCpsTextbox.Text);
            maxCps = float.Parse(maxCpsTextbox.Text);
        }

        private void InitializeElements() {
            minCpsTextbox = FindName(minCpsTextboxName) as System.Windows.Controls.TextBox;
            maxCpsTextbox = FindName(maxCpsTextboxName) as System.Windows.Controls.TextBox;
            hotkeyTextblock = FindName(hotkeyTextblockName) as TextBlock;
            holdCheckbox = FindName(holdCheckboxName) as System.Windows.Controls.CheckBox;
        }

        private void AutoClick() {
            int carryover = 0;
            while (true) {
                if (toggled) {
                    //int delay = rand.Next(min, max + 1) - carryover;
                    int delay = (int)System.Math.Round(1000f / (rand.NextDouble() * (maxCps - minCps) + minCps)) - carryover;

                    DateTime start = DateTime.Now;
                    LeftMouseClick();
                    while((DateTime.Now - start).Milliseconds <= delay) {
                        Thread.Sleep(1);
                    }
                    carryover = (DateTime.Now - start).Milliseconds - delay;
                } else {
                    Thread.Sleep(1);
                }
            }
        }
        #endregion

        #region Recurring Methods
        private void Toggle() {
            if (minCps <= 0) {
                minCpsTextbox.Text = "1";
            }

            if (maxCps < minCps) {
                maxCpsTextbox.Text = minCps.ToString();
            }

            toggled = !toggled;

            MatchHotkeyTextblockText();

            min = (int)System.Math.Round(1000f / (maxCps));
            max = (int)System.Math.Round(1000f / (minCps));

            Console.WriteLine(min + "." + max);
        }

        private void MatchHotkeyTextblockText() {
            hotkeyTextblock.Text = $"Press [" +
                $"{(hotkeyType == HotkeyType.key? key.ToString() : mouseButton.ToString())}" +
                $"] to " +
                $"{(toggled? "stop" : "start")}";
        }
        #endregion

        #region UI Commands
        private void ChangeHotkey_Click(object sender, RoutedEventArgs e) {
            if (wait) return;
            if (!listening) {
                listening = true;
                hotkeyTextblock.Text = "[Press a key]";
            } 
        }

        private void MinCpsTextBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (wait || minCps.ToString() == minCpsTextbox.Text) return;

            string newText = minCpsTextbox.Text;
            float n;

            if (float.TryParse(newText, out n)) {
                minCpsTextbox.Text = n.ToString();
                minCps = n;
            } else if (newText == string.Empty) {
                minCps = 0;
            } else {
                minCpsTextbox.Text = minCps.ToString();
            }
        }

        private void MaxCpsTextBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (wait || maxCps.ToString() == maxCpsTextbox.Text) return;

            string newText = maxCpsTextbox.Text;
            float n;

            if (float.TryParse(newText, out n)) {
                maxCpsTextbox.Text = n.ToString();
                maxCps = n;
            } else if (newText == string.Empty) {
                maxCps = 0;
            } else {
                maxCpsTextbox.Text = maxCps.ToString();
            }
        }
        #endregion
    }
}
