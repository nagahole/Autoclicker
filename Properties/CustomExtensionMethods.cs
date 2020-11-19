using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Autoclicker.Properties {
    public static class CustomExtensionMethods {
        /// <summary>
        /// It will repeat once over a minute
        /// </summary>
        /// <param name="dt"></param>
        /// <returns></returns>
        public static int TotalMilliseconds(this DateTime dt) {
            return dt.Hour * 3600000 + dt.Minute * 60000 + dt.Second * 1000 + dt.Millisecond;
        }
    }
}
