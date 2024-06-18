using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using TrafficSimulation.Models;
using System.Windows.Threading;
using System.Windows.Input;

namespace TrafficSimulation
{
    public partial class MainWindow : Window
    {
        private City city = new();
        private DispatcherTimer timer = new();
        private Random random = new();

        private bool isDragging = false;
        private Point clickPosition;

        private double[] globalOffset = [0, 0];

        public MainWindow()
        {
            InitializeComponent();
            InitializeCity();
            InitializeTimer();
            InitializeSliders();

            DrawCity();

            RectangleGeometry clipRect = new(new Rect(0, 0, CityCanvas.ActualWidth, CityCanvas.ActualHeight));
            CityCanvas.Clip = clipRect;

            CityCanvas.MouseDown += CityCanvas_MouseDown;
            CityCanvas.MouseMove += CityCanvas_MouseMove;
            CityCanvas.MouseUp += CityCanvas_MouseUp;
        }

        private void InitializeCity()
        {
            city.MaxAllowedSpeed = MaxSpeedSlider.Value;

            Intersection intersection1 = new() { Name = "Intersection1", X = 10, Y = 10 };
            Intersection intersection2 = new() { Name = "Intersection2", X = 110, Y = 10 };
            Intersection intersection3 = new() { Name = "Intersection3", X = 110, Y = 110 };
            Intersection intersection4 = new() { Name = "Intersection4", X = 210, Y = 10 };
            Intersection intersection5 = new() { Name = "Intersection5", X = 10, Y = 110 };

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

            TrafficLight light1 = new() { ID = "Light1", ControlledRoad = road1, ControlledIntersection = intersection1, State = TrafficLightState.Green, RedTimer = 10, GreenTimer = 15 };
            TrafficLight light2 = new() { ID = "Light2", ControlledRoad = road1, ControlledIntersection = intersection2, State = TrafficLightState.Red, RedTimer = 10, GreenTimer = 15 };
            TrafficLight light3 = new() { ID = "Light3", ControlledRoad = road2, ControlledIntersection = intersection2, State = TrafficLightState.Green, RedTimer = 10, GreenTimer = 15 };
            TrafficLight light4 = new() { ID = "Light4", ControlledRoad = road3, ControlledIntersection = intersection2, State = TrafficLightState.Red, RedTimer = 10, GreenTimer = 15 };
            TrafficLight light5 = new() { ID = "Light5", ControlledRoad = road5, ControlledIntersection = intersection3, State = TrafficLightState.Red, RedTimer = 10, GreenTimer = 15 };

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
            MaxSpeedSlider.ValueChanged += MaxSpeedSlider_ValueChanged;
        }

        private void MaxSpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (city != null)
            {
                foreach (var road in city.Roads)
                {
                    double maxSpeedInMps = MaxSpeedSlider.Value; //переводим км/ч в м/с
                    road.MaxAllowedSpeed = maxSpeedInMps;
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
                Fill = Brushes.Black
            };

            Canvas.SetLeft(rect, intersection.ActualX - maxRoadWidth / (2 * Constants.MetresPerPixel) + globalOffset[0]);
            Canvas.SetTop(rect, intersection.ActualY - maxRoadWidth / (2 * Constants.MetresPerPixel) + globalOffset[1]);
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
                X1 = startX + globalOffset[0],
                Y1 = startY + globalOffset[1],
                X2 = endX + globalOffset[0],
                Y2 = endY + globalOffset[1],
                Stroke = Brushes.Black,
                StrokeThickness = road.Width / Constants.MetresPerPixel,
            };

            CityCanvas.Children.Add(line);

            if (road.Type == RoadType.OneWay)
            {
                DrawOneWayArrow(road);
            }
        }

