using BikeAvailability.ViewModel;

namespace BikeAvailability;

public partial class FavoritesPage : ContentPage
{
    public FavoritesPage(CityBikesViewModel vm)
	{
		InitializeComponent();
        BindingContext = vm;
	}

    // Navigate to the map page to show a selected favorite station.
    private async void StationClicked(object sender, EventArgs e)
    {
        var stationButton = sender as Button;

        if (stationButton.BindingContext is Favorite fav)
        {
            Dictionary<string, object> mapParams = new()
            {
                { "favorite", fav }
            };
            await Shell.Current.GoToAsync("//MainPage", mapParams);
        }
    }
}