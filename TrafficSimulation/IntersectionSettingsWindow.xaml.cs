using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using TrafficSimulation.Models;

namespace TrafficSimulation
{
    public partial class IntersectionSettingsWindow : Window
    {
        private Intersection intersection;
        private City city;
        private Road selectedRoad;

        public IntersectionSettingsWindow(Intersection intersection, City city)
        {
            InitializeComponent();
            this.intersection = intersection;
            this.city = city;
            IntersectionNameTextBox.Text = intersection.Name;
            DrawIntersectionAndRoads();
            SizeChanged += IntersectionSettingsWindow_SizeChanged;
        }

        private void IntersectionSettingsWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ClipToBounds();
            DrawIntersectionAndRoads();
        }

        private void ClipToBounds()
        {
            RectangleGeometry clipRect = new(new Rect(0, 0, IntersectionCanvas.ActualWidth, IntersectionCanvas.ActualHeight));
            IntersectionCanvas.Clip = clipRect;
        }

        private void DrawIntersectionAndRoads()
        {
            IntersectionCanvas.Children.Clear();

            foreach (var road in intersection.IncomingRoads)
            {
                DrawRoad(road);
            }

            DrawIntersection();

            foreach (var light in city.TrafficLights)
            {
                if (light.ControlledIntersection == intersection)
                {
                    DrawTrafficLight(light);
                }
            }
        }

        private void DrawIntersection()
        {
            double maxRoadWidth = intersection.GetWidth();
            double intersectionCenterX = IntersectionCanvas.ActualWidth / 2;
            double intersectionCenterY = IntersectionCanvas.ActualHeight / 2;

            Rectangle rect = new()
            {
                Width = maxRoadWidth / Constants.MetresPerPixelInIntersectionSettings,
                Height = maxRoadWidth / Constants.MetresPerPixelInIntersectionSettings,
                Fill = Brushes.Black,
                Tag = intersection
            };

            Canvas.SetLeft(rect, intersectionCenterX - rect.Width / 2);
            Canvas.SetTop(rect, intersectionCenterY - rect.Height / 2);
            IntersectionCanvas.Children.Add(rect);
        }

        private void DrawRoad(Road road)
        {
            double intersectionCenterX = IntersectionCanvas.ActualWidth / 2;
            double intersectionCenterY = IntersectionCanvas.ActualHeight / 2;

            var start = road.StartIntersection == intersection ? new Point(intersectionCenterX, intersectionCenterY) :
                    new Point(intersectionCenterX + (road.StartIntersection.X - intersection.X) / Constants.MetresPerPixelInIntersectionSettings, intersectionCenterY + (road.StartIntersection.Y - intersection.Y) / Constants.MetresPerPixelInIntersectionSettings);

            var end = road.EndIntersection == intersection ? new Point(intersectionCenterX, intersectionCenterY) :
                new Point(intersectionCenterX + (road.EndIntersection.X - intersection.X) / Constants.MetresPerPixelInIntersectionSettings, intersectionCenterY + (road.EndIntersection.Y - intersection.Y) / Constants.MetresPerPixelInIntersectionSettings);

            Line line = new()
            {
                X1 = start.X,
                Y1 = start.Y,
                X2 = end.X,
                Y2 = end.Y,
                Stroke = Brushes.Black,
                StrokeThickness = road.Width / Constants.MetresPerPixelInIntersectionSettings,
                Tag = road
            };

            line.MouseDown += RoadLine_MouseDown;
            IntersectionCanvas.Children.Add(line);
        }

        private void DrawTrafficLight(TrafficLight light)
        {
            double intersectionCenterX = IntersectionCanvas.ActualWidth / 2;
            double intersectionCenterY = IntersectionCanvas.ActualHeight / 2;

            double[] position = { intersectionCenterX, intersectionCenterY };
            double roadAngle;
            if (light.ControlledIntersection == light.ControlledRoad.StartIntersection)
            {
                roadAngle = Math.Atan2(light.ControlledRoad.EndIntersection.Y - light.ControlledRoad.StartIntersection.Y,
                                       light.ControlledRoad.EndIntersection.X - light.ControlledRoad.StartIntersection.X);
            }
            else
            {
                roadAngle = Math.Atan2(light.ControlledRoad.StartIntersection.Y - light.ControlledRoad.EndIntersection.Y,
                                       light.ControlledRoad.StartIntersection.X - light.ControlledRoad.EndIntersection.X);
            }

            double offsetX = position[0] + Math.Cos(roadAngle) * Constants.RoadWidthMetres / Constants.MetresPerPixelInIntersectionSettings / 2;
            double offsetY = position[1] + Math.Sin(roadAngle) * Constants.RoadWidthMetres / Constants.MetresPerPixelInIntersectionSettings / 2;

            Ellipse ellipse = new()
            {
                Width = Constants.TrafficLightMetres / Constants.MetresPerPixelInIntersectionSettings,
                Height = Constants.TrafficLightMetres / Constants.MetresPerPixelInIntersectionSettings,
                Fill = Brushes.Red
            };

            Canvas.SetLeft(ellipse, offsetX - ellipse.Width / 2);
            Canvas.SetTop(ellipse, offsetY - ellipse.Height / 2);
            IntersectionCanvas.Children.Add(ellipse);
        }

