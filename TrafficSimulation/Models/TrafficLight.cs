namespace TrafficSimulation.Models
{
    public class TrafficLight
    {
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

        private double _redTimer = 30;
        public double RedTimer
        {
            get
            {
                return _redTimer;
            }
            set
            {
                if (value >= Constants.MinTrafficLightTimer)
                {
                    _redTimer = value;
                }
            }
        }
        private double ActualRedTimer => RedTimer / Constants.TimerInterval;

        private double _greenTimer = 30;
        public double GreenTimer
        {
            get
            {
                return _greenTimer;
            }
            set
            {
                if (value >= Constants.MinTrafficLightTimer)
                {
                    _greenTimer = value;
                }
            }
        }

        private double ActualGreenTimer => GreenTimer / Constants.TimerInterval;
        public double YellowTimer { get; private set; } = 2;
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
