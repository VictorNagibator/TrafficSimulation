namespace TrafficSimulation.Models
{
    internal class Road
    {
        public string Name { get; set; }

        public const double Width = 40;
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

        public double GetLength()
        {
            double start = StartIntersection.X;
            double end = EndIntersection.X;
            return end - start;
        }

        public List<Road> GetRoads(Vehicle vehicle, RoadDirection direction)
        {
            var neededRoads = new List<Road>();

            List<Road> lookingRoads;
            if (vehicle.IsMovingForward) lookingRoads = EndIntersection.OutgoingRoads;
            else lookingRoads = StartIntersection.OutgoingRoads;

                foreach (var road in lookingRoads)
                {
                    double angle = Math.Atan2(EndIntersection.Y - StartIntersection.Y, EndIntersection.X - StartIntersection.X);
                    double angleToRoad = Math.Atan2(road.EndIntersection.Y - road.StartIntersection.Y, road.EndIntersection.X - road.StartIntersection.X);

                    switch (direction)
                    {
                        case RoadDirection.Left:
                            if (angleToRoad < angle)
                            {
                                neededRoads.Add(road);
                            }
                            break;
                        case RoadDirection.Forward:
                            if (angleToRoad == angle && road != this)
                            {
                                neededRoads.Add(road);
                            }
                            break;
                        case RoadDirection.Right:
                            if (angleToRoad > angle)
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
