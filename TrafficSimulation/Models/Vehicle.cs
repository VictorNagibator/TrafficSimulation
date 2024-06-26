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

        public double StopDistance => NextIntersection.Width / 2 + Constants.VehicleLengthMetres / 2;
        public bool IsInCarAccident { get; set; } = false;
        private double SpeedInMetresPerSecond => Speed / 3600 * 1000;
        private double SpeedInMetresPerHundredMilliseconds => SpeedInMetresPerSecond * 0.1;
        private double AccelerationInMetresPerHundredMilliseconds => Acceleration * 0.01;

        private double AdditionToGetInCarAccidentChanceBecauseOfRaining = 0.05 * Constants.TimerInterval;

        private Random random = new();
        private int currentTimer;

        public void Update(City city)
        {
            if (IsInCarAccident)
            {
                currentTimer--;
                if (currentTimer <= 0)
                {
                    IsInCarAccident = false;
                }
                return;
            }
            double ActualChanceToGetInCarAccident = city.ActualChanceToGetInCarAccident + (1 - CurrentRoad.Quality) * city.ActualChanceToGetInCarAccident;
            ActualChanceToGetInCarAccident += city.IsRaining ? AdditionToGetInCarAccidentChanceBecauseOfRaining : 0;
            if (random.NextDouble() <= ActualChanceToGetInCarAccident)
            {
                IsInCarAccident = true;
                currentTimer = (int)city.ActualCarAccidentTimer;
                return;
            }

            //Проверка светофора на текущей дороге
            var trafficLight = city.TrafficLights.FirstOrDefault(light => light.ControlledIntersection == NextIntersection && light.ControlledRoad == CurrentRoad);
            if (trafficLight != null && (trafficLight.State == TrafficLightState.Red || trafficLight.State == TrafficLightState.Yellow))
            {
                if ((IsMovingForward && Position + SpeedInMetresPerHundredMilliseconds >= CurrentRoad.Length - StopDistance) || (!IsMovingForward && Position - SpeedInMetresPerHundredMilliseconds <= StopDistance))
                {
                    return;
                }
            }

            //Проверка расстояния до следующего автомобиля
            var nextVehicle = city.Vehicles
                .Where(v => v.CurrentRoad == CurrentRoad && v.IsMovingForward == IsMovingForward && v.LineNumber == LineNumber)
                .OrderBy(v => IsMovingForward ? v.Position : -v.Position)
                .FirstOrDefault(v => IsMovingForward ? v.Position > Position : v.Position < Position);

            if (nextVehicle != null)
            {
                double distanceToNextVehicle = Math.Abs(nextVehicle.Position - Position);
                if (distanceToNextVehicle < Constants.SafetyDistanceToStop && !nextVehicle.IsInCarAccident)
                {
                    return;
                }
                else if (nextVehicle.IsInCarAccident && distanceToNextVehicle < Constants.SafetyDistanceToStop)
                {
                    if (!TryToChangeLine()) return;
                }
            }

            //Проверка наличия автомобиля справа и расстояния до перекрестка
            if (((IsMovingForward && Position + SpeedInMetresPerHundredMilliseconds <= CurrentRoad.Length - StopDistance && Position + SpeedInMetresPerHundredMilliseconds >= CurrentRoad.Length - StopDistance * 1.5) || (!IsMovingForward && Position - SpeedInMetresPerHundredMilliseconds >= StopDistance && Position - SpeedInMetresPerHundredMilliseconds <= StopDistance *1.5)) && city.TrafficLights.Where(v => v.ControlledIntersection == NextIntersection && v.ControlledRoad == CurrentRoad).FirstOrDefault() == null)
            {
                var roadsOnRight = CurrentRoad.GetRoads(NextIntersection, RoadDirection.Right);
                var vehicleOnRight = city.Vehicles
                    .Where(v => roadsOnRight.Contains(v.CurrentRoad) && v.NextIntersection == NextIntersection && (!city.TrafficLights.Any(tl => tl.ControlledRoad == v.CurrentRoad && tl.ControlledIntersection == v.NextIntersection) || city.TrafficLights.Any(tl => tl.ControlledRoad == v.CurrentRoad && tl.ControlledIntersection == v.NextIntersection && tl.State == TrafficLightState.Green)))
                    .FirstOrDefault(v => Math.Abs(v.Position - (IsMovingForward ? v.CurrentRoad.Length : 0)) <= Constants.SafetyDistanceToStop * 2);

                if (vehicleOnRight != null)
                {
                    return;
                }
            }

            //Проверка затора на следующей дороге
            if (NextRoad != null && ((IsMovingForward && Position + SpeedInMetresPerHundredMilliseconds >= CurrentRoad.Length - StopDistance) || (!IsMovingForward && Position - SpeedInMetresPerHundredMilliseconds <= StopDistance)))
            {
                bool isNextRoadClear = NextRoad.IsClear(this);

                if (!isNextRoadClear)
                {
                    return;
                }
            }

            //если попал в тупик и не может развернуться
            if (NextRoad == null && LineNumber == 1 &&((IsMovingForward && Position + SpeedInMetresPerHundredMilliseconds >= CurrentRoad.Length - StopDistance) || (!IsMovingForward && Position - SpeedInMetresPerHundredMilliseconds <= StopDistance))) return;

            double speedAfterHundredMilliseconds = SpeedInMetresPerHundredMilliseconds + AccelerationInMetresPerHundredMilliseconds;
            Speed += speedAfterHundredMilliseconds * 10 * 3600 / 1000;
            double MaxPossibleSpeed = CurrentRoad.Quality * CurrentRoad.MaxAllowedSpeed;
            MaxPossibleSpeed -= city.IsRaining ? 10 : 0;
            if (Speed > MaxPossibleSpeed)
            {
                Speed = MaxPossibleSpeed;
            }
            
            Position += IsMovingForward ? SpeedInMetresPerHundredMilliseconds : -SpeedInMetresPerHundredMilliseconds;
            //Если автомобиль достиг конца или начала дороги
            if (Position >= CurrentRoad.Length || Position <= 0)
            {
                if (NextRoad != null)
                {
                    CurrentRoad.Quality -= city.IsRaining ? 0.0005 : 0.0002; //условное значение ухудшения качества дороги после каждого проезда
                    if (CurrentRoad == NextRoad)
                    {
                        LineNumber = random.Next(1, CurrentRoad.NumOfLinesPerDirection + 1);
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
                        LineNumber = random.Next(1, CurrentRoad.NumOfLinesPerDirection + 1);
                        IsMovingForward = NextRoad.StartIntersection == NextIntersection;
                        Position = IsMovingForward ? 0 : NextRoad.Length;
                        NextIntersection = IsMovingForward ? NextRoad.EndIntersection : NextRoad.StartIntersection;
                        NextRoad = GetNextRoad();
                    }
                }
                else
                {
                    LineNumber = 1;
                    NextRoad = GetNextRoad();
                }
            }
        }

        public bool TryToChangeLine()
        {
            if (CurrentRoad.NumOfLinesPerDirection == 1) return false;

            int newLine;
            while ((newLine = random.Next(Math.Max(1, LineNumber - 1), Math.Min(LineNumber + 1, CurrentRoad.NumOfLinesPerDirection) + 1)) == LineNumber);
            if (CurrentRoad.Vehicles.Any(v => v.LineNumber == newLine && Math.Abs(v.Position - Position) <= Constants.VehicleLengthMetres)) return false;

            LineNumber = newLine;
            return true;
        }

        public Road GetNextRoad()
        {
            int numOfLanes = CurrentRoad.NumOfLinesPerDirection;
            List<Road> outgoingRoads;
            switch (numOfLanes)
            {
                case 1:
                    outgoingRoads = NextIntersection.OutgoingRoads.ToList();
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
                    else if (LineNumber == CurrentRoad.NumOfLinesPerDirection)
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
