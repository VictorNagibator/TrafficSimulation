using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TrafficSimulation
{
    internal static class Constants
    {
        public const double MetresPerPixel = 0.2;
        public const double RoadWidthMetres = 4;
        public const double VehicleLengthMetres = 4.5;
        public const double VehicleWidthMetres = 1.8;
        public const double TrafficLightMetres = 1;
        public const double SafetyDistanceToStop = VehicleLengthMetres * 2;

        public static double RoadWidthPixels => RoadWidthMetres / MetresPerPixel;
        public static double VehicleLengthPixels => VehicleLengthMetres / MetresPerPixel;
        public static double VehicleWidthPixels => VehicleWidthMetres / MetresPerPixel;
        public static double TrafficLightPixels => TrafficLightMetres / MetresPerPixel;
        public static double SafetyDistanceToStopPixels => SafetyDistanceToStop / MetresPerPixel;
    }
}
