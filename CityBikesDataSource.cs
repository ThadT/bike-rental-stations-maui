using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.RealTime;
using System.Diagnostics;
using System.Text.Json;

namespace BikeAvailability;

internal class CityBikesDataSource : DynamicEntityDataSource
{
    // Timer to request updates at a given interval.
    private readonly IDispatcherTimer _getBikeUpdatesTimer = Application.Current.Dispatcher.CreateTimer();
    // REST endpoint for one of the cities described by the CityBikes API (http://api.citybik.es/).
    private readonly string _cityBikesUrl;
    // Dictionary of previous observations for bike stations (to evaluate change in inventory).
    private readonly Dictionary<string, Dictionary<string, object>> _previousObservations = new();
    // Name of the city.
    private readonly string _cityName;
    // Timer and related variables used to display observations at a consitent interval.
    private readonly IDispatcherTimer _addBikeUpdatesTimer = Application.Current.Dispatcher.CreateTimer();
    private readonly List<Tuple<MapPoint, Dictionary<string, object>>> _currentObservations = new();
    private readonly bool _showSmoothUpdates;

    public CityBikesDataSource(string cityName, string cityBikesUrl,
        int updateIntervalSeconds, bool smoothUpdateDisplay = true)
    {
        // Store the name of the city.
        _cityName = cityName;
        // Store the timer interval (how often to request updates from the URL).
        _getBikeUpdatesTimer.Interval = TimeSpan.FromSeconds(updateIntervalSeconds);
        // URL for a specific city's bike rental stations.
        _cityBikesUrl = cityBikesUrl;
        // Set the function that will run at each timer interval.
        _getBikeUpdatesTimer.Tick += (s, e) => _ = PullBikeUpdates();
        // Store whether updates should be shown consitently over time or when the first arrive.
        _showSmoothUpdates = smoothUpdateDisplay;
        if (smoothUpdateDisplay)
        {
            // _addBikeUpdatesTimer.Interval = TimeSpan.FromSeconds(3);
            _addBikeUpdatesTimer.Tick += (s, e) => AddBikeObservations();
        }
    }

    protected override Task OnConnectAsync(CancellationToken cancellationToken)
    {
        // Start the timer to pull updates periodically.
        _getBikeUpdatesTimer.Start();

        return Task.CompletedTask;
    }

    protected override Task OnDisconnectAsync()
    {
        // Stop the timers (suspend update requests).
        _getBikeUpdatesTimer.Stop();
        _addBikeUpdatesTimer.Stop();

        // Clear the dictionary of previous observations.
        _previousObservations.Clear();

        return Task.CompletedTask;
    }

    protected override Task<DynamicEntityDataSourceInfo> OnLoadAsync()
    {
        // When the data source is loaded, create metadata that defines:
        // - A schema (fields) for the entities (bike stations) and their observations
        // - Which field uniquely identifies entities (StationID)
        // - The spatial reference for the station locations (WGS84)
        var fields = new List<Field>
        {
            new Field(FieldType.Text, "StationID", "", 50),
            new Field(FieldType.Text, "StationName", "", 125),
            new Field(FieldType.Text, "Address", "", 125),
            new Field(FieldType.Date, "TimeStamp", "", 0),
            new Field(FieldType.Float32, "Longitude", "", 0),
            new Field(FieldType.Float32, "Latitude", "", 0),
            new Field(FieldType.Int32, "BikesAvailable", "", 0),
            new Field(FieldType.Int32, "EBikesAvailable", "", 0),
            new Field(FieldType.Int32, "EmptySlots", "", 0),
            new Field(FieldType.Text, "ObservationID", "", 50),
            new Field(FieldType.Int32, "InventoryChange", "", 0),
            new Field(FieldType.Text, "ImageUrl", "", 255),
            new Field(FieldType.Text, "CityName", "", 50)
        };
        var info = new DynamicEntityDataSourceInfo("StationID", fields)
        {
            SpatialReference = SpatialReferences.Wgs84
        };

        return Task.FromResult(info);
    }

