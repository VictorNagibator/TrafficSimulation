namespace TrafficSimulation.Models
{
    internal class Road
    {
        public string Name { get; set; }
        public RoadType Type { get; set; }
        public int NumOfLanesPerDirection { get; set; }
        public Intersection StartIntersection { get; set; }
        public Intersection EndIntersection { get; set; }
        public List<Vehicle> Vehicles { get; set; } = [];

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

        public double Length => Math.Sqrt((EndIntersection.X - StartIntersection.X) * (EndIntersection.X - StartIntersection.X) + (EndIntersection.Y - StartIntersection.Y) * (EndIntersection.Y - StartIntersection.Y));

        public double Width => Type != RoadType.OneWay ? NumOfLanesPerDirection * Constants.RoadWidthMetres : NumOfLanesPerDirection * Constants.RoadWidthMetres / 2;

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

                switch (direction)
                {
                    case RoadDirection.Left:
                        if (angleDifference > 0)
                        {
                            neededRoads.Add(road);
                        }
                        break;
                    case RoadDirection.Forward:
                        if (angleDifference == 0 && road != this)
                        {
                            neededRoads.Add(road);
                        }
                        break;
                    case RoadDirection.Right:
                        if (angleDifference < 0)
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
                return !Vehicles.Any(v => v.Position < passingVehicle.StopDistance && passingVehicle.IsMovingForward == v.IsMovingForward);
            }
            else
            {
                return !Vehicles.Any(v => v.Position > Length - passingVehicle.StopDistance && !passingVehicle.IsMovingForward == v.IsMovingForward);
            }
        }
    }

    internal enum RoadType
    {
        OneWay,
        TwoWay
    }

    internal enum RoadDirection
    {
        Left,
        Forward,
        Right
    }
}
