using System.Text.Json.Serialization;

namespace BikeAvailability;

// A class to represent a bike station returned from a CityBikes API JSON response.
public class BikeStation
{
    [JsonPropertyName("extra")]
    public StationInfo StationInfo { get; set; }

    [JsonPropertyName("name")]
    public string StationName { get; set; }

    [JsonPropertyName("timestamp")]
    public string TimeStamp { get; set; }

    [JsonPropertyName("longitude")]
    public double Longitude { get; set; }

    [JsonPropertyName("latitude")]
    public double Latitude { get; set; }

    [JsonPropertyName("free_bikes")]
    public int BikesAvailable { get; set; }

    [JsonPropertyName("empty_slots")]
    public int EmptySlots { get; set; }

    [JsonPropertyName("id")]
    public string ObservationID { get; set; }
}

// A class to represent the "extra" information for a bike station returned from a CityBikes API JSON response.
public class StationInfo
{
    [JsonPropertyName("uid")]
    public string StationID { get; set; }

    [JsonPropertyName("address")]
    public string Address { get; set; }

    [JsonPropertyName("ebikes")]
    public int EBikesAvailable { get; set; }
}
