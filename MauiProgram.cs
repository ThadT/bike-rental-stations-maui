using BikeRentalStations.ViewModel;
using Esri.ArcGISRuntime;
using Esri.ArcGISRuntime.Security;

namespace BikeRentalStations;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {        
        /* Authentication for ArcGIS location services:
         * Use of ArcGIS location services, including basemaps and geocoding, requires either:
         * 1) ArcGIS identity (formerly "named user"): An account that is a member of an organization in ArcGIS Online or ArcGIS Enterprise
         *    giving your application permission to access the content and location services authorized to an existing ArcGIS user's account.
         *    You'll get an identity by signing into the ArcGIS Portal.
         * 2) API key: A permanent token that grants your application access to ArcGIS location services.
         *    Create a new API key or access existing API keys from your ArcGIS for Developers
         *    dashboard (https://links.esri.com/arcgis-api-keys) then call .UseApiKey("[Your ArcGIS location services API Key]")
         *    in the UseArcGISRuntime call below. */
    
        /* Licensing:
         * Production deployment of applications built with the ArcGIS Maps SDK requires you to license ArcGIS functionality.
         * For more information see https://links.esri.com/arcgis-runtime-license-and-deploy.
         * You can set the license string by calling .UseLicense(licenseString) in the UseArcGISRuntime call below
         * or retrieve a license dynamically after signing into a portal:
         * ArcGISRuntimeEnvironment.SetLicense(await myArcGISPortal.GetLicenseInfoAsync()); */

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            })
            .UseArcGISRuntime(config => config
                .UseApiKey("YOUR_API_KEY")
                .ConfigureAuthentication(auth => auth
                .UseDefaultChallengeHandler() // Use the default authentication dialog
              )
            );

        builder.Services.AddSingleton<CityBikesViewModel>();
        builder.Services.AddSingleton<MainPage>();
        builder.Services.AddSingleton<FavoritesPage>();

        return builder.Build();
    }
}