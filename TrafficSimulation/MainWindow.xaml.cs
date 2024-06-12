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
            Intersection intersection1 = new() { Name = "Intersection1", X = 100, Y = 100 };
            Intersection intersection2 = new() { Name = "Intersection2", X = 300, Y = 100 };
            Intersection intersection3 = new() { Name = "Intersection3", X = 300, Y = 300 };
            Intersection intersection4 = new() { Name = "Intersection4", X = 600, Y = 100 };

            Road road1 = new() { Name = "Road1", Type = RoadType.TwoWay, NumOfLanesPerDirection = 1, StartIntersection = intersection1, EndIntersection = intersection2 };
            Road road2 = new() { Name = "Road2", Type = RoadType.TwoWay, NumOfLanesPerDirection = 1, StartIntersection = intersection2, EndIntersection = intersection3 };
            Road road3 = new() { Name = "Road3", Type = RoadType.TwoWay, NumOfLanesPerDirection = 2, StartIntersection = intersection2, EndIntersection = intersection4 };
            Road road4 = new() { Name = "Road4", Type = RoadType.OneWay, NumOfLanesPerDirection = 1, StartIntersection = intersection3, EndIntersection = intersection4 };
            Road road5 = new() { Name = "Road5", Type = RoadType.OneWay, NumOfLanesPerDirection = 2, StartIntersection = intersection1, EndIntersection = intersection3 };

            city.AddRoad(road1);
            city.AddRoad(road2);
            city.AddRoad(road3);
            city.AddRoad(road4);
            city.AddRoad(road5);

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

            foreach (var road in city.Roads)
            {
                DrawIntersection(road.StartIntersection);
                DrawIntersection(road.EndIntersection);
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

            foreach (var road in intersection.IncomingRoads)
            {
                if (road.EndIntersection.GetWidth() == maxRoadWidth)
                {
                    double roadAngle = Math.Atan2(road.EndIntersection.Y - road.StartIntersection.Y,
                                          road.EndIntersection.X - road.StartIntersection.X);

                    Rectangle rect = new()
                    {
                        Width = maxRoadWidth / Constants.MetresPerPixel,
                        Height = maxRoadWidth / Constants.MetresPerPixel,
                        Fill = Brushes.Gray
                    };

                    double angle = roadAngle * 180 / Math.PI;
                    rect.RenderTransform = new RotateTransform(angle, rect.Width / 2, rect.Height / 2);

                    Canvas.SetLeft(rect, intersection.X - maxRoadWidth / (2 * Constants.MetresPerPixel));
                    Canvas.SetTop(rect, intersection.Y - maxRoadWidth / (2 * Constants.MetresPerPixel));
                    CityCanvas.Children.Add(rect);
                }
            }

            foreach (var road in intersection.OutgoingRoads)
            {
                if (road.StartIntersection.GetWidth() == maxRoadWidth)
                {
                    double roadAngle = Math.Atan2(road.EndIntersection.Y - road.StartIntersection.Y,
                                          road.EndIntersection.X - road.StartIntersection.X);

                    Rectangle rect = new()
                    {
                        Width = maxRoadWidth / Constants.MetresPerPixel,
                        Height = maxRoadWidth / Constants.MetresPerPixel,
                        Fill = Brushes.Gray
                    };

                    double angle = roadAngle * 180 / Math.PI;
                    rect.RenderTransform = new RotateTransform(angle, rect.Width / 2, rect.Height / 2);

                    Canvas.SetLeft(rect, intersection.X - maxRoadWidth / (2 * Constants.MetresPerPixel));
                    Canvas.SetTop(rect, intersection.Y - maxRoadWidth / (2 * Constants.MetresPerPixel));
                    CityCanvas.Children.Add(rect);
                }
            }
        }

        private void DrawRoad(Road road)
        {
            double startIntersectionWidth = road.StartIntersection.GetWidth();
            double endIntersectionWidth = road.EndIntersection.GetWidth();

            double startX = road.StartIntersection.X;
            double startY = road.StartIntersection.Y;
            double endX = road.EndIntersection.X;
            double endY = road.EndIntersection.Y;

            double roadAngle = Math.Atan2(endY - startY, endX - startX);

            startX = road.StartIntersection.X + startIntersectionWidth / (2 * Constants.MetresPerPixel) * Math.Cos(roadAngle);
            startY = road.StartIntersection.Y + startIntersectionWidth / (2 * Constants.MetresPerPixel) * Math.Sin(roadAngle);
            endX = road.EndIntersection.X - endIntersectionWidth / (2 * Constants.MetresPerPixel) * Math.Cos(roadAngle);
            endY = road.EndIntersection.Y - endIntersectionWidth / (2 * Constants.MetresPerPixel) * Math.Sin(roadAngle);

            Line line = new()
            {
                X1 = startX,
                Y1 = startY,
                X2 = endX,
                Y2 = endY,
                Stroke = Brushes.Black,
                StrokeThickness = road.Type != RoadType.OneWay ? Constants.RoadWidthPixels * road.NumOfLanesPerDirection : Constants.RoadWidthPixels / 2 * road.NumOfLanesPerDirection,
            };

            CityCanvas.Children.Add(line);

            if (road.Type == RoadType.OneWay)
            {
                //DrawOneWayArrow(road);
            }
        }

        private void DrawOneWayArrow(Road road)
        {
            double arrowLength = 20 / Constants.MetresPerPixel;

            double startX = road.StartIntersection.X;
            double startY = road.StartIntersection.Y;
            double endX = road.EndIntersection.X;
            double endY = road.EndIntersection.Y;

            double roadAngle = Math.Atan2(endY - startY, endX - startX);

            // Determine the center of the road for the arrow
            double midX = (startX + endX) / 2;
            double midY = (startY + endY) / 2;

            // Calculate arrow points
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
            double roadAngle = Math.Atan2(vehicle.CurrentRoad.EndIntersection.Y - vehicle.CurrentRoad.StartIntersection.Y,
                                          vehicle.CurrentRoad.EndIntersection.X - vehicle.CurrentRoad.StartIntersection.X);

            Rectangle rect = new()
            {
                Width = Constants.VehicleLengthPixels,
                Height = Constants.VehicleWidthPixels,
                Fill = Brushes.Blue
            };

            // Calculate the angle for vehicle direction
            double angle = (vehicle.IsMovingForward ? roadAngle : roadAngle + Math.PI) * 180 / Math.PI;
            rect.RenderTransform = new RotateTransform(angle, rect.Width / 2, rect.Height / 2);

            // Check if the road has one lane
            if (vehicle.CurrentRoad.NumOfLanesPerDirection == 1 && vehicle.CurrentRoad.Type == RoadType.OneWay)
            {
                // Calculate the offset from the center
                double offset = Constants.RoadWidthPixels / 4; // Ширина дороги разделена на 4 для центрирования
                double offsetX = position[0] - offset * Math.Cos(roadAngle);
                double offsetY = position[1] - offset * Math.Sin(roadAngle);

                // Adjust position for forward or backward movement
                if (!vehicle.IsMovingForward)
                {
                    offsetX += offset * Math.Cos(roadAngle);
                    offsetY += offset * Math.Sin(roadAngle);
                }

                Canvas.SetLeft(rect, offsetX - rect.Width / 2);
                Canvas.SetTop(rect, offsetY - rect.Height / 2);
            }
            else if (vehicle.CurrentRoad.Type == RoadType.OneWay)
            {
                //Adjust the offset for the direction of movement and lane position (right-hand traffic)
                double laneOffset = Constants.RoadWidthPixels * (vehicle.LineNumber - 1);

                double offsetX = position[0] + Math.Sin(roadAngle) * laneOffset;
                double offsetY = position[1] - Math.Cos(roadAngle) * laneOffset;

                // Adjust position for forward or backward movement
                if (vehicle.IsMovingForward)
                {
                    offsetX -= Math.Sin(roadAngle) * rect.Width;
                    offsetY += Math.Cos(roadAngle) * rect.Width;
                }
                else
                {
                    offsetX += Math.Sin(roadAngle) * rect.Width;
                    offsetY -= Math.Cos(roadAngle) * rect.Width;
                }

                Canvas.SetLeft(rect, offsetX - rect.Width);
                Canvas.SetTop(rect, offsetY - rect.Height / 2); 
            }
            else
            {
                //Adjust the offset for the direction of movement and lane position (right-hand traffic)
                double laneWidth = Constants.RoadWidthPixels * vehicle.CurrentRoad.NumOfLanesPerDirection;
                double laneOffset = laneWidth * (vehicle.LineNumber - 1);
                if (vehicle.IsMovingForward)
                {
                    laneOffset *= -1;
                }

                double offsetX = position[0] + Math.Sin(roadAngle) * laneOffset;
                double offsetY = position[1] - Math.Cos(roadAngle) * laneOffset;

                // Adjust position for forward or backward movement
                if (vehicle.IsMovingForward)
                {
                    offsetX -= Math.Sin(roadAngle) * rect.Width;
                    offsetY += Math.Cos(roadAngle) * rect.Width / 2;
                }
                else
                {
                    offsetX += Math.Sin(roadAngle) * rect.Width;
                    offsetY -= Math.Cos(roadAngle) * rect.Width;
                }

                Canvas.SetLeft(rect, offsetX - rect.Width);
                Canvas.SetTop(rect, offsetY - rect.Height);
            }

            CityCanvas.Children.Add(rect);
        }



        private void DrawTrafficLight(TrafficLight light)
        {
            double[] position;
            double roadAngle;
            if (light.ControlledIntersection == light.ControlledRoad.StartIntersection)
            {
                position = light.ControlledRoad.GetPositionOnRoad(new Vehicle { Position = 0 });
                roadAngle = Math.Atan2(light.ControlledRoad.EndIntersection.Y - light.ControlledRoad.StartIntersection.Y,
                                       light.ControlledRoad.EndIntersection.X - light.ControlledRoad.StartIntersection.X);
            }
            else
            {
                position = light.ControlledRoad.GetPositionOnRoad(new Vehicle { Position = light.ControlledRoad.GetLength() });
                roadAngle = Math.Atan2(light.ControlledRoad.StartIntersection.Y - light.ControlledRoad.EndIntersection.Y,
                                       light.ControlledRoad.StartIntersection.X - light.ControlledRoad.EndIntersection.X);
            }

            // Смещение светофора вдоль дороги
            double offsetX = position[0] + Math.Cos(roadAngle) * Constants.RoadWidthPixels / 4;
            double offsetY = position[1] + Math.Sin(roadAngle) * Constants.RoadWidthPixels / 4;

            Ellipse ellipse = new Ellipse
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
