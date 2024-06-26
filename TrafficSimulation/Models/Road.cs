namespace TrafficSimulation.Models
{
    public class Road
    {
        public string Name { get; set; }
        private RoadType type;
        public RoadType Type
        {
            get { return type; }
            set
            {
                type = value;
                switch (type)
                {
                    case RoadType.OneWay:
                        StartIntersection.IncomingRoads.Remove(this);
                        EndIntersection.OutgoingRoads.Remove(this);

                        Vehicles.ForEach(car => car.NextRoad = car.GetNextRoad());
                        Vehicles.RemoveAll(car => !car.IsMovingForward);
                        break;
                    case RoadType.TwoWay:
                        StartIntersection.IncomingRoads.Add(this);
                        EndIntersection.OutgoingRoads.Add(this);
                        
                        Vehicles.ForEach(car => car.NextRoad = car.GetNextRoad());
                        break;
                    default:
                        break;
                }
            }
        }
        public int NumOfLinesPerDirection { get; set; }
        private Intersection startIntersection;
        public Intersection StartIntersection
        {
            get { return startIntersection; }
            set
            {
                startIntersection = value;
                if (Type == RoadType.OneWay)
                {
                    startIntersection.OutgoingRoads.Add(this);
                }
            }
        }
        private Intersection endIntersection;
        public Intersection EndIntersection
        {
            get { return endIntersection; }
            set
            {
                endIntersection = value;
                if (Type == RoadType.OneWay)
                {
                    endIntersection.IncomingRoads.Add(this);
                }
            }
        }
        public List<Vehicle> Vehicles { get; set; } = [];
        public double MaxAllowedSpeed { get; set; }
        public double MaxAllowedSpeedInMetresPerSecond => MaxAllowedSpeed / 3600 * 1000;

        public double Quality { get; set; } = 1;

        public void AddVehicle(Vehicle vehicle) => Vehicles.Add(vehicle);

        public void RemoveVehicle(Vehicle vehicle) => Vehicles.Remove(vehicle);

        public double[] GetPositionOnRoad(Vehicle vehicle)
        {
            double startX = StartIntersection.X;
            double startY = StartIntersection.Y;
            double endX = EndIntersection.X;
            double endY = EndIntersection.Y;

            double angle = Math.Atan2(endY - startY, endX - startX);

            double offsetX = Math.Cos(angle) * vehicle.Position;
            double offsetY = Math.Sin(angle) * vehicle.Position;

            return [startX + offsetX, startY + offsetY];
        }

        public double Angle => Math.Atan2(EndIntersection.Y - StartIntersection.Y, EndIntersection.X - StartIntersection.X);

        public double Length => Math.Sqrt((EndIntersection.X - StartIntersection.X) * (EndIntersection.X - StartIntersection.X) + (EndIntersection.Y - StartIntersection.Y) * (EndIntersection.Y - StartIntersection.Y));

        public double Width => Type != RoadType.OneWay ? NumOfLinesPerDirection * Constants.RoadWidthMetres : NumOfLinesPerDirection * Constants.RoadWidthMetres / 2;

        public List<Road> GetRoads(Intersection intersection, RoadDirection direction)
        {
            var neededRoads = new List<Road>();

            List<Road> roads = [];
            roads.AddRange(intersection.OutgoingRoads);
            roads.AddRange(intersection.IncomingRoads);
            HashSet<Road> lookingRoads = roads.ToHashSet();

            // Угол текущей дороги относительно перекрестка
            double currentRoadAngle = intersection == StartIntersection
                ? Math.Atan2(EndIntersection.Y - StartIntersection.Y, EndIntersection.X - StartIntersection.X)
                : Math.Atan2(StartIntersection.Y - EndIntersection.Y, StartIntersection.X - EndIntersection.X);

            foreach (var road in lookingRoads)
            {
                // Угол других дорог относительно перекрестка
                double roadAngle = intersection == road.StartIntersection
                    ? Math.Atan2(road.EndIntersection.Y - road.StartIntersection.Y, road.EndIntersection.X - road.StartIntersection.X)
                    : Math.Atan2(road.StartIntersection.Y - road.EndIntersection.Y, road.StartIntersection.X - road.EndIntersection.X);

                // Разница углов относительно текущей дороги
                double angleDifference = NormalizeAngle(roadAngle - currentRoadAngle);
                double angleTolerance = Math.PI / 12;

                switch (direction)
                {
                    case RoadDirection.Left:
                        if (angleDifference > angleTolerance)
                        {
                            neededRoads.Add(road);
                        }
                        break;
                    case RoadDirection.Forward:
                        if (Math.Abs(angleDifference) <= angleTolerance && road != this)
                        {
                            neededRoads.Add(road);
                        }
                        break;
                    case RoadDirection.Right:
                        if (angleDifference < -angleTolerance)
                        {
                            neededRoads.Add(road);
                        }
                        break;
                    default:
                        break;
                }
            }

            return neededRoads;
        }

        public List<Road> GetRoadsToGo(Intersection intersection, RoadDirection direction)
        {
            var list = GetRoads(intersection, direction).Where(v => intersection.OutgoingRoads.Contains(v)).ToList();
            return list;
        }

        private double NormalizeAngle(double angle)
        {
            while (angle > Math.PI) angle -= 2 * Math.PI;
            while (angle <= -Math.PI) angle += 2 * Math.PI;
            return angle;
        }

        public bool IsClear(Vehicle passingVehicle)
        {
            if (passingVehicle.NextIntersection == StartIntersection)
            {
                return !Vehicles.Any(v => v.Position < passingVehicle.StopDistance && v.IsMovingForward && v != passingVehicle);
            }
            else
            {
                return !Vehicles.Any(v => v.Position > Length - passingVehicle.StopDistance && !v.IsMovingForward && v != passingVehicle);
            }
        }

        public bool DoesIntersect(Road road)
        {
            double x1 = StartIntersection.X;
            double y1 = StartIntersection.Y;
            double x2 = EndIntersection.X;
            double y2 = EndIntersection.Y;
            double x3 = road.StartIntersection.X;
            double y3 = road.StartIntersection.Y;
            double x4 = road.EndIntersection.X;
            double y4 = road.EndIntersection.Y;

            double denominator = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);

            if (denominator == 0) return false;

            double t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / denominator;
            double u = -((x1 - x2) * (y1 - y3) - (y1 - y2) * (x1 - x3)) / denominator;

            return t >= 0 && t <= 1 && u >= 0 && u <= 1;
        }
    }

    public enum RoadType
    {
        OneWay,
        TwoWay
    }

    public enum RoadDirection
    {
        Left,
        Forward,
        Right
    }
}
