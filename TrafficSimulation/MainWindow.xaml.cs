using System;
using System.Linq;
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
        private Random random = new Random();

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
            city = new City();

            Intersection intersection1 = new Intersection { Name = "Intersection1", X = 100, Y = 100 };
            Intersection intersection2 = new Intersection { Name = "Intersection2", X = 300, Y = 100 };
            Intersection intersection3 = new Intersection { Name = "Intersection3", X = 300, Y = 300 };
            Intersection intersection4 = new Intersection { Name = "Intersection4", X = 600, Y = 100 };

            Road road1 = new Road { Name = "Road1", Type = RoadType.TwoWay, NumOfLanesPerDirection = 1, StartIntersection = intersection1, EndIntersection = intersection2 };
            Road road2 = new Road { Name = "Road2", Type = RoadType.TwoWay, NumOfLanesPerDirection = 1, StartIntersection = intersection2, EndIntersection = intersection3 };
            Road road3 = new Road { Name = "Road3", Type = RoadType.TwoWay, NumOfLanesPerDirection = 2, StartIntersection = intersection2, EndIntersection = intersection4 };

            city.AddRoad(road1);
            city.AddRoad(road2);
            city.AddRoad(road3);

            TrafficLight light1 = new TrafficLight { ID = "Light1", ControlledRoad = road1, ControlledIntersection = intersection1, State = TrafficLightState.Green, Timer = (int)TrafficLightSlider.Value };
            TrafficLight light2 = new TrafficLight { ID = "Light2", ControlledRoad = road1, ControlledIntersection = intersection2, State = TrafficLightState.Red, Timer = (int)TrafficLightSlider.Value };
            TrafficLight light3 = new TrafficLight { ID = "Light3", ControlledRoad = road2, ControlledIntersection = intersection2, State = TrafficLightState.Red, Timer = (int)TrafficLightSlider.Value };

            city.AddTrafficLight(light1);
            city.AddTrafficLight(light2);
            city.AddTrafficLight(light3);
        }

        private void InitializeTimer()
        {
            timer = new DispatcherTimer();
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
            double extension = Road.Width / 2;

            double startX = road.StartIntersection.X;
            double startY = road.StartIntersection.Y;
            double endX = road.EndIntersection.X;
            double endY = road.EndIntersection.Y;

            double roadAngle = Math.Atan2(endY - startY, endX - startX);

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
                StrokeThickness = Road.Width * road.NumOfLanesPerDirection
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

            // Поворачиваем машину в зависимости от направления движения
            double angle = (vehicle.IsMovingForward ? roadAngle : roadAngle + Math.PI) * 180 / Math.PI;
            if (!vehicle.IsMovingForward)
            {
                angle += 180; // Инвертируем угол для машин, движущихся в обратную сторону
            }
            rect.RenderTransform = new RotateTransform(angle, rect.Width / 2, rect.Height / 2);

            double offsetX = position[0];
            double offsetY = position[1];
            if (vehicle.IsMovingForward)
            {
                offsetX -= Math.Sin(roadAngle) * Road.Width / 4;
                offsetY += Math.Cos(roadAngle) * Road.Width / 4;
            }
            else
            {
                offsetX += Math.Sin(roadAngle) * Road.Width / 4;
                offsetY -= Math.Cos(roadAngle) * Road.Width / 4;
            }

            Canvas.SetLeft(rect, offsetX - rect.Width / 2);
            Canvas.SetTop(rect, offsetY - rect.Height / 2);

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
            double offsetX = position[0] + Math.Cos(roadAngle) * Road.Width / 4;
            double offsetY = position[1] + Math.Sin(roadAngle) * Road.Width / 4;

            Ellipse ellipse = new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = light.State == TrafficLightState.Red ? Brushes.Red :
                       light.State == TrafficLightState.Yellow ? Brushes.Yellow : Brushes.Green
            };

            Canvas.SetLeft(ellipse, offsetX - ellipse.Width / 2);
            Canvas.SetTop(ellipse, offsetY - ellipse.Height / 2);
            CityCanvas.Children.Add(ellipse);
        }

        private void DrawMetresLine()
        {
            Line line = new Line
            {
                X1 = CityCanvas.ActualWidth - 10,
                Y1 = CityCanvas.ActualHeight - 10,
                X2 = CityCanvas.ActualWidth - 100,
                Y2 = CityCanvas.ActualHeight - 10,
                Stroke = Brushes.Black,
                StrokeThickness = 1
            };

            CityCanvas.Children.Add(line);

            // Добавление текста "100 метров"
            TextBlock textBlock = new TextBlock
            {
                Text = "100 метров",
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

            // Определяем направление движения (0 - вперед, 1 - назад)
            bool isMovingForward = random.Next(2) == 0;

            var vehicle = new Vehicle
            {
                ID = "Car" + (city.Vehicles.Count + 1),
                Speed = 30, // Начальная скорость автомобиля (может быть изменена позже)
                MaxSpeed = MaxSpeedSlider.Value * 1000 / 3600, // Переводим км/ч в м/с
                CurrentRoad = road,
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
