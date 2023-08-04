﻿using CommunityToolkit.Mvvm.ComponentModel;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.RealTime;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.UI;
using System.ComponentModel;

namespace BikeRentalStations.ViewModel;

public partial class CityBikesViewModel: ObservableObject
{
    private CityBikesDataSource _cityBikesDataSource;
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
    private int _updateIntervalSeconds = 10;

    [ObservableProperty]
    private Dictionary<long, Favorite> _favoriteBikeStations = new();

    private Dictionary<string, Tuple<string, MapPoint>> _cityBikeStations;

    private void Init()
    {
        _cityBikeStations = new Dictionary<string, Tuple<string, MapPoint>>
        {
            {"Milano", new Tuple<string, MapPoint>("https://api.citybik.es/v2/networks/bikemi", new MapPoint(9.1865, 45.4654, SpatialReferences.Wgs84))},
            {"Los Angeles", new Tuple<string, MapPoint>("https://api.citybik.es/v2/networks/metro-bike-share", new MapPoint(-118.2437, 34.0522, SpatialReferences.Wgs84))},
            {"Mexico City", new Tuple<string, MapPoint>("https://api.citybik.es/v2/networks/ecobici", new MapPoint(-99.1332,19.4326, SpatialReferences.Wgs84))},
            {"New York", new Tuple<string, MapPoint>("https://api.citybik.es/v2/networks/citi-bike-nyc", new MapPoint(-74.0060, 40.7128, SpatialReferences.Wgs84))},
            {"Paris", new Tuple<string, MapPoint>("https://api.citybik.es/v2/networks/velib", new MapPoint(2.3522, 48.8566, SpatialReferences.Wgs84))},
            {"Montreal", new Tuple<string, MapPoint>("https://api.citybik.es/v2/networks/bixi-montreal", new MapPoint(-73.5539, 45.5086, SpatialReferences.Wgs84))},
            {"Washington DC", new Tuple<string, MapPoint>("https://api.citybik.es/v2/networks/capital-bikeshare", new MapPoint(-77.0369, 38.9072, SpatialReferences.Wgs84))}
        };

        CityList = _cityBikeStations.Keys.ToList();

        // Create a new map with a dark navigation basemap.
        Map = new Esri.ArcGISRuntime.Mapping.Map(BasemapStyle.ArcGISNavigationNight);
    }

    public async Task<Viewpoint> ShowBikeStations(string cityName)
    {
        var cityInfo = _cityBikeStations[cityName];
        var cityBikesUrl = cityInfo.Item1;
        var cityLocation = cityInfo.Item2;

        // Clean up any existing CityBikesDataSource.
        if (_cityBikesDataSource != null)
        {
            await _cityBikesDataSource.DisconnectAsync();
            _cityBikesDataSource = null;
        }

        // Create an instance of the custom dynamic entity data source
        _cityBikesDataSource = new CityBikesDataSource(cityBikesUrl, UpdateIntervalSeconds);
        _cityBikesDataSource.DynamicEntityReceived += (s, e) =>
        {
            var entity = e.DynamicEntity;
        };
        
        //_cityBikesDataSource.DynamicEntityObservationReceived += (s, e) =>
        //{
        //    var newObs = e.Observation;
        //    var dynEntity = newObs.GetDynamicEntity();
        //    _lastObsGraphic.Geometry = dynEntity.Geometry;

        //    if (_favoriteBikeStations.ContainsKey(dynEntity.EntityId))
        //    {

        //    }
        //};

        // Remove the existing dynamic entity layer from the map.
        Map.OperationalLayers.Remove(_dynamicEntityLayer);
        _dynamicEntityLayer = null;

        // Create a new DynamicEntityLayer with the new CityBikesDataSource and add it to the map.
        _dynamicEntityLayer = new DynamicEntityLayer(_cityBikesDataSource)
        {
            Renderer = CreateBikeStationsRenderer()
        };
        Map.OperationalLayers.Add(_dynamicEntityLayer);

        // Return a viewpoint for the city location.
        return new Viewpoint(cityLocation, 130000);
    }

    private Renderer CreateBikeStationsRenderer()
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
                             $"Bikes available: {availableBikes} ({availableEBikes} electric)");
        calloutDef.ButtonImage = FavoriteBikeStations.ContainsKey(dynEntity.EntityId) ? 
                                       new RuntimeImage(new Uri(favoriteIconUrl)) : 
                                       new RuntimeImage(new Uri(nonFavIconUrl));
        calloutDef.Tag = dynEntity;

        return calloutDef;
    }

    public bool CheckIsFavorite(DynamicEntity station, string city)
    {
        if (FavoriteBikeStations.ContainsKey(station.EntityId))
        {
            FavoriteBikeStations.Remove(station.EntityId);
            //dynEntity.DynamicEntityChanged -= DynEntity_DynamicEntityChanged;            
            return false;
        }
        else
        {
            var favorite = new Favorite(city, station);
            FavoriteBikeStations.Add(station.EntityId, favorite);
            //dynEntity.DynamicEntityChanged += DynEntity_DynamicEntityChanged;
            return true;
        }
    }

    //private void DynEntity_DynamicEntityChanged(object sender, DynamicEntityChangedEventArgs e)
    //{
    //    var newObs = e.ReceivedObservation;
    //    if (newObs == null) { return; }

    //    var dynEntity = sender as DynamicEntity;
    //    if (_favoriteBikeStations.ContainsKey(dynEntity.EntityId))
    //    {
    //    }
    //}
}
