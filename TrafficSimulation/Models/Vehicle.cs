using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TrafficSimulation.Models
{
    internal class Vehicle
    {
        public string ID { get; set; }
        public double Speed { get; set; }
        public double MaxSpeed { get; set; }
        public Road CurrentRoad { get; set; }
        public double Position { get; set; }
        public Intersection Destination { get; set; }

        private const double SafetyDistance = 40; // Расстояние до следующего автомобиля для остановки
        private const double StopDistance = 30.0;

        public void Update(City city)
        {
            // Проверка светофора на текущей дороге
            var trafficLight = city.TrafficLights.FirstOrDefault(light => light.ControlledRoad == CurrentRoad);
            if (trafficLight != null)
            {
                if (trafficLight.State == TrafficLightState.Red && Position + Speed >= CurrentRoad.Length - StopDistance)
                {
                    Speed = 0;
                }
                else
                {
                    Speed = MaxSpeed;
                }
            }

            // Проверка расстояния до следующего автомобиля
            var nextVehicle = city.Vehicles.FirstOrDefault(v => v.CurrentRoad == CurrentRoad && v.Position > Position);
            if (nextVehicle != null && nextVehicle.Position - Position < SafetyDistance)
            {
                Speed = 0;
            }

            Position += Speed;

            if (Position >= CurrentRoad.Length)
            {
                // Если достигнут конец дороги без перекрестка, развернуться и двигаться обратно
                if (CurrentRoad.EndIntersection.GetAllRoads().Count == 0)
                {
                    Position = CurrentRoad.Length;
                    Speed = -Speed; // Разворачиваемся, меняя знак скорости
                }
                else
                {
                    Position = 0;
                    CurrentRoad = GetNextRoad(city);
                }
            }
            else if (Position <= 0)
            {
                // Достигнут начало дороги без перекрестка, развернуться и двигаться обратно
                Speed = -Speed; // Разворачиваемся, меняя знак скорости
            }
        }

        public Road GetNextRoad(City city)
        {
            var currentIntersection = CurrentRoad.EndIntersection;
            var outgoingRoads = currentIntersection.OutgoingRoads;

            // Если на перекрестке нет исходящих дорог, возвращаем текущую дорогу
            if (outgoingRoads.Count == 0)
            {
                return CurrentRoad;
            }

            // Выбираем случайное направление из доступных на перекрестке
            Random random = new Random();
            var randomIndex = random.Next(outgoingRoads.Count);
            return outgoingRoads[randomIndex];
        }
    }

    internal class Bus : Vehicle
    {
        public int PassengerCapacity { get; set; }
    }

    internal class Car : Vehicle
    {
        public int PassengerCapacity { get; set; }
    }

    internal class Bicycle : Vehicle
    {
        public bool IsElectric { get; set; }
    }
}
