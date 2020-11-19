using Gma.System.MouseKeyHook;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using Autoclicker.Properties;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Globalization;

/// <summary>
/// DEFAULT MIN/MAX CPS VALUES SET IN XAML, NOT IN CODE
/// </summary>

namespace Autoclicker {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>

    public enum HotkeyType {
        mouseButton,
        key
    }

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

        public static void RightMouseClick() {
            var point = GetMousePosition();

            mouse_event(MOUSEEVENTF_RIGHTDOWN, (int)point.X, (int)point.Y, 0, 0);
            mouse_event(MOUSEEVENTF_RIGHTUP, (int)point.X, (int)point.Y, 0, 0);
        }

        public const int MOUSEEVENTF_LEFTDOWN = 0x0002;
        public const int MOUSEEVENTF_LEFTUP = 0x0004;

        public const int MOUSEEVENTF_RIGHTDOWN = 0x0008;
        public const int MOUSEEVENTF_RIGHTUP = 0x0010;
        #endregion

        #region UI References
        private System.Windows.Controls.TextBox lmbMinCpsTextbox;
        private System.Windows.Controls.TextBox lmbMaxCpsTextbox;
        private TextBlock lmbHotkeyTextblock;

        private System.Windows.Controls.TextBox rmbMinCpsTextbox;
        private System.Windows.Controls.TextBox rmbMaxCpsTextbox;
        private TextBlock rmbHotkeyTextblock;

        private TextBlock panicButtonTextblock;

        private System.Windows.Controls.Button lmbButton, rmbButton;


        //Advanced settings stuff
        private Grid advancedSettingsGrid;
        private TextBlock advancedSettingsToggleSymbol;

        private System.Windows.Controls.TextBox lmbRampupInitialTextBox;
        private System.Windows.Controls.TextBox rmbRampupInitialTextBox;

        private System.Windows.Controls.TextBox lmbRampupDurationTextBox;
        private System.Windows.Controls.TextBox rmbRampupDurationTextBox;

        private const string
            lmbMinCpsTextboxName = "LMBMinCpsTextBox",
            lmbMaxCpsTextboxName = "LMBMaxCpsTextBox",
            lmbHotkeyTextblockName = "LMBHotkeyText";

        private const string
            rmbMinCpsTextboxName = "RMBMinCpsTextBox",
            rmbMaxCpsTextboxName = "RMBMaxCpsTextBox",
            rmbHotkeyTextblockName = "RMBHotkeyText";

        private const string
            panicButtonTextblockName = "PanicButtonTextBlock";

        private const string
            lmbButtonName = "LMBButton",
            rmbButtonName = "RMBButton";

        private const string advancedSettingsGridName = "Advanced_Settings";
        private const string advancedSettingsToggleSymbolName = "AdvancedSettingsToggleSymbol";

        private const string
            lmbRampupInitialTextBoxName = "LMBRampupInitial",
            rmbRampupInitialTextBoxName = "RMBRampupInitial",

            lmbRampupDurationTextBoxName = "LMBRampupDuration",
            rmbRampupDurationTextBoxName = "RMBRampupDuration";

        private Brush toggledColor = new SolidColorBrush(Color.FromArgb(255, 39, 133, 43));
        #endregion

        private bool
            lmbToggled = false,
            rmbToggled = false,
            wait = true;

        float lmbMaxCps, lmbMinCps;
        float rmbMaxCps, rmbMinCps;

        #region Customisable Variables
        //Might remove this since this is kinda useless considering I can click right after an autoclicker click
        private int lmbMinDelay = 25, rmbMinDelay = 25; //When stacking normal clicking on top of autoclicking, this will ensure they won't be too close together
        private DateTime lastLmb, lastRmb;

        private int lmbRampupDuration = 800, rmbRampupDuration = 800; //In milliseconds - Time before reaching max cps after activating
        private DateTime lastLmbToggle, lastRmbToggle;

        private float lmbRampupInitial = 0.6f, rmbRampupInitial = 0.6f; //What fraction of target cps it begins at
        #endregion

        HotkeyButton lmbHotkeyButton, rmbHotkeyButton, panicHotkeyButton;