        private void DrawOneWayArrow(Road road)
        {
            double arrowLength = (road.Width - 0.5) / Constants.MetresPerPixel;

            double startX = road.StartIntersection.ActualX;
            double startY = road.StartIntersection.ActualY;
            double endX = road.EndIntersection.ActualX;
            double endY = road.EndIntersection.ActualY;

            double roadAngle = Math.Atan2(endY - startY, endX - startX);

            double midX = (startX + endX) / 2 + globalOffset[0];
            double midY = (startY + endY) / 2 + globalOffset[1];

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

            double offset, offsetX, offsetY;

            if (vehicle.CurrentRoad.Type == RoadType.OneWay)
            {
                if (vehicle.CurrentRoad.NumOfLanesPerDirection % 2 == 0)
                {
                    if (vehicle.LineNumber <= vehicle.CurrentRoad.NumOfLanesPerDirection / 2)
                    {
                        offset = rect.Height / 2 + Constants.VehicleWidthPixels * Math.Abs(vehicle.CurrentRoad.NumOfLanesPerDirection / 2 - vehicle.LineNumber);
                    }
                    else
                    {
                        offset = rect.Height / 2 + Constants.VehicleWidthPixels * Math.Abs(vehicle.CurrentRoad.NumOfLanesPerDirection / 2 - vehicle.LineNumber + 1);
                        offset = -offset;
                    }

                    offsetX = position[0] + Math.Sin(roadAngle) * offset;
                    offsetY = position[1] - Math.Cos(roadAngle) * offset;

                    Canvas.SetLeft(rect, offsetX - rect.Width / 2);
                    Canvas.SetTop(rect, offsetY - rect.Height / 2);
                }
                else
                {
                    offset = Constants.VehicleWidthPixels * Math.Abs(vehicle.CurrentRoad.NumOfLanesPerDirection / 2 - vehicle.LineNumber + 1);
                    if (vehicle.LineNumber > vehicle.CurrentRoad.NumOfLanesPerDirection / 2)
                    {
                        offset = -offset;
                    }

                    offsetX = position[0] + Math.Sin(roadAngle) * offset;
                    offsetY = position[1] - Math.Cos(roadAngle) * offset;
                }
            }
            else
            {
                offset = rect.Height / 2 + Constants.VehicleWidthPixels * (vehicle.LineNumber - 1);

                if (vehicle.IsMovingForward)
                {
                    offset = -offset;
                }

                offsetX = position[0] + Math.Sin(roadAngle) * offset;
                offsetY = position[1] - Math.Cos(roadAngle) * offset;
            }

            Canvas.SetLeft(rect, offsetX - rect.Width / 2 + globalOffset[0]);
            Canvas.SetTop(rect, offsetY - rect.Height / 2 + globalOffset[1]);
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
            double offsetX = position[0] + Math.Cos(roadAngle) * Constants.RoadWidthPixels / 2;
            double offsetY = position[1] + Math.Sin(roadAngle) * Constants.RoadWidthPixels / 2;

            Ellipse ellipse = new()
            {
                Width = Constants.TrafficLightPixels,
                Height = Constants.TrafficLightPixels,
                Fill = light.State == TrafficLightState.Red ? Brushes.Red :
                       light.State == TrafficLightState.Yellow ? Brushes.Yellow : Brushes.Green
            };

            Canvas.SetLeft(ellipse, offsetX - ellipse.Width / 2 + globalOffset[0]);
            Canvas.SetTop(ellipse, offsetY - ellipse.Height / 2 + globalOffset[1]);
            CityCanvas.Children.Add(ellipse);
        }

        private void DrawMetresLine()
        {
            Line line = new()
            {
                Name = "MetresLine",
                X1 = CityCanvas.ActualWidth - 10,
                Y1 = CityCanvas.ActualHeight - 10,
                X2 = CityCanvas.ActualWidth - 100,
                Y2 = CityCanvas.ActualHeight - 10,
                Stroke = Brushes.Black,
                StrokeThickness = 1,
            };

            CityCanvas.Children.Add(line);

            var text = Math.Round(100 * Constants.MetresPerPixel, 2) + " метров";
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
                Speed = MaxSpeedSlider.Value,
                CurrentRoad = road,
                LineNumber = lineNumber,
                Position = isMovingForward ? 0 : road.Length,
                NextIntersection = isMovingForward ? road.EndIntersection : road.StartIntersection,
                IsMovingForward = isMovingForward,
            };

            vehicle.NextRoad = vehicle.GetNextRoad();

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
            RectangleGeometry clipRect = new(new Rect(0, 0, CityCanvas.ActualWidth, CityCanvas.ActualHeight));
            CityCanvas.Clip = clipRect;

            DrawCity();
            DrawMetresLine();
        }

        private void ZoomInButton_Click(object sender, RoutedEventArgs e)
        {
            if (Constants.MetresPerPixel <= 0.1) return;

            Constants.MetresPerPixel = Math.Max(Constants.MetresPerPixel - 0.05, 0.1);
            DrawCity();
            DrawMetresLine();
        }

        private void ZoomOutButton_Click(object sender, RoutedEventArgs e)
        {
            Constants.MetresPerPixel = Math.Min(Constants.MetresPerPixel + 0.05, 1);
            DrawCity();
            DrawMetresLine();
        }

        private void CityCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
            {
                ZoomInButton_Click(sender, e);
            }
            else
            {
                ZoomOutButton_Click(sender, e);
            }
        }

        private void CityCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                isDragging = true;
                clickPosition = e.GetPosition(CityCanvas);
                CityCanvas.CaptureMouse();
            }
        }

        private void CityCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging && e.LeftButton == MouseButtonState.Pressed)
            {
                Point currentPosition = e.GetPosition(CityCanvas);
                double offsetX = currentPosition.X - clickPosition.X;
                double offsetY = currentPosition.Y - clickPosition.Y;

                globalOffset[0] += offsetX;
                globalOffset[1] += offsetY;

                foreach (UIElement element in CityCanvas.Children)
                {
                    double newLeft = Canvas.GetLeft(element) + offsetX;
                    double newTop = Canvas.GetTop(element) + offsetY;

                    if (!double.IsNaN(newLeft) && !double.IsNaN(newTop))
                    {
                        Canvas.SetLeft(element, newLeft);
                        Canvas.SetTop(element, newTop);
                    }
                    else
                    {
                        if (element is Line line)
                        {
                            if (line.Name != "MetresLine")
                            {
                                line.X1 += offsetX;
                                line.Y1 += offsetY;
                                line.X2 += offsetX;
                                line.Y2 += offsetY;
                            }
                        }
                        else if (element is Polygon polygon)
                        {
                            for (int i = 0; i < polygon.Points.Count; i++)
                            {
                                polygon.Points[i] = new Point(polygon.Points[i].X + offsetX, polygon.Points[i].Y + offsetY);
                            }
                        }
                    }
                }

                clickPosition = currentPosition;
            }
        }

        private void CityCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Released)
            {
                isDragging = false;
                CityCanvas.ReleaseMouseCapture();
            }
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            globalOffset = [0, 0];
            Constants.MetresPerPixel = 0.2;
            DrawCity();
            DrawMetresLine();
        }
    }
}
