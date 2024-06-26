namespace TrafficSimulation.Models
{
    public class Intersection
    {
        public string Name { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public HashSet<Road> IncomingRoads { get; set; } = [];
        public HashSet<Road> OutgoingRoads { get; set; } = [];

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


        public double Width => GetWidth();
        private double GetWidth()
        {
            double maxRoadWidth = 0;

            foreach (var road in IncomingRoads)
            {
                maxRoadWidth = Math.Max(maxRoadWidth, road.Type == RoadType.OneWay ? Constants.RoadWidthMetres * road.NumOfLinesPerDirection / 2 : Constants.RoadWidthMetres * road.NumOfLinesPerDirection);
            }
            foreach (var road in OutgoingRoads)
            {
                maxRoadWidth = Math.Max(maxRoadWidth, road.Type == RoadType.OneWay ? Constants.RoadWidthMetres * road.NumOfLinesPerDirection / 2 : Constants.RoadWidthMetres * road.NumOfLinesPerDirection);
            }

            return maxRoadWidth;
        }
        public double Angle => GetAngle();
        private double GetAngle()
        {
            var uniqueroads = UniqueBiggestRoads.ToList();
            if (uniqueroads.Count == 1) return uniqueroads[0].Angle;
            else if (uniqueroads.Count == 2 && Math.Abs(uniqueroads[0].Angle - uniqueroads[1].Angle) <= Constants.AngleTolerance) return uniqueroads[0].Angle;
            else if (uniqueroads.Count == 2) return uniqueroads[0].Angle - uniqueroads[1].Angle;
            else
            {
                foreach (var road1 in uniqueroads)
                {
                    foreach (var road2 in uniqueroads)
                    {
                        if (road1 != road2 && Math.Abs(road1.Angle - road2.Angle) <= Constants.AngleTolerance) return road1.Angle;
                    }
                }

                return 0;
            }
        }
        public HashSet<Road> UniqueBiggestRoads => GetUniqueBiggestRoads();
        private HashSet<Road> GetUniqueBiggestRoads()
        {
            var roads = new HashSet<Road>();
            foreach (var road in IncomingRoads) { if (road.Width == Width) roads.Add(road); }
            foreach (var road in OutgoingRoads) { if (road.Width == Width) roads.Add(road); }
            return roads;
        }
        public bool HasNoRoads()
        {
            return IncomingRoads.Count == 0 && OutgoingRoads.Count == 0;
        }

        public double BestRoadQuality => GetBestRoadQuality();

        private double GetBestRoadQuality()
        {
            double bestRoadQuality = 0;
            foreach (var road in IncomingRoads)
            {
                bestRoadQuality = Math.Max(bestRoadQuality, road.Quality);
            }
            foreach (var road in OutgoingRoads)
            {
                bestRoadQuality = Math.Max(bestRoadQuality, road.Quality);
            }

            return bestRoadQuality;
        }
    }
}
