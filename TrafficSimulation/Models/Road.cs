using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TrafficSimulation.Models
{
    internal class Road
    {
        public string Name { get; set; }
        public double Length { get; set; }
        public double Width { get; set; }
        public RoadType Type { get; set; }
        public Intersection StartIntersection { get; set; }
        public Intersection EndIntersection { get; set; }
        public List<Vehicle> Vehicles { get; set; } = new List<Vehicle>();

        public void AddVehicle(Vehicle vehicle) => Vehicles.Add(vehicle);

        public void RemoveVehicle(Vehicle vehicle) => Vehicles.Remove(vehicle);

        public double[] GetPositionOnRoad(Vehicle vehicle)
        {
            double startX = StartIntersection.X;
            double startY = StartIntersection.Y;
            double endX = EndIntersection.X;
            double endY = EndIntersection.Y;

            double angle = Math.Atan2(endY - startY, endX - startX);

            double offsetX = Math.Cos(angle) * vehicle.Position;
            double offsetY = Math.Sin(angle) * vehicle.Position;

            return [startX + offsetX, startY + offsetY];
        }
    }

    internal enum RoadType
    {
        OneWay,
        TwoWay
    }
}
