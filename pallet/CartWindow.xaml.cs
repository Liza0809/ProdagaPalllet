using Microsoft.Win32;
using PalletApp.Models;
using PalletApp.Services;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;

namespace pallet
{
    public partial class CartWindow : Window
    {
        private readonly ObservableCollection<CartItem> _items;
        private readonly User _currentUser;

        private OrderSession.Order _currentOrder;

        // Разделение заказа
        private bool _fullCartBackupUsed = false;
        private List<CartItem> _fullCartBackup;
        private List<CartItem> _sentItems;

        // Принудительный самовывоз
        private bool _forceSelfPickup = false;

        private const decimal TRUCK_CAPACITY = 80m;

        // Статусы
        private const int STATUS_ORDERED = 1;
        private const int STATUS_PAID = 2;
        private const int STATUS_SHIPPED = 3;
        private const int STATUS_READY = 4;
        private const int STATUS_CANCELED = 6;

        private List<DeliveryMethod> _deliveryMethods = new List<DeliveryMethod>();
        private List<PaymentMethod> _paymentMethods = new List<PaymentMethod>();

        public CartWindow(ObservableCollection<CartItem> items, User user)
        {
            InitializeComponent();
            _items = items;
            _currentUser = user;

            _forceSelfPickup = false;
            _fullCartBackupUsed = false;

            CartList.ItemsSource = _items;
            OrderItemsList.ItemsSource = _items;
            SplitList.ItemsSource = _items;

            LoadClientInfo();
            LoadDictionariesFromDb();
            LoadOrdersFromDbIfAny();

            UpdateUI();
            UpdateTotals();
        }
        // === ЗАГРУЗКА ИЗ БД ===
        private void LoadClientInfo()
        {
            try
            {
                var info = DbService.GetClientInfo(_currentUser.UserID);

                if (info != null)
                {
                    TxtRecipientName.Text = string.IsNullOrWhiteSpace(info.FullName)
                        ? _currentUser.FullName
                        : info.FullName;

                    TxtRecipientPhone.Text = info.Phone ?? "";
                    TxtAddress.Text = info.Address ?? "";
                }
                else
                {
                    TxtRecipientName.Text = _currentUser.FullName;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка автозаполнения данных клиента:\n{ex.Message}",
                                "Ошибка БД",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                TxtRecipientName.Text = _currentUser.FullName;
            }
        }

        private void LoadDictionariesFromDb()
        {
            try
            {
                _deliveryMethods = DbService.GetDeliveryMethods();
                _paymentMethods = DbService.GetPaymentMethods();

                CmbPaymentMethod.ItemsSource = _paymentMethods;
                if (_paymentMethods.Count > 0)
                    CmbPaymentMethod.SelectedIndex = 0;

                CmbDeliveryMethod.ItemsSource = _deliveryMethods;
                if (_deliveryMethods.Count > 0)
                    CmbDeliveryMethod.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки справочников:\n{ex.Message}",
                                "Ошибка БД",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadOrdersFromDbIfAny()
        {
            try
            {
                if (_currentUser.UserID <= 0)
                {
                    BtnGoToDelivery.Visibility = Visibility.Collapsed;
                    return;
                }

                var orders = DbService.GetClientOrders(_currentUser.UserID);

                if (orders != null && orders.Count > 0)
                {
                    OrderSession.SetOrders(orders);
                    BtnGoToDelivery.Visibility = Visibility.Visible;
                }
                else
                {
                    BtnGoToDelivery.Visibility = Visibility.Collapsed;
                }
            }
            catch
            {
                BtnGoToDelivery.Visibility = Visibility.Collapsed;
            }
        }

        private void SaveCart()
        {
            if (_currentUser.Role == "гость") return;
            CartStorage.Save(_currentUser.Login, _items);
        }
        // === РЕЖИМ 1: КОРЗИНА ===
        private void UpdateUI()
        {
            BtnGoToDelivery.Visibility = OrderSession.HasOrders
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (_items.Count == 0)
            {
                EmptyPanel.Visibility = Visibility.Visible;
                CartList.Visibility = Visibility.Collapsed;
            }
            else
            {
                EmptyPanel.Visibility = Visibility.Collapsed;
                CartList.Visibility = Visibility.Visible;
            }

            BtnGoCheckout.IsEnabled = _items.Count > 0;
            decimal total = _items.Sum(i => i.LineTotal);
            TxtTotal.Text = $"{total:N2} ₽";
        }

        private void BtnIncrease_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is CartItem item)
            {
                item.Quantity++;
                CartList.Items.Refresh();
                OrderItemsList.Items.Refresh();
                UpdateUI();
                UpdateTotals();
                SaveCart();
            }
        }

        private void BtnDecrease_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is CartItem item)
            {
                if (item.Quantity > 1)
                {
                    item.Quantity--;
                }
                else if (MessageBox.Show($"Удалить «{item.Product.Name}»?",
                                         "Подтверждение",
                                         MessageBoxButton.YesNo,
                                         MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    _items.Remove(item);
                }

                CartList.Items.Refresh();
                OrderItemsList.Items.Refresh();
                UpdateUI();
                UpdateTotals();
                SaveCart();
            }
        }

