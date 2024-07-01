namespace TrafficSimulation.Models
{
    public class City
    {
        public List<Road> Roads { get; private set; } = [];

        public List<Vehicle> Vehicles => Roads.SelectMany(road => road.Vehicles).ToList();
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
                if (value >= Constants.MinAllowedSpeed && value <= Constants.MaxAllowedSpeed)
                {
                    _maxAllowedSpeed = value;
                    Roads.ForEach(road => road.MaxAllowedSpeed = value);
                }
            }
        }
        private double _roadQuality = 1;
        public double RoadQuality
        {
            get
            {
                return _roadQuality;
            }
            set
            {
                if (value >= Constants.MinQuality && value <= Constants.MaxQuality)
                {
                    _roadQuality = value;
                    Roads.ForEach(road => road.Quality = value);
                }
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

            road.Vehicles.Clear();
            TrafficLights.RemoveAll(tl => tl.ControlledRoad == road);
        }

        public void AddVehicle(Vehicle vehicle)
        {
            vehicle.CurrentRoad.AddVehicle(vehicle);
        }
        public void RemoveVehicle(Vehicle vehicle)
        {
            vehicle.CurrentRoad.RemoveVehicle(vehicle);
        }

        public void RemoveAllVehicles()
        {
            foreach (var road in Roads) road.Vehicles.Clear();
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
                vehicle.Update(this);
            }

            foreach (var light in TrafficLights)
            {
                light.Tick();
            }
        }
    }
}