        private void RoadLine_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Line line && line.Tag is Road road)
            {
                DeselectCurrentRoad();

                TrafficLightOffRadioButton.IsEnabled = true;
                TrafficLightOnRadioButton.IsEnabled = true;
                selectedRoad = road;
                SelectCurrentRoad();
                RoadNameTextBlock.Text = $"Название дороги: {selectedRoad.Name}";
                UpdateTrafficLightSettings();
            }
        }

        private void SelectCurrentRoad()
        {
            foreach (var child in IntersectionCanvas.Children)
            {
                if (child is Line line && line.Tag == selectedRoad)
                {
                    line.Stroke = Brushes.Blue;
                }
            }
        }

        private void DeselectCurrentRoad()
        {
            foreach (var child in IntersectionCanvas.Children)
            {
                if (child is Line line && line.Stroke == Brushes.Blue)
                {
                    line.Stroke = Brushes.Black;
                }
            }
        }

        private void TrafficLightOnRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            TrafficLightSettingsPanel.Visibility = Visibility.Visible;

            if (!city.TrafficLights.Any(tl => tl.ControlledIntersection == intersection && tl.ControlledRoad == selectedRoad))
            {
                TrafficLight newTrafficLight = new()
                {
                    ControlledIntersection = intersection,
                    ControlledRoad = selectedRoad,
                    GreenTimer = 30,
                    RedTimer = 30
                };
                city.TrafficLights.Add(newTrafficLight);

                UpdateTrafficLightSettings();
                DrawIntersectionAndRoads();
                SelectCurrentRoad();
            }
        }

        private void TrafficLightOffRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            TrafficLightSettingsPanel.Visibility = Visibility.Collapsed;

            if (city.TrafficLights.Any(tl => tl.ControlledIntersection == intersection && tl.ControlledRoad == selectedRoad))
            {
                city.RemoveTrafficLight(city.TrafficLights.Where(tl => tl.ControlledIntersection == intersection && tl.ControlledRoad == selectedRoad).First());
                DrawIntersectionAndRoads();
                SelectCurrentRoad();
            }
        }

        private void UpdateTrafficLightSettings()
        {
            if (city.TrafficLights.Any(tl => tl.ControlledIntersection == intersection && tl.ControlledRoad == selectedRoad))
            {
                var trafficLight = city.TrafficLights.First(tl => tl.ControlledIntersection == intersection && tl.ControlledRoad == selectedRoad);
                TrafficLightOnRadioButton.IsChecked = true;
                GreenTimeSlider.Value = trafficLight.GreenTimer;
                RedTimeSlider.Value = trafficLight.RedTimer;
            }
            else
            {
                TrafficLightOffRadioButton.IsChecked = true;
            }
        }

        private void IntersectionNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            intersection.Name = IntersectionNameTextBox.Text;
        }

        private void GreenTimeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (selectedRoad != null && city.TrafficLights.Any(tl => tl.ControlledIntersection == intersection && tl.ControlledRoad == selectedRoad))
            {
                var trafficLight = city.TrafficLights.First(tl => tl.ControlledIntersection == intersection && tl.ControlledRoad == selectedRoad);
                trafficLight.GreenTimer = (int)GreenTimeSlider.Value;
            }
        }

        private void RedTimeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (selectedRoad != null && city.TrafficLights.Any(tl => tl.ControlledIntersection == intersection && tl.ControlledRoad == selectedRoad))
            {
                var trafficLight = city.TrafficLights.First(tl => tl.ControlledIntersection == intersection && tl.ControlledRoad == selectedRoad);
                trafficLight.RedTimer = (int)RedTimeSlider.Value;
            }
        }
    }
}
