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

            MaxSpeedSlider.Value = city.MaxAllowedSpeed;
            RoadQualitySlider.Value = city.RoadQuality * 100;
            CarAccidentSlider.Value = city.ChanceToGetInCarAccident * 100;
            CarAccidentTimerSlider.Value = city.CarAccidentTimer / 10;
            RainingSlider.Value = city.ChanceOfRaining * 100;
            RainingTimerSlider.Value = city.RainingTimer / 10;

            this.city = city;
        }

        private void MaxSpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (city != null) city.MaxAllowedSpeed = Math.Round(MaxSpeedSlider.Value);
        }

        private void RoadQualitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (city != null) city.RoadQuality = Math.Round(RoadQualitySlider.Value / 100, 2);
        }

        private void RainingSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (city != null) city.ChanceOfRaining = Math.Round(RainingSlider.Value / 100, 2);
        }

        private void CarAccidentSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (city != null) city.ChanceToGetInCarAccident = Math.Round(CarAccidentSlider.Value / 100, 2);
        }

        private void CarAccidentTimerSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (city != null) city.CarAccidentTimer = (int)Math.Round(CarAccidentTimerSlider.Value) * 10;
        }

        private void RainingTimerSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (city != null) city.RainingTimer = (int)Math.Round(RainingTimerSlider.Value) * 10;
        }
    }
}
