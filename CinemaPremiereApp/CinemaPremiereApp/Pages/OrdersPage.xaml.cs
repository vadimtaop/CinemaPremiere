using CinemaPremiereApp.Ado;
using CinemaPremiereApp.Classes;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CinemaPremiereApp.Pages
{
    /// <summary>
    /// Логика взаимодействия для OrdersPage.xaml
    /// </summary>
    public partial class OrdersPage : Page
    {
        // Основной список из базы
        List<Orders> allOrders = new List<Orders>();

        // Список после фильтров (для пагинации и вывода)
        private List<Orders> _filteredOrders;

        // Переменные для поиска и сброса
        private CancellationTokenSource _searchCts;
        private bool _isResetting = false;

        // Перменные для пагинации
        int currentPage = 1;
        int itemsPerPage = 10;
        int totalPages = 1;

        // Список для хранения строк, которые инвертировали за один клик
        private HashSet<Orders> _processedOrders = new HashSet<Orders>();

        // Переменная для редактирования
        private Orders _editingOrder = null;

        public OrdersPage()
        {
            InitializeComponent();

            Dispatcher.BeginInvoke(new Action(async () => await LoadDataAsync()));
        }

        private async Task LoadDataAsync()
        {
            try
            {
                // Сохраняем данные в список
                allOrders = await AppData.db.Orders
                    .Include(o => o.Films)
                    .Include(o => o.PaymentTypes)
                    .OrderByDescending(o => o.OrderId)
                    .ToListAsync();

                // Загружаем типы оплаты для фильтры
                var payments = await AppData.db.PaymentTypes.ToListAsync();
                PaymentTypeListBox.ItemsSource = payments;

                // Загружаем список фильмов в окно добавления
                AddFilmComboBox.ItemsSource = await AppData.db.Films
                    .OrderBy(f => f.Title)
                    .ToListAsync();

                // Список шаблонных цен (заменить в настройках)
                var defaultPrices = new List<int> { 250, 260, 300 };
                PriceTemplatesItemsControl.ItemsSource = defaultPrices;

                // Загружаем тип оплаты для окна добавления
                AddPaymentTypeComboBox.ItemsSource = payments;

                ApplyFilters();
            }
            catch (Exception ex)
            {
                MessageClass.ErrorMessage($"Ошибка\n{ex.Message}");
            }
        }

        public async void ApplyFilters()
        {
            if (OrdersDataGrid == null || SearchTextBox == null || _isResetting)
                return;

            string searchText = SearchTextBox.Text.ToLower().Trim();

            // Получаем состояние чекбокса
            bool onlyWithNotes = OnlyWithNotesCheckBox.IsChecked == true;

            var selectedPaymentIds = PaymentTypeListBox.SelectedItems
                .Cast<PaymentTypes>()
                .Select(p => p.PaymentTypeId)
                .ToList();

            DateTime? startSessionDate = StartSessionDatePicker.SelectedDate;
            DateTime? endSessionDate = EndSessionDatePicker.SelectedDate;

            DateTime? startBuyDate = StartBuyDatePicker.SelectedDate;
            DateTime? endBuyDate = EndBuyDatePicker.SelectedDate;

            int sortIndex = SortComboBox.SelectedIndex;

            int itemsPerPageLocal = itemsPerPage;
            int currentPageLocal = currentPage;

            var result = await Task.Run(() =>
            {
                var query = allOrders.AsQueryable();

                // Поиск по названию или номеру
                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    if (searchText.StartsWith("#"))
                    {
                        // Отрезаем решетку и пробелы вокруг
                        string idPart = searchText.TrimStart('#').Trim();

                        if (int.TryParse(idPart, out int searchId))
                        {
                            // Ищем строгое совпадение по Id заказа
                            query = query.Where(o => o.OrderId == searchId);
                        }
                    }
                    else
                    {
                        query = query.Where(o => o.Films.Title.ToLower().Contains(searchText));
                    }
                }

                // Фильтр по наличию заметок
                if (onlyWithNotes)
                    query = query.Where(o => !string.IsNullOrWhiteSpace(o.Note));

                // Фильтр по дате сеанса
                if (startSessionDate.HasValue)
                    query = query.Where(o => o.SessionDate >= startSessionDate.Value);

                if (endSessionDate.HasValue)
                    query = query.Where(o => o.SessionDate <= endSessionDate.Value);

                // Фильтр по дате покупки
                if (startBuyDate.HasValue)
                    query = query.Where(o => o.BuyDate >= startBuyDate.Value);

                if (endBuyDate.HasValue)
                    query = query.Where(o => o.BuyDate <= endBuyDate.Value);

                // Тип оплаты
                if (selectedPaymentIds.Any())
                {
                    query = query.Where(o => selectedPaymentIds.Contains(o.PaymentTypeId));
                }

                // Сортировка
                switch (sortIndex)
                {
                    case 1:
                        query = query.OrderByDescending(o => o.BuyDate);
                        break;
                    case 2:
                        query = query.OrderByDescending(o => o.SessionDate);
                        break;
                    case 3:
                        query = query.OrderBy(o => o.Films.Title);
                        break;
                    case 4:
                        query = query.OrderByDescending(o => o.TotalSum);
                        break;
                    case 5:
                        query = query.OrderByDescending(o => o.CountTickets);
                        break;
                    case 6:
                        query = query.OrderBy(o => o.PaymentTypes.Name);
                        break;
                    default:
                        query = query.OrderByDescending(o => o.OrderId);
                        break;
                }

                // Получаем отфильтрованный список
                var filteredList = query.ToList();

                // Счет выручки
                decimal totalSum = filteredList.Sum(o => (decimal)o.TotalSum);
                int totalTickets = filteredList.Sum(o => o.CountTickets);

                // Расчет пагинации
                int totalCount = filteredList.Count;
                int tPages = (int)Math.Ceiling((double)totalCount / itemsPerPageLocal);

                if (tPages < 1)
                    tPages = 1;

                // Корректируем текущую страницу
                int cPage = currentPageLocal;
                if (cPage > tPages)
                    cPage = tPages;
                if (cPage < 1)
                    cPage = 1;

                // Нарезаем данные для текущей страницы
                var pagedList = filteredList
                    .Skip((cPage - 1) * itemsPerPageLocal)
                    .Take(itemsPerPageLocal)
                    .ToList();

                return new
                {
                    FullFilteredList = filteredList,
                    PagedList = pagedList,
                    TotalCount = totalCount,
                    TotalPages = tPages,
                    CorrectedPage = cPage,
                    TotalSum = totalSum,
                    TotalTickets = totalTickets
                };
            });

            // Выводим в UI
            _filteredOrders = result.FullFilteredList;
            totalPages = result.TotalPages;
            currentPage = result.CorrectedPage;

            OrdersDataGrid.ItemsSource = result.PagedList;

            // Обновление счетчиков
            PageInputTextBox.Text = currentPage.ToString();
            PageInfoTextBlock.Text = $"из {totalPages}";
            CounterTextBlock.Text = $"Найдено: {result.TotalCount} из {allOrders.Count}";

            TotalSumTextBlock.Text = $"Выручка: {result.TotalSum:N0} ₽";
            TotalTicketsTextBlock.Text = $"Билетов: {result.TotalTickets} шт.";

            // Проверка на пустой список
            if (result.TotalCount == 0)
            {
                OrdersDataGrid.Visibility = Visibility.Collapsed;
                EmptyStackPanel.Visibility = Visibility.Visible;
            }
            else
            {
                OrdersDataGrid.Visibility = Visibility.Visible;
                EmptyStackPanel.Visibility = Visibility.Collapsed;
            }
        }

        private async void SearchTextBoxTextChanged(object sender, TextChangedEventArgs e)
        {
            _searchCts?.Cancel();
            _searchCts?.Dispose();
            _searchCts = new CancellationTokenSource();

            try
            {
                await Task.Delay(300, _searchCts.Token);
                ApplyFilters();
            }
            catch (OperationCanceledException)
            {

            }
        }

        private void FilterSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isResetting && DatePresetComboBox != null)
                DatePresetComboBox.SelectedIndex = 0;

            ApplyFilters();
        }

        private void ResetFiltersButtonClick(object sender, RoutedEventArgs e)
        {
            _isResetting = true;

            SearchTextBox.Text = "";
            StartSessionDatePicker.SelectedDate = null;
            EndSessionDatePicker.SelectedDate = null;
            StartBuyDatePicker.SelectedDate = null;
            EndBuyDatePicker.SelectedDate = null;
            DatePresetComboBox.SelectedIndex = 0;
            PaymentTypeListBox.SelectedItems.Clear();
            SortComboBox.SelectedIndex = 0;
            ShowNotesCheckBox.IsChecked = true;
            OnlyWithNotesCheckBox.IsChecked = false;

            _isResetting = false;
            ApplyFilters();
        }

        private void FirstPageButtonClick(object sender, RoutedEventArgs e)
        {
            currentPage = 1;
            ApplyFilters();
        }

        private void LastPageButtonClick(object sender, RoutedEventArgs e)
        {
            currentPage = totalPages;
            ApplyFilters();
        }

        private void NextPageButtonClick(object sender, RoutedEventArgs e)
        {
            if (currentPage < totalPages)
            {
                currentPage++;
                ApplyFilters();
            }
        }

        private void PrevPageButtonClick(object sender, RoutedEventArgs e)
        {
            if (currentPage > 1)
            {
                currentPage--;
                ApplyFilters();
            }
        }

        private void PageInputTextBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (int.TryParse(PageInputTextBox.Text, out int requestedPage)
                        && requestedPage <= totalPages)
                {
                    currentPage = requestedPage;
                    ApplyFilters();
                }
                else
                {
                    PageInputTextBox.Text = currentPage.ToString();
                }
            }
        }

        private void PageSizeComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PageSizeComboBox.SelectedItem == null || allOrders == null)
                return;

            var selectedItem = PageSizeComboBox.SelectedItem as ComboBoxItem;

            if (selectedItem != null)
            {
                itemsPerPage = Convert.ToInt32(selectedItem.Tag);
                currentPage = 1;
                ApplyFilters();
            }
        }

        private void PreviewMouseRightButtonDownDataGrid(object sender, MouseButtonEventArgs e)
        {
            // Блокируем стандартное поведение, чтобы не прогало выделение
            e.Handled = true;

            // Находим строку по которой кликнули
            DataGridRow row = sender as DataGridRow;

            if (row != null)
            {
                // Фокусируем строку
                row.Focus();

                // Запоминаем заказ, на который кликнули
                _editingOrder = row.DataContext as Orders;

                // Открываем контекстное меню
                if (OrdersDataGrid.ContextMenu != null)
                {
                    OrdersDataGrid.ContextMenu.PlacementTarget = row;
                    OrdersDataGrid.ContextMenu.IsOpen = true;
                }
            }
        }

        private void PreviewMouseLeftButtonDownDataGrid(object sender, MouseButtonEventArgs e)
        {
            // Находим строку, по которой кликнули
            DataGridRow row = sender as DataGridRow;

            if (row != null)
            {
                // Очищаем историю текущего выделения
                _processedOrders.Clear();

                var order = row.DataContext as Orders;

                if (order != null)
                {
                    row.IsSelected = !row.IsSelected;
                    _processedOrders.Add(order);
                }

                e.Handled = true;
                row.Focus();
            }
        }

        private void MouseEnterDataGrid(object sender, MouseEventArgs e)
        {
            // Если ЛКМ зажата
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DataGridRow row = sender as DataGridRow;
                var order = row?.DataContext as Orders;

                if (order != null && !_processedOrders.Contains(order))
                {
                    row.IsSelected = !row.IsSelected;
                    _processedOrders.Add(order);
                }
            }
        }

        private void OrdersDataGridSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int count = OrdersDataGrid.SelectedItems.Count;

            if (count > 0)
            {
                SelectionPanel.Visibility = Visibility.Visible;
                SelectionCountTextBlock.Text = $"Выбрано: {count}";
            }
            else
            {
                SelectionPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void ClearSelectionButtonClick(object sender, RoutedEventArgs e)
        {
            OrdersDataGrid.UnselectAll();
        }

        private void AddOrderButtonClick(object sender, RoutedEventArgs e)
        {
            _editingOrder = null;

            // Очистка полей
            AddFilmComboBox.SelectedIndex = -1;
            AddSessionDatePicker.SelectedDate = DateTime.Now;
            AddBuyDatePicker.SelectedDate = DateTime.Now;
            AddPriceTextBox.Text = string.Empty;
            AddCountTicketsUpDown.Value = 1;
            AddPaymentTypeComboBox.SelectedIndex = -1;
            AddNoteTextBox.Text = string.Empty;

            // Отображение диалога
            MainDialogHost.IsOpen = true;
        }

        private void QuickPriceButtonClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                AddPriceTextBox.Text = button.Tag.ToString();

                AddCountTicketsUpDown.Focus();
            }
        }

        private void CalculateTotalSum(object sender, TextChangedEventArgs e)
        {
            // Проверка на пустые поля
            if (AddPriceTextBox == null ||
                AddCountTicketsUpDown == null ||
                AddTotalSumTextBox == null)
                return;

            decimal.TryParse(AddPriceTextBox?.Text, out decimal price);
            int count = (int)AddCountTicketsUpDown.Value;

            AddTotalSumTextBox.Text = (price * count).ToString("N0");
        }

        private void AddCountTicketUpDownValueChanged(object sender, RoutedPropertyChangedEventArgs<int> e)
        {
            if (AddCountTicketsUpDown == null)
                return;

            if (AddCountTicketsUpDown.Value > AddCountTicketsUpDown.Maximum)
                AddCountTicketsUpDown.Value = AddCountTicketsUpDown.Maximum;

            if (AddCountTicketsUpDown.Value < AddCountTicketsUpDown.Minimum)
                AddCountTicketsUpDown.Value = AddCountTicketsUpDown.Minimum;

            CalculateTotalSum(null, null);
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            // Разрешаем вводить только цифры
            e.Handled = !int.TryParse(e.Text, out _);
        }

        private void SpaceValidationPreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Блокируем пробел
            if (e.Key == Key.Space)
            {
                e.Handled = true;
            }
        }

        private async void SaveOrderButtonClick(object sender, RoutedEventArgs e)
        {
            // Проверки на заполненные поля
            if (AddFilmComboBox.SelectedItem == null)
            {
                MessageClass.ErrorMessage($"Ошибка\nВыберите фильм");
                return;
            }
            if (AddSessionDatePicker.SelectedDate == null)
            {
                MessageClass.ErrorMessage($"Ошибка\nВведите дату сеанса");
                return;
            }
            if (AddBuyDatePicker.SelectedDate == null)
            {
                MessageClass.ErrorMessage($"Ошибка\nВыберите дату покупки");
                return;
            }
            if (string.IsNullOrWhiteSpace(AddPriceTextBox.Text))
            {
                MessageClass.ErrorMessage($"Ошибка\nВведите стоимость билета");
                return;
            }
            if (AddPaymentTypeComboBox.SelectedItem == null)
            {
                MessageClass.ErrorMessage($"Ошибка\nВыберите способ оплаты");
                return;
            }

            try
            {
                var selectedFilm = AddFilmComboBox.SelectedItem as Films;
                var selectedPayment = AddPaymentTypeComboBox.SelectedItem as PaymentTypes;

                if (_editingOrder == null)
                {
                    // Добавление
                    Orders newOrder = new Orders
                    {
                        FilmId = selectedFilm.FilmId,
                        SessionDate = AddSessionDatePicker.SelectedDate.Value,
                        BuyDate = AddBuyDatePicker.SelectedDate.Value,
                        Price = decimal.Parse(AddPriceTextBox.Text),
                        CountTickets = (int)AddCountTicketsUpDown.Value,
                        PaymentTypeId = selectedPayment.PaymentTypeId,
                        Note = AddNoteTextBox.Text,
                        UserId = 1
                    };

                    AppData.db.Orders.Add(newOrder);
                }
                else
                {
                    // Редактирование
                    _editingOrder.FilmId = selectedFilm.FilmId;
                    _editingOrder.SessionDate = AddSessionDatePicker.SelectedDate.Value;
                    _editingOrder.BuyDate = AddBuyDatePicker.SelectedDate ?? DateTime.Now;
                    _editingOrder.Price = decimal.Parse(AddPriceTextBox.Text);
                    _editingOrder.CountTickets = (int)AddCountTicketsUpDown.Value;
                    _editingOrder.PaymentTypeId = selectedPayment.PaymentTypeId;
                    _editingOrder.Note = AddNoteTextBox.Text;
                }
                
                // Сохраняем изменения в БД
                await AppData.db.SaveChangesAsync();

                string status = _editingOrder == null ? "добавлен" : "обновлен";

                MainDialogHost.IsOpen = false;
                _editingOrder = null;

                await LoadDataAsync();

                MessageClass.SuccessMessage($"Успех\nЗаказ {status}");
            }
            catch (Exception ex)
            {
                MessageClass.ErrorMessage($"Ошибка\n{ex.Message}");
            }
        }

        private void EditOrderMenuItemButtonClick(object sender, RoutedEventArgs e)
        {
            // Получаем выбранный заказ
            _editingOrder = (sender as MenuItem)?.DataContext as Orders;

            if (_editingOrder == null)
                return;

            // Заполняем поля
            AddFilmComboBox.SelectedItem = AddFilmComboBox.Items.Cast<Films>()
                .FirstOrDefault(f => f.FilmId == _editingOrder.FilmId);

            AddSessionDatePicker.SelectedDate = _editingOrder.SessionDate;
            AddBuyDatePicker.SelectedDate = _editingOrder.BuyDate;

            AddPriceTextBox.Text = _editingOrder.Price.ToString();
            AddCountTicketsUpDown.Value = _editingOrder.CountTickets;

            AddPaymentTypeComboBox.SelectedItem = AddPaymentTypeComboBox.Items.Cast<PaymentTypes>()
                .FirstOrDefault(p => p.PaymentTypeId == _editingOrder.PaymentTypeId);

            AddNoteTextBox.Text = _editingOrder.Note;

            // Отображение диалога
            MainDialogHost.IsOpen = true;
        }

        private async void DeleteOrderMenuItemButtonClick(object sender, RoutedEventArgs e)
        {
            // Получаем выбранный фильм
            var order = (sender as MenuItem)?.DataContext as Orders ?? _editingOrder;

            if (order != null)
                await ExecuteDeleteAsync(new List<Orders> { order });
        }

        private async void DeleteSelectionOrdersButtoncClick(object sender, RoutedEventArgs e)
        {
            var selected = OrdersDataGrid.SelectedItems.Cast<Orders>().ToList();

            if (selected.Any())
                await ExecuteDeleteAsync(selected);
        }

        // Общий метод удаления
        private async Task ExecuteDeleteAsync(List<Orders> ordersToDelete)
        {
            if (ordersToDelete == null || !ordersToDelete.Any())
                return;

            // Подтверждение пользователя
            string message = ordersToDelete.Count == 1
                ? $"Вы точно хотите удалить заказ #{ordersToDelete[0].OrderId}?"
                : $"Вы точно хотите удалить выбранные заказы ({ordersToDelete.Count} шт.)?";

            bool isConfirmed = await DialogClass.ShowConfirmDialog(
                "Удаление данных",
                message,
                "Удалить",
                "Отмена");

            if (isConfirmed)
            {
                try
                {
                    foreach (var order in ordersToDelete)
                        AppData.db.Orders.Remove(order);

                    await AppData.db.SaveChangesAsync();

                    await LoadDataAsync();
                    MessageClass.SuccessMessage($"Успех\nДанные удалены");
                }
                catch (Exception ex)
                {
                    MessageClass.ErrorMessage($"Ошибка\n{ex.Message}");
                }
            }
        }

        private void FilterCheckBoxClick(object sender, RoutedEventArgs e)
        {
            ApplyFilters();
        }

        private void DatePresetComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DatePresetComboBox == null ||
                StartBuyDatePicker == null ||
                EndBuyDatePicker == null)
                return;

            var selectedItem = DatePresetComboBox.SelectedItem as ComboBoxItem;

            if (selectedItem == null || selectedItem.Tag == null)
                return;

            string tag = selectedItem.Tag.ToString();

            if (tag == "Custom")
                return;

            _isResetting = true;

            DateTime today = DateTime.Today;

            switch (tag)
            {
                case "Today":
                    StartBuyDatePicker.SelectedDate = today;
                    EndBuyDatePicker.SelectedDate = today;
                    break;
                case "Yesterday":
                    StartBuyDatePicker.SelectedDate = today.AddDays(-1);
                    EndBuyDatePicker.SelectedDate = today.AddDays(-1);
                    break;
                case "Week":
                    int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
                    StartBuyDatePicker.SelectedDate = today.AddDays(-1 * diff);
                    EndBuyDatePicker.SelectedDate = today;
                    break;
                case "Month":
                    StartBuyDatePicker.SelectedDate = new DateTime(today.Year, today.Month, 1);
                    EndBuyDatePicker.SelectedDate = today;
                    break;
            }

            _isResetting = false;
            ApplyFilters();
        }
    }
}
