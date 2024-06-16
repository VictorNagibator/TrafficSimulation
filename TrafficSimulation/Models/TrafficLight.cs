namespace TrafficSimulation.Models
{
    internal class TrafficLight
    {
        public string ID { get; set; }
        public Road ControlledRoad { get; set; }
        public Intersection ControlledIntersection { get; set; }
        public TrafficLightState State { get; set; }
        public int Timer { get; set; }
        private int currentTime;

        public TrafficLight()
        {
            State = TrafficLightState.Red;
            currentTime = 0;
        }

        public void Tick()
        {
            currentTime++;
            if (State == TrafficLightState.Red && currentTime >= Timer)
            {
                State = TrafficLightState.Green;
                currentTime = 0;
            }
            else if (State == TrafficLightState.Green && currentTime >= Timer)
            {
                State = TrafficLightState.Yellow;
                currentTime = 0;
            }
            else if (State == TrafficLightState.Yellow && currentTime >= 5) // 5 секунд на желтый свет
            {
                State = TrafficLightState.Red;
                currentTime = 0;
            }
        }
    }

    internal enum TrafficLightState
    {
        Red,
        Yellow,
        Green,
        Transition //новое состояние для переходного момента
    }
}
