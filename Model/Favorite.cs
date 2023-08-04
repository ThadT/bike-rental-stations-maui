using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.RealTime;

namespace BikeRentalStations;

// Create a class called Favorite that has the following properties:
// - Name (string)
// - Location (MapPoint)
// - CityName (string)
// - AvailableBikes (int)
// - AvailableEBikes (int)
// Make the class public and add a default constructor that sets the properties to default values.
// Add a constructor that takes a DynamicEntity as a parameter and sets the properties based on the DynamicEntity.
public class Favorite
{
    public string Name { get; set; }
    public MapPoint Location { get; set; }
    public string CityName { get; set; }
    public int AvailableBikes { get; set; }
    public int AvailableEBikes { get; set; }
    public double PercentAvailable { get; set; }

    public Favorite()
    {
        Name = "";
        Location = null;
        CityName = "";
        AvailableBikes = 0;
        AvailableEBikes = 0;
        PercentAvailable = 0.0;
    }

    public Favorite(string city, DynamicEntity dynEntity)
    {
        Name = dynEntity.Attributes["StationName"].ToString();
        Location = dynEntity.Geometry as MapPoint;
        CityName = city;
        AvailableBikes = (int)dynEntity.Attributes["BikesAvailable"];
        AvailableEBikes = (int)dynEntity.Attributes["EBikesAvailable"];
        var emptySlots = (int)dynEntity.Attributes["EmptySlots"];
        PercentAvailable = (double)AvailableBikes / (AvailableBikes + emptySlots);
    }
}
