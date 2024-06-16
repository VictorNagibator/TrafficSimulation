namespace TrafficSimulation.Models
{
    internal class Intersection
    {
        public string Name { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double ActualX => X / Constants.MetresPerPixel;
        public double ActualY => Y / Constants.MetresPerPixel;
        public List<Road> IncomingRoads { get; set; } = [];
        public List<Road> OutgoingRoads { get; set; } = [];

        public void AddRoad(Road road)
        {
            if (road.Type == RoadType.TwoWay)
            {
                IncomingRoads.Add(road);
                OutgoingRoads.Add(road);
            }
            else
            {
                if (road.StartIntersection == this)
                {
                    OutgoingRoads.Add(road);
                }
                else
                {
                    IncomingRoads.Add(road);
                }
            }
        }

        public void RemoveRoad(Road road) 
        {
            IncomingRoads.Remove(road);
            OutgoingRoads.Remove(road);
        }

        public double GetWidth()
        {
            double maxRoadWidth = 0;

            foreach (var road in IncomingRoads)
            {
                maxRoadWidth = Math.Max(maxRoadWidth, road.Type == RoadType.OneWay ? Constants.RoadWidthMetres * road.NumOfLanesPerDirection / 2 : Constants.RoadWidthMetres * road.NumOfLanesPerDirection);
            }
            foreach (var road in OutgoingRoads)
            {
                maxRoadWidth = Math.Max(maxRoadWidth, road.Type == RoadType.OneWay ? Constants.RoadWidthMetres * road.NumOfLanesPerDirection / 2 : Constants.RoadWidthMetres * road.NumOfLanesPerDirection);
            }

            return maxRoadWidth;
        }
    }
}
