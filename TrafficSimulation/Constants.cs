using System;
namespace TrafficSimulation
{
    internal static class Constants
    {
        public static double MetresPerPixel = 0.2;
        public static double MetresPerPixelInIntersectionSettings = 0.2;
        public const double RoadWidthMetres = 3.75;
        public const double VehicleLengthMetres = 4.5;
        public const double VehicleWidthMetres = 1.8;
        public const double TrafficLightMetres = 1.5;
        public const double SafetyDistanceToStop = VehicleLengthMetres * 1.5;
        public const double TimerInterval = 0.1; //0.1 с
        public const double AngleTolerance = Math.PI / 12;

        public static double RoadWidthPixels => RoadWidthMetres / MetresPerPixel;
        public static double VehicleLengthPixels => VehicleLengthMetres / MetresPerPixel;
        public static double VehicleWidthPixels => VehicleWidthMetres / MetresPerPixel;
        public static double TrafficLightPixels => TrafficLightMetres / MetresPerPixel;
        public static double SafetyDistanceToStopPixels => SafetyDistanceToStop / MetresPerPixel;
    }
}
