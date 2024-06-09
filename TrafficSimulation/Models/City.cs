using System;
using System.Collections.Generic;

namespace TrafficSimulation.Models
{
    internal class City
    {
        public List<Road> Roads { get; private set; } = [];
        public List<Vehicle> Vehicles { get; private set; } = [];
        public List<TrafficLight> TrafficLights { get; private set; } = [];

        public void AddRoad(Road road)
        {
            Roads.Add(road);

            // Добавляем дорогу в список дорог каждого перекрестка
            road.StartIntersection.AddRoad(road);
            road.EndIntersection.AddRoad(road);
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
                vehicle.Update(this); //Обновляем положение каждой машины
            }

            foreach (var light in TrafficLights)
            {
                light.Tick(); //Уменьшаем таймер для каждого светофора
            }
        }
    }
}
