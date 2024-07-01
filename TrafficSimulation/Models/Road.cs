namespace TrafficSimulation.Models
{
    public class Road
    {
        public string? Name { get; internal set; }
        private RoadType _type;
        public RoadType Type
        {
            get { return _type; }
            set
            {
                _type = value;
                switch (_type)
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
        private Intersection _startIntersection;
        public Intersection StartIntersection
        {
            get { return _startIntersection; }
            set
            {
                _startIntersection = value;
                if (Type == RoadType.OneWay)
                {
                    _startIntersection.OutgoingRoads.Add(this);
                }
            }
        }
        private Intersection _endIntersection;
        public Intersection EndIntersection
        {
            get { return _endIntersection; }
            set
            {
                _endIntersection = value;
                if (Type == RoadType.OneWay)
                {
                    _endIntersection.IncomingRoads.Add(this);
                }
            }
        }
        public List<Vehicle> Vehicles { get; private set; } = [];

        private double _maxAllowedSpeed = 60;
        public double MaxAllowedSpeed
        {
            get
            {
                return _maxAllowedSpeed;
            }

            internal set
            {
                if (value >= Constants.MinAllowedSpeed && value <= Constants.MaxAllowedSpeed)
                {
                    _maxAllowedSpeed = value;
                }
            }
        }

        private double _quality = 1;
        public double Quality
        {
            get
            {
                return _quality;
            }
            internal set
            {
                if (value >= Constants.MinQuality && value <= Constants.MaxQuality)
                {
                    _quality = value;
                }
            }
        }

        private int MaxPossibleNumOfVehicles => (int)(Length * NumOfLinesPerDirection / (Constants.VehicleLengthMetres * 2));

        public void AddVehicle(Vehicle vehicle) => Vehicles.Add(vehicle);

        public void RemoveVehicle(Vehicle vehicle) => Vehicles.Remove(vehicle);

        public double[] GetPositionOnRoad(Vehicle vehicle)
        {
            double offsetX = Math.Cos(Angle) * vehicle.Position;
            double offsetY = Math.Sin(Angle) * vehicle.Position; 

            return [StartIntersection.X + offsetX, StartIntersection.Y + offsetY];
        }

        public double Angle => Math.Atan2(EndIntersection.Y - StartIntersection.Y, EndIntersection.X - StartIntersection.X);

        public double Length => Math.Sqrt((EndIntersection.X - StartIntersection.X) * (EndIntersection.X - StartIntersection.X) + (EndIntersection.Y - StartIntersection.Y) * (EndIntersection.Y - StartIntersection.Y));

        public double Width => Type != RoadType.OneWay ? NumOfLinesPerDirection * Constants.RoadWidthMetres : NumOfLinesPerDirection * Constants.RoadWidthMetres / 2;

        public List<Road> GetRoads(Intersection intersection, RoadDirection direction)
        {
            if (intersection != StartIntersection && intersection != EndIntersection) return null;

            var neededRoads = new List<Road>();

            List<Road> roads = [];
            roads.AddRange(intersection.OutgoingRoads);
            roads.AddRange(intersection.IncomingRoads);
            HashSet<Road> lookingRoads = roads.ToHashSet();

            //Угол текущей дороги относительно перекрестка
            double currentRoadAngle = intersection == StartIntersection
                ? Math.Atan2(EndIntersection.Y - StartIntersection.Y, EndIntersection.X - StartIntersection.X)
                : Math.Atan2(StartIntersection.Y - EndIntersection.Y, StartIntersection.X - EndIntersection.X);

            foreach (var road in lookingRoads)
            {
                //Угол других дорог относительно перекрестка
                double roadAngle = intersection == road.StartIntersection
                    ? Math.Atan2(road.EndIntersection.Y - road.StartIntersection.Y, road.EndIntersection.X - road.StartIntersection.X)
                    : Math.Atan2(road.StartIntersection.Y - road.EndIntersection.Y, road.StartIntersection.X - road.EndIntersection.X);

                //Разница углов относительно текущей дороги
                double angleDifference = NormalizeAngle(roadAngle - currentRoadAngle);

                switch (direction)
                {
                    case RoadDirection.Left:
                        if (angleDifference > Constants.AngleTolerance)
                        {
                            neededRoads.Add(road);
                        }
                        break;
                    case RoadDirection.Forward:
                        if (Math.Abs(angleDifference) <= Constants.AngleTolerance && road != this)
                        {
                            neededRoads.Add(road);
                        }
                        break;
                    case RoadDirection.Right:
                        if (angleDifference < -Constants.AngleTolerance)
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

        private static double NormalizeAngle(double angle)
        {
            while (angle > Math.PI) angle -= 2 * Math.PI;
            while (angle <= -Math.PI) angle += 2 * Math.PI;
            return angle;
        }

        public List<int> GetFreeLines(Vehicle passingVehicle)
        {
            List<int> freeLines = [];

            for (int linenum = 1; linenum <= NumOfLinesPerDirection; linenum++)
            {
                if (passingVehicle.NextIntersection == StartIntersection)
                {
                    if(!Vehicles.Any(v => v.LineNumber == linenum && v.Position < passingVehicle.StopDistance && v.IsMovingForward && v != passingVehicle && !v.IsInCarAccident)) freeLines.Add(linenum);
                }
                else
                {
                    if(!Vehicles.Any(v => v.LineNumber == linenum && v.Position > Length - passingVehicle.StopDistance && !v.IsMovingForward && v != passingVehicle && !v.IsInCarAccident)) freeLines.Add(linenum);
                }
            }

            return freeLines;
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

        public TrafficJamColor TrafficJamStatus => GetTrafficJamStatus();
        private TrafficJamColor GetTrafficJamStatus()
        {
            if (Vehicles.Count <= MaxPossibleNumOfVehicles * 0.25) return TrafficJamColor.Green;
            else if (Vehicles.Count <= MaxPossibleNumOfVehicles * 0.5) return TrafficJamColor.Yellow;
            else if (Vehicles.Count <= MaxPossibleNumOfVehicles * 0.75) return TrafficJamColor.Red;
            else return TrafficJamColor.DarkRed;
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

    public enum TrafficJamColor
    {
        DarkRed,
        Red,
        Yellow,
        Green
    }
}
