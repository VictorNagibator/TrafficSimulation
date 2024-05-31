using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using TrafficSimulation.Models;
using System.Windows.Threading;

namespace TrafficSimulation
{
    public partial class MainWindow : Window
    {
        private City city;
        private DispatcherTimer timer;

        public MainWindow()
        {
            InitializeComponent();
            InitializeCity();
            InitializeTimer();
            InitializeSliders();
        }

        private void InitializeCity()
        {
            city = new City();

            Intersection intersection1 = new Intersection { Name = "Intersection1", X = 100, Y = 100 };
            Intersection intersection2 = new Intersection { Name = "Intersection2", X = 300, Y = 100 };
            Intersection intersection3 = new Intersection { Name = "Intersection3", X = 300, Y = 300 };
            Intersection intersection4 = new Intersection { Name = "Intersection4", X = 100, Y = 300 };

            Road road1 = new Road { Name = "Road1", Length = 200, Width = 40, Type = RoadType.TwoWay, StartIntersection = intersection1, EndIntersection = intersection2 };
            Road road2 = new Road { Name = "Road2", Length = 200, Width = 40, Type = RoadType.TwoWay, StartIntersection = intersection2, EndIntersection = intersection3 };
            Road road3 = new Road { Name = "Road3", Length = 200, Width = 40, Type = RoadType.TwoWay, StartIntersection = intersection3, EndIntersection = intersection4 };
            Road road4 = new Road { Name = "Road4", Length = 200, Width = 40, Type = RoadType.TwoWay, StartIntersection = intersection4, EndIntersection = intersection1 };

            city.AddRoad(road1);
            city.AddRoad(road2);
            city.AddRoad(road3);
            city.AddRoad(road4);

            intersection1.AddOutgoingRoad(road1);
            intersection2.AddIncomingRoad(road1);
            intersection2.AddOutgoingRoad(road2);
            intersection3.AddIncomingRoad(road2);
            intersection3.AddOutgoingRoad(road3);
            intersection4.AddIncomingRoad(road3);
            intersection4.AddOutgoingRoad(road4);
            intersection1.AddIncomingRoad(road4);

            Car car1 = new Car { ID = "Car1", Speed = 10, MaxSpeed = (int)MaxSpeedSlider.Value, CurrentRoad = road1, Position = 0, Destination = intersection2 };
            Car car2 = new Car { ID = "Car2", Speed = 10, MaxSpeed = (int)MaxSpeedSlider.Value, CurrentRoad = road2, Position = 0, Destination = intersection3 };
            Car car3 = new Car { ID = "Car3", Speed = 10, MaxSpeed = (int)MaxSpeedSlider.Value, CurrentRoad = road3, Position = 0, Destination = intersection4 };
            Car car4 = new Car { ID = "Car4", Speed = 10, MaxSpeed = (int)MaxSpeedSlider.Value, CurrentRoad = road4, Position = 0, Destination = intersection1 };

            city.AddVehicle(car1);
            city.AddVehicle(car2);
            city.AddVehicle(car3);
            city.AddVehicle(car4);

            TrafficLight light1 = new TrafficLight { ID = "Light1", ControlledRoad = road1, State = TrafficLightState.Red, Timer = (int)TrafficLightSlider.Value };
            TrafficLight light2 = new TrafficLight { ID = "Light2", ControlledRoad = road2, State = TrafficLightState.Green, Timer = (int)TrafficLightSlider.Value };
            TrafficLight light3 = new TrafficLight { ID = "Light3", ControlledRoad = road3, State = TrafficLightState.Red, Timer = (int)TrafficLightSlider.Value };
            TrafficLight light4 = new TrafficLight { ID = "Light4", ControlledRoad = road4, State = TrafficLightState.Green, Timer = (int)TrafficLightSlider.Value };

            intersection1.AddTrafficLight(light1);
            intersection2.AddTrafficLight(light2);
            intersection3.AddTrafficLight(light3);
            intersection4.AddTrafficLight(light4);

            city.AddTrafficLight(light1);
            city.AddTrafficLight(light2);
            city.AddTrafficLight(light3);
            city.AddTrafficLight(light4);
        }

