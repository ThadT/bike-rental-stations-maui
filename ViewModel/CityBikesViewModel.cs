using CommunityToolkit.Mvvm.ComponentModel;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.RealTime;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.UI;

namespace BikeAvailability.ViewModel;

public partial class CityBikesViewModel : ObservableObject
{
    // A custom DynamicEntityDataSource for showing bike rental stations.
    private CityBikesDataSource _cityBikesDataSource;
    // A DynamicEntityLayer to handle display of dynamic entities from the data source.
    private DynamicEntityLayer _dynamicEntityLayer;
    private readonly object _thisLock = new();

    public CityBikesViewModel()
    {
        Init();
    }

    [ObservableProperty]
    private Esri.ArcGISRuntime.Mapping.Map _map;

    [ObservableProperty]
    private List<string> _cityList;

    [ObservableProperty]
    private string _cityName;

    [ObservableProperty]
    private int _updateIntervalSeconds = 240; // 4 minutes

    private readonly Dictionary<long, DynamicEntity> _favoriteBikeStations = new();

    [ObservableProperty]
    private List<DynamicEntity> _favoriteList = new();

    [ObservableProperty]
    private GraphicsOverlayCollection _graphicsOverlays = new();

    private Dictionary<string, Tuple<string, MapPoint>> _cityBikeStations;

    // Graphics overlay to show cities with bike station info.
    // (this is only shown before the user selects a city from the drop down)
    private GraphicsOverlay _citiesGraphicsOverlay;

    // Graphics overlay for flashing stations that have an inventory change.
    private GraphicsOverlay _flashOverlay;

    // Variables to track bike inventory.
    [ObservableProperty]
    private int _totalBikes;
    [ObservableProperty]
    private int _bikesOut;
    [ObservableProperty]
    private int _bikesAvailable;
    [ObservableProperty]
    private double _percentBikesAvailable;

    private void Init()
    {
        // A list of available cities to show in the app, along with their location and REST endpoint URL.
        _cityBikeStations = new Dictionary<string, Tuple<string, MapPoint>>
        {
            {"Milan", new Tuple<string, MapPoint>("https://api.citybik.es/v2/networks/bikemi", new MapPoint(9.1865, 45.4654, SpatialReferences.Wgs84))},
            {"Los Angeles", new Tuple<string, MapPoint>("https://api.citybik.es/v2/networks/metro-bike-share", new MapPoint(-118.2437, 34.0522, SpatialReferences.Wgs84))},
            {"Mexico City", new Tuple<string, MapPoint>("https://api.citybik.es/v2/networks/ecobici", new MapPoint(-99.1332, 19.4326, SpatialReferences.Wgs84))},
            {"New York", new Tuple<string, MapPoint>("https://api.citybik.es/v2/networks/citi-bike-nyc", new MapPoint(-74.0060, 40.7128, SpatialReferences.Wgs84))},
            {"Paris", new Tuple<string, MapPoint>("https://api.citybik.es/v2/networks/velib", new MapPoint(2.3522, 48.8566, SpatialReferences.Wgs84))},
            {"Montreal", new Tuple<string, MapPoint>("https://api.citybik.es/v2/networks/bixi-montreal", new MapPoint(-73.5539, 45.5086, SpatialReferences.Wgs84))},
            {"Washington DC", new Tuple<string, MapPoint>("https://api.citybik.es/v2/networks/capital-bikeshare", new MapPoint(-77.0369, 38.9072, SpatialReferences.Wgs84))}
        };

        // Show the city names as a list in the dropdown.
        CityList = _cityBikeStations.Keys.ToList();

        // Create a new map with a dark navigation basemap.
        Map = new Esri.ArcGISRuntime.Mapping.Map(BasemapStyle.ArcGISNavigationNight);

        // Create an overlay for flashing updated features.
        _flashOverlay = new GraphicsOverlay();

        // Create an overlay to show the city locations.
        _citiesGraphicsOverlay = new GraphicsOverlay
        {
            Id = "CitiesOverlay",
            MaxScale = 1000000,
            Renderer = new SimpleRenderer(new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.Circle, System.Drawing.Color.Yellow, 26))
        };

