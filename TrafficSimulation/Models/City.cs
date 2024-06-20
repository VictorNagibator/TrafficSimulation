namespace TrafficSimulation.Models
{
    public class City
    {
        public List<Intersection> Intersections { get; private set; } = [];
        public List<Road> Roads { get; private set; } = [];
        public List<Vehicle> Vehicles { get; private set; } = [];
        public List<TrafficLight> TrafficLights { get; private set; } = [];
        public double MaxAllowedSpeed { get; set; }
        public double ChanceToGetInCarAccident { get; set; }

        public void AddRoad(Road road)
        {
            road.MaxAllowedSpeed = MaxAllowedSpeed;

            Roads.Add(road);

            //Добавляем дорогу в список дорог каждого перекрестка
            road.StartIntersection.AddRoad(road);
            road.EndIntersection.AddRoad(road);

            if (!Intersections.Contains(road.StartIntersection)) Intersections.Add(road.StartIntersection);
            if (!Intersections.Contains(road.EndIntersection)) Intersections.Add(road.EndIntersection);
        }

        public void AddVehicle(Vehicle vehicle) => Vehicles.Add(vehicle);
        public void RemoveVehicle(Vehicle vehicle) => Vehicles.Remove(vehicle);

        public void AddTrafficLight(TrafficLight light) => TrafficLights.Add(light);
        public void RemoveTrafficLight(TrafficLight light) => TrafficLights.Remove(light);

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