        private void BtnRemove_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is CartItem item &&
                MessageBox.Show($"Удалить «{item.Product.Name}»?",
                                "Подтверждение",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _items.Remove(item);
                UpdateUI();
                UpdateTotals();
                SaveCart();
            }
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            if (_items.Count == 0) return;
            if (MessageBox.Show("Очистить корзину полностью?",
                                "Подтверждение",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _items.Clear();
                UpdateUI();
                UpdateTotals();
                SaveCart();
            }
        }

        private void BtnContinue_Click(object sender, RoutedEventArgs e) => this.Close();

        private void QuantityBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            foreach (char c in e.Text)
            {
                if (!char.IsDigit(c))
                {
                    e.Handled = true;
                    return;
                }
            }
        }

        private void QuantityBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!(sender is TextBox tb)) return;
            if (!(tb.DataContext is CartItem item)) return;

            if (item.Quantity < 1) item.Quantity = 1;

            CartList.Items.Refresh();
            OrderItemsList.Items.Refresh();
            UpdateUI();
            UpdateTotals();
            SaveCart();
        }

        private void BtnGoCheckout_Click(object sender, RoutedEventArgs e)
        {
            if (_items.Count == 0) return;

            decimal volume = GetTotalVolume();

            if (volume > TRUCK_CAPACITY)
            {
                EnterSplitMode();
                return;
            }

            _sentItems = null;
            _fullCartBackup = null;
            _fullCartBackupUsed = false;
            _forceSelfPickup = false;

            RefreshDeliveryMethods();

            CartView.Visibility = Visibility.Collapsed;
            CheckoutView.Visibility = Visibility.Visible;
            TxtHeaderTitle.Text = "Оформление заказа";
            UpdateTotals();
        }

        private void BtnGoToDelivery_Click(object sender, RoutedEventArgs e)
        {
            ShowOrdersListScreen();
        }
        // === РЕЖИМ РАЗДЕЛЕНИЯ ===
        private void EnterSplitMode()
        {
            foreach (var item in _items)
                item.QuantityForSplit = 0;

            CartView.Visibility = Visibility.Collapsed;
            SplitView.Visibility = Visibility.Visible;
            TxtHeaderTitle.Text = "Разделение заказа";

            UpdateSplitCounter();
        }

        private void ExitSplitMode()
        {
            foreach (var item in _items)
                item.QuantityForSplit = 0;

            SplitView.Visibility = Visibility.Collapsed;
            CartView.Visibility = Visibility.Visible;
            TxtHeaderTitle.Text = "Корзина";

            UpdateUI();
        }

        private void UpdateSplitCounter()
        {
            decimal selected = _items.Sum(i => i.SplitVolume);

            TxtSplitVolume.Text = $"{selected:0.#} / {TRUCK_CAPACITY:0} мест";

            BtnConfirmSplit.IsEnabled = selected > 0 && selected <= TRUCK_CAPACITY;

            TxtSplitVolume.Foreground = selected > TRUCK_CAPACITY
                ? Brushes.Red
                : (Brush)FindResource("PrimaryDarkBrush");
        }

        private void BtnSplitDecrease_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is CartItem item && item.QuantityForSplit > 0)
            {
                item.QuantityForSplit--;
                UpdateSplitCounter();
            }
        }

        private void BtnSplitIncrease_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is CartItem item && item.QuantityForSplit < item.Quantity)
            {
                item.QuantityForSplit++;
                UpdateSplitCounter();
            }
        }

        private void SplitQuantityBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!(sender is TextBox tb)) return;
            if (!(tb.DataContext is CartItem item)) return;

            if (item.QuantityForSplit < 0) item.QuantityForSplit = 0;
            if (item.QuantityForSplit > item.Quantity) item.QuantityForSplit = item.Quantity;

            UpdateSplitCounter();
        }

        private void BtnAutoSelectSplit_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _items)
                item.QuantityForSplit = 0;

            decimal total = 0;
            foreach (var item in _items)
            {
                decimal remaining = TRUCK_CAPACITY - total;
                if (remaining <= 0) break;

                int canFit = (int)Math.Floor(remaining);
                int take = Math.Min(canFit, item.Quantity);

                item.QuantityForSplit = take;
                total += take;
            }

            UpdateSplitCounter();
        }

        private void BtnClearSplitSelection_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _items)
                item.QuantityForSplit = 0;

            UpdateSplitCounter();
        }

        private void BtnCancelSplit_Click(object sender, RoutedEventArgs e)
        {
            ExitSplitMode();
        }

        private void BtnConfirmSplit_Click(object sender, RoutedEventArgs e)
        {
            decimal selectedVolume = _items.Sum(i => i.SplitVolume);
            int totalItems = _items.Sum(i => i.QuantityForSplit);

            if (totalItems <= 0)
            {
                MessageBox.Show("Укажите, сколько штук поедет в первой партии.");
                return;
            }

            if (selectedVolume > TRUCK_CAPACITY)
            {
                MessageBox.Show($"Превышен лимит фуры: {selectedVolume:0.#} мест > {TRUCK_CAPACITY}.");
                return;
            }

            EnterCheckoutWithSelection();
        }

        private void BtnSelfPickup_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Оформить все товары как самовывоз со склада?\n\n" +
                "Заказ не будет разделяться, способ доставки будет зафиксирован как «Самовывоз».",
                "Подтверждение",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            foreach (var item in _items)
                item.QuantityForSplit = 0;

            _fullCartBackup = null;
            _sentItems = null;
            _fullCartBackupUsed = false;
            _forceSelfPickup = true;

            SetupSelfPickupOnly();

            SplitView.Visibility = Visibility.Collapsed;
            CheckoutView.Visibility = Visibility.Visible;
            TxtHeaderTitle.Text = "Оформление заказа (самовывоз)";
            UpdateTotals();
        }

        private void SetupSelfPickupOnly()
        {
            var selfPickup = _deliveryMethods.FirstOrDefault(m => m.Name == "Самовывоз");

            if (selfPickup == null)
            {
                MessageBox.Show("Способ доставки «Самовывоз» не найден в БД.");
                return;
            }

            CmbDeliveryMethod.ItemsSource = new List<DeliveryMethod> { selfPickup };
            CmbDeliveryMethod.SelectedIndex = 0;
            CmbDeliveryMethod.IsEnabled = false;

            TxtAddress.IsEnabled = false;
            TxtAddress.Text = "";
            LblAddress.Foreground = Brushes.Gray;

            TxtVolumeHint.Text = $"Объём заказа: {GetTotalVolume():0.#} паллетомест — самовывоз со склада, разделение не требуется.";
        }

        private void EnterCheckoutWithSelection()
        {
            _fullCartBackup = _items.Select(i => new CartItem
            {
                Product = i.Product,
                Quantity = i.Quantity
            }).ToList();

            _sentItems = _items
                .Where(i => i.QuantityForSplit > 0)
                .Select(i => new CartItem
                {
                    Product = i.Product,
                    Quantity = i.QuantityForSplit
                }).ToList();

            var toRemove = _items.Where(i => i.QuantityForSplit <= 0).ToList();
            foreach (var r in toRemove)
                _items.Remove(r);

            foreach (var item in _items)
            {
                item.Quantity = item.QuantityForSplit;
                item.QuantityForSplit = 0;
            }

            CartList.Items.Refresh();
            OrderItemsList.Items.Refresh();

            _forceSelfPickup = false;
            _fullCartBackupUsed = true;

            RefreshDeliveryMethods();

            SplitView.Visibility = Visibility.Collapsed;
            CheckoutView.Visibility = Visibility.Visible;
            TxtHeaderTitle.Text = "Оформление заказа (партия 1)";
            UpdateTotals();
        }

        private void RestoreNonSentItems()
        {
            if (_fullCartBackup == null)
            {
                _items.Clear();
                return;
            }

            _items.Clear();

            foreach (var original in _fullCartBackup)
            {
                var sent = _sentItems.FirstOrDefault(s => s.Product.ProductID == original.Product.ProductID);
                int sentQty = sent?.Quantity ?? 0;
                int remaining = original.Quantity - sentQty;

                if (remaining > 0)
                {
                    _items.Add(new CartItem
                    {
                        Product = original.Product,
                        Quantity = remaining
                    });
                }
            }

            _fullCartBackup = null;
            _sentItems = null;
            _fullCartBackupUsed = false;

            UpdateUI();
            UpdateTotals();
        }
        // === РАСЧЁТ НАПОЛНЕННОСТИ ===
        private decimal GetTotalVolume() => _items.Sum(i => i.TotalVolume);

        private bool FitsInDelivery(DeliveryMethod method)
        {
            if (method == null) return false;
            if (method.Capacity <= 0) return true;
            return GetTotalVolume() <= method.Capacity;
        }

        private List<DeliveryMethod> GetAvailableDeliveryMethods()
            => _deliveryMethods.Where(FitsInDelivery).ToList();

        private void RefreshDeliveryMethods()
        {
            if (_forceSelfPickup)
            {
                SetupSelfPickupOnly();
                return;
            }

            var available = GetAvailableDeliveryMethods();
            CmbDeliveryMethod.ItemsSource = available;
            CmbDeliveryMethod.IsEnabled = true;

            if (available.Count > 0)
                CmbDeliveryMethod.SelectedIndex = 0;
            else
                CmbDeliveryMethod.SelectedIndex = -1;

            UpdateDeliveryHint();
        }

        private void UpdateDeliveryHint()
        {
            if (_forceSelfPickup)
            {
                TxtVolumeHint.Text = $"Объём заказа: {GetTotalVolume():0.#} паллетомест — самовывоз со склада, разделение не требуется.";
                return;
            }

            decimal volume = GetTotalVolume();
            var gazelle = _deliveryMethods.FirstOrDefault(m => m.Name == "Доставка газелью");
            var truck = _deliveryMethods.FirstOrDefault(m => m.Name == "Доставка фурой");

            string hint;

            if (volume <= 0)
            {
                hint = "Добавьте товары в корзину.";
            }
            else if (gazelle != null && volume <= gazelle.Capacity)
            {
                hint = $"Объём заказа: {volume:0.#} паллетомест — " +
                       $"влезает в газель (до {gazelle.Capacity} мест). " +
                       $"Фура (до {truck?.Capacity} мест) также доступна.";
            }
            else if (truck != null && volume <= truck.Capacity)
            {
                hint = $"Объём заказа: {volume:0.#} паллетомест. " +
                       $"В газель (до {gazelle?.Capacity} мест) не влезает — " +
                       $"доступна доставка фурой (до {truck.Capacity} мест).";
            }
            else
            {
                hint = $"Объём заказа: {volume:0.#} паллетомест. " +
                       $"Превышает вместимость одной фуры (до {truck?.Capacity} мест).";
            }

            TxtVolumeHint.Text = hint;
        }
        // === РЕЖИМ 2: ОФОРМЛЕНИЕ ===
        private void CmbDeliveryMethod_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(CmbDeliveryMethod.SelectedItem is DeliveryMethod method)) return;

            if (method.Name == "Самовывоз")
            {
                TxtAddress.IsEnabled = false;
                TxtAddress.Text = "";
                LblAddress.Foreground = Brushes.Gray;
            }
            else
            {
                TxtAddress.IsEnabled = true;
                LblAddress.Foreground = (Brush)FindResource("TextPrimaryBrush");
            }

            UpdateTotals();
        }

        private void CmbPaymentMethod_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(CmbPaymentMethod.SelectedItem is PaymentMethod method)) return;

            if (method.Name == "Оплата картой")
            {
                CardPanel.Visibility = Visibility.Visible;
            }
            else
            {
                CardPanel.Visibility = Visibility.Collapsed;
                TxtCardNumber.Clear();
                TxtCardExpiry.Clear();
                TxtCardCvc.Clear();
                TxtCardHolder.Clear();
            }
        }

        private void TxtCardNumber_TextChanged(object sender, TextChangedEventArgs e)
        {
            var tb = sender as TextBox;
            if (tb == null) return;

            string digits = new string(tb.Text.Where(char.IsDigit).ToArray());
            if (digits.Length > 16) digits = digits.Substring(0, 16);

            var sb = new StringBuilder();
            for (int i = 0; i < digits.Length; i++)
            {
                if (i > 0 && i % 4 == 0) sb.Append(' ');
                sb.Append(digits[i]);
            }

            string result = sb.ToString();
            if (tb.Text != result)
            {
                int caret = tb.CaretIndex;
                tb.Text = result;
                tb.CaretIndex = Math.Min(caret + 1, result.Length);
            }
        }

        private void TxtCardExpiry_TextChanged(object sender, TextChangedEventArgs e)
        {
            var tb = sender as TextBox;
            if (tb == null) return;

            string digits = new string(tb.Text.Where(char.IsDigit).ToArray());
            if (digits.Length > 4) digits = digits.Substring(0, 4);

            string result = digits.Length >= 3
                ? digits.Substring(0, 2) + "/" + digits.Substring(2)
                : digits;

            if (tb.Text != result)
            {
                int caret = tb.CaretIndex;
                tb.Text = result;
                tb.CaretIndex = Math.Min(caret + 1, result.Length);
            }
        }

        private void UpdateTotals()
        {
            decimal subtotal = _items.Sum(i => i.LineTotal);
            var method = CmbDeliveryMethod.SelectedItem as DeliveryMethod;
            decimal delivery = method?.BaseCost ?? 0m;
            decimal volume = GetTotalVolume();

            TxtSubtotal.Text = $"{subtotal:N2} ₽";
            TxtDeliveryCost.Text = $"{delivery:N2} ₽";
            TxtGrandTotal.Text = $"{subtotal + delivery:N2} ₽";
            TxtVolumeSummary.Text = $"{volume:0.#} мест";

            if (method != null && volume > 0)
            {
                bool isDelivery = method.Name != "Самовывоз";
                int totalPallets = (int)volume;
                DateTime now = DateTime.Now;

                DateTime ready = TimeCalculator.CalculateReadyTime(now, totalPallets, isDelivery);

                TxtReadyTime.Text = ready.ToString("dd.MM HH:mm");
                TxtReadyTimeBig.Text = ready.ToString("dd.MM.yyyy HH:mm");

                int prepHours = (int)Math.Ceiling(totalPallets / 20.0);
                int totalHours = prepHours + (isDelivery ? 2 : 0);

                string hint;
                if (isDelivery)
                    hint = $"Подготовка: {prepHours} ч + доставка: 2 ч = {totalHours} ч";
                else
                    hint = $"Подготовка к самовывозу: {prepHours} ч ({totalPallets} паллет / 20)";

                if (ready.Date > now.Date)
                    hint += "\nПеренесено на следующий рабочий день (9:00)";

                TxtReadyTimeHint.Text = hint;
            }
            else
            {
                TxtReadyTime.Text = "—";
                TxtReadyTimeBig.Text = "—";
                TxtReadyTimeHint.Text = "Добавьте товары в корзину";
            }

            if (CheckoutView.Visibility == Visibility.Visible)
                UpdateDeliveryHint();
        }

        private void BtnBackToCart_Click(object sender, RoutedEventArgs e)
        {
            CheckoutView.Visibility = Visibility.Collapsed;
            CartView.Visibility = Visibility.Visible;
            TxtHeaderTitle.Text = "Корзина";
            UpdateUI();
        }
        // === ПОДТВЕРЖДЕНИЕ ЗАКАЗА ===
        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtRecipientName.Text) || TxtRecipientName.Text.Trim().Length < 3)
            {
                MessageBox.Show("Укажите корректное ФИО получателя (минимум 3 символа).");
                TxtRecipientName.Focus();
                return;
            }

            string phone = TxtRecipientPhone.Text.Trim();
            if (string.IsNullOrWhiteSpace(phone))
            {
                MessageBox.Show("Укажите телефон.");
                TxtRecipientPhone.Focus();
                return;
            }

            foreach (char c in phone)
            {
                if (!char.IsDigit(c) && c != '+' && c != '-' && c != ' ' && c != '(' && c != ')')
                {
                    MessageBox.Show("Телефон может содержать только цифры и символы + - ( )");
                    TxtRecipientPhone.Focus();
                    return;
                }
            }

            if (phone.Count(char.IsDigit) < 10)
            {
                MessageBox.Show("Телефон должен содержать минимум 10 цифр.");
                TxtRecipientPhone.Focus();
                return;
            }

            var delivery = CmbDeliveryMethod.SelectedItem as DeliveryMethod;
            if (delivery == null)
            {
                MessageBox.Show("Выберите способ доставки.");
                return;
            }

            if (!FitsInDelivery(delivery))
            {
                MessageBox.Show("Выбранный транспорт не вмещает такой объём заказа.");
                return;
            }

            if (delivery.Name != "Самовывоз" && string.IsNullOrWhiteSpace(TxtAddress.Text))
            {
                MessageBox.Show("Укажите адрес доставки.");
                TxtAddress.Focus();
                return;
            }

            var payment = CmbPaymentMethod.SelectedItem as PaymentMethod;
            if (payment == null)
            {
                MessageBox.Show("Выберите способ оплаты.");
                return;
            }

            if (payment.Name == "Оплата картой")
            {
                string digits = new string(TxtCardNumber.Text.Where(char.IsDigit).ToArray());
                if (digits.Length != 16) { MessageBox.Show("Введите 16-значный номер карты."); return; }
                if (TxtCardExpiry.Text.Length != 5) { MessageBox.Show("Введите срок в формате ММ/ГГ."); return; }
                if (TxtCardCvc.Text.Length != 3) { MessageBox.Show("Введите CVC/CVV."); return; }
                if (string.IsNullOrWhiteSpace(TxtCardHolder.Text)) { MessageBox.Show("Введите держателя карты."); return; }
            }

            foreach (var item in _items)
            {
                int available = DbService.GetAvailableQuantity(item.Product.ProductID);
                if (available < item.Quantity)
                {
                    MessageBox.Show(
                        $"Товар «{item.Product.Name}»: доступно {available} шт., в корзине {item.Quantity}.",
                        "Недостаточно товара",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            decimal subtotal = _items.Sum(i => i.LineTotal);
            decimal deliveryCost = delivery.BaseCost;
            decimal total = subtotal + deliveryCost;
            string address = delivery.Name == "Самовывоз"
                ? "Самовывоз со склада"
                : TxtAddress.Text.Trim();

            bool isDelivery = delivery.Name != "Самовывоз";
            int totalVolume = (int)GetTotalVolume();
            DateTime expectedReady = TimeCalculator.CalculateReadyTime(DateTime.Now, totalVolume, isDelivery);

            string itemsList = string.Join("\n",
                _items.Select(i => $"• {i.Product.Name} × {i.Quantity} = {i.LineTotal:N2} ₽"));

            var result = MessageBox.Show(
                $"Проверьте данные заказа:\n\n" +
                $"Получатель: {TxtRecipientName.Text}\n" +
                $"Телефон: {phone}\n" +
                $"Доставка: {delivery.Name}\n" +
                $"Адрес: {address}\n" +
                $"Оплата: {payment.Name}\n\n" +
                $"Состав заказа:\n{itemsList}\n\n" +
                $"Товары: {subtotal:N2} ₽\n" +
                $"Доставка: {deliveryCost:N2} ₽\n" +
                $"ИТОГО: {total:N2} ₽\n\n" +
                $"Подтвердить оформление?",
                "Подтверждение заказа",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            string readyMessage = TimeCalculator.BuildReadyMessage(
                DateTime.Now, totalVolume, isDelivery, delivery.Name);

            MessageBox.Show(readyMessage,
                            "Время готовности",
                            MessageBoxButton.OK, MessageBoxImage.Information);

            var snapshot = _items.Select(i => new CartItem
            {
                Product = i.Product,
                Quantity = i.Quantity
            }).ToList();

            string orderNumber;
            try
            {
                orderNumber = DbService.CreateOrder(
                    _currentUser.UserID,
                    delivery.DeliveryMethodID,
                    payment.PaymentMethodID,
                    address,
                    subtotal,
                    deliveryCost,
                    total,
                    TxtComment.Text,
                    snapshot);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения заказа:\n{ex.Message}",
                                "Ошибка БД",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var savedOrder = OrderSession.Add(
                orderNumber, DateTime.Now,
                TxtRecipientName.Text, phone,
                delivery.Name, address, payment.Name,
                subtotal, deliveryCost, total,
                TxtComment.Text, snapshot);

            savedOrder.ExpectedReadyAt = expectedReady;

            if (_fullCartBackupUsed)
                RestoreNonSentItems();
            else
                _items.Clear();

            _forceSelfPickup = false;

            // Сохраняем корзину
            SaveCart();

            _currentOrder = savedOrder;
            ShowOrderDetailsScreen();
        }
        // === РЕЖИМ 3: СПИСОК ЗАКАЗОВ ===
        private void ShowOrdersListScreen()
        {
            try
            {
                var orders = DbService.GetClientOrders(_currentUser.UserID);
                OrderSession.SetOrders(orders);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки заказов:\n{ex.Message}",
                                "Ошибка БД",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }

            if (OrderSession.Orders.Count == 0)
            {
                EmptyOrdersPanel.Visibility = Visibility.Visible;
                OrdersList.Visibility = Visibility.Collapsed;
            }
            else
            {
                EmptyOrdersPanel.Visibility = Visibility.Collapsed;
                OrdersList.Visibility = Visibility.Visible;

                OrdersList.ItemsSource = OrderSession.Orders
                    .OrderByDescending(o => o.OrderDate)
                    .ToList();
            }

            CartView.Visibility = Visibility.Collapsed;
            CheckoutView.Visibility = Visibility.Collapsed;
            SplitView.Visibility = Visibility.Collapsed;
            DeliveryView.Visibility = Visibility.Collapsed;
            OrdersListView.Visibility = Visibility.Visible;

            TxtHeaderTitle.Text = "Мои заказы";
        }

        private void OrderCard_Click(object sender, MouseButtonEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is OrderSession.Order order)
            {
                _currentOrder = order;
                ShowOrderDetailsScreen();
            }
        }

        private void BtnBackFromOrders_Click(object sender, RoutedEventArgs e)
        {
            OrdersListView.Visibility = Visibility.Collapsed;
            CartView.Visibility = Visibility.Visible;
            TxtHeaderTitle.Text = "Корзина";
            UpdateUI();
        }
        // === РЕЖИМ 4: ДЕТАЛИ ЗАКАЗА ===
        private void ShowOrderDetailsScreen()
        {
            if (_currentOrder == null) return;

            TxtOrderNumber.Text = $"Номер заказа: {_currentOrder.OrderNumber}";
            TxtOrderStatus.Text = _currentOrder.ClientStatus;

            if (_currentOrder.IsReady)
                TxtOrderStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));
            else if (_currentOrder.StatusID == STATUS_CANCELED)
                TxtOrderStatus.Foreground = Brushes.Red;
            else
                TxtOrderStatus.Foreground = (Brush)FindResource("PrimaryLightBrush");

            TxtInfoRecipient.Text = _currentOrder.Recipient;
            TxtInfoPhone.Text = _currentOrder.Phone;
            TxtInfoDelivery.Text = _currentOrder.DeliveryMethod;
            TxtInfoAddress.Text = _currentOrder.Address;

            if (_currentOrder.IsReady)
                TxtInfoReadyAt.Text = "Готов к получению";
            else if (_currentOrder.StatusID == STATUS_CANCELED)
                TxtInfoReadyAt.Text = "Заказ отменён";
            else if (_currentOrder.ExpectedReadyAt.HasValue)
                TxtInfoReadyAt.Text = "Ожидается к " +
                    _currentOrder.ExpectedReadyAt.Value.ToString("dd.MM.yyyy HH:mm");
            else
                TxtInfoReadyAt.Text = "—";

            TxtInfoPayment.Text = _currentOrder.PaymentMethod;
            TxtInfoSubtotal.Text = $"{_currentOrder.Subtotal:N2} ₽";
            TxtInfoDeliveryCost.Text = $"{_currentOrder.DeliveryCost:N2} ₽";
            TxtInfoTotal.Text = $"{_currentOrder.Total:N2} ₽";

            DeliveryItemsList.ItemsSource = _currentOrder.Items;

            CartView.Visibility = Visibility.Collapsed;
            CheckoutView.Visibility = Visibility.Collapsed;
            SplitView.Visibility = Visibility.Collapsed;
            OrdersListView.Visibility = Visibility.Collapsed;
            DeliveryView.Visibility = Visibility.Visible;

            TxtHeaderTitle.Text = "Доставка";
        }

        private void BtnBackToOrders_Click(object sender, RoutedEventArgs e)
        {
            ShowOrdersListScreen();
        }
        // === СОХРАНЕНИЕ ЧЕКА В PDF ===
        private void BtnDownloadReceipt_Click(object sender, RoutedEventArgs e)
        {
            if (_currentOrder == null) return;

            var dialog = new SaveFileDialog
            {
                Filter = "PDF-файл (*.pdf)|*.pdf",
                FileName = $"Чек_{_currentOrder.OrderNumber}.pdf",
                Title = "Сохранить чек"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                SaveReceiptToPdf(dialog.FileName, _currentOrder);
                MessageBox.Show($"Чек сохранён:\n{dialog.FileName}",
                                "Готово",
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения PDF:\n{ex.Message}",
                                "Ошибка",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveReceiptToPdf(string filePath, OrderSession.Order order)
        {
            var document = new PdfDocument();
            document.Info.Title = $"Чек {order.OrderNumber}";

            var page = document.AddPage();
            double pageWidthPt = 80 * 2.834645;
            page.Width = XUnit.FromPoint(pageWidthPt);
            page.Height = XUnit.FromPoint(1200);

            var gfx = XGraphics.FromPdfPage(page);

            var titleFont = new XFont("Arial", 12, XFontStyleEx.Bold);
            var boldFont = new XFont("Arial", 10, XFontStyleEx.Bold);
            var regularFont = new XFont("Arial", 9, XFontStyleEx.Regular);
            var smallFont = new XFont("Arial", 8, XFontStyleEx.Regular);
            var totalFont = new XFont("Arial", 12, XFontStyleEx.Bold);

            double margin = 10;
            double y = 20;
            double width = pageWidthPt - margin * 2;

            void DrawLine()
            {
                gfx.DrawLine(XPens.LightGray, margin, y, margin + width, y);
                y += 8;
            }
            void DrawCentered(string text, XFont font)
            {
                var size = gfx.MeasureString(text, font);
                gfx.DrawString(text, font, XBrushes.Black,
                    new XPoint((pageWidthPt - size.Width) / 2, y + size.Height));
                y += size.Height + 3;
            }
            void DrawLeft(string text, XFont font)
            {
                gfx.DrawString(text, font, XBrushes.Black, new XPoint(margin, y + font.Height));
                y += font.Height + 2;
            }
            void DrawTwoColumns(string left, string right, XFont font)
            {
                gfx.DrawString(left, font, XBrushes.Black, new XPoint(margin, y + font.Height));
                var size = gfx.MeasureString(right, font);
                gfx.DrawString(right, font, XBrushes.Black,
                    new XPoint(margin + width - size.Width, y + font.Height));
                y += font.Height + 2;
            }

            DrawCentered("ООО «ПАЛЛЕТ-ТОРГ»", titleFont);
            DrawCentered("ИНН 5912345678 / КПП 591201001", smallFont);
            DrawCentered("г. Лысьва, ул. Ленина, д. 2", smallFont);
            DrawCentered("+7 (342) 200-10-01", smallFont);
            y += 6;
            DrawLine();

            DrawCentered("КАССОВЫЙ ЧЕК", boldFont);
            y += 4;

            DrawLeft($"Заказ:  {order.OrderNumber}", regularFont);
            DrawLeft($"Дата:   {order.OrderDate:dd.MM.yyyy HH:mm}", regularFont);
            DrawLeft($"Статус: {order.ClientStatus}", regularFont);
            DrawLine();

            DrawLeft("ПОЛУЧАТЕЛЬ", boldFont);
            DrawLeft($"ФИО: {order.Recipient}", regularFont);
            DrawLeft($"Тел: {order.Phone}", regularFont);
            DrawLine();

            DrawLeft("ДОСТАВКА", boldFont);
            DrawLeft($"Способ: {order.DeliveryMethod}", regularFont);
            DrawLeft($"Адрес:  {order.Address}", regularFont);

            if (order.ExpectedReadyAt.HasValue && !order.IsReady)
            {
                DrawLeft($"Готов к: {order.ExpectedReadyAt.Value:dd.MM.yyyy HH:mm}", regularFont);
            }

            DrawLine();

            DrawLeft("СОСТАВ ЗАКАЗА", boldFont);
            y += 4;

            foreach (var item in order.Items)
            {
                DrawLeft(item.Product.Name, regularFont);
                DrawTwoColumns(
                    $"   {item.Quantity} шт. × {item.Product.Price:N2} ₽",
                    $"{item.LineTotal:N2} ₽",
                    smallFont);
            }

            DrawLine();

            DrawTwoColumns("Товары:", $"{order.Subtotal:N2} ₽", regularFont);
            DrawTwoColumns("Доставка:", $"{order.DeliveryCost:N2} ₽", regularFont);
            y += 4;
            DrawLine();

            DrawTwoColumns("ИТОГО:", $"{order.Total:N2} ₽", totalFont);

            DrawLine();

            DrawLeft($"Оплата: {order.PaymentMethod}", regularFont);

            if (!string.IsNullOrWhiteSpace(order.Comment))
            {
                y += 2;
                DrawLeft($"Комментарий: {order.Comment}", smallFont);
            }

            y += 15;
            DrawCentered("Спасибо за покупку!", smallFont);
            DrawCentered("Товарный чек действителен без печати", smallFont);

            document.Save(filePath);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => this.Close();
    }
}