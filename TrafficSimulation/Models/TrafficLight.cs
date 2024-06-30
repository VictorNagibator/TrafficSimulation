namespace TrafficSimulation.Models
{
    public class TrafficLight
    {
        public string? Name { get; internal set; }
        public Road ControlledRoad { get; internal set; }
        public Intersection ControlledIntersection { get; internal set; }
        private TrafficLightState _state;
        public TrafficLightState State 
        {
            get { return _state; }
            internal set
            {
                _state = value;
                switch (_state)
                {
                    case TrafficLightState.Red:
                        currentTime = (int)ActualRedTimer;
                        break;
                    case TrafficLightState.Yellow:
                        currentTime = (int)ActualYellowTimer;
                        break;
                    case TrafficLightState.Green:
                        currentTime = (int)ActualGreenTimer;
                        break;
                    default:
                        break;
                }
            }
        }
        public double RedTimer { get; set; } = 30;
        private double ActualRedTimer => RedTimer / Constants.TimerInterval;
        public double GreenTimer { get; set; } = 30;
        private double ActualGreenTimer => GreenTimer / Constants.TimerInterval;
        public double YellowTimer { get; set; } = 2;
        private double ActualYellowTimer => YellowTimer / Constants.TimerInterval;
        private int currentTime;

        public void Tick()
        {
            currentTime--;
            if (currentTime <= 0)
            {
                switch (State)
                {
                    case TrafficLightState.Red:
                        State = TrafficLightState.Yellow;
                        break;
                    case TrafficLightState.Yellow:
                        State = TrafficLightState.Green;
                        break;
                    case TrafficLightState.Green:
                        State = TrafficLightState.Red;
                        break;
                    default:
                        break;
                }
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
