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
        private enum RoadCreationState { None, AddingStart, AddingEnd }
        private RoadCreationState currentRoadState = RoadCreationState.None;
        private Road previewRoad;
        private bool isAllowedToCreateRoad = true;

        private bool isWatchingTrafficJams = false;

        public MainWindow()
        {
            InitializeComponent();
            InitializeTimer();

            DrawCity();
        }

        private void InitializeTimer()
        {
            timer.Interval = TimeSpan.FromSeconds(Constants.TimerInterval);
            timer.Tick += Timer_Tick;
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

            if (!isWatchingTrafficJams)
            {
                foreach (var vehicle in city.Vehicles)
                {
                    DrawVehicle(vehicle);
                }

                foreach (var light in city.TrafficLights)
                {
                    DrawTrafficLight(light);
                }
            }
            
            if (previewRoad != null)
            {
                DrawRoad(previewRoad);
            }

            DrawMetresLine();
            DrawRain();
        }

        private void DrawRain()
        {
            if (city.IsRaining) RainGIF.Visibility = Visibility.Visible;
            else RainGIF.Visibility = Visibility.Hidden;
        }

        private void DrawIntersection(Intersection intersection)
        {
            Rectangle rect = new()
            {
                Width = intersection.Width / Constants.MetresPerPixel,
                Height = intersection.Width / Constants.MetresPerPixel,
                Fill = Brushes.Black,
                Tag = intersection,
                Opacity = intersection.BestRoadQuality
            };

            rect.RenderTransform = new RotateTransform(intersection.Angle * 180 / Math.PI, rect.Width / 2, rect.Height / 2);
            rect.MouseLeftButtonDown += Intersection_LeftButtonMouseDown;

            Canvas.SetLeft(rect, intersection.X / Constants.MetresPerPixel - intersection.Width / (2 * Constants.MetresPerPixel) + globalOffset[0]);
            Canvas.SetTop(rect, intersection.Y / Constants.MetresPerPixel - intersection.Width / (2 * Constants.MetresPerPixel) + globalOffset[1]);
            CityCanvas.Children.Add(rect);
        }

        private void LaunchWindow(Window window)
        {
            bool isSimulationWorked = timer.IsEnabled;

            window.Show();
            window.Closed += (s, e) => DrawCity();

            if (isSimulationWorked)
            {
                StopSimulation();
                window.Closed += (s, e) => StartSimulation();
            }
        }

        private void Intersection_LeftButtonMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Rectangle rect && rect.Tag is Intersection intersection)
            {
                if (currentRoadState == RoadCreationState.AddingStart)
                {
                    AddRoadStarting(intersection);
                }
                else if (currentRoadState == RoadCreationState.AddingEnd && previewRoad.StartIntersection != previewRoad.EndIntersection && isAllowedToCreateRoad)
                {
                    AddRoadEnding(intersection);
                }
                else if (currentRoadState == RoadCreationState.None)
                {
                    IntersectionSettingsWindow settingsWindow = new(intersection, city);
                    LaunchWindow(settingsWindow);
                }    
            }
        }

        private void DrawRoad(Road road)
        {
            double startIntersectionWidth = road.StartIntersection.Width;
            double endIntersectionWidth = road.EndIntersection.Width;

            double startX = road.StartIntersection.X / Constants.MetresPerPixel;
            double startY = road.StartIntersection.Y / Constants.MetresPerPixel;
            double endX = road.EndIntersection.X / Constants.MetresPerPixel;
            double endY = road.EndIntersection.Y / Constants.MetresPerPixel;

            double roadAngle = Math.Atan2(endY - startY, endX - startX);

            startX = road.StartIntersection.X / Constants.MetresPerPixel + startIntersectionWidth / (2 * Constants.MetresPerPixel) * Math.Cos(roadAngle);
            startY = road.StartIntersection.Y / Constants.MetresPerPixel + startIntersectionWidth / (2 * Constants.MetresPerPixel) * Math.Sin(roadAngle);
            endX = road.EndIntersection.X / Constants.MetresPerPixel - endIntersectionWidth / (2 * Constants.MetresPerPixel) * Math.Cos(roadAngle);
            endY = road.EndIntersection.Y / Constants.MetresPerPixel - endIntersectionWidth / (2 * Constants.MetresPerPixel) * Math.Sin(roadAngle);

            Line line = new()
            {
                X1 = startX + globalOffset[0],
                Y1 = startY + globalOffset[1],
                X2 = endX + globalOffset[0],
                Y2 = endY + globalOffset[1],
                StrokeThickness = road.Width / Constants.MetresPerPixel,
                Tag = road,
                Opacity = road.Quality
            };

            if (road == previewRoad)
            {
                line.Stroke = isAllowedToCreateRoad ? Brushes.Aqua : Brushes.Red;
            }
            else if (isWatchingTrafficJams)
            {
                switch (road.TrafficJamStatus)
                {
                    case TrafficJamColor.DarkRed:
                        line.Stroke = Brushes.DarkRed;
                        break;
                    case TrafficJamColor.Red:
                        line.Stroke = Brushes.Red;
                        break;
                    case TrafficJamColor.Yellow:
                        line.Stroke = Brushes.Yellow;
                        break;
                    case TrafficJamColor.Green:
                        line.Stroke = Brushes.Green;
                        break;
                    default:
                        break;
                }
            }
            else
            {
                line.Stroke = Brushes.Black;
            }

            line.MouseLeftButtonDown += Road_LeftButtonMouseDown;
            CityCanvas.Children.Add(line);

            if (road.Type == RoadType.OneWay)
            {
                DrawOneWayArrow(road);
            }

            DrawRoadName(road);

            if (road.MaxAllowedSpeed != city.MaxAllowedSpeed)
            {
                DrawSpeedLimitSign(road);
            }

            if (road != previewRoad)
            {
                DrawIntersection(road.StartIntersection);
                DrawIntersection(road.EndIntersection);
            }
        }

        private void Road_LeftButtonMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Line line && line.Tag is Road road && road != previewRoad)
            {
                if (currentRoadState == RoadCreationState.AddingStart || (currentRoadState == RoadCreationState.AddingEnd && isAllowedToCreateRoad))
                {
                    Intersection intersection = new()
                    {
                        X = e.GetPosition(CityCanvas).X * Constants.MetresPerPixel - globalOffset[0] * Constants.MetresPerPixel,
                    };
                    intersection.Y = (intersection.X - road.StartIntersection.X) * (road.EndIntersection.Y - road.StartIntersection.Y) / (road.EndIntersection.X - road.StartIntersection.X) + road.StartIntersection.Y - globalOffset[1] * Constants.MetresPerPixel;

                    Road newroad1 = new()
                    {
                        Name = road.Name,
                        NumOfLinesPerDirection = road.NumOfLinesPerDirection,
                        StartIntersection = road.StartIntersection,
                        EndIntersection = intersection,
                        Quality = road.Quality,
                        Type = road.Type,
                        MaxAllowedSpeed = road.MaxAllowedSpeed,
                    };

                    var vehiclesOnFirstHalfOfRoad = road.Vehicles.Where(v => v.Position <= newroad1.Length).ToList();
                    if (vehiclesOnFirstHalfOfRoad.Count != 0)
                    {
                        vehiclesOnFirstHalfOfRoad.ForEach(v => { 
                            newroad1.AddVehicle(v);
                            v.CurrentRoad = newroad1;
                            v.NextIntersection = v.IsMovingForward ? intersection : road.StartIntersection;
                            v.NextRoad = v.GetNextRoad();
                            road.RemoveVehicle(v); 
                        });
                    }

                    Road newroad2 = new()
                    {
                        Name = road.Name,
                        NumOfLinesPerDirection = road.NumOfLinesPerDirection,
                        StartIntersection = intersection,
                        EndIntersection = road.EndIntersection,
                        Quality = road.Quality,
                        Type = road.Type,
                        MaxAllowedSpeed = road.MaxAllowedSpeed
                    };

                    var vehiclesOnSecondHalfOfRoad = road.Vehicles.Where(v => v.Position > newroad1.Length).ToList();
                    if (vehiclesOnSecondHalfOfRoad.Count != 0)
                    {
                        vehiclesOnSecondHalfOfRoad.ForEach(v => { 
                            newroad2.AddVehicle(v);
                            v.CurrentRoad = newroad2; 
                            v.Position = v.Position - newroad1.Length;
                            v.NextIntersection = v.IsMovingForward ? road.EndIntersection : intersection;
                            v.NextRoad = v.GetNextRoad();
                            road.RemoveVehicle(v);
                        });
                    }

                    var trafficlightInBegin = city.TrafficLights.Where(tl => tl.ControlledRoad == road && tl.ControlledIntersection == road.StartIntersection).FirstOrDefault();
                    var trafficlightInEnd = city.TrafficLights.Where(tl => tl.ControlledRoad == road && tl.ControlledIntersection == road.EndIntersection).FirstOrDefault();
                    if (trafficlightInBegin != null) trafficlightInBegin.ControlledRoad = newroad1;
                    if (trafficlightInEnd != null) trafficlightInEnd.ControlledRoad = newroad2;
                    city.RemoveRoad(road);
                    
                    city.AddRoad(newroad1);
                    city.AddRoad(newroad2);

                    if (currentRoadState == RoadCreationState.AddingStart) AddRoadStarting(intersection);
                    else AddRoadEnding(intersection);
                }
                else if (currentRoadState == RoadCreationState.None)
                {
                    RoadSettingsWindow settingsWindow = new(road, city);
                    LaunchWindow(settingsWindow);
                }     
            }
        }

        private void DrawOneWayArrow(Road road)
        {
            double arrowLength = (road.Width - 0.5) / Constants.MetresPerPixel;

            double startX = road.StartIntersection.X / Constants.MetresPerPixel;
            double startY = road.StartIntersection.Y / Constants.MetresPerPixel;
            double endX = road.EndIntersection.X / Constants.MetresPerPixel;
            double endY = road.EndIntersection.Y / Constants.MetresPerPixel;

            double roadAngle = Math.Atan2(endY - startY, endX - startX);

            double midX = (startX + endX) / 2 + globalOffset[0];
            double midY = (startY + endY) / 2 + globalOffset[1];

            Point p1 = new(midX, midY);
            Point p2 = new(midX - arrowLength * Math.Cos(roadAngle - Math.PI / 6), midY - arrowLength * Math.Sin(roadAngle - Math.PI / 6));
            Point p3 = new(midX - arrowLength * Math.Cos(roadAngle + Math.PI / 6), midY - arrowLength * Math.Sin(roadAngle + Math.PI / 6));

            Polygon arrow = new()
            {
                Points = { p1, p2, p3 },
                Fill = Brushes.Gray
            };

            CityCanvas.Children.Add(arrow);
        }

        private void DrawRoadName(Road road)
        {
            double startIntersectionWidth = road.StartIntersection.Width;
            double endIntersectionWidth = road.EndIntersection.Width;

            double startX = road.StartIntersection.X / Constants.MetresPerPixel;
            double startY = road.StartIntersection.Y / Constants.MetresPerPixel;
            double endX = road.EndIntersection.X / Constants.MetresPerPixel;
            double endY = road.EndIntersection.Y / Constants.MetresPerPixel;

            double roadAngle = Math.Atan2(endY - startY, endX - startX);
            double roadAngleDegrees = roadAngle * 180 / Math.PI;

            startX = road.StartIntersection.X / Constants.MetresPerPixel + startIntersectionWidth / (2 * Constants.MetresPerPixel) * Math.Cos(roadAngle);
            startY = road.StartIntersection.Y / Constants.MetresPerPixel + startIntersectionWidth / (2 * Constants.MetresPerPixel) * Math.Sin(roadAngle);
            endX = road.EndIntersection.X / Constants.MetresPerPixel - endIntersectionWidth / (2 * Constants.MetresPerPixel) * Math.Cos(roadAngle);
            endY = road.EndIntersection.Y / Constants.MetresPerPixel - endIntersectionWidth / (2 * Constants.MetresPerPixel) * Math.Sin(roadAngle);

            TextBlock roadNameTextBlock = new()
            {
                Text = road.Name,
                FontSize = 2.5 / Constants.MetresPerPixel,
                Foreground = Brushes.White
            };

            roadNameTextBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            roadNameTextBlock.Arrange(new Rect(roadNameTextBlock.DesiredSize));

            double midX = (startX + endX) / 2 + globalOffset[0];
            double midY = (startY + endY) / 2 + globalOffset[1];

            roadNameTextBlock.RenderTransform = new RotateTransform(roadAngleDegrees, roadNameTextBlock.ActualWidth / 2, roadNameTextBlock.ActualHeight / 2);
            Canvas.SetLeft(roadNameTextBlock, midX - roadNameTextBlock.ActualWidth / 2);
            Canvas.SetTop(roadNameTextBlock, midY - roadNameTextBlock.ActualHeight / 2);
            CityCanvas.Children.Add(roadNameTextBlock);
        }

        private void DrawSpeedLimitSign(Road road)
        {
            double startIntersectionWidth = road.StartIntersection.Width;
            double endIntersectionWidth = road.EndIntersection.Width;

            double startX = road.StartIntersection.X / Constants.MetresPerPixel;
            double startY = road.StartIntersection.Y / Constants.MetresPerPixel;
            double endX = road.EndIntersection.X / Constants.MetresPerPixel;
            double endY = road.EndIntersection.Y / Constants.MetresPerPixel;

            double roadAngle = Math.Atan2(endY - startY, endX - startX);

            startX = road.StartIntersection.X / Constants.MetresPerPixel + startIntersectionWidth / (2 * Constants.MetresPerPixel) * Math.Cos(roadAngle);
            startY = road.StartIntersection.Y / Constants.MetresPerPixel + startIntersectionWidth / (2 * Constants.MetresPerPixel) * Math.Sin(roadAngle);
            endX = road.EndIntersection.X / Constants.MetresPerPixel - endIntersectionWidth / (2 * Constants.MetresPerPixel) * Math.Cos(roadAngle);
            endY = road.EndIntersection.Y / Constants.MetresPerPixel - endIntersectionWidth / (2 * Constants.MetresPerPixel) * Math.Sin(roadAngle);

            double midX = (startX + endX) / 2 + globalOffset[0];
            double midY = (startY + endY) / 2 + globalOffset[1];

            Ellipse speedLimitEllipse = new()
            {
                Width = 6 / Constants.MetresPerPixel,
                Height = 6 / Constants.MetresPerPixel,
                Fill = Brushes.White,
                Stroke = Brushes.Red,
                StrokeThickness = 0.4 / Constants.MetresPerPixel
            };

            TextBlock speedLimitTextBlock = new()
            {
                Text = road.MaxAllowedSpeed.ToString(),
                FontSize = 3 / Constants.MetresPerPixel,
                Foreground = Brushes.Black
            };

            speedLimitTextBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            speedLimitTextBlock.Arrange(new Rect(speedLimitTextBlock.DesiredSize));

            double offsetX = midX + (road.Width + 2) * Math.Cos(roadAngle + Math.PI / 2) / Constants.MetresPerPixel;
            double offsetY = midY + (road.Width + 2) * Math.Sin(roadAngle + Math.PI / 2) / Constants.MetresPerPixel;

            Canvas.SetLeft(speedLimitEllipse, offsetX - speedLimitEllipse.Width / 2);
            Canvas.SetTop(speedLimitEllipse, offsetY - speedLimitEllipse.Height / 2);
            CityCanvas.Children.Add(speedLimitEllipse);

            Canvas.SetLeft(speedLimitTextBlock, offsetX - speedLimitTextBlock.ActualWidth / 2);
            Canvas.SetTop(speedLimitTextBlock, offsetY - speedLimitTextBlock.ActualHeight / 2);
            CityCanvas.Children.Add(speedLimitTextBlock);
        }

        private void DrawVehicle(Vehicle vehicle)
        {
            double[] position = vehicle.CurrentRoad.GetPositionOnRoad(vehicle);
            position[0] /= Constants.MetresPerPixel;
            position[1] /= Constants.MetresPerPixel;
            double roadAngle = Math.Atan2(vehicle.CurrentRoad.EndIntersection.Y / Constants.MetresPerPixel - vehicle.CurrentRoad.StartIntersection.Y / Constants.MetresPerPixel,
                                          vehicle.CurrentRoad.EndIntersection.X / Constants.MetresPerPixel - vehicle.CurrentRoad.StartIntersection.X / Constants.MetresPerPixel);

            Rectangle rect = new()
            {
                Width = Constants.VehicleLengthPixels,
                Height = Constants.VehicleWidthPixels,
                Fill = vehicle.IsInCarAccident ? Brushes.IndianRed : Brushes.Blue,
                Tag = vehicle
            };

            double angle = (vehicle.IsMovingForward ? roadAngle : roadAngle + Math.PI) * 180 / Math.PI;
            rect.RenderTransform = new RotateTransform(angle, rect.Width / 2, rect.Height / 2);

            double offset, offsetX, offsetY;

            if (vehicle.CurrentRoad.Type == RoadType.OneWay)
            {
                if (vehicle.CurrentRoad.NumOfLinesPerDirection % 2 == 0)
                {
                    if (vehicle.LineNumber <= vehicle.CurrentRoad.NumOfLinesPerDirection / 2)
                    {
                        offset = rect.Height / 2 + Constants.VehicleWidthPixels * Math.Abs(vehicle.CurrentRoad.NumOfLinesPerDirection / 2 - vehicle.LineNumber);
                    }
                    else
                    {
                        offset = rect.Height / 2 + Constants.VehicleWidthPixels * Math.Abs(vehicle.CurrentRoad.NumOfLinesPerDirection / 2 - vehicle.LineNumber + 1);
                        offset = -offset;
                    }

                    offsetX = position[0] + Math.Sin(roadAngle) * offset;
                    offsetY = position[1] - Math.Cos(roadAngle) * offset;

                    Canvas.SetLeft(rect, offsetX - rect.Width / 2);
                    Canvas.SetTop(rect, offsetY - rect.Height / 2);
                }
                else
                {
                    offset = Constants.VehicleWidthPixels * Math.Abs(vehicle.CurrentRoad.NumOfLinesPerDirection / 2 - vehicle.LineNumber + 1);
                    if (vehicle.LineNumber > vehicle.CurrentRoad.NumOfLinesPerDirection / 2)
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

            rect.MouseDown += Vehicle_MouseDown;
            rect.ToolTip = new ToolTip() { Content = "Скорость: " + (int)vehicle.Speed };

            Canvas.SetLeft(rect, offsetX - rect.Width / 2 + globalOffset[0]);
            Canvas.SetTop(rect, offsetY - rect.Height / 2 + globalOffset[1]);
            CityCanvas.Children.Add(rect);
        }

        private void Vehicle_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Rectangle rect && rect.Tag is Vehicle vehicle)
            {
                bool isSimulationWorked = timer.IsEnabled;

                if (isSimulationWorked)
                {
                    StopSimulation();
                }

                if (MessageBox.Show("Вы желаете удалить эту машину?", "Удаление машины", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    city.RemoveVehicle(vehicle);
                    DrawCity();
                }

                if (isSimulationWorked)
                {
                    StartSimulation();
                }
            }
        }

        private void DrawTrafficLight(TrafficLight light)
        {
            double[] position = [light.ControlledIntersection.X / Constants.MetresPerPixel, light.ControlledIntersection.Y / Constants.MetresPerPixel];
            double roadAngle;
            if (light.ControlledIntersection == light.ControlledRoad.StartIntersection)
            {
                roadAngle = Math.Atan2(light.ControlledRoad.EndIntersection.Y / Constants.MetresPerPixel - light.ControlledRoad.StartIntersection.Y / Constants.MetresPerPixel,
                                       light.ControlledRoad.EndIntersection.X / Constants.MetresPerPixel - light.ControlledRoad.StartIntersection.X / Constants.MetresPerPixel);
            }
            else
            {
                roadAngle = Math.Atan2(light.ControlledRoad.StartIntersection.Y / Constants.MetresPerPixel - light.ControlledRoad.EndIntersection.Y / Constants.MetresPerPixel,
                                       light.ControlledRoad.StartIntersection.X / Constants.MetresPerPixel - light.ControlledRoad.EndIntersection.X / Constants.MetresPerPixel);
            }

            //Смещение светофора вдоль дороги
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
            Vehicle vehicle = new(city);
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
            timer.Start();

            StartSimulationButton.Visibility = Visibility.Collapsed;
            StopSimulationButton.Visibility = Visibility.Visible;
        }

        private void StopSimulation()
        {
            timer.Stop();

            StartSimulationButton.Visibility = Visibility.Visible;
            StopSimulationButton.Visibility = Visibility.Collapsed;
        }

        private void CityCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RectangleGeometry clipRect = new(new Rect(0, 0, CityCanvas.ActualWidth, CityCanvas.ActualHeight));
            CityCanvas.Clip = clipRect;

            DrawCity();
        }

        private void ZoomInButton_Click(object sender, RoutedEventArgs e)
        {
            if (Math.Round(Constants.MetresPerPixel, 2) <= 0.1) return;

            if (Math.Round(Constants.MetresPerPixel, 2) > 0.5 && Math.Round(Constants.MetresPerPixel, 2) <= 1) Constants.MetresPerPixel = Math.Max(Constants.MetresPerPixel - 0.1, 0.1);
            else Constants.MetresPerPixel = Math.Max(Constants.MetresPerPixel - 0.05, 0.1);
            DrawCity();
        }

        private void ZoomOutButton_Click(object sender, RoutedEventArgs e)
        {
            if (Math.Round(Constants.MetresPerPixel, 2) >= 0.5 && Math.Round(Constants.MetresPerPixel, 2) <= 1) Constants.MetresPerPixel = Math.Min(Constants.MetresPerPixel + 0.1, 1);
            else Constants.MetresPerPixel = Math.Min(Constants.MetresPerPixel + 0.05, 1);
            DrawCity();
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

        private void CityCanvas_LeftMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (currentRoadState == RoadCreationState.AddingStart || (currentRoadState == RoadCreationState.AddingEnd && previewRoad.StartIntersection != previewRoad.EndIntersection && isAllowedToCreateRoad))
            {
                Intersection intersection = new()
                {
                    X = e.GetPosition(CityCanvas).X * Constants.MetresPerPixel - globalOffset[0] * Constants.MetresPerPixel,
                    Y = e.GetPosition(CityCanvas).Y * Constants.MetresPerPixel - globalOffset[1] * Constants.MetresPerPixel
                };

                if (currentRoadState == RoadCreationState.AddingStart) AddRoadStarting(intersection);
                else AddRoadEnding(intersection);
            }
            else
            {
                isDragging = true;
                clickPosition = e.GetPosition(CityCanvas);
                CityCanvas.CaptureMouse();
            }
        }

        private void CityCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (currentRoadState == RoadCreationState.AddingEnd && e.LeftButton != MouseButtonState.Pressed)
            { 
               isAllowedToCreateRoad = city.Roads.Where(r => r.DoesIntersect(previewRoad) && r.StartIntersection != previewRoad.StartIntersection && r.EndIntersection != previewRoad.StartIntersection).ToList().Count == 0;

               previewRoad.EndIntersection = new Intersection
               {
                   X = e.GetPosition(CityCanvas).X * Constants.MetresPerPixel - globalOffset[0] * Constants.MetresPerPixel - 0.2 * Math.Cos(previewRoad.Angle),
                   Y = e.GetPosition(CityCanvas).Y * Constants.MetresPerPixel - globalOffset[1] * Constants.MetresPerPixel - 0.2 * Math.Sin(previewRoad.Angle)
               };

               DrawCity();
            }
            else if (isDragging && e.LeftButton == MouseButtonState.Pressed)
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
            Constants.MetresPerPixel = Constants.MetresPerPixelBasic;
            DrawCity();
        }

        private void DeleteAllVehiclesButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Вы желаете удалить все машины?", "Удаление машин", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                city.RemoveAllVehicles();
                DrawCity();
            }
        }

        private void AddRoadButton_Click(object sender, RoutedEventArgs e)
        {
            currentRoadState = RoadCreationState.AddingStart;
            AddRoadButton.Visibility = Visibility.Collapsed;
            CancelAddRoadButton.Visibility = Visibility.Visible;
        }

        private void CancelAddRoadButton_Click(object sender, RoutedEventArgs e)
        {
            currentRoadState = RoadCreationState.None;
            AddRoadButton.Visibility = Visibility.Visible;
            CancelAddRoadButton.Visibility = Visibility.Collapsed;

            if (previewRoad != null)
            {
                previewRoad = null;
            }

            DrawCity();
        }

        private void CitySettingsButton_Click(object sender, RoutedEventArgs e)
        {
            CitySettingsWindow window = new(city);
            LaunchWindow(window);
        }

        private void TrafficJamButton_Click(object sender, RoutedEventArgs e)
        {
            isWatchingTrafficJams = !isWatchingTrafficJams;
            DrawCity();
        }

        private void AddRoadStarting(Intersection intersection)
        {
            previewRoad = new Road()
            {
                 StartIntersection = intersection,
                 EndIntersection = intersection,
                 Type = RoadType.TwoWay,
                 NumOfLinesPerDirection = 1,
                 MaxAllowedSpeed = city.MaxAllowedSpeed
            };

            currentRoadState = RoadCreationState.AddingEnd;
        }

        private void AddRoadEnding(Intersection intersection)
        {
            previewRoad.EndIntersection = intersection;

            city.AddRoad(previewRoad);
            previewRoad = null;

            currentRoadState = RoadCreationState.None;
            AddRoadButton.Visibility = Visibility.Visible;
            CancelAddRoadButton.Visibility = Visibility.Collapsed;

            DrawCity();
        }

        private void ClearAllButton_Copy_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Вы желаете очистить всю карту?", "Очистка города", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                city = new();
                ResetButton_Click(sender, e);
                DrawCity();
            }
        }
    }
}
