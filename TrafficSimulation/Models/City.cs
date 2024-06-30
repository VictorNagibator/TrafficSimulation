namespace TrafficSimulation.Models
{
    public class City
    {
        public List<Road> Roads { get; private set; } = [];
        public List<Vehicle> Vehicles { get; private set; } = [];
        public List<TrafficLight> TrafficLights { get; private set; } = [];

        private double _maxAllowedSpeed = 60;
        public double MaxAllowedSpeed
        {
            get
            {
                return _maxAllowedSpeed;
            }
            set
            {
                _maxAllowedSpeed = value;
                Roads.ForEach(road => road.MaxAllowedSpeed = value);
            }
        }
        private double roadQuality = 1;
        public double RoadQuality
        {
            get
            {
                return roadQuality;
            }
            set
            {
                roadQuality = value;
                Roads.ForEach(road => road.Quality = value);
            }
        }
        public double ChanceToGetInCarAccident { get; set; } = 0.005;
        public double ActualChanceToGetInCarAccident => ChanceToGetInCarAccident * Constants.TimerInterval;

        public double CarAccidentTimer { get; set; } = 30;
        public double ActualCarAccidentTimer => CarAccidentTimer / Constants.TimerInterval;

        public double RainingTimer { get; set; } = 60;
        private double ActualRainingTimer => RainingTimer / Constants.TimerInterval;
        private int currentRainingTimer;
        public double ChanceOfRaining { get; set; } = 0.01;
        private double ActualChanceOfRaining => ChanceOfRaining * Constants.TimerInterval;
        public bool IsRaining { get; private set; } = false;

        public void AddRoad(Road road)
        {
            road.MaxAllowedSpeed = MaxAllowedSpeed;

            Roads.Add(road);

            //Добавляем дорогу в список дорог каждого перекрестка
            road.StartIntersection.AddRoad(road);
            road.EndIntersection.AddRoad(road);
        }

        public void RemoveRoad(Road road)
        {
            Roads.Remove(road);

            road.StartIntersection.IncomingRoads.Remove(road);
            road.StartIntersection.OutgoingRoads.Remove(road);
            road.EndIntersection.IncomingRoads.Remove(road);
            road.EndIntersection.OutgoingRoads.Remove(road);

            Vehicles.RemoveAll(v => v.CurrentRoad == road);
            TrafficLights.RemoveAll(tl => tl.ControlledRoad == road);
        }

        public void AddVehicle(Vehicle vehicle)
        {
            Vehicles.Add(vehicle);
            vehicle.CurrentRoad.AddVehicle(vehicle);
        }
        public void RemoveVehicle(Vehicle vehicle)
        {
            Vehicles.Remove(vehicle);
            vehicle.CurrentRoad.RemoveVehicle(vehicle);
        }

        public void RemoveAllVehicles()
        {
            foreach (var road in Roads) road.Vehicles.Clear();
            Vehicles.Clear();
        }

        public void AddTrafficLight(TrafficLight light) => TrafficLights.Add(light);
        public void RemoveTrafficLight(TrafficLight light) => TrafficLights.Remove(light);

        public void Update()
        {
            if (IsRaining)
            {
                currentRainingTimer--;
                if (currentRainingTimer <= 0)
                {
                    IsRaining = false;
                }
            }
            else
            {
                Random random = new();
                if (random.NextDouble() < ActualChanceOfRaining)
                {
                    IsRaining = true;
                    currentRainingTimer = (int)ActualRainingTimer;
                }
            }

            foreach (var vehicle in Vehicles)
            {
                vehicle.Update();
            }

            foreach (var light in TrafficLights)
            {
                light.Tick();
            }
        }
    }
}
