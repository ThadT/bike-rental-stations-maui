using BikeRentalStations.ViewModel;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.RealTime;
using Esri.ArcGISRuntime.UI;

namespace BikeRentalStations;

public partial class MainPage : ContentPage
{
    private Dictionary<long, Favorite> _favoriteBikeStations = new Dictionary<long, Favorite>();
    private string _bikeImage = "http://static.arcgis.com/images/Symbols/Transportation/esriDefaultMarker_189.png";
    private string _redStarImage = "http://static.arcgis.com/images/Symbols/Shapes/RedStarLargeB.png";
    private string _redPushPinImage = "http://static.arcgis.com/images/Symbols/Basic/RedShinyPin.png";

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

        var calloutDef = _vm.GetCalloutDefinitionForStation(bikeStation, _redStarImage, _bikeImage);
        calloutDef.OnButtonClick = (tag) =>
        {
            // Add or remove this station from the favorites list.
            var isFavorite = _vm.CheckIsFavorite(tag as DynamicEntity, CityPicker.SelectedItem.ToString());

            // Apply the correct image to the callout and show it again.
            calloutDef.ButtonImage = isFavorite ?
                                       new RuntimeImage(new Uri(_redStarImage)) :
                                       new RuntimeImage(new Uri(_bikeImage));
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
