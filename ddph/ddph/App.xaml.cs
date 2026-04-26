using System.Configuration;
using System.Data;
using System.Windows;
using System.Globalization;
using System.Threading;
using System.Windows.Markup;
using ddph.Views;

namespace ddph
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

            var cultureInfo = new CultureInfo("en-PH");
            Thread.CurrentThread.CurrentCulture = cultureInfo;
            Thread.CurrentThread.CurrentUICulture = cultureInfo;
            
            FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(
                    XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));
            
            base.OnStartup(e);

            Window startupWindow;
            if (AuthSessionStore.IsRemembered())
            {
                AuthSessionStore.GetRememberedUsername();
                startupWindow = new MainWindow();
            }
            else
            {
                startupWindow = new LoginWindow();
            }

            MainWindow = startupWindow;
            startupWindow.Show();
        }
    }
}
