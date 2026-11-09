using PalletApp.Models;
using PalletApp.Services;
using System.Windows;

namespace pallet
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void BtnGuest_Click(object sender, RoutedEventArgs e)
        {
            var guest = new User
            {
                UserID = 0,
                Login = "guest",
                FullName = "Гость",
                Role = "гость",
                IsAuthenticated = false
            };

            OpenCatalog(guest);
        }

        private void BtnUser_Click(object sender, RoutedEventArgs e)
        {
            LoginPanel.Visibility = Visibility.Visible;
            TxtLogin.Focus();
            TxtError.Visibility = Visibility.Collapsed;
        }

        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            string login = TxtLogin.Text.Trim();

            if (string.IsNullOrWhiteSpace(login))
            {
                ShowError("Введите логин.");
                return;
            }

            try
            {
                var user = DbService.FindUserByLogin(login);

                if (user == null)
                {
                    ShowError("Пользователь с таким логином не найден.");
                    return;
                }

                OpenCatalog(user);
            }
            catch (System.Exception ex)
            {
                ShowError($"Ошибка подключения к БД: {ex.Message}");
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            LoginPanel.Visibility = Visibility.Collapsed;
            TxtLogin.Clear();
            TxtError.Visibility = Visibility.Collapsed;
        }

        private void BtnExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void ShowError(string message)
        {
            TxtError.Text = message;
            TxtError.Visibility = Visibility.Visible;
        }

        private void OpenCatalog(User user)
        {
            var catalog = new CatalogWindow(user);
            catalog.Show();
            this.Hide();
        }
    }
}