using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Autoclicker.Properties {
    class BackgroundBeep {
        static Thread _beepThread;
        static AutoResetEvent _signalBeep;

        private static int frequency = 14000, duration = 1000;

        static BackgroundBeep() {
            _signalBeep = new AutoResetEvent(false);
            _beepThread = new Thread(() =>
            {
                for (; ; )
                {
                    _signalBeep.WaitOne();
                    Console.Beep(frequency, duration);
                }
            }, 1);
            _beepThread.IsBackground = true;
            _beepThread.Start();
        }

        public static void Beep(int frequency, int duration) {
            BackgroundBeep.frequency = frequency;
            BackgroundBeep.duration = duration;
            _signalBeep.Set();
        }
    }

}
