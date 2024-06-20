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
        }

        private void RoadNameTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            selectedRoad.Name = RoadNameTextBox.Text;
        }

        private void MaxSpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (city != null) selectedRoad.MaxAllowedSpeed = (int)MaxSpeedSlider.Value;
        }
    }
}
