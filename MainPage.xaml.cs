using BikeRentalStations.ViewModel;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.RealTime;
using Esri.ArcGISRuntime.UI;

namespace BikeRentalStations;

public partial class MainPage : ContentPage
{
    private Dictionary<long, Favorite> _favoriteBikeStations = new Dictionary<long, Favorite>();
   // private string _bikeImage = "http://static.arcgis.com/images/Symbols/Transportation/esriDefaultMarker_189.png";
   // private string _redStarImage = "http://static.arcgis.com/images/Symbols/Shapes/RedStarLargeB.png";
   // private string _redPushPinImage = "http://static.arcgis.com/images/Symbols/Basic/RedShinyPin.png";
    private string _makeFavoriteImage = "https://raw.githubusercontent.com/ThadT/bike-rental-stations-maui/main/MakeFavorite.png";
    private string _unFavoriteImage = "https://raw.githubusercontent.com/ThadT/bike-rental-stations-maui/main/UnFavorite.png";
    private CityBikesViewModel _vm;

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
        if (results.GeoElements.FirstOrDefault() is not DynamicEntityObservation bikeStation) { return; }

        var calloutDef = _vm.GetCalloutDefinitionForStation(bikeStation, _unFavoriteImage, _makeFavoriteImage);
        calloutDef.OnButtonClick = (tag) =>
        {
            // Add or remove this station from the favorites list.
            var isFavorite = _vm.ToggleIsFavorite(tag as DynamicEntity, CityPicker.SelectedItem.ToString());

            // Apply the correct image to the callout and show it again.
            calloutDef.ButtonImage = isFavorite ?
                                       new RuntimeImage(new Uri(_unFavoriteImage)) :
                                       new RuntimeImage(new Uri(_makeFavoriteImage));
            mapView.ShowCalloutAt(e.Location, calloutDef);
        };
        mapView.ShowCalloutAt(e.Location, calloutDef);
    }

    private async void CityPicker_SelectedIndexChanged(object sender, EventArgs e)
    {
        // Get the selected city name and pass it to the VM to show the stations for that city.
        var cityName = CityPicker.SelectedItem.ToString();
        var viewpoint = await _vm.ShowBikeStations(cityName);

        mapView.SetViewpoint(viewpoint);
    }
}