        // Add each city as a graphic in the cities overlay, add attributes for the name and URL.
        foreach (var city in _cityBikeStations.Keys)
        {
            var cityInfo = _cityBikeStations[city];
            var g = new Graphic(cityInfo.Item2);
            g.Attributes.Add("Name", city);
            g.Attributes.Add("URL", cityInfo.Item1);
            _citiesGraphicsOverlay.Graphics.Add(g);
        }

        GraphicsOverlays.Add(_citiesGraphicsOverlay);
        GraphicsOverlays.Add(_flashOverlay);

        var citiesExtent = GeometryEngine.CombineExtents(_citiesGraphicsOverlay.Graphics.Select(g => g.Geometry));
        var envBldr = new EnvelopeBuilder(citiesExtent);
        envBldr.Expand(1.1);
        Map.InitialViewpoint = new Viewpoint(envBldr.ToGeometry());
    }

    public async Task<Viewpoint> ShowBikeStations(string cityName)
    {
        // Store the city name.
        CityName = cityName;

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

        // Clear inventory values.
        TotalBikes = 0;
        BikesAvailable = 0;
        BikesOut = 0;

        // Create an instance of the custom dynamic entity data source with the URL and interval.
        _cityBikesDataSource = new CityBikesDataSource(cityName, cityBikesUrl, UpdateIntervalSeconds);

        // When the connection is established, request an initial set of data.
        _cityBikesDataSource.ConnectionStatusChanged += (s, e) =>
        {
            if (e == ConnectionStatus.Connected)
            {
                _ = _cityBikesDataSource.GetInitialBikeStations();
            }
        };

        // Listen for dynamic entities being created, calculate the initial bike inventory.
        _cityBikesDataSource.DynamicEntityReceived += (s, e) => CreateTotalBikeInventory(e.DynamicEntity);

        // Listen for new observations; flash the station and update inventory if there's an update.
        _cityBikesDataSource.DynamicEntityObservationReceived += async (s, e) =>
        {
            var bikesAdded = (int)e.Observation.Attributes["InventoryChange"];
            if (bikesAdded == 0) { return; }

            UpdateBikeInventory(bikesAdded); // note: this might be negative if more bikes were taken than returned.
            await Task.Run(() => FlashDynamicEntityObservationAsync(e.Observation.Geometry as MapPoint, bikesAdded > 0));
        };

        // Remove the existing dynamic entity layer from the map.
        Map.OperationalLayers.Remove(_dynamicEntityLayer);
        _dynamicEntityLayer = null;

        // Filter the favorites list for this city.
        FavoriteList = _favoriteBikeStations.Values.Where(f => f.Attributes["CityName"].ToString() == cityName).ToList();

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
        var defaultSymbol = new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.Triangle, System.Drawing.Color.Beige, 14);

        var classBreakNo = new ClassBreak("no bikes", "None", 0, 0, noneSymbol);
        var classBreakFew = new ClassBreak("1-4 bikes", "A few", 0, 4, fewSymbol);
        var classBreakLots = new ClassBreak("5-8 bikes", "Lots", 4, 9, lotsSymbol);
        var classBreakPlenty = new ClassBreak("9-999 bikes", "Plenty", 9, 999, plentySymbol);

        classBreaksRenderer.ClassBreaks.Add(classBreakNo);
        classBreaksRenderer.ClassBreaks.Add(classBreakFew);
        classBreaksRenderer.ClassBreaks.Add(classBreakLots);
        classBreaksRenderer.ClassBreaks.Add(classBreakPlenty);
        classBreaksRenderer.DefaultSymbol = defaultSymbol;

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

    public static CalloutDefinition GetCalloutDefinitionForStation(DynamicEntity favoriteStation,
        string removeFavoriteIconUrl)
    {
        // Show a callout with the bike station name and the number of available bikes.
        var stationName = favoriteStation.Attributes["StationName"].ToString();
        var availableBikes = (int)favoriteStation.Attributes["BikesAvailable"];
        var availableEBikes = (int)favoriteStation.Attributes["EBikesAvailable"];
        var calloutDef = new CalloutDefinition(stationName,
                             $"Bikes available: {availableBikes} ({availableEBikes} electric)")
        {
            ButtonImage = new RuntimeImage(new Uri(removeFavoriteIconUrl)),
            // Set the dynamic entity as the callout definition tag.
            // (the click event code will use the tag to get the dynamic entity).
            Tag = favoriteStation
        };

        return calloutDef;
    }

    public bool ToggleIsFavorite(DynamicEntity station, string city)
    {
        var isFavorite = _favoriteBikeStations.ContainsKey(station.EntityId);
        if (isFavorite)
        {
            _favoriteBikeStations.Remove(station.EntityId);
            station.DynamicEntityChanged -= DynEntity_DynamicEntityChanged;
            isFavorite = false;
        }
        else
        {
            _favoriteBikeStations.Add(station.EntityId, station);
            station.DynamicEntityChanged += DynEntity_DynamicEntityChanged;
            isFavorite = true;
        }

        // Update the favorite list that's shown in the app (for this city).
        FavoriteList = _favoriteBikeStations.Values.Where(f => f.Attributes["CityName"].ToString() == city).ToList();

        return isFavorite;
    }

    private void DynEntity_DynamicEntityChanged(object sender, DynamicEntityChangedEventArgs e)
    {
        // TODO: handle changes to bike inventory for favorite stations.
        //    Perhaps highlight or flash the card in the UI.
    }

    private void CreateTotalBikeInventory(DynamicEntity bikeStation)
    {
        var emptySlots = (int)bikeStation.Attributes["EmptySlots"];
        var availableBikes = (int)bikeStation.Attributes["BikesAvailable"] +
            (int)bikeStation.Attributes["EBikesAvailable"];

        TotalBikes += emptySlots + availableBikes;
        BikesAvailable += availableBikes;

        BikesOut = TotalBikes - BikesAvailable;
        PercentBikesAvailable = (double)BikesAvailable / TotalBikes;
    }

    private void UpdateBikeInventory(int inventoryChange)
    {
        BikesAvailable += inventoryChange;
        BikesOut = TotalBikes - BikesAvailable;
        PercentBikesAvailable = BikesAvailable / TotalBikes;
    }

    private async Task FlashDynamicEntityObservationAsync(MapPoint point, bool bikeAdded)
    {
        // When an observation comes in, flash it on the map: green if more available bikes, red for less
        Graphic halo = null;
        try
        {
            var attr = new Dictionary<string, object>
            {
                { "BikesAdded", bikeAdded }
            };
            var bikeAddedSymbol = new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.Circle, System.Drawing.Color.FromArgb(128, System.Drawing.Color.Blue), 18);
            var bikeTakenSymbol = new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.Circle, System.Drawing.Color.FromArgb(128, System.Drawing.Color.Red), 18);
            var bikeSym = bikeAdded ? bikeAddedSymbol : bikeTakenSymbol;
            halo = new Graphic(point, bikeSym) { IsVisible = false };
            lock (_thisLock)
            {
                _flashOverlay.Graphics.Add(halo);
            }
            for (var n = 0; n < 3; ++n)
            {
                halo.IsVisible = true;
                await Task.Delay(100);
                halo.IsVisible = false;
                await Task.Delay(100);
            }
        }
        catch
        {
            // Ignore
        }
        finally
        {
            try
            {
                lock (_thisLock)
                {
                    if (halo != null && _flashOverlay.Graphics.Contains(halo))
                    {
                        _flashOverlay.Graphics.Remove(halo);
                    }
                }
            }
            catch
            {
            }
        }
    }
}
