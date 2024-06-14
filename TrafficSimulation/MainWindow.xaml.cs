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
        private City city = new();
        private DispatcherTimer timer = new();
        private Random random = new();

        public MainWindow()
        {
            InitializeComponent();
            InitializeCity();
            InitializeTimer();
            InitializeSliders();

            DrawCity();
        }

        private void InitializeCity()
        {
            Intersection intersection1 = new() { Name = "Intersection1", X = 10, Y = 10 };
            Intersection intersection2 = new() { Name = "Intersection2", X = 60, Y = 10 };
            Intersection intersection3 = new() { Name = "Intersection3", X = 60, Y = 50 };
            Intersection intersection4 = new() { Name = "Intersection4", X = 110, Y = 10 };
            Intersection intersection5 = new() { Name = "Intersection5", X = 10, Y = 40 };

            Road road1 = new() { Name = "Road1", Type = RoadType.TwoWay, NumOfLanesPerDirection = 1, StartIntersection = intersection1, EndIntersection = intersection2 };
            Road road2 = new() { Name = "Road2", Type = RoadType.TwoWay, NumOfLanesPerDirection = 1, StartIntersection = intersection2, EndIntersection = intersection3 };
            Road road3 = new() { Name = "Road3", Type = RoadType.TwoWay, NumOfLanesPerDirection = 2, StartIntersection = intersection2, EndIntersection = intersection4 };
            Road road4 = new() { Name = "Road4", Type = RoadType.OneWay, NumOfLanesPerDirection = 1, StartIntersection = intersection3, EndIntersection = intersection4 };
            Road road5 = new() { Name = "Road5", Type = RoadType.OneWay, NumOfLanesPerDirection = 2, StartIntersection = intersection1, EndIntersection = intersection3 };
            Road road6 = new() { Name = "Road6", Type = RoadType.OneWay, NumOfLanesPerDirection = 3, StartIntersection = intersection1, EndIntersection = intersection5 };
            Road road7 = new() { Name = "Road7", Type = RoadType.OneWay, NumOfLanesPerDirection = 2, StartIntersection = intersection5, EndIntersection = intersection3 };

            city.AddRoad(road1);
            city.AddRoad(road2);
            city.AddRoad(road3);
            city.AddRoad(road4);
            city.AddRoad(road5);
            city.AddRoad(road6);
            city.AddRoad(road7);

            TrafficLight light1 = new() { ID = "Light1", ControlledRoad = road1, ControlledIntersection = intersection1, State = TrafficLightState.Green, Timer = (int)TrafficLightSlider.Value };
            TrafficLight light2 = new() { ID = "Light2", ControlledRoad = road1, ControlledIntersection = intersection2, State = TrafficLightState.Red, Timer = (int)TrafficLightSlider.Value };
            TrafficLight light3 = new() { ID = "Light3", ControlledRoad = road2, ControlledIntersection = intersection2, State = TrafficLightState.Red, Timer = (int)TrafficLightSlider.Value };
            TrafficLight light4 = new() { ID = "Light4", ControlledRoad = road3, ControlledIntersection = intersection2, State = TrafficLightState.Red, Timer = (int)TrafficLightSlider.Value };
            TrafficLight light5 = new() { ID = "Light5", ControlledRoad = road5, ControlledIntersection = intersection3, State = TrafficLightState.Red, Timer = (int)TrafficLightSlider.Value };

            city.AddTrafficLight(light1);
            city.AddTrafficLight(light2);
            city.AddTrafficLight(light3);
            city.AddTrafficLight(light4);
            city.AddTrafficLight(light5);
        }

        private void InitializeTimer()
        {
            timer.Interval = TimeSpan.FromMilliseconds(100);
            timer.Tick += Timer_Tick;
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
                    double maxSpeedInMps = MaxSpeedSlider.Value * 1000 / 3600; // переводим км/ч в м/с
                    vehicle.MaxSpeed = maxSpeedInMps;
                }
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            city.Update();
            DrawCity();
            DrawMetresLine();
        }

        private void DrawCity()
        {
            CityCanvas.Children.Clear();

            foreach (var intersection in city.Intersections)
            {
                DrawIntersection(intersection);
            }

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

        private void DrawIntersection(Intersection intersection)
        {
            double maxRoadWidth = intersection.GetWidth();

            Rectangle rect = new()
            {
                Width = maxRoadWidth / Constants.MetresPerPixel,
                Height = maxRoadWidth / Constants.MetresPerPixel,
                Fill = Brushes.Gray
            };

            Canvas.SetLeft(rect, intersection.ActualX - maxRoadWidth / (2 * Constants.MetresPerPixel));
            Canvas.SetTop(rect, intersection.ActualY - maxRoadWidth / (2 * Constants.MetresPerPixel));
            CityCanvas.Children.Add(rect);
        }

        private void DrawRoad(Road road)
        {
            double startIntersectionWidth = road.StartIntersection.GetWidth();
            double endIntersectionWidth = road.EndIntersection.GetWidth();

            double startX = road.StartIntersection.ActualX;
            double startY = road.StartIntersection.ActualY;
            double endX = road.EndIntersection.ActualX;
            double endY = road.EndIntersection.ActualY;

            double roadAngle = Math.Atan2(endY - startY, endX - startX);

            startX = road.StartIntersection.ActualX + startIntersectionWidth / (2 * Constants.MetresPerPixel) * Math.Cos(roadAngle);
            startY = road.StartIntersection.ActualY + startIntersectionWidth / (2 * Constants.MetresPerPixel) * Math.Sin(roadAngle);
            endX = road.EndIntersection.ActualX - endIntersectionWidth / (2 * Constants.MetresPerPixel) * Math.Cos(roadAngle);
            endY = road.EndIntersection.ActualY - endIntersectionWidth / (2 * Constants.MetresPerPixel) * Math.Sin(roadAngle);

            Line line = new()
            {
                X1 = startX,
                Y1 = startY,
                X2 = endX,
                Y2 = endY,
                Stroke = Brushes.Black,
                StrokeThickness = road.GetWidth() / Constants.MetresPerPixel,
            };

            CityCanvas.Children.Add(line);

            if (road.Type == RoadType.OneWay)
            {
                DrawOneWayArrow(road);
            }
        }

        private void DrawOneWayArrow(Road road)
        {
            double arrowLength = (road.GetWidth() - 0.5) / Constants.MetresPerPixel;

            double startX = road.StartIntersection.ActualX;
            double startY = road.StartIntersection.ActualY;
            double endX = road.EndIntersection.ActualX;
            double endY = road.EndIntersection.ActualY;

            double roadAngle = Math.Atan2(endY - startY, endX - startX);

            double midX = (startX + endX) / 2;
            double midY = (startY + endY) / 2;

            Point p1 = new(midX, midY);
            Point p2 = new(midX - arrowLength * Math.Cos(roadAngle - Math.PI / 6), midY - arrowLength * Math.Sin(roadAngle - Math.PI / 6));
            Point p3 = new(midX - arrowLength * Math.Cos(roadAngle + Math.PI / 6), midY - arrowLength * Math.Sin(roadAngle + Math.PI / 6));

            Polygon arrow = new()
            {
                Points = { p1, p2, p3 },
                Fill = Brushes.White
            };

            CityCanvas.Children.Add(arrow);
        }

        private void DrawVehicle(Vehicle vehicle)
        {
            double[] position = vehicle.CurrentRoad.GetPositionOnRoad(vehicle);
            position[0] /= Constants.MetresPerPixel;
            position[1] /= Constants.MetresPerPixel;
            double roadAngle = Math.Atan2(vehicle.CurrentRoad.EndIntersection.ActualY - vehicle.CurrentRoad.StartIntersection.ActualY,
                                          vehicle.CurrentRoad.EndIntersection.ActualX - vehicle.CurrentRoad.StartIntersection.ActualX);

            Rectangle rect = new()
            {
                Width = Constants.VehicleLengthPixels,
                Height = Constants.VehicleWidthPixels,
                Fill = Brushes.Blue
            };

            double angle = (vehicle.IsMovingForward ? roadAngle : roadAngle + Math.PI) * 180 / Math.PI;
            rect.RenderTransform = new RotateTransform(angle, rect.Width / 2, rect.Height / 2);

            if (vehicle.CurrentRoad.Type == RoadType.OneWay)
            {
                if (vehicle.CurrentRoad.NumOfLanesPerDirection % 2 == 0)
                {
                    double offset; 
                    if (vehicle.LineNumber <= vehicle.CurrentRoad.NumOfLanesPerDirection / 2)
                    {
                        offset = rect.Height / 2 + Constants.VehicleWidthPixels * Math.Abs(vehicle.CurrentRoad.NumOfLanesPerDirection / 2 - vehicle.LineNumber);
                    }
                    else
                    {
                        offset = rect.Height / 2 + Constants.VehicleWidthPixels * Math.Abs(vehicle.CurrentRoad.NumOfLanesPerDirection / 2 - vehicle.LineNumber + 1);
                        offset = -offset;
                    }

                    double offsetX = position[0] + Math.Sin(roadAngle) * offset;
                    double offsetY = position[1] - Math.Cos(roadAngle) * offset;

                    Canvas.SetLeft(rect, offsetX - rect.Width / 2);
                    Canvas.SetTop(rect, offsetY - rect.Height / 2);
                }
                else
                {
                    double offset = Constants.VehicleWidthPixels * Math.Abs(vehicle.CurrentRoad.NumOfLanesPerDirection / 2 - vehicle.LineNumber + 1);
                    if (vehicle.LineNumber > vehicle.CurrentRoad.NumOfLanesPerDirection / 2)
                    {
                        offset = -offset;
                    }

                    double offsetX = position[0] + Math.Sin(roadAngle) * offset;
                    double offsetY = position[1] - Math.Cos(roadAngle) * offset;

                    Canvas.SetLeft(rect, offsetX - rect.Width / 2);
                    Canvas.SetTop(rect, offsetY - rect.Height / 2);
                }
            }
            else
            {
                double laneOffset = rect.Height / 2 + Constants.VehicleWidthPixels * (vehicle.LineNumber - 1);

                if (vehicle.IsMovingForward)
                {
                    laneOffset = -laneOffset;
                }

                double offsetX = position[0] + Math.Sin(roadAngle) * laneOffset;
                double offsetY = position[1] - Math.Cos(roadAngle) * laneOffset;

                Canvas.SetLeft(rect, offsetX - rect.Width / 2);
                Canvas.SetTop(rect, offsetY - rect.Height / 2);
            }

            CityCanvas.Children.Add(rect);
        }



        private void DrawTrafficLight(TrafficLight light)
        {
            double[] position = [light.ControlledIntersection.ActualX, light.ControlledIntersection.ActualY];
            double roadAngle;
            if (light.ControlledIntersection == light.ControlledRoad.StartIntersection)
            {
                roadAngle = Math.Atan2(light.ControlledRoad.EndIntersection.ActualY - light.ControlledRoad.StartIntersection.ActualY,
                                       light.ControlledRoad.EndIntersection.ActualX - light.ControlledRoad.StartIntersection.ActualX);
            }
            else
            {
                roadAngle = Math.Atan2(light.ControlledRoad.StartIntersection.ActualY - light.ControlledRoad.EndIntersection.ActualY,
                                       light.ControlledRoad.StartIntersection.ActualX - light.ControlledRoad.EndIntersection.ActualX);
            }

            // Смещение светофора вдоль дороги
            double offsetX = position[0] + Math.Cos(roadAngle) * Constants.RoadWidthPixels / 4;
            double offsetY = position[1] + Math.Sin(roadAngle) * Constants.RoadWidthPixels / 4;

            Ellipse ellipse = new()
            {
                Width = Constants.TrafficLightPixels,
                Height = Constants.TrafficLightPixels,
                Fill = light.State == TrafficLightState.Red ? Brushes.Red :
                       light.State == TrafficLightState.Yellow ? Brushes.Yellow : Brushes.Green
            };

            Canvas.SetLeft(ellipse, offsetX - ellipse.Width / 2);
            Canvas.SetTop(ellipse, offsetY - ellipse.Height / 2);
            CityCanvas.Children.Add(ellipse);
        }

        private void DrawMetresLine()
        {
            Line line = new()
            {
                X1 = CityCanvas.ActualWidth - 10,
                Y1 = CityCanvas.ActualHeight - 10,
                X2 = CityCanvas.ActualWidth - 100,
                Y2 = CityCanvas.ActualHeight - 10,
                Stroke = Brushes.Black,
                StrokeThickness = 1
            };

            CityCanvas.Children.Add(line);

            var text = 100 * Constants.MetresPerPixel + " метров";
            TextBlock textBlock = new()
            {
                Text = text,
                Foreground = Brushes.Black,
                FontSize = 10,
                Margin = new Thickness(CityCanvas.ActualWidth - 80, CityCanvas.ActualHeight - 25, 0, 0)
            };

            CityCanvas.Children.Add(textBlock);
        }


        private void AddVehicleButton_Click(object sender, RoutedEventArgs e)
        {
            if (city.Roads.Count == 0) return;

            // Выбираем случайную дорогу
            var road = city.Roads[random.Next(city.Roads.Count)];

            // Определяем направление движения
            bool isMovingForward;
            if (road.Type == RoadType.OneWay)
            {
                isMovingForward = true;
            }
            else
            {
                isMovingForward = random.Next(2) == 0;
            }

            int lineNumber = random.Next(1, road.NumOfLanesPerDirection + 1);

            var vehicle = new Vehicle
            {
                ID = "Car" + (city.Vehicles.Count + 1),
                Speed = 40,
                MaxSpeed = MaxSpeedSlider.Value * 1000 / 3600, // Переводим км/ч в м/с
                CurrentRoad = road,
                LineNumber = lineNumber,
                Position = isMovingForward ? 0 : road.GetLength(),
                NextIntersection = isMovingForward ? road.EndIntersection : road.StartIntersection,
                IsMovingForward = isMovingForward
            };

            city.AddVehicle(vehicle);
        }

        private void StartSimulationButton_Click(object sender, RoutedEventArgs e)
        {
            StartSimulation();
        }

        private void StopSimulationButton_Click(object sender, RoutedEventArgs e)
        {
            StopSimulation();
        }

        private void StartSimulation()
        {
            // Запустить таймер
            timer.Start();

            // Изменить видимость кнопок
            StartSimulationButton.Visibility = Visibility.Collapsed;
            StopSimulationButton.Visibility = Visibility.Visible;
        }

        private void StopSimulation()
        {
            // Остановить таймер
            timer.Stop();

            // Изменить видимость кнопок
            StartSimulationButton.Visibility = Visibility.Visible;
            StopSimulationButton.Visibility = Visibility.Collapsed;
        }

        private void CityCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            DrawCity();
            DrawMetresLine();
        }
    }
}
