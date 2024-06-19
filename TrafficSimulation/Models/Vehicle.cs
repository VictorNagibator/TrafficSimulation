namespace TrafficSimulation.Models
{
    public class Vehicle
    {
        public string ID { get; set; }
        public double Speed { get; set; } //км/ч
        public double Acceleration { get; set; } = 2; //в м/с^2
        public Road CurrentRoad { get; set; }

        public int LineNumber { get; set; }
        public double Position { get; set; }
        public Intersection NextIntersection { get; set; }
        public Road NextRoad { get; set; }
        public bool IsMovingForward { get; set; } = true;

        public double StopDistance => NextIntersection.GetWidth() / 2 + Constants.VehicleLengthMetres / 2;
        private double SpeedInMetresPerSecond => Speed / 3600 * 1000;
        private double SpeedInMetresPerHundredMilliseconds => SpeedInMetresPerSecond * 0.1;
        private double AccelerationInMetresPerHundredMilliseconds => Acceleration * 0.01;

        public void Update(City city)
        {
            //Проверка светофора на текущей дороге
            var trafficLight = city.TrafficLights.FirstOrDefault(light => light.ControlledIntersection == NextIntersection && light.ControlledRoad == CurrentRoad);
            bool shouldStopForTrafficLight = false;
            if (trafficLight != null && (trafficLight.State == TrafficLightState.Red || trafficLight.State == TrafficLightState.Yellow))
            {
                if ((IsMovingForward && Position + SpeedInMetresPerHundredMilliseconds >= CurrentRoad.Length - StopDistance) || (!IsMovingForward && Position - SpeedInMetresPerHundredMilliseconds <= StopDistance))
                {
                    shouldStopForTrafficLight = true;
                }
            }

            //Проверка расстояния до следующего автомобиля
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

            //Проверка наличия автомобиля справа и расстояния до перекрестка
            bool shouldStopBecauseOfVehicleOnRight = false;
            if ((IsMovingForward && Position + SpeedInMetresPerHundredMilliseconds >= CurrentRoad.Length - StopDistance) || (!IsMovingForward && Position - SpeedInMetresPerHundredMilliseconds <= StopDistance) && city.TrafficLights.Where(v => v.ControlledIntersection == NextIntersection && v.ControlledRoad == CurrentRoad).FirstOrDefault() == null)
            {
                var roadsOnRight = CurrentRoad.GetRoads(NextIntersection, RoadDirection.Right);
                var vehicleOnRight = city.Vehicles
                    .Where(v => roadsOnRight.Contains(v.CurrentRoad) && v.NextIntersection == NextIntersection)
                    .FirstOrDefault(v => Math.Abs(v.Position - (IsMovingForward ? v.CurrentRoad.Length : 0)) <= 20);

                if (vehicleOnRight != null)
                {
                    shouldStopBecauseOfVehicleOnRight = true;
                }
            }

            //Проверка затора на следующей дороге
            bool shouldStopForTrafficJam = false;
            if (NextRoad != null && ((IsMovingForward && Position + SpeedInMetresPerHundredMilliseconds >= CurrentRoad.Length - StopDistance) || (!IsMovingForward && Position - SpeedInMetresPerHundredMilliseconds <= StopDistance)))
            {
                bool isNextRoadClear = NextRoad.IsClear(this);

                if (!isNextRoadClear)
                {
                    shouldStopForTrafficJam = true;
                }
            }

            if (shouldStopForTrafficLight || shouldStopForNextVehicle || shouldStopBecauseOfVehicleOnRight || shouldStopForTrafficJam)
            {
                Speed = 0;
            }
            else
            {
                double speedAfterHundredMilliseconds = SpeedInMetresPerHundredMilliseconds + AccelerationInMetresPerHundredMilliseconds;
                Speed += speedAfterHundredMilliseconds * 10 * 3600 / 1000;
                if (Speed > CurrentRoad.MaxAllowedSpeed)
                {
                    Speed = CurrentRoad.MaxAllowedSpeed;
                }
            }

            //Обновление позиции автомобиля
            if (!shouldStopForTrafficLight && !shouldStopForNextVehicle && !shouldStopBecauseOfVehicleOnRight && !shouldStopForTrafficJam)
            {
                Position += IsMovingForward ? SpeedInMetresPerHundredMilliseconds : -SpeedInMetresPerHundredMilliseconds;
            }
            else if ((shouldStopForTrafficLight || shouldStopBecauseOfVehicleOnRight || shouldStopForTrafficJam) && !shouldStopForNextVehicle)
            {
                Position = IsMovingForward ? CurrentRoad.Length - StopDistance : StopDistance;
            }

            //Если автомобиль достиг конца или начала дороги
            if (Speed != 0 && (Position >= CurrentRoad.Length || Position <= 0))
            {
                if (NextRoad != null)
                {
                    if (CurrentRoad == NextRoad)
                    {
                        Random random = new();
                        LineNumber = random.Next(1, CurrentRoad.NumOfLanesPerDirection + 1);
                        IsMovingForward = !IsMovingForward;
                        Position = IsMovingForward ? 0 : NextRoad.Length;
                        NextIntersection = IsMovingForward ? NextRoad.EndIntersection : NextRoad.StartIntersection;
                        NextRoad = GetNextRoad();
                    }
                    else
                    {
                        CurrentRoad.RemoveVehicle(this);
                        NextRoad.AddVehicle(this);
                        CurrentRoad = NextRoad;
                        Random random = new();
                        LineNumber = random.Next(1, CurrentRoad.NumOfLanesPerDirection + 1);
                        IsMovingForward = NextRoad.StartIntersection == NextIntersection;
                        Position = IsMovingForward ? 0 : NextRoad.Length;
                        NextIntersection = IsMovingForward ? NextRoad.EndIntersection : NextRoad.StartIntersection;
                        NextRoad = GetNextRoad();
                    }
                }
            }
            else
            {
                LineNumber = 1;
                NextRoad = GetNextRoad();
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
                    outgoingRoads = CurrentRoad.GetRoadsToGo(NextIntersection, RoadDirection.Forward);
                    if (LineNumber == 1)
                    {
                        outgoingRoads.AddRange(CurrentRoad.GetRoadsToGo(NextIntersection, RoadDirection.Left));
                        if (CurrentRoad.Type != RoadType.OneWay) outgoingRoads.Add(CurrentRoad);
                    }
                    else
                    {
                        outgoingRoads.AddRange(CurrentRoad.GetRoadsToGo(NextIntersection, RoadDirection.Right));
                    }
                    break;
                default:
                    outgoingRoads = CurrentRoad.GetRoadsToGo(NextIntersection, RoadDirection.Forward);
                    if (LineNumber == 1)
                    {
                        outgoingRoads.AddRange(CurrentRoad.GetRoadsToGo(NextIntersection, RoadDirection.Left));
                        if (CurrentRoad.Type != RoadType.OneWay) outgoingRoads.Add(CurrentRoad);
                    }
                    else if (LineNumber == CurrentRoad.NumOfLanesPerDirection)
                    {
                        outgoingRoads.AddRange(CurrentRoad.GetRoadsToGo(NextIntersection, RoadDirection.Right));
                    }
                    break;
            }

            //Выбираем случайное направление из доступных на перекрестке
            if (outgoingRoads.Count == 0)
            {
                return null; //Если нет доступных дорог, возвращаем null
            }

            Random random = new();
            var randomIndex = random.Next(outgoingRoads.Count);
            return outgoingRoads[randomIndex];
        }
    }
}
