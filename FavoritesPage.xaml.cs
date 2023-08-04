using BikeRentalStations.ViewModel;

namespace BikeRentalStations;

public partial class FavoritesPage : ContentPage
{
    //private CityBikesViewModel _vm;

    public FavoritesPage(CityBikesViewModel vm)
	{
		InitializeComponent();
        BindingContext = vm;
       // _vm = vm;
	}
}