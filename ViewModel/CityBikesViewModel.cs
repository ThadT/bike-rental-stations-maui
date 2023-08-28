using CommunityToolkit.Mvvm.ComponentModel;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.RealTime;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.UI;

namespace BikeAvailability.ViewModel;

public partial class CityBikesViewModel: ObservableObject
{
    // A custom DynamicEntityDataSource for showing bike rental stations.
    private CityBikesDataSource _cityBikesDataSource;
    // A DynamicEntityLayer to handle display of dynamic entities from the data source.
    private DynamicEntityLayer _dynamicEntityLayer;

    public CityBikesViewModel()
    {
        Init();
    }

    [ObservableProperty]
    private Esri.ArcGISRuntime.Mapping.Map _map;

    [ObservableProperty]
    private List<string> _cityList;

    [ObservableProperty]
    private int _updateIntervalSeconds = 300; // data are updated every 5 minutes.

    private readonly Dictionary<long, Favorite> _favoriteBikeStations = new();

    private readonly Dictionary<long, DynamicEntity> _dynamicEntities = new();

    [ObservableProperty]
    private List<Favorite> _favoriteList = new();

    private Dictionary<string, Tuple<string, MapPoint>> _cityBikeStations;

    private void Init()
    {
        // A list of available cities to show in the app, along with their location and REST endpoint URL.
        _cityBikeStations = new Dictionary<string, Tuple<string, MapPoint>>
        {
            {"Milan", new Tuple<string, MapPoint>("https://api.citybik.es/v2/networks/bikemi", new MapPoint(9.1865, 45.4654, SpatialReferences.Wgs84))},
            {"Los Angeles", new Tuple<string, MapPoint>("https://api.citybik.es/v2/networks/metro-bike-share", new MapPoint(-118.2437, 34.0522, SpatialReferences.Wgs84))},
            {"Mexico City", new Tuple<string, MapPoint>("https://api.citybik.es/v2/networks/ecobici", new MapPoint(-99.1332,19.4326, SpatialReferences.Wgs84))},
            {"New York", new Tuple<string, MapPoint>("https://api.citybik.es/v2/networks/citi-bike-nyc", new MapPoint(-74.0060, 40.7128, SpatialReferences.Wgs84))},
            {"Paris", new Tuple<string, MapPoint>("https://api.citybik.es/v2/networks/velib", new MapPoint(2.3522, 48.8566, SpatialReferences.Wgs84))},
            {"Montreal", new Tuple<string, MapPoint>("https://api.citybik.es/v2/networks/bixi-montreal", new MapPoint(-73.5539, 45.5086, SpatialReferences.Wgs84))},
            {"Washington DC", new Tuple<string, MapPoint>("https://api.citybik.es/v2/networks/capital-bikeshare", new MapPoint(-77.0369, 38.9072, SpatialReferences.Wgs84))}
        };

        // Show the city names as a list in the dropdown.
        CityList = _cityBikeStations.Keys.ToList();

        // Create a new map with a dark navigation basemap.
        Map = new Esri.ArcGISRuntime.Mapping.Map(BasemapStyle.ArcGISNavigationNight);
    }

    public async Task<Viewpoint> ShowBikeStations(string cityName)
    {
        // Get the city's REST URL and location (map point).
        var cityInfo = _cityBikeStations[cityName];
        var cityBikesUrl = cityInfo.Item1;
        var cityLocation = cityInfo.Item2;

        // Clean up any existing CityBikesDataSource.
        if (_cityBikesDataSource != null)
        {
            await _cityBikesDataSource.DisconnectAsync();
            _cityBikesDataSource = null;
        }

        // Create an instance of the custom dynamic entity data source with the URL and interval.
        _cityBikesDataSource = new CityBikesDataSource(cityBikesUrl, UpdateIntervalSeconds);
        
        // Remove the existing dynamic entity layer from the map.
        Map.OperationalLayers.Remove(_dynamicEntityLayer);
        _dynamicEntityLayer = null;

        // Clear any favorites added for another city.
        _favoriteBikeStations.Clear();
        _dynamicEntities.Clear();

        // Create a new DynamicEntityLayer with the new CityBikesDataSource and add it to the map.
        _dynamicEntityLayer = new DynamicEntityLayer(_cityBikesDataSource)
        {
            Renderer = CreateBikeStationsRenderer()
        };
        Map.OperationalLayers.Add(_dynamicEntityLayer);

        // Return a viewpoint for the city location.
        return new Viewpoint(cityLocation, 130000);
    }

