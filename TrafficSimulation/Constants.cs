namespace TrafficSimulation
{
    internal static class Constants
    {
        public static double MetresPerPixel = 0.2;
        public static double MetresPerPixelInIntersectionSettings = 0.2;
        public const double MetresPerPixelBasic = 0.2;
        public const double RoadWidthMetres = 3.75;
        public const double VehicleLengthMetres = 4.5;
        public const double VehicleWidthMetres = 1.8;
        public const double TrafficLightMetres = 1.5;
        public const double SafetyDistanceToStop = VehicleLengthMetres * 1.5;
        public const double TimerInterval = 0.1; //0.1 с
        public const double AngleTolerance = Math.PI / 18;
        public const double MaxAllowedSpeed = 100;
        public const double MinAllowedSpeed = 20;
        public const double MaxQuality = 1;
        public const double MinQuality = 0.05;
        public const double MinTrafficLightTimer = 5;

        public static double RoadWidthPixels => RoadWidthMetres / MetresPerPixel;
        public static double VehicleLengthPixels => VehicleLengthMetres / MetresPerPixel;
        public static double VehicleWidthPixels => VehicleWidthMetres / MetresPerPixel;
        public static double TrafficLightPixels => TrafficLightMetres / MetresPerPixel;
        public static double SafetyDistanceToStopPixels => SafetyDistanceToStop / MetresPerPixel;
    }
}
