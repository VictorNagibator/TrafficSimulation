namespace TrafficSimulation.Models
{
    internal class City
    {
        public List<Intersection> Intersections { get; set; } = [];
        public List<Road> Roads { get; private set; } = [];
        public List<Vehicle> Vehicles { get; private set; } = [];
        public List<TrafficLight> TrafficLights { get; private set; } = [];

        public void AddRoad(Road road)
        {
            Roads.Add(road);

            //Добавляем дорогу в список дорог каждого перекрестка
            road.StartIntersection.AddRoad(road);
            road.EndIntersection.AddRoad(road);

            if (!Intersections.Contains(road.StartIntersection)) Intersections.Add(road.StartIntersection);
            if (!Intersections.Contains(road.EndIntersection)) Intersections.Add(road.EndIntersection);
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
                vehicle.Update(this);
            }

            foreach (var light in TrafficLights)
            {
                light.Tick();
            }
        }
    }
}
