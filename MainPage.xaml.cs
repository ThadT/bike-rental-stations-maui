using BikeRentalStations.ViewModel;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.RealTime;
using Esri.ArcGISRuntime.UI;

namespace BikeRentalStations;

public partial class MainPage : ContentPage, IQueryAttributable
{
    private readonly string _makeFavoriteImage = "https://raw.githubusercontent.com/ThadT/bike-rental-stations-maui/main/MakeFavorite.png";
    private readonly string _unFavoriteImage = "https://raw.githubusercontent.com/ThadT/bike-rental-stations-maui/main/UnFavorite.png";
    private readonly CityBikesViewModel _vm;

    public MainPage(CityBikesViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        _vm = vm;
    }

    private async void MapViewTapped(object sender, GeoViewInputEventArgs e)
    {
        // Close any currently open callout.
        mapView.DismissCallout();

        // Identify a bike station from the tap.
        var dynamicEntityLayer = mapView.Map.OperationalLayers.OfType<DynamicEntityLayer>().FirstOrDefault();
        var results = await mapView.IdentifyLayerAsync(dynamicEntityLayer, e.Position, 4, false, 1);
        if (results.GeoElements.Count == 0 || results.GeoElements[0] is not DynamicEntityObservation bikeStation) { return; }

        var calloutDef = _vm.GetCalloutDefinitionForStation(bikeStation, _unFavoriteImage, _makeFavoriteImage);
        calloutDef.OnButtonClick = (tag) =>
        {
            // Add or remove this station from the favorites list.
            var isFavorite = _vm.ToggleIsFavorite(tag as DynamicEntity, CityPicker.SelectedItem.ToString());

            // Apply the correct image to the callout and show it again.
            calloutDef.ButtonImage = isFavorite ?
                                       new RuntimeImage(new Uri(_unFavoriteImage)) :
                                       new RuntimeImage(new Uri(_makeFavoriteImage));
            mapView.ShowCalloutAt(bikeStation.Geometry as MapPoint, calloutDef);
        };
        mapView.ShowCalloutAt(bikeStation.Geometry as MapPoint, calloutDef);
    }

    private async void CityPicker_SelectedIndexChanged(object sender, EventArgs e)
    {
        // Get the selected city name and pass it to the VM to show the stations for that city.
        var cityName = CityPicker.SelectedItem.ToString();
        var viewpoint = await _vm.ShowBikeStations(cityName);

        mapView.SetViewpoint(viewpoint);
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query["favorite"] is Favorite favorite)
        {
            var location = favorite.Location;
            mapView.SetViewpoint(new Viewpoint(location, 10000));

            // Close any currently open callout.
            mapView.DismissCallout();

            var calloutDef = _vm.GetCalloutDefinitionForStation(favorite, _unFavoriteImage);
            calloutDef.OnButtonClick = (tag) =>
            {
                // Add or remove this station from the favorites list.
                var isFavorite = _vm.ToggleIsFavorite(tag as DynamicEntity, CityPicker.SelectedItem.ToString());

                // Apply the correct image to the callout and show it again.
                calloutDef.ButtonImage = isFavorite ?
                                           new RuntimeImage(new Uri(_unFavoriteImage)) :
                                           new RuntimeImage(new Uri(_makeFavoriteImage));
                mapView.ShowCalloutAt(location, calloutDef);
            };
            mapView.ShowCalloutAt(location, calloutDef);
        }
    }
}
