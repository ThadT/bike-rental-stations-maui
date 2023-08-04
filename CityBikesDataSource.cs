using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.RealTime;
using System.Diagnostics;
using System.Text.Json;

namespace BikeRentalStations;

internal class CityBikesDataSource : DynamicEntityDataSource
{
    private IDispatcherTimer _checkBikesTimer = Application.Current.Dispatcher.CreateTimer();
    private string _cityBikesUrl;
    private HttpClient _client;
    private JsonSerializerOptions _serializerOptions;
    private Dictionary<string, Dictionary<string, object>> _latestObservations = new();

    public CityBikesDataSource(string cityBikesUrl, int updateIntervalSeconds)
    {
        _checkBikesTimer.Interval = TimeSpan.FromSeconds(updateIntervalSeconds);
        _checkBikesTimer.Tick += (s, e) => PullBikeUpdates();
        _cityBikesUrl = cityBikesUrl;

        _client = new HttpClient();
        _serializerOptions = new JsonSerializerOptions
        {
            IncludeFields = true
        };
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

        return Task.CompletedTask;
    }

    protected override Task<DynamicEntityDataSourceInfo> OnLoadAsync()
    {
        // When the data source is loaded, create metadata that defines:
        // - A schema (fields) for the entities (bike stations) and their obserations
        // - Which field uniquely identifies entities (StationID)
        // - The spatial reference for the station locations (WGS84)
        var fields = GetCityBikeFields();
        var info = new DynamicEntityDataSourceInfo("StationID", fields)
        {
            SpatialReference = SpatialReferences.Wgs84
        };

        return Task.FromResult(info);
    }

    private List<Field> GetCityBikeFields()
    {
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
            new Field(FieldType.Text, "ObservationID", "", 50)
        };

        return fields;
    }

    private async void PullBikeUpdates()
    {
        if (this.ConnectionStatus == ConnectionStatus.Disconnected) { return; }

        try
        {
            HttpResponseMessage response = await _client.GetAsync(new Uri(_cityBikesUrl));
            if (response.IsSuccessStatusCode)
            {
                // Get a response with the JSON for this bike network (including all stations).
                var cityBikeJson = await response.Content.ReadAsStringAsync();

                // Get the "stations" portion of the JSON and deserialize the list of stations.
                var stationsStartPos = cityBikeJson.IndexOf(@"""stations"":[") + 11;
                var stationsEndPos = cityBikeJson.LastIndexOf(@"]") + 1;
                var stationsJson = cityBikeJson.Substring(stationsStartPos, stationsEndPos - stationsStartPos);
                var observations = JsonSerializer.Deserialize<List<BikeStation>>(stationsJson, _serializerOptions);
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
                        { "ObservationID", observation.ObservationID }
                    };
                    var location = new MapPoint(observation.Longitude, observation.Latitude, SpatialReferences.Wgs84);

                    _latestObservations.TryGetValue(attributes["StationID"].ToString(), out Dictionary<string, object> lastObservation);
                    if (lastObservation is not null)
                    {
                        // Check if the new observation has different values for BikesAvailable or EBikesAvailable.
                        if ((int)attributes["BikesAvailable"] != (int)lastObservation["BikesAvailable"] ||
                                                           (int)attributes["EBikesAvailable"] != (int)lastObservation["EBikesAvailable"])
                        {
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
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(@"\tERROR {0}", ex.Message);
        }
    }
}