        #region API / USEFUL CLASSES
        private static IKeyboardMouseEvents m_GlobalHook;
        private Random rand = new Random();
        #endregion

        #region Initializations / Fired once

        private void Exit() {
            App.Current.Shutdown();
            Process.GetCurrentProcess().Kill();
        }

        public MainWindow() {
            InitializeComponent();
            InitializeElements();
            Subscribe();
            HotkeyButton.Initialize();

            CultureInfo ci = (CultureInfo)CultureInfo.CurrentCulture.Clone();
            ci.NumberFormat.CurrencyDecimalSeparator = ".";

            InitializeHotkeyButtons();

            this.Closed += (s, e) => {
                Exit();
            };

            Thread lmbAc = new Thread(LMBAutoClick);
            lmbAc.Start();

            Thread rmbAc = new Thread(RMBAutoClick);
            rmbAc.Start();

            Thread.Sleep(1);
            wait = false;

            lmbMinCps = float.Parse(lmbMinCpsTextbox.Text);
            lmbMaxCps = float.Parse(lmbMaxCpsTextbox.Text);

            rmbMinCps = float.Parse(rmbMinCpsTextbox.Text);
            rmbMaxCps = float.Parse(rmbMaxCpsTextbox.Text);
        }

        private void Subscribe() {
            m_GlobalHook = Hook.GlobalEvents();

            m_GlobalHook.MouseClick += GlobalHookMouseClick;
        }

        private void GlobalHookMouseClick(object sender, MouseEventArgs e) {
            if (e.Button == MouseButtons.Left) {
                if (rmbToggled && (DateTime.Now - lastRmbToggle).TotalMilliseconds < 20) {
                    ToggleRMB();
                }
                lastLmb = DateTime.Now;
            } else if (e.Button == MouseButtons.Right) {
                if (lmbToggled && (DateTime.Now - lastLmbToggle).TotalMilliseconds < 20) {
                    ToggleLMB();
                }
                lastRmb = DateTime.Now;
            }
        }

        private void InitializeElements() {
            lmbMinCpsTextbox = FindName(lmbMinCpsTextboxName) as System.Windows.Controls.TextBox;
            lmbMaxCpsTextbox = FindName(lmbMaxCpsTextboxName) as System.Windows.Controls.TextBox;
            lmbHotkeyTextblock = FindName(lmbHotkeyTextblockName) as TextBlock;

            rmbMinCpsTextbox = FindName(rmbMinCpsTextboxName) as System.Windows.Controls.TextBox;
            rmbMaxCpsTextbox = FindName(rmbMaxCpsTextboxName) as System.Windows.Controls.TextBox;
            rmbHotkeyTextblock = FindName(rmbHotkeyTextblockName) as TextBlock;

            panicButtonTextblock = FindName(panicButtonTextblockName) as TextBlock;

            lmbButton = FindName(lmbButtonName) as System.Windows.Controls.Button;
            rmbButton = FindName(rmbButtonName) as System.Windows.Controls.Button;

            advancedSettingsGrid = FindName(advancedSettingsGridName) as Grid;
            advancedSettingsToggleSymbol = FindName(advancedSettingsToggleSymbolName) as TextBlock;

            lmbRampupInitialTextBox = FindName(lmbRampupInitialTextBoxName) as System.Windows.Controls.TextBox;
            rmbRampupInitialTextBox = FindName(rmbRampupInitialTextBoxName) as System.Windows.Controls.TextBox;

            lmbRampupDurationTextBox = FindName(lmbRampupDurationTextBoxName) as System.Windows.Controls.TextBox;
            rmbRampupDurationTextBox = FindName(rmbRampupDurationTextBoxName) as System.Windows.Controls.TextBox;
        }

