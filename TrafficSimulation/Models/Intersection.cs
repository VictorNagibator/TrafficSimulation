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
        public List<Road> IncomingRoads { get; set; } = new List<Road>();
        public List<Road> OutgoingRoads { get; set; } = new List<Road>();
        public List<TrafficLight> TrafficLights { get; set; } = new List<TrafficLight>();

        public void AddIncomingRoad(Road road) => IncomingRoads.Add(road);
        public void AddOutgoingRoad(Road road) => OutgoingRoads.Add(road);
        public void AddTrafficLight(TrafficLight trafficLight) => TrafficLights.Add(trafficLight);

        public bool IsGreenLight(Road road)
        {
            var light = TrafficLights.FirstOrDefault(t => t.ControlledRoad == road);
            return light != null && light.State == TrafficLightState.Green;
        }

        public List<Road> GetAllRoads()
        {
            var roads = new List<Road>();
            roads.AddRange(IncomingRoads);
            roads.AddRange(OutgoingRoads);

            return roads;
        }
    }
}
