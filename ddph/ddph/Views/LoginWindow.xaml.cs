using System.Windows;
using System.Windows.Input;
using ddph.Data;

namespace ddph.Views
{
    public partial class LoginWindow : Window
    {
        private readonly EmployeeRepository _employeeRepository = new();

        public LoginWindow()
        {
            InitializeComponent();
            Loaded += (_, _) => UsernameTextBox.Focus();
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            await LoginAsync();
        }

        private async void LoginInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            e.Handled = true;
            await LoginAsync();
        }

        private async Task LoginAsync()
        {
            bool isAdmin;
            bool isEmployee;
            var username = UsernameTextBox.Text.Trim();

            try
            {
                var adminCredentials = AuthCredentialStore.GetAdminCredentials();
                isAdmin = string.Equals(username, adminCredentials.Username, StringComparison.OrdinalIgnoreCase) &&
                    PasswordBox.Password == adminCredentials.Password;
                isEmployee = !isAdmin && await _employeeRepository.ValidateEmployeeAsync(username, PasswordBox.Password);
            }
            catch (Exception ex)
            {
                ErrorTextBlock.Text = $"Login error: {ex.Message}";
                ErrorTextBlock.Visibility = Visibility.Visible;
                return;
            }

            if (!isAdmin && !isEmployee)
            {
                ErrorTextBlock.Text = "Wrong password or user does not exist.";
                ErrorTextBlock.Visibility = Visibility.Visible;
                PasswordBox.SelectAll();
                PasswordBox.Focus();
                return;
            }

            AuthSessionStore.SignIn(username);

            if (RememberMeCheckBox.IsChecked == true)
            {
                AuthSessionStore.Remember(username);
            }

            OpenMainWindow();
        }

        private void OpenMainWindow()
        {
            var mainWindow = new MainWindow();
            Application.Current.MainWindow = mainWindow;
            mainWindow.Show();
            Close();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
