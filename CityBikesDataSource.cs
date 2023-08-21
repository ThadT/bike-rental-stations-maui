using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.RealTime;
using System.Diagnostics;
using System.Text.Json;

namespace BikeRentalStations;

internal class CityBikesDataSource : DynamicEntityDataSource
{
    private readonly IDispatcherTimer _checkBikesTimer = Application.Current.Dispatcher.CreateTimer();
    private readonly string _cityBikesUrl;
    private readonly Dictionary<string, Dictionary<string, object>> _latestObservations = new();

    public CityBikesDataSource(string cityBikesUrl, int updateIntervalSeconds)
    {
        _checkBikesTimer.Interval = TimeSpan.FromSeconds(updateIntervalSeconds);
        _cityBikesUrl = cityBikesUrl;
        _checkBikesTimer.Tick += (s, e) => { PullBikeUpdates(); };
    }

    protected override Task OnConnectAsync(CancellationToken cancellationToken)
    {
        // Get the initial set of bike stations.
        PullBikeUpdates();

        // Start the timer to pull updates periodically.
        _checkBikesTimer.Start();

        return Task.CompletedTask;
    }

    protected override Task OnDisconnectAsync()
    {
        _checkBikesTimer.Stop();
        _latestObservations.Clear();

        return Task.CompletedTask;
    }

    protected override Task<DynamicEntityDataSourceInfo> OnLoadAsync()
    {
        // When the data source is loaded, create metadata that defines:
        // - A schema (fields) for the entities (bike stations) and their obserations
        // - Which field uniquely identifies entities (StationID)
        // - The spatial reference for the station locations (WGS84)
        var fields = new List<Field>
        {
            new Field(FieldType.Text, "StationID", "", 50),
            new Field(FieldType.Text, "StationName", "", 125),
            new Field(FieldType.Text, "Address", "", 125),
            new Field(FieldType.Text, "TimeStamp", "", 50),
            new Field(FieldType.Float32, "Longitude", "", 0),
            new Field(FieldType.Float32, "Latitude", "", 0),
            new Field(FieldType.Int32, "BikesAvailable", "", 0),
            new Field(FieldType.Int32, "EBikesAvailable", "", 0),
            new Field(FieldType.Int32, "EmptySlots", "", 0),
            new Field(FieldType.Text, "ObservationID", "", 50),
            new Field(FieldType.Int32, "InventoryChange", "", 0),
            new Field(FieldType.Text, "ImageUrl", "", 255)
        };
        var info = new DynamicEntityDataSourceInfo("StationID", fields)
        {
            SpatialReference = SpatialReferences.Wgs84
        };

        return Task.FromResult(info);
    }

    private async void PullBikeUpdates()
    {
        if (this.ConnectionStatus == ConnectionStatus.Disconnected) { return; }

        try
        {
            var client = new HttpClient();
            HttpResponseMessage response = await client.GetAsync(new Uri(_cityBikesUrl));
            if (response.IsSuccessStatusCode)
            {
                // Get a response with the JSON for this bike network (including all stations).
                var cityBikeJson = await response.Content.ReadAsStringAsync();

                // Get the "stations" portion of the JSON and deserialize the list of stations.
                var stationsStartPos = cityBikeJson.IndexOf(@"""stations"":[") + 11;
                var stationsEndPos = cityBikeJson.LastIndexOf(@"]") + 1;
                var stationsJson = cityBikeJson[stationsStartPos..stationsEndPos];
                var observations = JsonSerializer.Deserialize<List<BikeStation>>(stationsJson);

                var totalInventoryChange = 0;
                foreach (var observation in observations)
                {
                    var attributes = new Dictionary<string, object>
                    {
                        { "StationID", observation.StationInfo.StationID },
                        { "StationName", observation.StationName },
                        { "Address", observation.StationInfo.Address },
                        { "TimeStamp", observation.TimeStamp },
                        { "Longitude", observation.Longitude },
                        { "Latitude", observation.Latitude },
                        { "BikesAvailable", observation.BikesAvailable },
                        { "EBikesAvailable", observation.StationInfo.EBikesAvailable },
                        { "EmptySlots", observation.EmptySlots },
                        { "ObservationID", observation.ObservationID },
                        { "InventoryChange", 0 },
                        { "ImageUrl", "https://static.arcgis.com/images/Symbols/Transportation/esriDefaultMarker_189.png" }
                    };
                    var location = new MapPoint(observation.Longitude, observation.Latitude, SpatialReferences.Wgs84);

                    _latestObservations.TryGetValue(attributes["StationID"].ToString(), out Dictionary<string, object> lastObservation);
                    if (lastObservation is not null)
                    {
                        // Check if the new observation has different values for BikesAvailable or EBikesAvailable.
                        if ((int)attributes["BikesAvailable"] != (int)lastObservation["BikesAvailable"] ||
                                                           (int)attributes["EBikesAvailable"] != (int)lastObservation["EBikesAvailable"])
                        {
                            // Calculate the change in inventory.
                            var stationInventoryChange = (int)attributes["BikesAvailable"] - (int)lastObservation["BikesAvailable"];
                            attributes["InventoryChange"] = stationInventoryChange;
                            totalInventoryChange += stationInventoryChange;

                            // Add the observation to the data source.
                            AddObservation(location, attributes);
                        }

                        // Update the latest observation for this station.
                        _latestObservations[attributes["StationID"].ToString()] = attributes;
                    } 
                    else
                    {
                        _latestObservations.Add(attributes["StationID"].ToString(), attributes);
                        // Add the observation to the data source.
                        AddObservation(location, attributes);
                    }
                }

                Debug.WriteLine($"Total inventory change: {totalInventoryChange}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(@"\tERROR {0}", ex.Message);
        }
    }
}
