using PalletApp.Models;
using PalletApp.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace pallet
{
    public partial class CatalogWindow : Window
    {
        private readonly User _currentUser;
        private ObservableCollection<CartItem> _cart = new ObservableCollection<CartItem>();
        private List<Product> _allProducts = new List<Product>();

        public CatalogWindow(User user)
        {
            InitializeComponent();
            _currentUser = user;

            LoadUserInfo();
            LoadProducts();
            LoadCartFromStorage();
        }

        private void LoadUserInfo()
        {
            BtnLogout.Visibility = Visibility.Visible;

            switch (_currentUser.Role)
            {
                case "гость":
                    TxtUserInfo.Text = "Гость";
                    BtnCart.Visibility = Visibility.Collapsed;
                    break;

                default:
                    TxtUserInfo.Text = _currentUser.FullName;
                    BtnCart.Visibility = Visibility.Visible;
                    BtnCart.Content = "🛒  Корзина (0)";
                    break;
            }
        }

        private void LoadProducts()
        {
            try
            {
                _allProducts = DbService.GetProducts();
                ProductsList.ItemsSource = _allProducts;
                TxtStatus.Text = $"Загружено товаров: {_allProducts.Count}";
                LoadCategories();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки товаров:\n{ex.Message}",
                                "Ошибка БД",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                TxtStatus.Text = "Ошибка загрузки данных";
            }
        }

        private void LoadCategories()
        {
            var categories = _allProducts
                .Select(p => p.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            categories.Insert(0, "Все категории");
            CmbCategory.ItemsSource = categories;
            CmbCategory.SelectedIndex = 0;
        }

        // СОХРАНЕНИЕ / ЗАГРУЗКА КОРЗИНЫ 
        private void LoadCartFromStorage()
        {
            try
            {
                if (_currentUser.Role == "гость") return;

                var saved = CartStorage.Load(_currentUser.Login);

                foreach (var (productId, qty) in saved)
                {
                    var product = _allProducts.FirstOrDefault(p => p.ProductID == productId);
                    if (product == null) continue;

                    // Проверяем наличие
                    if (product.Quantity <= 0) continue;

                    int qtyToAdd = Math.Min(qty, product.Quantity);
                    if (qtyToAdd <= 0) continue;

                    _cart.Add(new CartItem
                    {
                        Product = product,
                        Quantity = qtyToAdd
                    });
                }

                UpdateCartButton();
            }
            catch
            {
                // Не блокируем
            }
        }

        private void SaveCart()
        {
            if (_currentUser.Role == "гость") return;
            CartStorage.Save(_currentUser.Login, _cart);
        }

        // ПОИСК 
        private void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            string search = TxtSearch.Text.Trim().ToLower();
            string category = CmbCategory.SelectedItem as string;

            var filtered = _allProducts.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                filtered = filtered.Where(p =>
                    p.Name.ToLower().Contains(search) ||
                    (p.SKU ?? "").ToLower().Contains(search));
            }

            if (!string.IsNullOrWhiteSpace(category) && category != "Все категории")
            {
                filtered = filtered.Where(p => p.Category == category);
            }

            var result = filtered.ToList();
            ProductsList.ItemsSource = result;
            TxtStatus.Text = $"Найдено товаров: {result.Count}";
        }

        // КОРЗИНА
        private void BtnAddToCart_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser.Role == "гость")
            {
                MessageBox.Show("Для оформления заказа необходимо авторизоваться.",
                                "Доступ ограничен",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var button = sender as Button;
            if (button?.Tag is int productId)
            {
                var product = _allProducts.FirstOrDefault(p => p.ProductID == productId);
                if (product == null) return;

                // Проверка на остаток
                int inCart = _cart.Where(i => i.Product.ProductID == productId).Sum(i => i.Quantity);
                if (inCart >= product.Quantity)
                {
                    MessageBox.Show($"На складе доступно только {product.Quantity} шт. товара «{product.Name}».",
                                    "Недостаточно товара",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var existing = _cart.FirstOrDefault(i => i.Product.ProductID == productId);
                if (existing != null)
                {
                    existing.Quantity++;
                }
                else
                {
                    _cart.Add(new CartItem { Product = product, Quantity = 1 });
                }

                UpdateCartButton();
                TxtStatus.Text = $"Товар «{product.Name}» добавлен в корзину.";
            }
        }

        private void BtnCart_Click(object sender, RoutedEventArgs e)
        {
            var cartWindow = new CartWindow(_cart, _currentUser);
            cartWindow.Owner = this;
            cartWindow.ShowDialog();
            UpdateCartButton();
        }

        private void UpdateCartButton()
        {
            int totalItems = _cart.Sum(i => i.Quantity);

            if (_currentUser.Role == "менеджер")
                BtnCart.Content = $"🛒  Корзина ({totalItems}) [менеджер]";
            else
                BtnCart.Content = $"🛒  Корзина ({totalItems})";

            // Сохраняем корзину
            SaveCart();
        }

        // ВЫХОД
        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Вы действительно хотите выйти?",
                "Подтверждение выхода",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            // Сохраняем корзину перед выходом
            SaveCart();

            var loginWindow = new MainWindow();
            loginWindow.Show();
            this.Close();
        }
    }
}