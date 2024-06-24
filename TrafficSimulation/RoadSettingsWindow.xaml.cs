using System.Windows;
using TrafficSimulation.Models;

namespace TrafficSimulation
{
    public partial class RoadSettingsWindow : Window
    {
        private Road selectedRoad;
        private City city;

        public RoadSettingsWindow(Road road, City city)
        {
            InitializeComponent();

            selectedRoad = road;
            this.city = city;

            RoadNameTextBox.Text = selectedRoad.Name;
            MaxSpeedSlider.Value = selectedRoad.MaxAllowedSpeed;

            if (road.Type == RoadType.OneWay) OneWayRadioButton.IsChecked = true;
            else TwoWayRadioButton.IsChecked = true;

            NumOfLanesSlider.Value = road.NumOfLanesPerDirection;
            RoadQualitySlider.Value = road.Quality * 100;
        }

        private void RoadNameTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            selectedRoad.Name = RoadNameTextBox.Text;
        }

        private void MaxSpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (city != null) selectedRoad.MaxAllowedSpeed = (int)MaxSpeedSlider.Value;
        }

        private void NumOfLanesSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (city != null) selectedRoad.NumOfLanesPerDirection = (int)NumOfLanesSlider.Value;
        }

        private void OneWayRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            selectedRoad.Type = RoadType.OneWay;
        }

        private void TwoWayRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            selectedRoad.Type = RoadType.TwoWay;
        }

        private void DeleteRoadButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Вы уверены, что хотите удалить эту дорогу?", "Удаление дороги", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                city.RemoveRoad(selectedRoad);
                Close();
            }
        }

        private void RoadQualitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (city != null) selectedRoad.Quality = Math.Round(RoadQualitySlider.Value / 100, 2);
        }
    }
}
