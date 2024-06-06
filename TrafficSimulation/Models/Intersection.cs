using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TrafficSimulation.Models
{
    internal class Intersection
    {
        public string Name { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
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
    }
}