        private void InitializeHotkeyButtons() {
            //LMB
            lmbHotkeyButton = new HotkeyButton(MouseButtons.XButton2);
            lmbHotkeyTextblock.Text = $"Press [{(lmbHotkeyButton.GetHotkey())}] to start";
            lmbHotkeyButton.HotkeySetEvent += (sender, e) => {
                lmbHotkeyTextblock.Text = $"Press [{(lmbHotkeyButton.GetHotkey())}] to start";
            };
            lmbHotkeyButton.HotkeyPressed += (sender, e) => {
                ToggleLMB();
            };
            lmbHotkeyButton.SelectHotkeyEvent += (sender, e) => {
                lmbHotkeyTextblock.Text = "[Press any key]";
            };

            //RMB

            rmbHotkeyButton = new HotkeyButton(MouseButtons.XButton1);
            rmbHotkeyTextblock.Text = $"Press [{(rmbHotkeyButton.GetHotkey())}] to start";
            rmbHotkeyButton.HotkeySetEvent += (sender, e) => {
                rmbHotkeyTextblock.Text = $"Press [{(rmbHotkeyButton.GetHotkey())}] to start";
            };
            rmbHotkeyButton.HotkeyPressed += (sender, e) => {
                ToggleRMB();
            };
            rmbHotkeyButton.SelectHotkeyEvent += (sender, e) => {
                rmbHotkeyTextblock.Text = "[Press any key]";
            };

            //Hotkey Button
            panicHotkeyButton = new HotkeyButton(Keys.Multiply);
            panicButtonTextblock.Text = $"Panic Button [{panicHotkeyButton.GetHotkey()}]";
            panicHotkeyButton.HotkeySetEvent += (sender, e) => {
                panicButtonTextblock.Text = $"Panic Button [{panicHotkeyButton.GetHotkey()}]";
            };
            panicHotkeyButton.HotkeyPressed += (sender, e) => {
                Close();
            };
            panicHotkeyButton.SelectHotkeyEvent += (sender, e) => {
                panicButtonTextblock.Text = "[Press any key]";
            };
        }

        private void LMBAutoClick() {
            int carryover = 0;
            while (true) {
                if (lmbToggled) {
                    //int delay = rand.Next(min, max + 1) - carryover;
                    int delay = (int) ((System.Math.Round(1000f / (rand.NextDouble() * (lmbMaxCps - lmbMinCps) + lmbMinCps)) - carryover)
                        / ((System.Math.Min((DateTime.Now - lastLmbToggle).TotalMilliseconds, lmbRampupDuration) / (float)lmbRampupDuration) * (1 - lmbRampupInitial) + lmbRampupInitial)
                        );

                    DateTime start = DateTime.Now;

                    if((lastLmb - DateTime.Now).TotalMilliseconds <= lmbMinDelay) {
                        LeftMouseClick();
                    }
                    
                    while((DateTime.Now - start).TotalMilliseconds <= delay) {
                        Thread.Sleep(1);
                    }
                    carryover = (int) (DateTime.Now - start).TotalMilliseconds - delay;
                } else {
                    Thread.Sleep(1);
                }
            }
        }

        private void RMBAutoClick() {
            int carryover = 0;
            while (true) {
                if (rmbToggled) {
                    //int delay = rand.Next(min, max + 1) - carryover;
                    int delay = (int)((System.Math.Round(1000f / (rand.NextDouble() * (rmbMaxCps - rmbMinCps) + rmbMinCps)) - carryover)
                        / ((System.Math.Min((DateTime.Now - lastRmbToggle).TotalMilliseconds, rmbRampupDuration) / (float)rmbRampupDuration) * (1 - rmbRampupInitial) + rmbRampupInitial)
                        );

                    DateTime start = DateTime.Now;

                    if ((lastRmb - DateTime.Now).TotalMilliseconds <= rmbMinDelay) {
                        RightMouseClick();
                    }
                    
                    while ((DateTime.Now - start).TotalMilliseconds <= delay) {
                        Thread.Sleep(1);
                    }
                    carryover = (int) (DateTime.Now - start).TotalMilliseconds - delay;
                } else {
                    Thread.Sleep(1);
                }
            }
        }
        #endregion

        #region Recurring Methods

        private void ToggleLMB() {
            lastLmbToggle = DateTime.Now;
            if (lmbMinCps <= 0) {
                lmbMinCpsTextbox.Text = "1";
            }

            if (lmbMaxCps < lmbMinCps) {
                lmbMaxCpsTextbox.Text = lmbMinCps.ToString();
            }

            lmbToggled = !lmbToggled;
            rmbToggled = false;

            Console.Beep(lmbToggled ? 15000 : 8000, 1);

            UpdateLMBVisuals();
            UpdateRMBVisuals();
        }

