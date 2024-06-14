using TrafficSimulation;

namespace TrafficSimulation.Models
{
    internal class Vehicle
    {
        public string ID { get; set; }
        public double Speed { get; set; }
        public double MaxSpeed { get; set; }
        public Road CurrentRoad { get; set; }

        public int LineNumber { get; set; }
        public double Position { get; set; }
        public Intersection NextIntersection { get; set; }
        public bool IsMovingForward { get; set; } = true;

        public void Update(City city)
        {
            double stopDistance = NextIntersection.GetWidth() / 2 + Constants.VehicleLengthMetres / 2;

            // Проверка светофора на текущей дороге
            var trafficLight = city.TrafficLights.FirstOrDefault(light => light.ControlledIntersection == NextIntersection && light.ControlledRoad == CurrentRoad);
            bool shouldStopForTrafficLight = false;
            if (trafficLight != null && trafficLight.State == TrafficLightState.Red)
            {
                if ((IsMovingForward && Position + Speed >= CurrentRoad.GetLength() - stopDistance) || (!IsMovingForward && Position - Speed <= stopDistance))
                {
                    shouldStopForTrafficLight = true;
                }
            }

            // Проверка расстояния до следующего автомобиля
            var nextVehicle = city.Vehicles
                .Where(v => v.CurrentRoad == CurrentRoad && v.IsMovingForward == IsMovingForward && v.LineNumber == LineNumber)
                .OrderBy(v => IsMovingForward ? v.Position : -v.Position)
                .FirstOrDefault(v => IsMovingForward ? v.Position > Position : v.Position < Position);

            bool shouldStopForNextVehicle = false;
            if (nextVehicle != null)
            {
                double distanceToNextVehicle = Math.Abs(nextVehicle.Position - Position);
                if (distanceToNextVehicle < Constants.SafetyDistanceToStop)
                {
                    shouldStopForNextVehicle = true;
                }
            }

            if (shouldStopForTrafficLight || shouldStopForNextVehicle)
            {
                Speed = 0;
            }
            else
            {
                Speed = MaxSpeed;
            }

            // Обновление позиции автомобиля
            if (!shouldStopForTrafficLight && !shouldStopForNextVehicle)
            {
                Position += IsMovingForward ? Speed : -Speed;
            }
            else if (shouldStopForTrafficLight && !shouldStopForNextVehicle)
            {
                Position = IsMovingForward ? CurrentRoad.GetLength() - stopDistance : stopDistance;
            }

            // Если автомобиль достиг конца или начала дороги
            if (Speed != 0 && (Position >= CurrentRoad.GetLength() || Position <= 0))
            {
                Road nextRoad = GetNextRoad();

                if (nextRoad != null)
                {
                    if (CurrentRoad == nextRoad)
                    {
                        Random random = new();
                        LineNumber = random.Next(1, CurrentRoad.NumOfLanesPerDirection + 1);
                        IsMovingForward = !IsMovingForward;
                        Position = IsMovingForward ? 0 : nextRoad.GetLength();
                        NextIntersection = IsMovingForward ? nextRoad.EndIntersection : nextRoad.StartIntersection;
                    }
                    else
                    {
                        CurrentRoad.RemoveVehicle(this);
                        nextRoad.AddVehicle(this);
                        CurrentRoad = nextRoad;
                        Random random = new();
                        LineNumber = random.Next(1, CurrentRoad.NumOfLanesPerDirection + 1);
                        IsMovingForward = nextRoad.StartIntersection == NextIntersection;
                        Position = IsMovingForward ? 0 : nextRoad.GetLength();
                        NextIntersection = IsMovingForward ? nextRoad.EndIntersection : nextRoad.StartIntersection;
                    }
                }
                else
                {
                    LineNumber = 1;
                }
            }
        }

        public Road GetNextRoad()
        {
            int numOfLanes = CurrentRoad.NumOfLanesPerDirection;
            List<Road> outgoingRoads;
            switch (numOfLanes)
            {
                case 1:
                    outgoingRoads = NextIntersection.OutgoingRoads;
                    break;
                case 2:
                    outgoingRoads = CurrentRoad.GetRoads(this, RoadDirection.Forward);
                    if (LineNumber == 1)
                    {
                        outgoingRoads.AddRange(CurrentRoad.GetRoads(this, RoadDirection.Left));
                        if (CurrentRoad.Type != RoadType.OneWay) outgoingRoads.Add(CurrentRoad);
                    }
                    else
                    {
                        outgoingRoads.AddRange(CurrentRoad.GetRoads(this, RoadDirection.Right));
                    }
                    break;
                default:
                    outgoingRoads = CurrentRoad.GetRoads(this, RoadDirection.Forward);
                    if (LineNumber == 1)
                    {
                        outgoingRoads.AddRange(CurrentRoad.GetRoads(this, RoadDirection.Left));
                        if (CurrentRoad.Type != RoadType.OneWay) outgoingRoads.Add(CurrentRoad);
                    }
                    else if (LineNumber == CurrentRoad.NumOfLanesPerDirection)
                    {
                        outgoingRoads.AddRange(CurrentRoad.GetRoads(this, RoadDirection.Right));
                    }
                    break;
            }

            //Выбираем случайное направление из доступных на перекрестке
            if (outgoingRoads.Count == 0)
            {
                return null; //Если нет доступных дорог, возвращаем null
            }

            Random random = new Random();
            var randomIndex = random.Next(outgoingRoads.Count);
            return outgoingRoads[randomIndex];
        }
    }
}
