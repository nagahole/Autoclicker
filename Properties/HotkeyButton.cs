using Gma.System.MouseKeyHook;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using Autoclicker;

namespace Autoclicker.Properties {
    class HotkeyButton {
        private HotkeyType hotkeyType;
        private Keys key;
        private MouseButtons mouseButton;

        private static HotkeyButton listeningToButton = null;

        public static bool listening => listeningToButton != null;

        private static DateTime lastSet;

        #region Events
        public event EventHandler<SelectHotkeyEventArgs> SelectHotkeyEvent;

        protected virtual void OnSelectHotkey(SelectHotkeyEventArgs e) {
            SelectHotkeyEvent?.Invoke(this, e);
        }

        public event EventHandler<HotkeySetEventArgs> HotkeySetEvent;

        protected virtual void OnHotkeySet(HotkeySetEventArgs e) {
            HotkeySetEvent?.Invoke(this, e);
        }

        public event EventHandler<HotkeyPressedEventArgs> HotkeyPressed;

        protected virtual void OnHotkeyPressed(HotkeyPressedEventArgs e) {
            HotkeyPressed?.Invoke(this, e);
        }
        #endregion

        private static IKeyboardMouseEvents m_GlobalHook;

        public static void Initialize() {
            m_GlobalHook = Hook.GlobalEvents();

            m_GlobalHook.KeyDown += StaticGlobalHookKeyDown;
            m_GlobalHook.MouseClick += StaticGlobalMousePress;
        }

        private static void StaticGlobalHookKeyDown(object sender, KeyEventArgs e) {
            if (listening) {
                listeningToButton.key = e.KeyCode;
                listeningToButton.hotkeyType = HotkeyType.key;
                HotkeySetEventArgs ev = new HotkeySetEventArgs();
                listeningToButton.OnHotkeySet(ev);

                listeningToButton = null;
                lastSet = DateTime.Now;
                return;
            }
        }

        private static void StaticGlobalMousePress(object sender, MouseEventArgs e) {
            if (listening) {
                listeningToButton.mouseButton = e.Button;
                listeningToButton.hotkeyType = HotkeyType.mouseButton;
                HotkeySetEventArgs ev = new HotkeySetEventArgs();
                listeningToButton.OnHotkeySet(ev);
                listeningToButton = null;

                lastSet = DateTime.Now;
                return;
            }
        }

        public HotkeyButton(Keys key) {
            BaseConstructor();
            this.key = key;
            hotkeyType = HotkeyType.key;
        }

        public HotkeyButton(MouseButtons button) {
            BaseConstructor();
            mouseButton = button;
            hotkeyType = HotkeyType.mouseButton;
        }

        private void BaseConstructor() {
            m_GlobalHook.KeyDown += ListenForKeyDown;
            m_GlobalHook.MouseClick += ListenForMouseClick;
        }

        private void ListenForKeyDown(object sender, KeyEventArgs e) {
            if ((DateTime.Now - lastSet).Milliseconds < 10) {
                return;
            }
            if(hotkeyType == HotkeyType.key && e.KeyCode == key && listeningToButton == null) {
                HotkeyPressedEventArgs ev = new HotkeyPressedEventArgs();
                OnHotkeyPressed(ev);
            }
        }

        private void ListenForMouseClick(object sender, MouseEventArgs e) {
            if ((DateTime.Now - lastSet).Milliseconds < 10) {
                return;
            }
            if (hotkeyType == HotkeyType.mouseButton && e.Button == mouseButton && listeningToButton == null) {
                HotkeyPressedEventArgs ev = new HotkeyPressedEventArgs();
                OnHotkeyPressed(ev);
            }
        }

        public void OnClick() {
            if(listeningToButton == null) {
                listeningToButton = this;
            }
            SelectHotkeyEventArgs e = new SelectHotkeyEventArgs();
            OnSelectHotkey(e);
        }

        public string GetHotkey() {
            return (hotkeyType == HotkeyType.key) ? key.ToString() : mouseButton.ToString();
        }
    }

    public class SelectHotkeyEventArgs : EventArgs {

    }

    public class HotkeySetEventArgs : EventArgs {

    }

    public class HotkeyPressedEventArgs : EventArgs {

    }
}
