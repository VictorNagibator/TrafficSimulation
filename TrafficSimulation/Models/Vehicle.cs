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
        public double Position { get; set; }
        public Intersection NextIntersection { get; set; }
        public bool IsMovingForward { get; set; } = true;

        private const double SafetyDistance = 40; // Расстояние до следующего автомобиля для остановки

        public void Update(City city)
        {
            var stopDistance = CurrentRoad.Width / 1.4;
            // Проверка светофора на текущей дороге
            var trafficLight = city.TrafficLights.FirstOrDefault(light => light.ControlledIntersection == NextIntersection && light.ControlledRoad == CurrentRoad);
            if (trafficLight != null)
            {
                if (trafficLight.State == TrafficLightState.Red && ((IsMovingForward && Position + Speed >= CurrentRoad.Length - stopDistance) || (!IsMovingForward && Position - Speed <= stopDistance)))
                {
                    Position = IsMovingForward ? CurrentRoad.Length - stopDistance : stopDistance;
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
            var nextVehicle = city.Vehicles.FirstOrDefault(v => v.CurrentRoad == CurrentRoad && v.Position > Position);
            if (nextVehicle != null && nextVehicle.Position - Position < SafetyDistance)
            {
                Speed = 0;
            }

            // Обновляем позицию автомобиля в зависимости от направления движения
            Position += IsMovingForward ? Speed : -Speed;

            // Если автомобиль достиг конца или начала дороги
            if (Speed != 0 && (Position >= CurrentRoad.Length || Position <= 0))
            {
                // Получаем следующую дорогу на маршруте
                Road nextRoad = GetNextRoad(city);

                if (nextRoad != null)
                {
                    // Перемещаем автомобиль на следующую дорогу
                    CurrentRoad.RemoveVehicle(this);
                    nextRoad.AddVehicle(this);
                    CurrentRoad = nextRoad;
                    IsMovingForward = nextRoad.StartIntersection == NextIntersection;
                    Position = IsMovingForward ? 0 : nextRoad.Length;
                    NextIntersection = IsMovingForward ? nextRoad.EndIntersection : nextRoad.StartIntersection;
                }
                else
                {
                    Speed = 0; // Останавливаем машину, если нет доступной следующей дороги
                }
            }
        }

        public Road GetNextRoad(City city)
        {
            var currentIntersection = IsMovingForward ? CurrentRoad.EndIntersection : CurrentRoad.StartIntersection;
            var outgoingRoads = currentIntersection.OutgoingRoads;

            // Выбираем случайное направление из доступных на перекрестке
            if (outgoingRoads.Count == 0)
            {
                return null; // Если нет доступных дорог, возвращаем null
            }

            Random random = new Random();
            var randomIndex = random.Next(outgoingRoads.Count);
            return outgoingRoads[randomIndex];
        }
    }
}
