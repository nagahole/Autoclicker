using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Autoclicker.Properties {
    class BiasedRandom {
        private static Random rnd = new Random();

        /// <summary>
        /// Positive bias leans towards 1 and negative bias leans towards 0
        /// </summary>
        /// <param name="bias"></param>
        /// <returns></returns>
        public static double NextDouble(double bias) {
            bias = -bias;
            //https://www.desmos.com/calculator/u1mqpu7rh4
            if (bias >= 0) {
                return Math.Pow(rnd.NextDouble(), bias + 1);
            } else {
                return 1.0 - Math.Pow(1 - rnd.NextDouble(), -bias + 1.0);
            }
        }

        public static double Range(double min, double max, double bias) {
            return NextDouble(bias) * (max - min) + min;
        }
    }
}
