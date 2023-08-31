using BikeAvailability.ViewModel;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.RealTime;
using Esri.ArcGISRuntime.UI;

namespace BikeAvailability;

public partial class MainPage : ContentPage, IQueryAttributable
{
    // Images to use for the "add favorite" and "remove from favorites" buttons.
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
        
        var dynamicEntityLayer = mapView.Map.OperationalLayers.OfType<DynamicEntityLayer>().FirstOrDefault();

        // Identify city graphics if they are displayed on the map.
        if (mapView.GraphicsOverlays["CitiesOverlay"].MaxScale < mapView.GetCurrentViewpoint(ViewpointType.CenterAndScale).TargetScale)
        {
            var results = await mapView.IdentifyGraphicsOverlayAsync(mapView.GraphicsOverlays["CitiesOverlay"], e.Position, 4, false);
            if (results.Graphics.Count == 0) { return; }

            // Load the bike stations for the city that was clicked.
            var cityGraphic = results.Graphics[0];
            var cityName = cityGraphic.Attributes["Name"].ToString();
            CityPicker.SelectedItem = cityName;
        }
        else if (dynamicEntityLayer != null)
        {
            // Identify a bike station from the tap.
            var results = await mapView.IdentifyLayerAsync(dynamicEntityLayer, e.Position, 4, false, 1);
            if (results.GeoElements.Count == 0 || results.GeoElements[0] is not DynamicEntityObservation bikeStation) { return; }

            // Get a callout definition from the view model.
            var calloutDef = _vm.GetCalloutDefinitionForStation(bikeStation, _unFavoriteImage, _makeFavoriteImage);
            // Set the button click to add/remove this station as a favorite.
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
            // Show the callout.
            mapView.ShowCalloutAt(bikeStation.Geometry as MapPoint, calloutDef);
        } 
    }

    private async void CityPicker_SelectedIndexChanged(object sender, EventArgs e)
    {
        // Get the selected city name and pass it to the VM to show the stations for that city.
        var cityName = CityPicker.SelectedItem.ToString();
        var viewpoint = await _vm.ShowBikeStations(cityName);

        // Zoom to the extent of the selected city.
        mapView.SetViewpoint(viewpoint);

        // Show bike inventory for the entire city.
        BikeInventoryPanel.IsVisible = true;
    }

    // Handle navigation from a button click in the favorites page to a station.
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query["favorite"] is DynamicEntity favorite)
        {
            var location = favorite.Geometry as MapPoint;
            mapView.SetViewpoint(new Viewpoint(location, 10000));

            // Close any currently open callout.
            mapView.DismissCallout();

            var calloutDef = CityBikesViewModel.GetCalloutDefinitionForStation(favorite, _unFavoriteImage);
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
