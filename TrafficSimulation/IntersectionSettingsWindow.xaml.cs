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
        private TrafficLight selectedTrafficLight => city.TrafficLights.FirstOrDefault(tl => tl.ControlledIntersection == intersection && tl.ControlledRoad == selectedRoad);

        public IntersectionSettingsWindow(Intersection intersection, City city)
        {
            InitializeComponent();

            this.intersection = intersection;
            this.city = city;
            IntersectionNameTextBox.Text = intersection.Name;
            DrawIntersectionAndRoads();
        }

        private void IntersectionSettingsWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RectangleGeometry clipRect = new(new Rect(0, 0, IntersectionCanvas.ActualWidth, IntersectionCanvas.ActualHeight));
            IntersectionCanvas.Clip = clipRect;

            DrawIntersectionAndRoads();
        }

        private void DrawIntersectionAndRoads()
        {
            IntersectionCanvas.Children.Clear();

            foreach (var road in intersection.IncomingRoads)
            {
                DrawRoad(road);
            }

            DrawIntersection();

            DrawTrafficLight(selectedTrafficLight);
        }

        private void DrawIntersection()
        {
            double maxRoadWidth = intersection.Width;
            double intersectionCenterX = IntersectionCanvas.ActualWidth / 2;
            double intersectionCenterY = IntersectionCanvas.ActualHeight / 2;

            Rectangle rect = new()
            {
                Width = maxRoadWidth / Constants.MetresPerPixelInIntersectionSettings,
                Height = maxRoadWidth / Constants.MetresPerPixelInIntersectionSettings,
                Fill = Brushes.Black,
                Tag = intersection,
                Opacity = intersection.BestRoadQuality
            };

            rect.RenderTransform = new RotateTransform(intersection.Angle * 180 / Math.PI, rect.Width / 2, rect.Height / 2);
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
                Tag = road,
                Opacity = road.Quality
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

                if (selectedTrafficLight != null) IsEnabledTrafficLightCheckBox.IsChecked = true;
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

        private void UpdateTrafficLightSettings()
        {
            if (selectedTrafficLight != null)
            {
                IsEnabledTrafficLightCheckBox.IsChecked = true;
                GreenTimeSlider.Value = selectedTrafficLight.GreenTimer;
                RedTimeSlider.Value = selectedTrafficLight.RedTimer;
                switch (selectedTrafficLight.State)
                {
                    case TrafficLightState.Red:
                        RedLightRadioButton.IsChecked = true;
                        break;
                    case TrafficLightState.Yellow:
                        YellowLightRadioButton.IsChecked = true;
                        break;
                    case TrafficLightState.Green:
                        GreenLightRadioButton.IsChecked = true;
                        break;
                    default:
                        break;
                }
            }
            else
            {
                IsEnabledTrafficLightCheckBox.IsChecked = false;
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
                selectedTrafficLight.GreenTimer = (int)GreenTimeSlider.Value;
            }
        }

        private void RedTimeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (selectedRoad != null && city.TrafficLights.Any(tl => tl.ControlledIntersection == intersection && tl.ControlledRoad == selectedRoad))
            {
                selectedTrafficLight.RedTimer = (int)RedTimeSlider.Value;
            }
        }

        private void IsEnabledTrafficLightCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            TrafficLightSettingsPanel.Visibility = Visibility.Visible;

            if (selectedTrafficLight == null)
            {
                TrafficLight newTrafficLight = new()
                {
                    ControlledIntersection = intersection,
                    ControlledRoad = selectedRoad,
                };
                city.TrafficLights.Add(newTrafficLight);

                UpdateTrafficLightSettings();
                DrawIntersectionAndRoads();
                SelectCurrentRoad();
            }
        }

        private void IsEnabledTrafficLightCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            TrafficLightSettingsPanel.Visibility = Visibility.Collapsed;

            if (selectedTrafficLight != null)
            {
                city.RemoveTrafficLight(city.TrafficLights.Where(tl => tl.ControlledIntersection == intersection && tl.ControlledRoad == selectedRoad).First());
                DrawIntersectionAndRoads();
                SelectCurrentRoad();
            }
        }

        private void RedLightRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            selectedTrafficLight.State = TrafficLightState.Red;
        }

        private void YellowLightRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            selectedTrafficLight.State = TrafficLightState.Yellow;
        }

        private void GreenLightRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            selectedTrafficLight.State = TrafficLightState.Green;
        }
    }
}
