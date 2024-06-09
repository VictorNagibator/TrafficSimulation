using System;
using System.Linq;

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

        private const double safetyDistance = 40; //Расстояние до следующего автомобиля для остановки

        public void Update(City city)
        {
            double stopDistance = Road.Width / 1.35;

            // Проверка светофора на текущей дороге
            var trafficLight = city.TrafficLights.FirstOrDefault(light => light.ControlledIntersection == NextIntersection && light.ControlledRoad == CurrentRoad);
            if (trafficLight != null)
            {
                if (trafficLight.State == TrafficLightState.Red && ((IsMovingForward && Position + Speed >= CurrentRoad.GetLength() - stopDistance) || (!IsMovingForward && Position - Speed <= stopDistance)))
                {
                    Position = IsMovingForward ? CurrentRoad.GetLength() - stopDistance : stopDistance;
                    Speed = 0;
                }
                else
                {
                    Speed = MaxSpeed;
                }
            }
            else
            {
                Speed = MaxSpeed;
            }

            // Проверка расстояния до следующего автомобиля
            var nextVehicle = city.Vehicles
                .Where(v => v.CurrentRoad == CurrentRoad && v.IsMovingForward == IsMovingForward && v.LineNumber == LineNumber)
                .OrderBy(v => Math.Abs(v.Position - Position))
                .FirstOrDefault(v => IsMovingForward ? v.Position > Position : v.Position < Position);

            if (nextVehicle != null)
            {
                double distanceToNextVehicle = Math.Abs(nextVehicle.Position - Position);
                if (distanceToNextVehicle < safetyDistance)
                {
                    Speed = 0;
                }
            }

            // Обновляем позицию автомобиля в зависимости от направления движения
            Position += IsMovingForward ? Speed : -Speed;

            // Если автомобиль достиг конца или начала дороги
            if (Speed != 0 && (Position >= CurrentRoad.GetLength() || Position <= 0))
            {
                // Получаем следующую дорогу на маршруте
                Road nextRoad = GetNextRoad(city);

                if (nextRoad != null)
                {
                    // Перемещаем автомобиль на следующую дорогу
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

        public Road GetNextRoad(City city)
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
                        outgoingRoads.Add(CurrentRoad);
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
                        outgoingRoads.Add(CurrentRoad);
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
