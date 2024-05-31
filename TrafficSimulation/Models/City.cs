using System;
using System.Collections.Generic;

namespace TrafficSimulation.Models
{
    internal class City
    {
        public List<Road> Roads { get; private set; }
        public List<Vehicle> Vehicles { get; private set; }
        public List<TrafficLight> TrafficLights { get; private set; }

        public City()
        {
            Roads = new List<Road>();
            Vehicles = new List<Vehicle>();
            TrafficLights = new List<TrafficLight>();
        }

        public void AddRoad(Road road)
        {
            Roads.Add(road);
        }

        public void AddVehicle(Vehicle vehicle)
        {
            Vehicles.Add(vehicle);
        }

        public void AddTrafficLight(TrafficLight light)
        {
            TrafficLights.Add(light);
        }

        public void Update()
        {
                foreach (var vehicle in Vehicles)
                {
                    vehicle.Update(this); // Обновляем положение каждой машины

                    // Проверяем, достигла ли машина конечного перекрестка
                    if (vehicle.CurrentRoad.EndIntersection != null &&
                        vehicle.Position >= vehicle.CurrentRoad.Length)
                    {
                        // Если машина достигла перекрестка, выбираем следующую дорогу
                        Road nextRoad = vehicle.GetNextRoad(this);
                        vehicle.Position = 0; // Сбрасываем позицию машины

                        // Перемещаем машину на следующую дорогу
                        vehicle.CurrentRoad = nextRoad;
                        vehicle.Destination = nextRoad.EndIntersection; // Устанавливаем новый пункт назначения
                    }
                }

                // Обновляем состояние светофоров
                foreach (var light in TrafficLights)
                {
                    light.Tick(); // Уменьшаем таймер для каждого светофора
                }
        }
    }
}
