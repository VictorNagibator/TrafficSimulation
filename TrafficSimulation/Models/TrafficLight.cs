namespace TrafficSimulation.Models
{
    public class TrafficLight
    {
        public string ID { get; set; }
        public Road ControlledRoad { get; set; }
        public Intersection ControlledIntersection { get; set; }
        public TrafficLightState State { get; set; }
        public int RedTimer { get; set; }
        public int GreenTimer { get; set; }
        private int currentTime;

        public TrafficLight()
        {
            State = TrafficLightState.Red;
            currentTime = 0;
        }

        public void Tick()
        {
            currentTime++;
            if (State == TrafficLightState.Red && currentTime >= RedTimer * 10)
            {
                State = TrafficLightState.Yellow;
                currentTime = 0;
            }
            else if (State == TrafficLightState.Yellow && currentTime >= 2 * 10) //2 секунды на желтый свет
            {
                State = TrafficLightState.Green;
                currentTime = 0;
            }
            else if (State == TrafficLightState.Green && currentTime >= GreenTimer * 10)
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