        private void ToggleRMB() {
            lastRmbToggle = DateTime.Now;
            if (rmbMinCps <= 0) {
                rmbMinCpsTextbox.Text = "1";
            }

            if (rmbMaxCps < rmbMinCps) {
                rmbMaxCpsTextbox.Text = rmbMinCps.ToString();
            }

            rmbToggled = !rmbToggled;
            lmbToggled = false;

            Console.Beep(rmbToggled ? 15000 : 8000, 1);

            UpdateLMBVisuals();
            UpdateRMBVisuals();
        }

        private void UpdateLMBVisuals() {
            if (lmbToggled) {
                lmbButton.Background = toggledColor;
            } else {
                lmbButton.ClearValue(BackgroundProperty);
            }
            
            lmbHotkeyTextblock.Text = $"Press [" +
                $"{lmbHotkeyButton.GetHotkey()}" +
                $"] to " +
                $"{(lmbToggled? "stop" : "start")}";
        }

        private void UpdateRMBVisuals() {
            if (rmbToggled) {
                rmbButton.Background = toggledColor;
            } else {
                rmbButton.ClearValue(BackgroundProperty);
            }

            rmbHotkeyTextblock.Text = $"Press [" +
                $"{rmbHotkeyButton.GetHotkey()}" +
                $"] to " +
                $"{(rmbToggled ? "stop" : "start")}";
        }
        #endregion

        #region UI Commands
        private void LMB_ChangeHotkey_Click(object sender, RoutedEventArgs e) {
            lmbHotkeyButton.OnClick();
        }

        private void RMB_ChangeHotkey_Click(object sender, RoutedEventArgs e) {
            rmbHotkeyButton.OnClick();
        }

        private void PanicButton_Click(object sender, RoutedEventArgs e) {
            panicHotkeyButton.OnClick();
        }

        private void LMBMinCpsTextBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (wait) return;

            string newText = lmbMinCpsTextbox.Text;
            float n;

            if (newText.Length > 0 && newText[newText.Length - 1] == '.') {
                newText = newText.Remove(newText.Length - 1, 1);
            }