        private void InitializeTimer()
        {
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(100);
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void InitializeSliders()
        {
            TrafficLightSlider.ValueChanged += TrafficLightSlider_ValueChanged;
            MaxSpeedSlider.ValueChanged += MaxSpeedSlider_ValueChanged;
        }

        private void TrafficLightSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (city != null)
            {
                foreach (var light in city.TrafficLights)
                {
                    light.Timer = (int)TrafficLightSlider.Value;
                }
            }
        }

        private void MaxSpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (city != null)
            {
                foreach (var vehicle in city.Vehicles)
                {
                    vehicle.MaxSpeed = (int)MaxSpeedSlider.Value;
                }
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            city.Update();
            DrawCity();
        }

        private void DrawCity()
        {
            CityCanvas.Children.Clear();

            foreach (var road in city.Roads)
            {
                DrawRoad(road);
            }

            foreach (var vehicle in city.Vehicles)
            {
                DrawVehicle(vehicle);
            }

            foreach (var light in city.TrafficLights)
            {
                DrawTrafficLight(light);
            }
        }

        private void DrawRoad(Road road)
        {
            double extension = road.Width / 2;

            double startX = road.StartIntersection.X;
            double startY = road.StartIntersection.Y;
            double endX = road.EndIntersection.X;
            double endY = road.EndIntersection.Y;

            double roadAngle = Math.Atan2(endY - startY, endX - startX);

            // Extend road endpoints slightly beyond intersections
            startX -= extension * Math.Cos(roadAngle);
            startY -= extension * Math.Sin(roadAngle);
            endX += extension * Math.Cos(roadAngle);
            endY += extension * Math.Sin(roadAngle);

            Line line = new Line
            {
                X1 = startX,
                Y1 = startY,
                X2 = endX,
                Y2 = endY,
                Stroke = Brushes.Black,
                StrokeThickness = road.Width
            };

            CityCanvas.Children.Add(line);
        }

        private void DrawVehicle(Vehicle vehicle)
        {
            double[] position = vehicle.CurrentRoad.GetPositionOnRoad(vehicle);
            double roadAngle = Math.Atan2(vehicle.CurrentRoad.EndIntersection.Y - vehicle.CurrentRoad.StartIntersection.Y,
                                          vehicle.CurrentRoad.EndIntersection.X - vehicle.CurrentRoad.StartIntersection.X);

            Rectangle rect = new Rectangle
            {
                Width = 20,
                Height = 10,
                Fill = Brushes.Blue
            };

            double angle = roadAngle * 180 / Math.PI;
            rect.RenderTransform = new RotateTransform(angle, rect.Width / 2, rect.Height / 2);

            double offsetX = position[0] - (Math.Sin(roadAngle) * vehicle.CurrentRoad.Width / 4);
            double offsetY = position[1] + (Math.Cos(roadAngle) * vehicle.CurrentRoad.Width / 4);

            Canvas.SetLeft(rect, offsetX - rect.Width / 2);
            Canvas.SetTop(rect, offsetY - rect.Height / 2);
            CityCanvas.Children.Add(rect);
        }

        private void DrawTrafficLight(TrafficLight light)
        {
            double[] position = light.ControlledRoad.GetPositionOnRoad(new Vehicle { Position = light.ControlledRoad.Length - 10 });

            Ellipse ellipse = new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = light.State == TrafficLightState.Red ? Brushes.Red :
                       light.State == TrafficLightState.Yellow ? Brushes.Yellow : Brushes.Green
            };

            Canvas.SetLeft(ellipse, position[0] - ellipse.Width / 2);
            Canvas.SetTop(ellipse, position[1] - ellipse.Height / 2);
            CityCanvas.Children.Add(ellipse);
        }
    }
}