    private static Renderer CreateBikeStationsRenderer()
    {
        // Create a render that shows bike stations according to how many bikes are available.
        // The more bikes available, the larger the circle.
        var classBreaksRenderer = new ClassBreaksRenderer
        {
            FieldName = "BikesAvailable"
        };
        var noneSymbol = new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.Circle, System.Drawing.Color.LightGray, 10);
        var fewSymbol = new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.Circle, System.Drawing.Color.LightYellow, 12);
        var lotsSymbol = new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.Circle, System.Drawing.Color.LightGreen, 14);
        var plentySymbol = new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.Circle, System.Drawing.Color.Green, 16);
        var classBreakNo = new ClassBreak("no bikes", "None", 0, 0, noneSymbol);
        var classBreakFew = new ClassBreak("1-4 bikes", "A few", 1, 4, fewSymbol);
        var classBreakLots = new ClassBreak("5-8 bikes", "Lots", 5, 8, lotsSymbol);
        var classBreakPlenty = new ClassBreak("9-999 bikes", "Plenty", 9, 999, plentySymbol);

        classBreaksRenderer.ClassBreaks.Add(classBreakNo);
        classBreaksRenderer.ClassBreaks.Add(classBreakFew);
        classBreaksRenderer.ClassBreaks.Add(classBreakLots);
        classBreaksRenderer.ClassBreaks.Add(classBreakPlenty);

        return classBreaksRenderer;
    }

    public CalloutDefinition GetCalloutDefinitionForStation(DynamicEntityObservation bikeStation,
        string favoriteIconUrl, string nonFavIconUrl)
    {
        var dynEntity = bikeStation.GetDynamicEntity();

        // Show a callout with the bike station name and the number of available bikes.
        var stationName = bikeStation.Attributes["StationName"].ToString();
        var availableBikes = bikeStation.Attributes["BikesAvailable"].ToString();
        var availableEBikes = bikeStation.Attributes["EBikesAvailable"].ToString();
        var calloutDef = new CalloutDefinition(stationName,
                             $"Bikes available: {availableBikes} ({availableEBikes} electric)")
        {
            ButtonImage = _favoriteBikeStations.ContainsKey(dynEntity.EntityId) ?
                                       new RuntimeImage(new Uri(favoriteIconUrl)) :
                                       new RuntimeImage(new Uri(nonFavIconUrl)),
            Tag = dynEntity
        };

        return calloutDef;
    }

    public CalloutDefinition GetCalloutDefinitionForStation(Favorite favoriteStation,
        string removeFavoriteIconUrl)
    {
        // Show a callout with the bike station name and the number of available bikes.
        var stationName = favoriteStation.Name;
        var availableBikes = favoriteStation.AvailableBikes;
        var availableEBikes = favoriteStation.AvailableEBikes;
        var calloutDef = new CalloutDefinition(stationName,
                             $"Bikes available: {availableBikes} ({availableEBikes} electric)")
        {
            ButtonImage = new RuntimeImage(new Uri(removeFavoriteIconUrl))
        };

        // Get the corresponding dynamic entity and set it as the callout definition tag.
        // (the click event code will use the tag to get the dynamic entity for this station).
        var dynamicEntityId = _favoriteBikeStations.FirstOrDefault(kvp => kvp.Value == favoriteStation).Key;
        var dynamicEntity = _dynamicEntities[dynamicEntityId];
        calloutDef.Tag = dynamicEntity;

        return calloutDef;
    }

    public bool ToggleIsFavorite(DynamicEntity station, string city)
    {
        var isFavorite = _favoriteBikeStations.ContainsKey(station.EntityId);
        if (isFavorite)
        {
           _favoriteBikeStations.Remove(station.EntityId);
            station.DynamicEntityChanged -= DynEntity_DynamicEntityChanged;
            _dynamicEntities.Remove(station.EntityId);
            isFavorite = false;
        }
        else
        {
            var favorite = new Favorite(city, station);
            _favoriteBikeStations.Add(station.EntityId, favorite);
            station.DynamicEntityChanged += DynEntity_DynamicEntityChanged;
            _dynamicEntities.Add(station.EntityId, station);
            isFavorite = true;
        }

        // Update the favorite list that's shown in the app (for this city).
        FavoriteList = _favoriteBikeStations.Values.Where(f => f.CityName == city).ToList();

        return isFavorite;
    }

    private void DynEntity_DynamicEntityChanged(object sender, DynamicEntityChangedEventArgs e)
    {
        // If an observation comes in for a favorite bike station, update it's attributes for display in the list.
        var newObs = e.ReceivedObservation;
        if (newObs == null) { return; }

        var dynEntity = sender as DynamicEntity;
        var id = dynEntity.Attributes["StationId"].ToString();
        var favorite = FavoriteList.FirstOrDefault(f => f.StationId == id);

        favorite?.UpdateFavorite(dynEntity);
    }
}
