using Windows.UI.Xaml;
using System.Threading.Tasks;
using MasterPasswordUWP.Services.SettingsServices;
using Windows.ApplicationModel.Activation;
using Template10.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using Windows.Foundation.Metadata;
using Windows.Globalization;
using Windows.System.UserProfile;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml.Data;
using Autofac;
using MasterPassword.Client.Models.Marshallers;
using MasterPassword.Client.Services.ImportExport;
using MasterPassword.Client.Services.Providers;
using MasterPasswordUWP.Services;
using MasterPasswordUWP.Services.DataSources;
using MasterPasswordUWP.ViewModels;
using Template10.Services.LoggingService;

namespace MasterPasswordUWP
{
    /// Documentation on APIs used in this page:
    /// https://github.com/Windows-XAML/Template10/wiki

    [Bindable]
    sealed partial class App : Template10.Common.BootStrapper
    {
        private static Lazy<IContainer> LazyContainer { get; } = new Lazy<IContainer>(BuildContainer);

        public static IContainer Container => LazyContainer.Value;

        private static IContainer BuildContainer()
        {
            var bldr = new ContainerBuilder();
            // register all models
            bldr.RegisterInstance(new SettingsService()).ExternallyOwned().SingleInstance();
            bldr.RegisterType<SiteProvider>().As<ISiteProvider>().SingleInstance();
            bldr.RegisterType<SitePersistor>().As<ISitePersistor>().As<ISiteImporterExporter>();
            bldr.RegisterType<SiteDataSourceJson>().As<ISiteDataSource>().WithParameter(new TypedParameter(typeof(DataSourceType), DataSourceType.Json));
            bldr.RegisterType<SiteDataSourceMpSites>().As<ISiteDataSource>().WithParameter(new TypedParameter(typeof(DataSourceType), DataSourceType.MpSites));
            bldr.RegisterType<DefaultMetadataProvider>().As<IMetadataProvider>();
            bldr.RegisterType<PasswordClipboardService>().As<IPasswordClipboardService>();
            bldr.RegisterType<SiteImportExportService>().As<ISiteImportExportService>();
            bldr.RegisterType<MpSiteMarshaller>().As<ICustomSiteMarshaller>();
            bldr.RegisterType<MpSiteUnmarshaller>().As<ICustomSiteUnmarshaller>();
            bldr.RegisterType<TelemetryService>().As<ITelemetryService>();
            //bldr.RegisterType<SettingsService>().SingleInstance();
            // register all ViewModels
            //bldr.RegisterType<SitesPageViewModel>().As<ISitesPageViewModel>().PropertiesAutowired();
            return bldr.Build();
        }

        public App()
        {
            var _settings = App.Container.Resolve<SettingsService>();

            ApplicationLanguages.PrimaryLanguageOverride = GlobalizationPreferences.Languages.FirstOrDefault() ?? "en-US";
            // apply users language override
            if (!string.IsNullOrWhiteSpace(_settings.AppLanguage))
            {
                ApplicationLanguages.PrimaryLanguageOverride = _settings.AppLanguage;
            }

            InitializeComponent();
            SplashFactory = e => new Views.Splash(e);

            // Xbox one stuff
            //ApplicationView.GetForCurrentView().SetDesiredBoundsMode(ApplicationViewBoundsMode.UseCoreWindow);
            //Windows.UI.Xaml.Application.Current.re
            //this.RequiresPointerMode = Windows.UI.Xaml.ApplicationRequiresPointerMode.WhenRequested;

            LoggingService.Enabled = true;
            LoggingService.WriteLine = (text, severity, target, caller) => Debug.WriteLine( $"{caller}: {target}, {text}" );

            #region App settings

            RequestedTheme = _settings.AppTheme;
            CacheMaxDuration = _settings.CacheMaxDuration;

            #endregion
        }

        public override async Task OnInitializeAsync(IActivatedEventArgs args)
        {
            if (args.Kind == ActivationKind.ShareTarget)
            {
                //Windows.System.Launcher.LaunchUriAsync( new Uri( $"md-masterpassword://test" ) );
                return;
            }
            if (!(Window.Current.Content is ModalDialog))
            {
                // create a new frame 
                var nav = NavigationServiceFactory(BackButton.Attach, ExistingContent.Include);

                // create modal root
                Window.Current.Content = new ModalDialog
                {
                    DisableBackButtonWhenModal = true,
                    Content = new Views.Shell(nav),
                    ModalContent = new Views.Busy(),
                };
                // Xbox one stuff
                //TODO: check if we are on xbox
                //ApplicationView.GetForCurrentView().SetDesiredBoundsMode(ApplicationViewBoundsMode.UseCoreWindow);
            }

            if (ApiInformation.IsApiContractPresent("Windows.Phone.PhoneContract", 1, 0))
            {
                // on phones: setting the status bar to the same color as the pages header...
                var statusBar = StatusBar.GetForCurrentView();
                if (statusBar != null)
                {
                    statusBar.BackgroundColor = (Color)Resources["SystemAccentColor"];
                    statusBar.BackgroundOpacity = 1.0f;
                }
            }

            await Task.CompletedTask;
        }

        static IDictionary<string, string> GetParams(string uri)
        {
            var matches = Regex.Matches( uri, @"[\?&](([^&=]+)=([^&=#]*))", RegexOptions.Compiled );
            return matches.Cast<Match>().ToDictionary(
                m => Uri.UnescapeDataString( m.Groups[2].Value ),
                m => Uri.UnescapeDataString( m.Groups[3].Value )
            );
        }

        public override async Task OnStartAsync(StartKind startKind, IActivatedEventArgs args)
        {
            // long-running startup tasks go here
            object loginParams = null;
            if (args.Kind == ActivationKind.Protocol && args is ProtocolActivatedEventArgs)
            {
                var protoArgs = (ProtocolActivatedEventArgs) args;
                var parameters = GetParams( protoArgs.Uri.ToString() );

                string userName, password;
                parameters.TryGetValue( "user", out userName );
                parameters.TryGetValue( "pass", out password );
                loginParams = new LoginPageParameters {UserName = userName, MasterPassword = password};
            }

            NavigationService.Navigate(typeof(Views.LoginPage), loginParams);
            await Task.CompletedTask;
        }
    }
}