    private async Task PullBikeUpdates()
    {
        // Exit if the data source is not connected.
        if (this.ConnectionStatus != ConnectionStatus.Connected) { return; }

        try
        {
            // Stop the timer that adds observations while getting updates.
            _addBikeUpdatesTimer.Stop();

            // If showing consistent updates, process any remaining ones from the last update.
            if (_showSmoothUpdates)
            {
                for (int i = _currentObservations.Count - 1; i > 0; i--)
                {
                    var obs = _currentObservations[i];
                    AddObservation(obs.Item1, obs.Item2);
                    _currentObservations.Remove(obs);
                }
            }

            // Call a function to get a set of bike stations (locations and attributes).
            var bikeUpdates = await GetDeserializedCityBikeResponse();
            var updatedStationCount = 0;
            var totalInventoryChange = 0;

            // Iterate the info for each station.
            foreach (var update in bikeUpdates)
            {
                // Get the location, attributes, and ID for this station.
                var location = update.Item1;
                var attributes = update.Item2;
                var id = attributes["StationID"].ToString();

                // Get the last set of values for this station (if they exist).
                _previousObservations.TryGetValue(id, out Dictionary<string, object> lastObservation);
                if (lastObservation is not null)
                {
                    // Check if the new update has different values for BikesAvailable or EBikesAvailable.
                    if ((int)attributes["BikesAvailable"] != (int)lastObservation["BikesAvailable"] ||
                        (int)attributes["EBikesAvailable"] != (int)lastObservation["EBikesAvailable"])
                    {
                        // Calculate the change in inventory.
                        var stationInventoryChange = (int)attributes["BikesAvailable"] - (int)lastObservation["BikesAvailable"];
                        attributes["InventoryChange"] = stationInventoryChange;
                        totalInventoryChange += stationInventoryChange;
                        updatedStationCount++;

                        // If showing updates immediately, add the update to the data source.
                        if (!_showSmoothUpdates)
                        {
                            AddObservation(location, attributes);
                        }
                        else
                        {
                            // If showing smooth (consistent) updates, add to the current observations list for processing.
                            var observation = new Tuple<MapPoint, Dictionary<string, object>>(location, attributes);
                            _currentObservations.Add(observation);
                        }
                    }

                    // Update the latest update for this station.
                    _previousObservations[id] = attributes;
                }
            }

            // If showing consistent updates, set up the timer for adding observations to the data source.
            if (_showSmoothUpdates)
            {
                if (_currentObservations.Count > 0)
                {
                    var updatesPerSecond = (int)Math.Ceiling(_currentObservations.Count / _getBikeUpdatesTimer.Interval.TotalSeconds);
                    if (updatesPerSecond > 0)
                    {
                        long ticksPerUpdate = 10000000 / updatesPerSecond;
                        _addBikeUpdatesTimer.Interval = TimeSpan.FromTicks(ticksPerUpdate);
                        _addBikeUpdatesTimer.Start(); // Tick event will add one update.
                    }

                    Debug.WriteLine($"**** Stations from this update = {updatedStationCount}, total to process = {_currentObservations.Count}");
                }
            }

            Debug.WriteLine($"**** Total inventory change: {totalInventoryChange} for {updatedStationCount} stations");
        }
        catch (Exception ex)
        {
            Debug.WriteLine(@"\tERROR {0}", ex.Message);
        }
    }

    private void AddBikeObservations()
    {
        // Add one observation on the timer interval.
        // The interval was determined to spread these additions over the span required to get the next updates.
        if (_currentObservations.Count > 0)
        {
            var obs = _currentObservations[^1];
            AddObservation(obs.Item1, obs.Item2);
            _currentObservations.Remove(obs);
        }
    }

    public async Task GetInitialBikeStations()
    {
        // Exit if the data source is not connected.
        if (this.ConnectionStatus != ConnectionStatus.Connected) { return; }

        try
        {
            // Call a function to get a set of bike stations (locations and attributes).
            var bikeUpdates = await GetDeserializedCityBikeResponse();

            // Iterate the info for each station.
            foreach (var update in bikeUpdates)
            {
                var location = update.Item1;
                var attributes = update.Item2;

                // Update the latest update for this station.
                _previousObservations[attributes["StationID"].ToString()] = attributes;

                // Add the update to the data source.
                AddObservation(location, attributes);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(@"\tERROR {0}", ex.Message);
        }
    }

    private async Task<List<Tuple<MapPoint, Dictionary<string, object>>>> GetDeserializedCityBikeResponse()
    {
        // Deserialize a response from CityBikes as a list of bike station locations and attributes.
        List<Tuple<MapPoint, Dictionary<string, object>>> bikeInfo = new();

        try
        {
            // Get a JSON response from the REST service.
            var client = new HttpClient();
            HttpResponseMessage response = await client.GetAsync(new Uri(_cityBikesUrl));
            if (response.IsSuccessStatusCode)
            {
                // Read the JSON response for this bike network (including all stations).
                var cityBikeJson = await response.Content.ReadAsStringAsync();

                // Get the "stations" portion of the JSON and deserialize the list of stations.
                var stationsStartPos = cityBikeJson.IndexOf(@"""stations"":[") + 11;
                var stationsEndPos = cityBikeJson.LastIndexOf(@"]") + 1;
                var stationsJson = cityBikeJson[stationsStartPos..stationsEndPos];
                var bikeUpdates = JsonSerializer.Deserialize<List<BikeStation>>(stationsJson);

                // Iterate the info for each station.
                foreach (var update in bikeUpdates)
                {
                    // Build a dictionary of attributes from the response.
                    var attributes = new Dictionary<string, object>
                    {
                        { "StationID", update.StationInfo.StationID },
                        { "StationName", update.StationName },
                        { "Address", update.StationInfo.Address },
                        { "TimeStamp", DateTime.Parse(update.TimeStamp) },
                        { "Longitude", update.Longitude },
                        { "Latitude", update.Latitude },
                        { "BikesAvailable", update.BikesAvailable },
                        { "EBikesAvailable", update.StationInfo.EBikesAvailable },
                        { "EmptySlots", update.EmptySlots },
                        { "ObservationID", update.ObservationID },
                        { "InventoryChange", 0 },
                        { "ImageUrl", "https://static.arcgis.com/images/Symbols/Transportation/esriDefaultMarker_189.png" },
                        { "CityName", _cityName }
                    };
                    // Create a map point from the longitude (x) and latitude (y) values.
                    var location = new MapPoint(update.Longitude, update.Latitude, SpatialReferences.Wgs84);

                    // Add this bike station's info to the list.
                    bikeInfo.Add(new Tuple<MapPoint, Dictionary<string, object>>(location, attributes));
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(@"\tERROR {0}", ex.Message);
        }

        return bikeInfo;
    }
}
