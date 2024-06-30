namespace TrafficSimulation.Models
{
    public class Vehicle
    {
        public string ID { get; internal set; }
        public double Speed { get; private set; } = 0; //км/ч
        public double Acceleration { get; private set; } = 1; //в м/с^2
        public Road CurrentRoad { get; internal set; }
        public City CurrentCity { get; internal set; }

        public int LineNumber { get; private set; }
        public double Position { get; internal set; }
        public Intersection NextIntersection { get; internal set; }
        public Road NextRoad { get; internal set; }
        public bool IsMovingForward { get; private set; } = true;

        public double StopDistance => NextIntersection.Width / 2 + Constants.VehicleLengthMetres / 2;
        public bool IsInCarAccident { get; private set; } = false;
        private double SpeedInMetresPerSecond => Speed / 3600 * 1000;
        private double SpeedInMetresPerHundredMilliseconds => SpeedInMetresPerSecond * 0.1;
        private double AccelerationInMetresPerHundredMilliseconds => Acceleration * 0.01;

        private double AdditionToGetInCarAccidentChanceBecauseOfRaining = 0.005 * Constants.TimerInterval;
        private double ChanceToChangeLine = 0.02 * Constants.TimerInterval;

        private Random random = new();
        private int currentTimer;

        public Vehicle(City city)
        {
            CurrentCity = city;
            if (city.Roads.Count == 0) return;

            //Выбираем случайную дорогу
            var road = city.Roads[random.Next(city.Roads.Count)];

            //Определяем направление движения
            if (road.Type == RoadType.OneWay)
            {
                IsMovingForward = true;
            }
            else
            {
                IsMovingForward = random.Next(2) == 0;
            }

            int lineNumber = random.Next(1, road.NumOfLinesPerDirection + 1);

            ID = "Car" + (city.Vehicles.Count + 1);
            CurrentRoad = road;
            LineNumber = lineNumber;
            Position = IsMovingForward ? 0 : road.Length;
            NextIntersection = IsMovingForward ? road.EndIntersection : road.StartIntersection;
            NextRoad = GetNextRoad();
        }

        public void Update()
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
            double ActualChanceToGetInCarAccident = CurrentCity.ActualChanceToGetInCarAccident + (1 - CurrentRoad.Quality) * CurrentCity.ActualChanceToGetInCarAccident;
            ActualChanceToGetInCarAccident += CurrentCity.IsRaining ? AdditionToGetInCarAccidentChanceBecauseOfRaining : 0;
            if (random.NextDouble() <= ActualChanceToGetInCarAccident && !WillBeOuttaRoad())
            {
                IsInCarAccident = true;
                currentTimer = (int)CurrentCity.ActualCarAccidentTimer;
                return;
            }

            //Проверка вариантов для остановки машины
            if (ShouldStopBecauseOfTrafficLight() || ShouldStopBecauseOfNextVehicle() || ShouldStopBecauseOfVehicleOnRight() || ShouldStopBecauseOfTrafficJam() || ShouldStopBecauseOfDeadEnd()) return;

            //Разгоняемся и передвигаем позицию машины
            SpeedUp();
            Position += IsMovingForward ? SpeedInMetresPerHundredMilliseconds : -SpeedInMetresPerHundredMilliseconds;

            //Машина пытается поменять полосу движения
            if (random.NextDouble() <= ChanceToChangeLine) TryToChangeLine();

            //Если автомобиль достиг конца или начала дороги
            if (Position >= CurrentRoad.Length || Position <= 0)
            {
                if (NextRoad != null)
                {
                    CurrentRoad.Quality -= CurrentCity.IsRaining ? 0.0005 : 0.0002; //условное значение ухудшения качества дороги после каждого проезда
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

        private bool WillBeOuttaRoad()
        {
            return (IsMovingForward && Position + SpeedInMetresPerHundredMilliseconds >= CurrentRoad.Length - StopDistance) ||
                (!IsMovingForward && Position - SpeedInMetresPerHundredMilliseconds <= StopDistance);
        }

        private bool ShouldStopBecauseOfTrafficLight()
        {
            var trafficLight = CurrentCity.TrafficLights.FirstOrDefault(light => light.ControlledIntersection == NextIntersection && light.ControlledRoad == CurrentRoad);
            if (trafficLight != null && (trafficLight.State == TrafficLightState.Red || trafficLight.State == TrafficLightState.Yellow))
            {
                if (WillBeOuttaRoad())
                {
                    return true;
                }
            }
            return false;
        }

        private bool ShouldStopBecauseOfNextVehicle()
        {
            var nextVehicle = CurrentCity.Vehicles
                .Where(v => v.CurrentRoad == CurrentRoad && v.IsMovingForward == IsMovingForward && v.LineNumber == LineNumber)
                .OrderBy(v => IsMovingForward ? v.Position : -v.Position)
                .FirstOrDefault(v => IsMovingForward ? v.Position > Position : v.Position < Position);

            if (nextVehicle != null)
            {
                double distanceToNextVehicle = Math.Abs(nextVehicle.Position - Position);
                if (distanceToNextVehicle < Constants.SafetyDistanceToStop && !nextVehicle.IsInCarAccident)
                {
                    return true;
                }
                else if (nextVehicle.IsInCarAccident && distanceToNextVehicle < Constants.SafetyDistanceToStop)
                {
                    if (!TryToChangeLine()) return true;
                }
            }
            return false;
        }

        private bool ShouldStopBecauseOfVehicleOnRight()
        {
            if (WillBeOuttaRoad() && CurrentCity.TrafficLights.Where(v => v.ControlledIntersection == NextIntersection && v.ControlledRoad == CurrentRoad).FirstOrDefault() == null)
            {
                var roadsOnRight = CurrentRoad.GetRoads(NextIntersection, RoadDirection.Right);
                var vehicleOnRight = CurrentCity.Vehicles
                    .Where(v => roadsOnRight.Contains(v.CurrentRoad) && v.NextIntersection == NextIntersection && (!CurrentCity.TrafficLights.Any(tl => tl.ControlledRoad == v.CurrentRoad && tl.ControlledIntersection == v.NextIntersection) || CurrentCity.TrafficLights.Any(tl => tl.ControlledRoad == v.CurrentRoad && tl.ControlledIntersection == v.NextIntersection && tl.State == TrafficLightState.Green)) && !v.IsInCarAccident && Math.Abs(v.Position - (IsMovingForward ? v.CurrentRoad.Length : 0)) <= Constants.SafetyDistanceToStop * 2)
                    .FirstOrDefault();

                if (vehicleOnRight != null)
                {
                    return true;
                }
            }
            return false;
        }

        private bool ShouldStopBecauseOfTrafficJam()
        {
            if (NextRoad != null && WillBeOuttaRoad())
            {
                bool isNextRoadClear = NextRoad.IsClear(this);

                if (!isNextRoadClear)
                {
                    return true;
                }
            }
            return false;
        }

        private bool ShouldStopBecauseOfDeadEnd()
        {
            if (NextRoad == null && LineNumber == 1 && WillBeOuttaRoad()) return true;
            return false;
        }

        private void SpeedUp()
        {
            double speedAfterHundredMilliseconds = SpeedInMetresPerHundredMilliseconds + AccelerationInMetresPerHundredMilliseconds;
            Speed += speedAfterHundredMilliseconds * 10 * 3600 / 1000;
            double MaxPossibleSpeed = CurrentRoad.Quality * CurrentRoad.MaxAllowedSpeed;
            MaxPossibleSpeed -= CurrentCity.IsRaining ? 15 : 0;
            if (Speed > MaxPossibleSpeed)
            {
                Speed = MaxPossibleSpeed;
            }
        }

        private bool TryToChangeLine()
        {
            if (CurrentRoad.NumOfLinesPerDirection == 1) return false;

            int newLine;
            while ((newLine = random.Next(Math.Max(1, LineNumber - 1), Math.Min(LineNumber + 1, CurrentRoad.NumOfLinesPerDirection) + 1)) == LineNumber);
            if (CurrentRoad.Vehicles.Any(v => v.LineNumber == newLine && Math.Abs(v.Position - Position) <= Constants.VehicleLengthMetres)) return false;

            LineNumber = newLine;
            NextRoad = GetNextRoad();
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