            Console.WriteLine(newText);
            if (float.TryParse(newText, out n)) {
                lmbMinCps = n;
            } else if (newText == string.Empty) {
                lmbMinCps = 0;
            } else {
                lmbMinCpsTextbox.Text = lmbMinCps.ToString();
            }
        }

        private void LMBMaxCpsTextBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (wait) return;

            string newText = lmbMaxCpsTextbox.Text;
            float n;

            if (newText.Length > 0 && newText[newText.Length - 1] == '.') {
                newText = newText.Remove(newText.Length - 1, 1);
            }

            if (float.TryParse(newText, out n)) {
                lmbMaxCps = n;
            } else if (newText == string.Empty) {
                lmbMaxCps = 0;
            } else {
                lmbMaxCpsTextbox.Text = lmbMaxCps.ToString();
            }
        }

        private void RMBMinCpsTextBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (wait) return;

            string newText = rmbMinCpsTextbox.Text;
            float n;

            if (newText.Length > 0 && newText[newText.Length - 1] == '.') {
                newText = newText.Remove(newText.Length - 1, 1);
            }

            if (float.TryParse(newText, out n)) {
                rmbMinCps = n;
            } else if (newText == string.Empty) {
                rmbMinCps = 0;
            } else {
                rmbMinCpsTextbox.Text = rmbMinCps.ToString();
            }
        }

        private void RMBMaxCpsTextBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (wait) return;

            string newText = rmbMaxCpsTextbox.Text;
            float n;

            if (newText.Length > 0 && newText[newText.Length - 1] == '.') {
                newText = newText.Remove(newText.Length - 1, 1);
            }

            if (float.TryParse(newText, out n)) {
                rmbMaxCps = n;
            } else if (newText == string.Empty) {
                rmbMaxCps = 0;
            } else {
                rmbMaxCpsTextbox.Text = rmbMaxCps.ToString();
            }
        }

        private bool transition = false;

        private void ToggleAdvancedSettingsButton_Click(object sender, RoutedEventArgs e) {
            if (transition) return;

            if(advancedSettingsGrid.Visibility == Visibility.Hidden) { //Showing - Going Down
                advancedSettingsGrid.Visibility = Visibility.Visible;
                transition = true;

                var rotateAnimation = new DoubleAnimation(0, 180, TimeSpan.FromSeconds(0.2f));
                rotateAnimation.AccelerationRatio = 0.4f;
                rotateAnimation.DecelerationRatio = 0.4f;
                var rt = (RotateTransform) advancedSettingsToggleSymbol.RenderTransform;
                rt.BeginAnimation(RotateTransform.AngleProperty, rotateAnimation);

                var slideoutAnimation = new DoubleAnimation(0, 100, TimeSpan.FromSeconds(0.2f));
                slideoutAnimation.AccelerationRatio = 0.4f;
                slideoutAnimation.DecelerationRatio = 0.4f;
                slideoutAnimation.Completed += (s, ev) => {
                    transition = false;
                };

                advancedSettingsGrid.BeginAnimation(HeightProperty, slideoutAnimation);

            } else { //Taking away, Going up
                transition = true;

                var rotateAnimation = new DoubleAnimation(180, 0, TimeSpan.FromSeconds(0.2f));
                rotateAnimation.AccelerationRatio = 0.4f;
                rotateAnimation.DecelerationRatio = 0.4f;
                var rt = (RotateTransform)advancedSettingsToggleSymbol.RenderTransform;
                rt.BeginAnimation(RotateTransform.AngleProperty, rotateAnimation);

                var slideinAnimation = new DoubleAnimation(100, 0, TimeSpan.FromSeconds(0.2f));
                slideinAnimation.AccelerationRatio = 0.4f;
                slideinAnimation.DecelerationRatio = 0.4f;
                slideinAnimation.Completed += (s, ev) => {
                    advancedSettingsGrid.Visibility = Visibility.Hidden;
                    transition = false;
                };

                advancedSettingsGrid.BeginAnimation(HeightProperty, slideinAnimation);
            }
        }

        private void RMBRampupInitial_TextChanged(object sender, TextChangedEventArgs e) {
            if (wait) return;

            string newText = rmbRampupInitialTextBox.Text;
            float n;

            if (newText.Length > 0 && newText[newText.Length - 1] == '.') {
                newText = newText.Remove(newText.Length - 1, 1);
            }

            if (float.TryParse(newText, out n)) {
                rmbRampupInitial = n;
            } else if (newText == string.Empty) {
                rmbRampupInitial = 0;
            } else {
                rmbRampupInitialTextBox.Text = rmbRampupInitial.ToString();
            }
        }

        private void LMBRampupInitial_TextChanged(object sender, TextChangedEventArgs e) {
            if (wait) return;

            string newText = lmbRampupInitialTextBox.Text;
            float n;

            if (newText.Length > 0 && newText[newText.Length - 1] == '.') {
                newText = newText.Remove(newText.Length - 1, 1);
            }

            if (float.TryParse(newText, out n)) {
                lmbRampupInitial = n;
            } else if (newText == string.Empty) {
                lmbRampupInitial = 0;
            } else {
                lmbRampupInitialTextBox.Text = lmbRampupInitial.ToString();
            }
        }

        private void RMBRampupDuration_TextChanged(object sender, TextChangedEventArgs e) {
            if (wait) return;

            string newText = rmbRampupDurationTextBox.Text;
            int n;

            if (int.TryParse(newText, out n)) {
                rmbRampupDuration = n;
            } else if (newText == string.Empty) {
                rmbRampupDuration = 0;
            } else {
                rmbRampupDurationTextBox.Text = rmbRampupDuration.ToString();
            }
        }

        private void LMBRampupDuration_TextChanged(object sender, TextChangedEventArgs e) {
            if (wait) return;

            string newText = lmbRampupDurationTextBox.Text;
            int n;

            if (int.TryParse(newText, out n)) {
                lmbRampupDuration = n;
            } else if (newText == string.Empty) {
                lmbRampupDuration = 0;
            } else {
                lmbRampupDurationTextBox.Text = lmbRampupDuration.ToString();
            }
        }
        #endregion
    }
}
