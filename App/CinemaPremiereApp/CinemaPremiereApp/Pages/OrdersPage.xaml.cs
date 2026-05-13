using CinemaPremiereApp.Ado;
using CinemaPremiereApp.Classes;
using CinemaPremiereApp.Properties;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Spreadsheet;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.IO;
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
    public partial class OrdersPage : System.Windows.Controls.Page
    {
        // Вспомогательный класс для экспорта/импорта
        public class OrderDto
        {
            public int OrderId { get; set; }
            public string FilmTitle { get; set; }
            public DateTime SessionDate { get; set; }
            public DateTime BuyDate { get; set; }
            public decimal Price { get; set; }
            public int CountTickets { get; set; }
            public decimal TotalSum { get; set; }
            public string PaymentTypeName { get; set; }
            public string Note { get; set; }
        }

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

                // Считываем список шаблонных цен из настроек
                var defaultPrices = new List<int>();
                string savedPrices = Settings.Default.TemplatePrices;

                if (!string.IsNullOrWhiteSpace(savedPrices))
                {
                    foreach (var part in savedPrices.Split(new[] { ',', ' ' },
                        StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (int.TryParse(part, out int price))
                            defaultPrices.Add(price);
                    }
                }

                // Если пользователь оставил пустую строку
                if (defaultPrices.Count == 0)
                {
                    defaultPrices = new List<int> { 250, 300, 350};
                }

                PriceTemplatesItemsControl.ItemsSource = defaultPrices;

                // Загружаем тип оплаты для окна добавления
                AddPaymentTypeComboBox.ItemsSource = payments;

                ApplyFilters();
            }
            catch (Exception ex)
            {
                foreach (var entry in AppData.db.ChangeTracker.Entries().ToList())
                {
                    switch (entry.State)
                    {
                        case EntityState.Added:
                            entry.State = EntityState.Detached;
                            break;
                        case EntityState.Modified:
                        case EntityState.Deleted:
                            entry.State = EntityState.Unchanged;
                            break;
                    }
                }

                await DialogClass.ShowConfirmDialog(
                    "Ошибка при загрузке данных",
                    $"{ex.Message}",
                    "Понятно",
                    "Отмена");
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
                {
                    DateTime endLimit = endSessionDate.Value.AddDays(1);
                    query = query.Where(o => o.SessionDate <= endLimit);
                }
                    
                // Фильтр по дате покупки
                if (startBuyDate.HasValue)
                    query = query.Where(o => o.BuyDate >= startBuyDate.Value);

                if (endBuyDate.HasValue)
                {
                    DateTime endLimit = endBuyDate.Value.AddDays(1);
                    query = query.Where(o => o.BuyDate <= endBuyDate.Value);
                }

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

            // Обновляем статистику
            // Расчет для банка
            var bankData = result.FullFilteredList.Where(o => o.PaymentTypes.Name.ToLower().Contains("банк"));
            decimal bankSum = bankData.Sum(o => (decimal)o.TotalSum);
            int bankCount = bankData.Sum(o => o.CountTickets);

            // Расчет для внешки
            var vneshkaData = result.FullFilteredList.Where(o => o.PaymentTypes.Name.ToLower().Contains("внеш"));
            decimal vneshkaSum = vneshkaData.Sum(o => (decimal)o.TotalSum);
            int vneshkaCount = vneshkaData.Sum(o => o.CountTickets);

            // Расчет для пушки
            var pushkaData = result.FullFilteredList.Where(o => o.PaymentTypes.Name.ToLower().Contains("пушк"));
            decimal pushkaSum = pushkaData.Sum(o => (decimal)o.TotalSum);
            int pushkaCount = pushkaData.Sum(o => o.CountTickets);

            TotalSummaryTextBlock.Text = $"Всего: {result.TotalSum:N0} ₽ / {result.TotalTickets} шт.";
            BankStatsTextBlock.Text = $"Банк: {bankSum:N0} ₽ / {bankCount} шт.";
            CashStatsTextBlock.Text = $"Внешка: {vneshkaSum:N0} ₽ / {vneshkaCount} шт.";
            PushkinStatsTextBlock.Text = $"Пушка: {pushkaSum:N0} ₽ / {pushkaCount} шт.";

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

            // Меняем заголовок диалога
            TitleDialogTextBlock.Text = "Добавление нового заказа";

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
                MessageClass.WarningMessage($"Предупреждение\nВыберите фильм");
                return;
            }
            if (AddSessionDatePicker.SelectedDate == null)
            {
                MessageClass.WarningMessage($"Предупреждение\nВведите дату сеанса");
                return;
            }
            if (AddBuyDatePicker.SelectedDate == null)
            {
                MessageClass.WarningMessage($"Предупреждение\nВыберите дату покупки");
                return;
            }
            if (string.IsNullOrWhiteSpace(AddPriceTextBox.Text))
            {
                MessageClass.WarningMessage($"Предупреждение\nВведите стоимость билета");
                return;
            }
            if (AddPaymentTypeComboBox.SelectedItem == null)
            {
                MessageClass.WarningMessage($"Предупреждение\nВыберите способ оплаты");
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
                        UserId = AppData.CurrentUser.UserId
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

                // Меняем заголовок диалога
                TitleDialogTextBlock.Text = "Добавление нового заказа";

                await LoadDataAsync();

                MessageClass.SuccessMessage($"Успех\nЗаказ {status}");
            }
            catch (Exception ex)
            {
                foreach (var entry in AppData.db.ChangeTracker.Entries().ToList())
                {
                    switch (entry.State)
                    {
                        case EntityState.Added:
                            entry.State = EntityState.Detached;
                            break;
                        case EntityState.Modified:
                        case EntityState.Deleted:
                            entry.State = EntityState.Unchanged;
                            break;
                    }
                }

                await DialogClass.ShowConfirmDialog(
                    "Ошибка при сохранении заказа",
                    $"{ex.Message}",
                    "Понятно",
                    "Отмена");
            }
        }

        private void EditOrderMenuItemButtonClick(object sender, RoutedEventArgs e)
        {
            // Получаем выбранный заказ
            _editingOrder = (sender as MenuItem)?.DataContext as Orders;

            if (_editingOrder == null)
                return;

            // Меняем заголовок диалога
            TitleDialogTextBlock.Text = $"Редактирование заказа #{_editingOrder.OrderId}";

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
                    foreach (var entry in AppData.db.ChangeTracker.Entries().ToList())
                    {
                        switch (entry.State)
                        {
                            case EntityState.Added:
                                entry.State = EntityState.Detached;
                                break;
                            case EntityState.Modified:
                            case EntityState.Deleted:
                                entry.State = EntityState.Unchanged;
                                break;
                        }
                    }

                    await DialogClass.ShowConfirmDialog(
                        "Ошибка при удалении заказа",
                        $"{ex.Message}",
                        "Понятно",
                        "Отмена");
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

        private async void ExportToExcelButtonClick(object sender, RoutedEventArgs e)
        {
            // Собираем отфильтрованные данные
            if (_filteredOrders == null || !_filteredOrders.Any())
            {
                MessageClass.WarningMessage($"Предупреждение\nНет данных для экспорта");
                return;
            }

            // Создаем диалоговое окно сохранения
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog()
            {
                Filter = "Книга Excel (*.xlsx)|*.xlsx",
                FileName = $"Экспорт_заказов_Excel_{DateTime.Now:dd_MM_yyyy}"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    Mouse.OverrideCursor = Cursors.Wait;

                    await Task.Run(() =>
                    {
                        // Создаем пустую книгу Excel в памяти
                        using (var workbook = new XLWorkbook())
                        {
                            // Добавляем лист
                            var worksheet = workbook.Worksheets.Add("Заказы");

                            // Заполнеяем шапку (1 строка)
                            worksheet.Cell(1, 1).Value = "# Заказа";
                            worksheet.Cell(1, 2).Value = "Название фильма";
                            worksheet.Cell(1, 3).Value = "Дата сеанса";
                            worksheet.Cell(1, 4).Value = "Дата покупки";
                            worksheet.Cell(1, 5).Value = "Цена";
                            worksheet.Cell(1, 6).Value = "Кол-во билетов";
                            worksheet.Cell(1, 7).Value = "Сумма";
                            worksheet.Cell(1, 8).Value = "Тип оплаты";
                            worksheet.Cell(1, 9).Value = "Заметка";

                            // Настраиваем стиль шапки
                            var headerRange = worksheet.Range("A1:I1");
                            headerRange.Style.Font.Bold = true;

                            // Заполняем строки данными
                            int currentRow = 2;
                            foreach (var o in _filteredOrders)
                            {
                                worksheet.Cell(currentRow, 1).Value = o.OrderId;
                                worksheet.Cell(currentRow, 2).Value = o.Films?.Title;
                                worksheet.Cell(currentRow, 3).Value = o.SessionDate.ToShortDateString();
                                worksheet.Cell(currentRow, 4).Value = o.BuyDate.ToShortDateString();
                                worksheet.Cell(currentRow, 5).Value = o.Price;
                                worksheet.Cell(currentRow, 6).Value = o.CountTickets;
                                worksheet.Cell(currentRow, 7).Value = o.TotalSum;
                                worksheet.Cell(currentRow, 8).Value = o.PaymentTypes?.Name;
                                worksheet.Cell(currentRow, 9).Value = o.Note;

                                currentRow++;
                            }

                            // Автоматически подбираем ширину столбцов под текст
                            worksheet.Columns().AdjustToContents();

                            // Сохраняем файл по пути, который выбрал пользователь
                            workbook.SaveAs(saveFileDialog.FileName);
                        }
                    });

                    MessageClass.SuccessMessage($"Успех\nДанные сохранены в формате Excel");
                }

                catch (Exception ex)
                {
                    await DialogClass.ShowConfirmDialog(
                        "Ошибка при попытке экспорта",
                        $"{ex.Message}",
                        "Понятно",
                        "Отмена");
                }
                finally
                {
                    Mouse.OverrideCursor = null;
                }
            }
        }

        private async void ExportToCsvButtonClick(object sender, RoutedEventArgs e)
        {
            // Собираем отфильтрованные данные
            if (_filteredOrders == null || !_filteredOrders.Any())
            {
                MessageClass.WarningMessage($"Предупреждение\nНет данных для экспорта");
                return;
            }

            // Создаем диалоговое окно сохранения
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog()
            {
                Filter = "CSV файл (*.csv)|*.csv",
                FileName = $"Экспорт_заказов_CSV_{DateTime.Now:dd_MM_yyyy}"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    Mouse.OverrideCursor = Cursors.Wait;

                    await Task.Run(() =>
                    {
                        var csv = new StringBuilder();

                        // Заполняем шапку (1 строка)
                        csv.AppendLine("Film;SessionDate;BuyDate;Price;CountTickets;PaymentType;Note");

                        // Заполняем данные
                        foreach (var o in _filteredOrders)
                        {
                            // Формируем строку
                            string line = string.Format("{0};{1};{2};{3};{4};{5};{6}",
                                o.Films?.Title,
                                o.SessionDate,
                                o.BuyDate,
                                o.Price,
                                o.CountTickets,
                                o.PaymentTypes?.Name,
                                o.Note);

                            csv.AppendLine(line);
                        }

                        // Сохранение
                        File.WriteAllText(saveFileDialog.FileName, csv.ToString(), Encoding.UTF8);
                    });

                    MessageClass.SuccessMessage($"Успех\nДанные сохранены в формате CSV");
                }
                catch (Exception ex)
                {
                    await DialogClass.ShowConfirmDialog(
                        "Ошибка при попытке экспорта",
                        $"{ex.Message}",
                        "Понятно",
                        "Отмена");
                }
                finally
                {
                    Mouse.OverrideCursor = null;
                }
            }
        }

        private async void ExportToJsonButtonClick(object sender, RoutedEventArgs e)
        {
            // Собираем отфильтрованные данные
            if (_filteredOrders == null || !_filteredOrders.Any())
            {
                MessageClass.WarningMessage($"Предупреждение\nНет данных для экспорта");
                return;
            }

            // Создаем диалоговое окно сохранения
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog()
            {
                Filter = "JSON файл (*.json)|*.json",
                FileName = $"Экспорт_заказов_JSON_{DateTime.Now:dd_MM_yyyy}"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    Mouse.OverrideCursor = Cursors.Wait;

                    await Task.Run(() =>
                    {
                        // Создаем чистый список с нужными полями
                        var dataToExport = _filteredOrders.Select(o => new OrderDto
                        {
                            OrderId = o.OrderId,
                            FilmTitle = o.Films?.Title,
                            SessionDate = o.SessionDate,
                            BuyDate = o.BuyDate,
                            Price = o.Price,
                            CountTickets = o.CountTickets,
                            TotalSum = (decimal)o.TotalSum,
                            PaymentTypeName = o.PaymentTypes?.Name,
                            Note = o.Note
                        }).ToList();

                        // Настройки сериализации
                        var setting = new JsonSerializerSettings
                        {
                            Formatting = Formatting.Indented
                        };

                        // Превращаем список объектов в строку
                        string json = JsonConvert.SerializeObject(dataToExport, setting);

                        // Сохранение
                        File.WriteAllText(saveFileDialog.FileName, json);
                    });

                    MessageClass.SuccessMessage($"Успех\nДанные сохранены в формате JSON");
                }
                catch (Exception ex)
                {
                    await DialogClass.ShowConfirmDialog(
                        "Ошибка при попытке экспорта",
                        $"{ex.Message}",
                        "Понятно",
                        "Отмена");
                }
                finally
                {
                    Mouse.OverrideCursor = null;
                }
            }
        }

        private async void ImportFromCsvButtonClick(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog()
            {
                Filter = "CSV файл (*.csv)|*.csv",
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    Mouse.OverrideCursor = Cursors.Wait;

                    // Читаем все строки и пропускаем шапку
                    var lines = await Task.Run(() =>
                        File.ReadAllLines(openFileDialog.FileName, Encoding.UTF8).ToList());

                    // Если файл пустой или в нем только шапка
                    if (lines.Count <= 1)
                    {
                        MessageClass.WarningMessage("Предупреждение\nФайл пуст или не содержит данных");
                        return;
                    }

                    // Кэшируем данные из БД
                    var filmsDict = await AppData.db.Films.ToDictionaryAsync(f => f.Title.Trim(), StringComparer.OrdinalIgnoreCase);
                    var paymentsDict = await AppData.db.PaymentTypes.ToDictionaryAsync(p => p.Name.Trim(), StringComparer.OrdinalIgnoreCase);

                    int added = 0;

                    for (int i = 1; i < lines.Count; i++)
                    {
                        var parts = lines[i].Split(';');

                        // Пропускаем битые строки
                        if (parts.Length < 6)
                            continue;

                        // Извлекаем сырые данные
                        string filmTitle = parts[0].Trim();
                        string sessionDateRaw = parts[1].Trim();
                        string buyDateRaw = parts[2].Trim();
                        string priceRaw = parts[3].Trim();
                        string countRaw = parts[4].Trim();
                        string paymentTypeRaw = parts[5].Trim();
                        string note = parts.Length > 6 ? parts[6].Trim() : "";

                        string[] dateFormats = { "dd.MM.yyyy", "dd.MM.yyyy H:mm:ss" };

                        // Проверка даты сеанса
                        if (!DateTime.TryParseExact(sessionDateRaw, dateFormats, System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None, out DateTime sessionDate))
                            throw new Exception($"Ошибка в строке {i + 1}:\nНеверный формат даты сеанса '{sessionDate}'. Ожидается ДД.ММ.ГГГГ.");

                        // Проверка даты покупки
                        if (!DateTime.TryParseExact(buyDateRaw, dateFormats, System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None, out DateTime buyDate))
                            throw new Exception($"Ошибка в строке {i + 1}:\nНеверный формат даты покупки '{buyDate}'. Ожидается ДД.ММ.ГГГГ.");

                        // Проверка цены
                        if (!decimal.TryParse(priceRaw.Replace('.', ','), out decimal price))
                            throw new Exception($"Ошибка в строке {i + 1}:\nОжидалась цена (число), а получено '{priceRaw}'.");

                        // Проверка количества билетов
                        if (!int.TryParse(countRaw, out int countTickets))
                            throw new Exception($"Ошибка в строке {i + 1}:\nОжидалось количество билетов (целое число), а получено '{countRaw}'.");

                        // Ищем фильм
                        if (!filmsDict.TryGetValue(filmTitle, out var film))
                            throw new Exception($"Ошибка в строке {i + 1}:\nФильм '{filmTitle}' не найден в базе данных\n Сначала добавьте этот фильм в разделе 'Фильмы'.");

                        // Ищем тип оплаты
                        if (!paymentsDict.TryGetValue(paymentTypeRaw, out var payment))
                            throw new Exception($"Ошибка в строке {i + 1}:\nТип оплаты '{paymentTypeRaw}' не найден в базе данных.");

                        AppData.db.Orders.Add(new Orders
                        {
                            FilmId = film.FilmId,
                            SessionDate = sessionDate,
                            BuyDate = buyDate,
                            Price = price,
                            CountTickets = countTickets,
                            PaymentTypeId = payment.PaymentTypeId,
                            Note = note,
                            UserId = AppData.CurrentUser.UserId
                        });
                        added++;
                    }

                    await AppData.db.SaveChangesAsync();
                    await LoadDataAsync();

                    MessageClass.SuccessMessage($"Успех\nЗаказов импортировано: {added}");
                }
                catch (Exception ex)
                {
                    foreach (var entry in AppData.db.ChangeTracker.Entries().ToList())
                    {
                        switch (entry.State)
                        {
                            case EntityState.Added:
                                entry.State = EntityState.Detached;
                                break;
                            case EntityState.Modified:
                            case EntityState.Deleted:
                                entry.State = EntityState.Unchanged;
                                break;
                        }
                    }

                    await DialogClass.ShowConfirmDialog(
                        "Ошибка при попытке импорта",
                        $"{ex.Message}",
                        "Понятно",
                        "Отмена");
                }
                finally
                {
                    Mouse.OverrideCursor = null;
                }
            }
        }
    }
}
