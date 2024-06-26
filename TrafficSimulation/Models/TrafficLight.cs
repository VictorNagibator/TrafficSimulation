namespace TrafficSimulation.Models
{
    public class TrafficLight
    {
        public string ID { get; set; }
        public Road ControlledRoad { get; set; }
        public Intersection ControlledIntersection { get; set; }
        public TrafficLightState State { get; set; }
        public double RedTimer { get; set; } = 30;
        public double ActualRedTimer => RedTimer / Constants.TimerInterval;
        public double GreenTimer { get; set; } = 30;
        public double ActualGreenTimer => GreenTimer / Constants.TimerInterval;
        public double YellowTimer { get; set; } = 2;
        public double ActualYellowTimer => YellowTimer / Constants.TimerInterval;
        private int currentTime;

        public void Tick()
        {
            currentTime++;
            if (State == TrafficLightState.Red && currentTime >= ActualRedTimer)
            {
                State = TrafficLightState.Yellow;
                currentTime = 0;
            }
            else if (State == TrafficLightState.Yellow && currentTime >= ActualYellowTimer)
            {
                State = TrafficLightState.Green;
                currentTime = 0;
            }
            else if (State == TrafficLightState.Green && currentTime >= ActualGreenTimer)
            {
                State = TrafficLightState.Red;
                currentTime = 0;
            }
        }
    }

    public enum TrafficLightState
    {
        Red,
        Yellow,
        Green
    }
}
