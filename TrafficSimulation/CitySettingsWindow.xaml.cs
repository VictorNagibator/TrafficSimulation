using System.Windows;
using TrafficSimulation.Models;

namespace TrafficSimulation
{
    public partial class CitySettingsWindow : Window
    {
        private City city;
        public CitySettingsWindow(City city)
        {
            InitializeComponent();

            this.city = city;

            MaxSpeedSlider.Value = city.MaxAllowedSpeed;
            RoadQualitySlider.Value = city.RoadQuality * 100;
        }

        private void MaxSpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (city != null) city.MaxAllowedSpeed = (int)MaxSpeedSlider.Value;
        }

        private void RoadQualitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (city != null) city.RoadQuality = Math.Round(RoadQualitySlider.Value / 100, 2);
        }

        private void RainingSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

        }

        private void CarAccidentSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

        }
    }
}
