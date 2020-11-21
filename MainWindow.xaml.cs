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
using System.Collections.Generic;
using Newtonsoft.Json;
using System.IO;

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
        private Brush toggledColor = new SolidColorBrush(Color.FromArgb(255, 39, 133, 43));
        #endregion

        #region Not customisable fields
        private bool
            lmbToggled = false,
            rmbToggled = false,
            wait = true;

        private DateTime lastLmbToggle, lastRmbToggle;

        private const string profileFileName = "profile.autoclicker";
        #endregion

        #region Customisable Variables
        private float lmbMaxCps, lmbMinCps;
        private float rmbMaxCps, rmbMinCps;

        private int lmbRampupDuration = 800, rmbRampupDuration = 800; //In milliseconds - Time before reaching max cps after activating

        private float lmbRampupInitial = 0.6f, rmbRampupInitial = 0.6f; //What fraction of target cps it begins at

        private bool canBothBeActive = false, clicksDisablesOtherSide = true, numbersDisablesAutoclicker = true, playToggleSounds = true, playClickSounds = true;

        private float lmbBias = 0, rmbBias = 0;

        private float lmbMiniDeviation = 0, rmbMiniDeviation = 0;

        private int normalizationClicks = 100; //Number of clicks to find average Thread.Sleep deviation to account for that

        private bool blockHit = false;
        #endregion

        #region API / USEFUL CLASSES
        private static IKeyboardMouseEvents m_GlobalHook;
        private Random rand = new Random();
        #endregion

        #region UI references

        HotkeyButton lmbHotkeyButton, rmbHotkeyButton, blockhitHotkeyButton, panicHotkeyButton;

        #endregion

        #region Initializations / Fired once

        public MainWindow() {
            InitializeComponent();
            Subscribe();
            HotkeyButton.Initialize();
            InitializeHotkeyButtons();
            InitiallizeAutoclickers();
            MatchFieldsFromXaml();

            string filePath = Path.Combine(Directory.GetCurrentDirectory(), profileFileName);

            advancedGridHeight = (int) Advanced_Settings.Height;

            this.Closed += (s, e) => {
                App.Current.Shutdown();
                Process.GetCurrentProcess().Kill();
            };

            Thread.Sleep(1);
            wait = false;

            if (File.Exists(filePath)) {
                ImportProfile(File.ReadAllText(filePath));
            }
        }

        private void InitiallizeAutoclickers() {
            Thread lmbAc = new Thread(LMBAutoClick);
            lmbAc.Start();

            lmbAc.Priority = ThreadPriority.Highest;

            Thread rmbAc = new Thread(RMBAutoClick);
            rmbAc.Start();

            rmbAc.Priority = ThreadPriority.Highest;
        }

        private void MatchFieldsFromXaml() {
            lmbMinCps = float.Parse(LMBMinCpsTextBox.Text);
            lmbMaxCps = float.Parse(LMBMaxCpsTextBox.Text);

            rmbMinCps = float.Parse(RMBMinCpsTextBox.Text);
            rmbMaxCps = float.Parse(RMBMaxCpsTextBox.Text);

            lmbRampupInitial = float.Parse(LMBRampupInitial.Text);
            rmbRampupInitial = float.Parse(RMBRampupInitial.Text);

            lmbRampupDuration = int.Parse(LMBRampupDuration.Text);
            rmbRampupDuration = int.Parse(RMBRampupDuration.Text);

            canBothBeActive = (bool)BothSidesCanBeActive.IsChecked;
            clicksDisablesOtherSide = (bool)ClicksDisablesOtherSide.IsChecked;
            numbersDisablesAutoclicker = (bool)NumberDisablesAutoclicker.IsChecked;
            playToggleSounds = (bool)PlayToggleSounds.IsChecked;
            playClickSounds = (bool)PlayClickSounds.IsChecked;

            lmbBias = float.Parse(LMBBiasTextBox.Text);
            rmbBias = float.Parse(RMBBiasTextBox.Text);

            lmbMiniDeviation = float.Parse(LMBMiniDeviationTextBox.Text);
            rmbMiniDeviation = float.Parse(RMBMiniDeviationTextBox.Text);
        }

        private void Subscribe() {
            m_GlobalHook = Hook.GlobalEvents();

            m_GlobalHook.MouseClick += GlobalHookMouseClick;
            m_GlobalHook.KeyDown += GlobalHookKeyPress;
        }

        private void GlobalHookMouseClick(object sender, MouseEventArgs e) {
            if (e.Button == MouseButtons.Left) {
                if (rmbToggled && (DateTime.Now - lastRmbToggle).TotalMilliseconds > 20 && clicksDisablesOtherSide) {
                    ToggleRMB();
                }
            } else if (e.Button == MouseButtons.Right) {
                if (lmbToggled && (DateTime.Now - lastLmbToggle).TotalMilliseconds > 20 && clicksDisablesOtherSide && !blockHit) {
                    ToggleLMB();
                }
            }
        }

        private HashSet<Keys> numberKeys = new HashSet<Keys> {
            Keys.D0,
            Keys.D1,
            Keys.D2,
            Keys.D3,
            Keys.D4,
            Keys.D5,
            Keys.D6,
            Keys.D7,
            Keys.D8,
            Keys.D9,

            Keys.NumPad0,
            Keys.NumPad1,
            Keys.NumPad2,
            Keys.NumPad3,
            Keys.NumPad4,
            Keys.NumPad5,
            Keys.NumPad6,
            Keys.NumPad7,
            Keys.NumPad8,
            Keys.NumPad9
        };

        private void GlobalHookKeyPress(object sender, KeyEventArgs e) {
            if(numbersDisablesAutoclicker && numberKeys.Contains(e.KeyCode)) {
                if (lmbToggled) {
                    ToggleLMB();
                }
                if (rmbToggled) {
                    ToggleRMB();
                }
            }
        }

        private void InitializeHotkeyButtons() {
            //LMB
            lmbHotkeyButton = new HotkeyButton(MouseButtons.XButton2);
            LMBHotkeyText.Text = $"Press [{(lmbHotkeyButton.GetHotkey())}] to start";
            lmbHotkeyButton.HotkeySetEvent += (sender, e) => {
                LMBHotkeyText.Text = $"Press [{(lmbHotkeyButton.GetHotkey())}] to start";
            };
            lmbHotkeyButton.HotkeyPressed += (sender, e) => {
                ToggleLMB();
            };
            lmbHotkeyButton.SelectHotkeyEvent += (sender, e) => {
                LMBHotkeyText.Text = "[Press any key]";
            };

            //RMB

            rmbHotkeyButton = new HotkeyButton(MouseButtons.XButton1);
            RMBHotkeyText.Text = $"Press [{(rmbHotkeyButton.GetHotkey())}] to start";
            rmbHotkeyButton.HotkeySetEvent += (sender, e) => {
                RMBHotkeyText.Text = $"Press [{(rmbHotkeyButton.GetHotkey())}] to start";
            };
            rmbHotkeyButton.HotkeyPressed += (sender, e) => {
                ToggleRMB();
            };
            rmbHotkeyButton.SelectHotkeyEvent += (sender, e) => {
                RMBHotkeyText.Text = "[Press any key]";
            };

            //Blockhit

            blockhitHotkeyButton = new HotkeyButton(Keys.G);
            BlockhitHotkeyText.Text = $"Press [{blockhitHotkeyButton.GetHotkey()}] to enable blockhits";
            blockhitHotkeyButton.HotkeySetEvent += (sender, e) => {
                BlockhitHotkeyText.Text = $"Press [{blockhitHotkeyButton.GetHotkey()}] to enable blockhits";
            };
            blockhitHotkeyButton.HotkeyPressed += (sender, e) => {
                blockHit = !blockHit;
                BlockhitHotkeyText.Text = $"Press [{blockhitHotkeyButton.GetHotkey()}] to {((blockHit) ? "disable" : "enable")} blockhits";

                if (blockHit) {
                    BlockhitChangeHotkey.Background = toggledColor;
                } else {
                    BlockhitChangeHotkey.ClearValue(BackgroundProperty);
                }

                if (playToggleSounds) {
                    BackgroundBeep.Beep(blockHit ? 16000 : 8000, 1);
                }
            };

            blockhitHotkeyButton.SelectHotkeyEvent += (sender, e) => {
                BlockhitHotkeyText.Text = "[Press any key]";
            };

            //Panic button
            panicHotkeyButton = new HotkeyButton(Keys.Multiply);
            PanicButtonTextBlock.Text = $"Panic Button [{panicHotkeyButton.GetHotkey()}]";
            panicHotkeyButton.HotkeySetEvent += (sender, e) => {
                PanicButtonTextBlock.Text = $"Panic Button [{panicHotkeyButton.GetHotkey()}]";
            };
            panicHotkeyButton.HotkeyPressed += (sender, e) => {
                Close();
            };
            panicHotkeyButton.SelectHotkeyEvent += (sender, e) => {
                PanicButtonTextBlock.Text = "[Press any key]";
            };
        }

        private void LMBAutoClick() {
            #region init
            Stopwatch sw = new Stopwatch();
            double rampupProportion;
            double baseDelay;
            double miniDeviation;
            int delay;

            double deviation;
            double totalMilliseconds = 0;
            int clicks = 0;

            int carryover = 0;
            #endregion

            while (true) {
                if (lmbToggled) {
                    rampupProportion = ((lmbRampupDuration == 0) ? 1 : ((System.Math.Min((DateTime.Now - lastLmbToggle).TotalMilliseconds, lmbRampupDuration) / (float)lmbRampupDuration) * (1 - lmbRampupInitial) + lmbRampupInitial));
                    baseDelay = (1000f / (BiasedRandom.NextDouble(lmbBias) * (lmbMaxCps - lmbMinCps) + lmbMinCps)) - carryover;

                    miniDeviation = (rand.NextDouble() - 0.5) * lmbMiniDeviation;

                    delay = (int)System.Math.Round(((baseDelay + miniDeviation) / rampupProportion));

                    if(clicks >= 10) {
                        delay = delay - (int)System.Math.Round(totalMilliseconds / clicks);
                    }
                  
                    LeftMouseClick();

                    if (playClickSounds) {
                        BackgroundBeep.Beep(2000, 1);
                    }

                    if (blockHit && rand.NextDouble() < 0.92) {
                        sw.Reset();
                        sw.Start();
                        Thread.Sleep(rand.Next(1, 4));
                        RightMouseClick();
                        sw.Stop();
                        delay -= (int) sw.ElapsedMilliseconds;
                    }

                    sw.Reset();
                    sw.Start();
                    Thread.Sleep(delay);
                    sw.Stop();

                    deviation = sw.Elapsed.TotalMilliseconds - delay;

                    if (clicks < normalizationClicks) {
                        clicks++;
                        totalMilliseconds += deviation;
                    } 

                    carryover = (int)sw.ElapsedMilliseconds - delay;
                } else {
                    Thread.Sleep(1);
                }
            }
        }

        private void RMBAutoClick() {
            #region init
            Stopwatch sw = new Stopwatch();
            double rampupProportion;
            double baseDelay;
            double miniDeviation;
            int delay;

            double deviation;
            double totalMilliseconds = 0;
            int clicks = 0;

            int carryover = 0;
            #endregion


            while (true) {
                if (rmbToggled) {
                    rampupProportion = ((rmbRampupDuration == 0) ? 1 : ((System.Math.Min((DateTime.Now - lastRmbToggle).TotalMilliseconds, rmbRampupDuration) / (float)rmbRampupDuration) * (1 - rmbRampupInitial) + rmbRampupInitial));
                    baseDelay = (1000f / (BiasedRandom.NextDouble(rmbBias) * (rmbMaxCps - rmbMinCps) + rmbMinCps)) - carryover;

                    miniDeviation = (rand.NextDouble() - 0.5) * rmbMiniDeviation;

                    delay = (int)System.Math.Round(((baseDelay + miniDeviation) / rampupProportion));

                    if (clicks >= 10) {
                        delay = delay - (int)System.Math.Round(totalMilliseconds / clicks);
                    }

                    RightMouseClick();

                    if (playClickSounds) {
                        BackgroundBeep.Beep(1000, 1);
                    }

                    sw.Reset();
                    sw.Start();
                    Thread.Sleep(delay);
                    sw.Stop();

                    deviation = sw.Elapsed.TotalMilliseconds - delay;

                    if (clicks < normalizationClicks) {
                        clicks++;
                        totalMilliseconds += deviation;
                    }

                    carryover = (int)sw.ElapsedMilliseconds - delay;
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
                LMBMinCpsTextBox.Text = "1";
            }

            if (lmbMaxCps < lmbMinCps) {
                LMBMaxCpsTextBox.Text = lmbMinCps.ToString();
            }

            if (lmbRampupInitial < 0.1f) {
                LMBRampupInitial.Text = "0.1";
            }

            lmbToggled = !lmbToggled;
            if (!canBothBeActive && lmbToggled) {
                rmbToggled = false;
                UpdateRMBVisuals();
            }

            UpdateLMBVisuals();

            if (playToggleSounds) {
                BackgroundBeep.Beep(lmbToggled ? 16000 : 10000, 1);
            }
        }

        private void ToggleRMB() {
            lastRmbToggle = DateTime.Now;
            if (rmbMinCps <= 0) {
                RMBMinCpsTextBox.Text = "1";
            }

            if (rmbMaxCps < rmbMinCps) {
                RMBMaxCpsTextBox.Text = rmbMinCps.ToString();
            }

            if(rmbRampupInitial < 0.1f) {
                RMBRampupInitial.Text = "0.1";
            }

            rmbToggled = !rmbToggled;

            if (!canBothBeActive && rmbToggled) {
                lmbToggled = false;
                UpdateLMBVisuals();
            }

            UpdateRMBVisuals();

            if (playToggleSounds) {
                BackgroundBeep.Beep(rmbToggled ? 16000 : 10000, 1);
            }
        }

        private void UpdateLMBVisuals() {
            if (lmbToggled) {
                LMBButton.Background = toggledColor;
            } else {
                LMBButton.ClearValue(BackgroundProperty);
            }
            
            LMBHotkeyText.Text = $"Press [" +
                $"{lmbHotkeyButton.GetHotkey()}" +
                $"] to " +
                $"{(lmbToggled? "stop" : "start")}";
        }

        private void UpdateRMBVisuals() {
            if (rmbToggled) {
                RMBButton.Background = toggledColor;
            } else {
                RMBButton.ClearValue(BackgroundProperty);
            }

            RMBHotkeyText.Text = $"Press [" +
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

        private void BlockhitChangeHotkey_Click(object sender, RoutedEventArgs e) {
            blockhitHotkeyButton.OnClick();
        }

        private void PanicButton_Click(object sender, RoutedEventArgs e) {
            panicHotkeyButton.OnClick();
        }

        private void LMBMinCpsTextBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (wait) return;

            string newText = LMBMinCpsTextBox.Text;
            float n;

            if (newText.Length > 0 && newText[newText.Length - 1] == '.') {
                newText = newText.Remove(newText.Length - 1, 1);
            }

            if (float.TryParse(newText, out n)) {
                lmbMinCps = n;
            } else if (newText == string.Empty) {
                lmbMinCps = 0;
            } else {
                LMBMinCpsTextBox.Text = lmbMinCps.ToString();
            }
        }

        private void LMBMaxCpsTextBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (wait) return;

            string newText = LMBMaxCpsTextBox.Text;
            float n;

            if (newText.Length > 0 && newText[newText.Length - 1] == '.') {
                newText = newText.Remove(newText.Length - 1, 1);
            }

            if (float.TryParse(newText, out n)) {
                lmbMaxCps = n;
            } else if (newText == string.Empty) {
                lmbMaxCps = 0;
            } else {
                LMBMaxCpsTextBox.Text = lmbMaxCps.ToString();
            }
        }

        private void RMBMinCpsTextBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (wait) return;

            string newText = RMBMinCpsTextBox.Text;
            float n;

            if (newText.Length > 0 && newText[newText.Length - 1] == '.') {
                newText = newText.Remove(newText.Length - 1, 1);
            }

            if (float.TryParse(newText, out n)) {
                rmbMinCps = n;
            } else if (newText == string.Empty) {
                rmbMinCps = 0;
            } else {
                RMBMinCpsTextBox.Text = rmbMinCps.ToString();
            }
        }

        private void RMBMaxCpsTextBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (wait) return;

            string newText = RMBMaxCpsTextBox.Text;
            float n;

            if (newText.Length > 0 && newText[newText.Length - 1] == '.') {
                newText = newText.Remove(newText.Length - 1, 1);
            }

            if (float.TryParse(newText, out n)) {
                rmbMaxCps = n;
            } else if (newText == string.Empty) {
                rmbMaxCps = 0;
            } else {
                RMBMaxCpsTextBox.Text = rmbMaxCps.ToString();
            }
        }

        private bool transition = false;
        private int advancedGridHeight; 

        private void ToggleAdvancedSettingsButton_Click(object sender, RoutedEventArgs e) {
            if (transition) return;

            if(Advanced_Settings.Visibility == Visibility.Hidden) { //Showing - Going Down
                Advanced_Settings.Visibility = Visibility.Visible;
                transition = true;

                var rotateAnimation = new DoubleAnimation(0, 180, TimeSpan.FromSeconds(0.2f));
                rotateAnimation.AccelerationRatio = 0.4f;
                rotateAnimation.DecelerationRatio = 0.4f;
                var rt = (RotateTransform) AdvancedSettingsToggleSymbol.RenderTransform;
                rt.BeginAnimation(RotateTransform.AngleProperty, rotateAnimation);

                var slideoutAnimation = new DoubleAnimation(0, advancedGridHeight, TimeSpan.FromSeconds(0.2f));
                slideoutAnimation.AccelerationRatio = 0.4f;
                slideoutAnimation.DecelerationRatio = 0.4f;
                slideoutAnimation.Completed += (s, ev) => {
                    transition = false;
                };

                Advanced_Settings.BeginAnimation(HeightProperty, slideoutAnimation);

            } else { //Taking away, Going up
                transition = true;

                var rotateAnimation = new DoubleAnimation(180, 0, TimeSpan.FromSeconds(0.2f));
                rotateAnimation.AccelerationRatio = 0.4f;
                rotateAnimation.DecelerationRatio = 0.4f;
                var rt = (RotateTransform) AdvancedSettingsToggleSymbol.RenderTransform;
                rt.BeginAnimation(RotateTransform.AngleProperty, rotateAnimation);

                var slideinAnimation = new DoubleAnimation(advancedGridHeight, 0, TimeSpan.FromSeconds(0.2f));
                slideinAnimation.AccelerationRatio = 0.4f;
                slideinAnimation.DecelerationRatio = 0.4f;
                slideinAnimation.Completed += (s, ev) => {
                    Advanced_Settings.Visibility = Visibility.Hidden;
                    transition = false;
                };

                Advanced_Settings.BeginAnimation(HeightProperty, slideinAnimation);
            }
        }

        private void RMBRampupInitial_TextChanged(object sender, TextChangedEventArgs e) {
            if (wait) return;

            string newText = RMBRampupInitial.Text;
            float n;

            if (newText.Length > 0 && newText[newText.Length - 1] == '.') {
                newText = newText.Remove(newText.Length - 1, 1);
            }

            if (float.TryParse(newText, out n) && n >= 0) {
                rmbRampupInitial = n;
            } else if (newText == string.Empty) {
                rmbRampupInitial = 0;
            } else {
                RMBRampupInitial.Text = rmbRampupInitial.ToString();
            }
        }

        private void LMBRampupInitial_TextChanged(object sender, TextChangedEventArgs e) {
            if (wait) return;

            string newText = LMBRampupInitial.Text;
            float n;

            if (newText.Length > 0 && newText[newText.Length - 1] == '.') {
                newText = newText.Remove(newText.Length - 1, 1);
            }

            if (float.TryParse(newText, out n) && n >= 0) {
                lmbRampupInitial = n;
            } else if (newText == string.Empty) {
                lmbRampupInitial = 0;
            } else {
                LMBRampupInitial.Text = lmbRampupInitial.ToString();
            }
        }

        private void RMBRampupDuration_TextChanged(object sender, TextChangedEventArgs e) {
            if (wait) return;

            string newText = RMBRampupDuration.Text;
            uint n;

            if (uint.TryParse(newText, out n)) {
                rmbRampupDuration = (int) n;
            } else if (newText == string.Empty) {
                rmbRampupDuration = 0;
            } else {
                RMBRampupDuration.Text = rmbRampupDuration.ToString();
            }
        }

        private void LMBRampupDuration_TextChanged(object sender, TextChangedEventArgs e) {
            if (wait) return;

            string newText = LMBRampupDuration.Text;
            uint n;

            if (uint.TryParse(newText, out n)) {
                lmbRampupDuration = (int) n;
            } else if (newText == string.Empty) {
                lmbRampupDuration = 0;
            } else {
                LMBRampupDuration.Text = lmbRampupDuration.ToString();
            }
        }

        private void RMBBiasTextBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (wait) return;

            string newText = RMBBiasTextBox.Text;
            float n;

            if (newText.Length > 0 && newText[newText.Length - 1] == '.') {
                newText = newText.Remove(newText.Length - 1, 1);
            }

            if (float.TryParse(newText, out n)) {
                rmbBias = n;
            } else if (newText == string.Empty) {
                rmbBias = 0;
            } else {
                RMBBiasTextBox.Text = rmbBias.ToString();
            }
        }

        private void LMBBiasTextBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (wait) return;

            string newText = LMBBiasTextBox.Text;
            float n;

            if (newText.Length > 0 && newText[newText.Length - 1] == '.') {
                newText = newText.Remove(newText.Length - 1, 1);
            }

            if (float.TryParse(newText, out n)) {
                lmbBias = n;
            } else if (newText == string.Empty) {
                lmbBias = 0;
            } else {
                LMBBiasTextBox.Text = lmbBias.ToString();
            }
        }

        private void LMBMiniDeviationTextBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (wait) return;

            string newText = LMBMiniDeviationTextBox.Text;
            float n;

            if (newText.Length > 0 && newText[newText.Length - 1] == '.') {
                newText = newText.Remove(newText.Length - 1, 1);
            }

            if (float.TryParse(newText, out n)) {
                lmbMiniDeviation = n;
            } else if (newText == string.Empty) {
                lmbMiniDeviation = 0;
            } else {
                LMBMiniDeviationTextBox.Text = lmbMiniDeviation.ToString();
            }
        }

        private void RMBMiniDeviationTextBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (wait) return;

            string newText = RMBMiniDeviationTextBox.Text;
            float n;

            if (newText.Length > 0 && newText[newText.Length - 1] == '.') {
                newText = newText.Remove(newText.Length - 1, 1);
            }

            if (float.TryParse(newText, out n)) {
                rmbMiniDeviation = n;
            } else if (newText == string.Empty) {
                rmbMiniDeviation = 0;
            } else {
                RMBMiniDeviationTextBox.Text = rmbMiniDeviation.ToString();
            }
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e) {
            System.Windows.Clipboard.SetText(ExportCurrentProfile());
        }

        private void ImportProfileButton_Click(object sender, RoutedEventArgs e) {
            ImportProfile(ImportProfileTextbox.Text);
        }

        #region Checkbox Stuff
        private void BothSidesCanBeActive_Checked(object sender, RoutedEventArgs e) {
            canBothBeActive = true;
        }

        private void BothSidesCanBeActive_Unchecked(object sender, RoutedEventArgs e) {
            canBothBeActive = false;
        }

        private void ClicksDisablesOtherSide_Checked(object sender, RoutedEventArgs e) {
            clicksDisablesOtherSide = true;
        }

        private void ClicksDisablesOtherSide_Unchecked(object sender, RoutedEventArgs e) {
            clicksDisablesOtherSide = false;
        }

        private void NumberDisablesAutoclicker_Checked(object sender, RoutedEventArgs e) {
            numbersDisablesAutoclicker = true;
        }

        private void NumberDisablesAutoclicker_Unchecked(object sender, RoutedEventArgs e) {
            numbersDisablesAutoclicker = false;
        }

        private void PlayToggleSounds_Checked(object sender, RoutedEventArgs e) {
            playToggleSounds = true;
        }

        private void PlayToggleSounds_Unchecked(object sender, RoutedEventArgs e) {
            playToggleSounds = false;
        }

        private void PlayClickSounds_Checked(object sender, RoutedEventArgs e) {
            playClickSounds = true;
        }

        private void PlayClickSounds_Unchecked(object sender, RoutedEventArgs e) {
            playClickSounds = false;
        }

        #endregion

        #endregion

        #region Import/Export

        private void LoadProfile(AutoclickerProfile profile) {
            LMBMinCpsTextBox.Text = profile.lmbMinCps.ToString();
            RMBMaxCpsTextBox.Text = profile.rmbMaxCps.ToString();

            LMBMaxCpsTextBox.Text = profile.lmbMaxCps.ToString();
            RMBMaxCpsTextBox.Text = profile.rmbMaxCps.ToString();

            LMBRampupDuration.Text = profile.lmbRampupDuration.ToString();
            RMBRampupDuration.Text = profile.rmbRampupDuration.ToString();

            LMBRampupInitial.Text = profile.lmbRampupInitial.ToString();
            RMBRampupInitial.Text = profile.rmbRampupInitial.ToString();

            BothSidesCanBeActive.IsChecked = profile.canBothBeActive;
            ClicksDisablesOtherSide.IsChecked = profile.clicksDisablesOtherSide;
            NumberDisablesAutoclicker.IsChecked = profile.numbersDisablesAutoclicker;
            PlayToggleSounds.IsChecked = profile.playToggleSounds;
            PlayClickSounds.IsChecked = profile.playClickSounds;

            LMBBiasTextBox.Text = profile.lmbBias.ToString();
            RMBBiasTextBox.Text = profile.rmbBias.ToString();

            LMBMiniDeviationTextBox.Text = profile.lmbMiniDeviation.ToString();
            RMBMiniDeviationTextBox.Text = profile.rmbMiniDeviation.ToString();
        }

        private string ExportCurrentProfile() {
            AutoclickerProfile profile = new AutoclickerProfile();
            profile.lmbMinCps = lmbMinCps;
            profile.rmbMinCps = rmbMinCps;
            profile.lmbMaxCps = lmbMaxCps;
            profile.rmbMaxCps = rmbMaxCps;

            profile.lmbRampupDuration = lmbRampupDuration;
            profile.rmbRampupDuration = rmbRampupDuration;

            profile.lmbRampupInitial = lmbRampupInitial;
            profile.rmbRampupInitial = rmbRampupInitial;

            profile.canBothBeActive = canBothBeActive;
            profile.clicksDisablesOtherSide = clicksDisablesOtherSide;
            profile.numbersDisablesAutoclicker = numbersDisablesAutoclicker;
            profile.playToggleSounds = playToggleSounds;
            profile.playClickSounds = playClickSounds;

            profile.lmbBias = lmbBias;
            profile.rmbBias = rmbBias;

            profile.lmbMiniDeviation = lmbMiniDeviation;
            profile.rmbMiniDeviation = rmbMiniDeviation;

            return ExportProfile(profile);
        }

        private string ExportProfile(AutoclickerProfile profile) {
            try {
                string raw = JsonConvert.SerializeObject(profile);
                return raw;
            } catch (Exception e) {
                System.Windows.MessageBox.Show($"Error: {e.Message}");
                return string.Empty;
            }
            
        }

        private void ImportProfile(string raw) {
            try {
                AutoclickerProfile profile = JsonConvert.DeserializeObject<AutoclickerProfile>(raw);
                LoadProfile(profile);
            } catch (Exception e) {
                System.Windows.MessageBox.Show($"Error: {e.Message}");
            }
        }
        #endregion
    }

    public struct AutoclickerProfile {
        public float lmbMinCps, rmbMinCps, lmbMaxCps, rmbMaxCps;
        public int lmbRampupDuration, rmbRampupDuration;
        public float lmbRampupInitial, rmbRampupInitial;
        public bool canBothBeActive, clicksDisablesOtherSide, numbersDisablesAutoclicker, playToggleSounds, playClickSounds;
        public float lmbBias, rmbBias;
        public float lmbMiniDeviation, rmbMiniDeviation;
    }

    internal static class ConsoleAllocator {
        [DllImport(@"kernel32.dll", SetLastError = true)]
        static extern bool AllocConsole();

        [DllImport(@"kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport(@"user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SwHide = 0;
        const int SwShow = 5;


        public static void ShowConsoleWindow() {
            var handle = GetConsoleWindow();

            if (handle == IntPtr.Zero) {
                AllocConsole();
            } else {
                ShowWindow(handle, SwShow);
            }
        }

        public static void HideConsoleWindow() {
            var handle = GetConsoleWindow();

            ShowWindow(handle, SwHide);
        }
    }
}
