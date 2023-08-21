using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.RealTime;
using System.ComponentModel;

namespace BikeRentalStations;

// Create a class called Favorite that has the following properties:
// - Name (string)
// - Location (MapPoint)
// - CityName (string)
// - AvailableBikes (int)
// - AvailableEBikes (int)
// Make the class public and add a default constructor that sets the properties to default values.
// Add a constructor that takes a DynamicEntity as a parameter and sets the properties based on the DynamicEntity.
public class Favorite: INotifyPropertyChanged
{
    public string StationId { get; set; }
    public string Name { get; set; }
    public MapPoint Location { get; set; }
    public string CityName { get; set; }
    public int AvailableBikes { get; set; }
    public int AvailableEBikes { get; set; }
    public int EmptySlots { get; set; }
    public double PercentAvailable { get; set; }
    public string ImageUrl { get; set; }
    public DateTime LastUpdated { get; set; }
    public int InventoryChange { get; set; }
    
    public Favorite(string city, DynamicEntity dynEntity)
    {
        // Set the properties of the station that won't change.
        CityName = city;
        StationId = dynEntity.Attributes["StationId"].ToString();
        Name = dynEntity.Attributes["StationName"].ToString();
        Location = dynEntity.Geometry as MapPoint;
        ImageUrl = dynEntity.Attributes["ImageUrl"].ToString();

        // Call the UpdateFavorite method to set the dynamic properties.
        UpdateFavorite(dynEntity);
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public void UpdateFavorite(DynamicEntity dynEntity)
    {
        // Read the bike inventory information from the DynamicEntity.
        AvailableBikes = (int)dynEntity.Attributes["BikesAvailable"];
        AvailableEBikes = (int)dynEntity.Attributes["EBikesAvailable"];
        EmptySlots = (int)dynEntity.Attributes["EmptySlots"];
        PercentAvailable = (double)AvailableBikes / (AvailableBikes + EmptySlots);
        var dateTimeString = dynEntity.Attributes["TimeStamp"].ToString();
        LastUpdated = DateTime.Parse(dateTimeString);
        InventoryChange = (int)dynEntity.Attributes["InventoryChange"];

        // Raise the PropertyChanged event for all of the properties.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AvailableBikes)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AvailableEBikes)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EmptySlots)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PercentAvailable)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LastUpdated)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InventoryChange)));
    }
}
