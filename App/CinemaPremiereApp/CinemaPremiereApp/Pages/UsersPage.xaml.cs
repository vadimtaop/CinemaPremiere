using CinemaPremiereApp.Ado;
using CinemaPremiereApp.Classes;
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Security.Cryptography;
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
    /// Логика взаимодействия для UsersPage.xaml
    /// </summary>
    public partial class UsersPage : Page
    {
        private List<Users> allUsers = new List<Users>();
        private Users _editingUser = null;
        private CancellationTokenSource _searchCts;
        private bool _isResetting = false;

        int currentPage = 1;
        int itemsPerPage = 10;
        int totalPages = 1;

        public UsersPage()
        {
            InitializeComponent();
            Dispatcher.BeginInvoke(new Action(async () 
                => await LoadDataAsync()));
        }

        private async Task LoadDataAsync()
        {
            try
            {
                var roles = await AppData.db.Roles
                    .OrderBy(r => r.Name)
                    .ToListAsync();

                var filterRoles = new List<Roles> 
                { 
                    new Roles 
                    { 
                        RoleId = 0,
                        Name = "Все роли" 
                    } 
                };

                filterRoles.AddRange(roles);

                RoleFilterComboBox.ItemsSource = filterRoles;
                RoleFilterComboBox.DisplayMemberPath = "Name";
                RoleFilterComboBox.SelectedValuePath = "RoleId";

                _isResetting = true;
                RoleFilterComboBox.SelectedIndex = 0;
                _isResetting = false;

                RoleComboBox.ItemsSource = roles;
                RoleComboBox.DisplayMemberPath = "Name";
                RoleComboBox.SelectedValuePath = "RoleId";

                allUsers = await AppData.db.Users.Include(u => u.Roles).ToListAsync();

                if (SortComboBox.SelectedIndex == -1 || SortComboBox.SelectedIndex == 0)
                {
                    _isResetting = true;
                    SortComboBox.SelectedIndex = 2;
                    _isResetting = false;
                }

                ApplyFilters();
            }
            catch (Exception ex)
            {
                CleanTracker();

                await DialogClass.ShowConfirmDialog(
                    "Ошибка при загрузке данных",
                    $"{ex.Message}",
                    "Понятно",
                    "Отмена");
            }
        }

        public async void ApplyFilters()
        {
            if (UsersDataGrid == null || SearchTextBox == null || _isResetting) return;

            string searchText = SearchTextBox.Text.ToLower().Trim();
            int selectedRoleId = RoleFilterComboBox.SelectedValue != null ? (int)RoleFilterComboBox.SelectedValue : 0;
            int sortIndex = SortComboBox.SelectedIndex;

            int itemsPerPageLocal = itemsPerPage;
            int currentPageLocal = currentPage;

            var result = await Task.Run(() =>
            {
                var query = allUsers.AsQueryable();

                if (!string.IsNullOrWhiteSpace(searchText))
                    query = query.Where(u => u.Login.ToLower().Contains(searchText));

                if (selectedRoleId != 0)
                    query = query.Where(u => u.RoleId == selectedRoleId);

                switch (sortIndex)
                {
                    case 1: 
                        query = query.OrderBy(u => u.Login);
                        break;
                    case 2: 
                        query = query.OrderBy(u => u.RoleId).ThenBy(u => u.Login); 
                        break;
                    case 3: 
                        query = query.OrderByDescending(u => u.LockoutEnd != null).ThenBy(u => u.Login);
                        break;
                    default:
                        query = query.OrderByDescending(u => u.UserId);
                        break;
                }

                var filteredList = query.ToList();
                int totalCount = filteredList.Count;
                int tPages = (int)Math.Ceiling((double)totalCount / itemsPerPageLocal);
                if (tPages < 1) tPages = 1;

                int cPage = currentPageLocal;
                if (cPage > tPages) cPage = tPages;
                if (cPage < 1) cPage = 1;

                var pagedList = filteredList.Skip((cPage - 1) * itemsPerPageLocal).Take(itemsPerPageLocal).ToList();

                return new 
                { 
                    PagedList = pagedList, 
                    TotalCount = totalCount,
                    TotalPages = tPages, 
                    CorrectedPage = cPage 
                };
            });

            totalPages = result.TotalPages;
            currentPage = result.CorrectedPage;

            UsersDataGrid.ItemsSource = result.PagedList;

            PageInputTextBox.Text = currentPage.ToString();
            PageInfoTextBlock.Text = $"из {totalPages}";
            CounterTextBlock.Text = $"Найдено: {result.TotalCount} из {allUsers.Count}";

            if (result.TotalCount == 0)
            {
                UsersDataGrid.Visibility = Visibility.Collapsed;
                EmptyStackPanel.Visibility = Visibility.Visible;
            }
            else
            {
                UsersDataGrid.Visibility = Visibility.Visible;
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

        private void FilterSelectionChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void FilterSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void PageSizeComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PageSizeComboBox.SelectedItem is ComboBoxItem item)
            {
                itemsPerPage = Convert.ToInt32(item.Tag);
                currentPage = 1;
                ApplyFilters();
            }
        }

        private void ResetFiltersButtonClick(object sender, RoutedEventArgs e)
        {
            _isResetting = true;
            SearchTextBox.Text = "";
            RoleFilterComboBox.SelectedIndex = 0;
            SortComboBox.SelectedIndex = 2;
            currentPage = 1;
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
                currentPage++; ApplyFilters(); 
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
                if (int.TryParse(PageInputTextBox.Text, out int req) && req <= totalPages) 
                {
                    currentPage = req;
                    ApplyFilters();
                }
                else 
                    PageInputTextBox.Text = currentPage.ToString();
            }
        }

        private void AddUserButtonClick(object sender, RoutedEventArgs e)
        {
            _editingUser = null;
            LoginTextBox.Text = string.Empty;
            PasswordBox.Password = string.Empty;
            RoleComboBox.SelectedIndex = -1;

            HintAssist.SetHint(PasswordBox, "Пароль");

            MainDialogHost.IsOpen = true;
        }

        private void EditUserMenuItemButtonClick(object sender, RoutedEventArgs e)
        {
            var user = (sender as MenuItem)?.DataContext as Users ?? UsersDataGrid.SelectedItem as Users;
            if (user == null) return;

            _editingUser = user;
            LoginTextBox.Text = user.Login;
            PasswordBox.Password = string.Empty;
            RoleComboBox.SelectedValue = user.RoleId;

            HintAssist.SetHint(PasswordBox, "Пароль (пусто = без изменений)");

            MainDialogHost.IsOpen = true;
        }

        private async void SaveUserButtonClick(object sender, RoutedEventArgs e)
        {
            string login = LoginTextBox.Text.Trim();
            string password = PasswordBox.Password.Trim();

            if (string.IsNullOrWhiteSpace(login) || RoleComboBox.SelectedItem == null)
            {
                MessageClass.WarningMessage("Предупреждение\nЛогин и Роль обязательны для заполнения");
                return;
            }

            if (login.Length < 4)
            {
                MessageClass.WarningMessage("Предупреждение\nДлина логина должна быть не менее 4-х символов");
                return;
            }

            try
            {
                int roleId = (int)RoleComboBox.SelectedValue;

                if (_editingUser == null)
                {
                    if (string.IsNullOrWhiteSpace(password))
                    {
                        MessageClass.WarningMessage("Предупреждение\nДля нового пользователя необходимо задать пароль");
                        return;
                    }

                    if (password.Length < 4)
                    {
                        MessageClass.WarningMessage("Предупреждение\nДлина пароля должна быть не менее 4-х символов");
                        return;
                    }

                    if (await AppData.db.Users.AnyAsync(u => u.Login == login))
                    {
                        MessageClass.WarningMessage("Предупреждение\nПользователь с таким логином уже существует");
                        return;
                    }

                    var newUser = new Users
                    {
                        Login = login,
                        Password = textToHash(password),
                        RoleId = roleId,
                        FailedAttempts = 0,
                        LockoutEnd = null
                    };

                    AppData.db.Users.Add(newUser);
                    await AppData.db.SaveChangesAsync();

                    MessageClass.SuccessMessage("Успех\nУчетная запись создана");
                }
                else
                {
                    if (_editingUser.Login != login && await AppData.db.Users.AnyAsync(u => u.Login == login))
                    {
                        MessageClass.WarningMessage("Предупреждение\nПользователь с таким логином уже существует");
                        return;
                    }

                    _editingUser.Login = login;
                    _editingUser.RoleId = roleId;

                    if (!string.IsNullOrWhiteSpace(password))
                    {
                        if (password.Length < 4)
                        {
                            MessageClass.WarningMessage("Предупреждение\nДлина нового пароля должна быть не менее 4-х символов");
                            return;
                        }
                        _editingUser.Password = textToHash(password);
                    }

                    await AppData.db.SaveChangesAsync();
                    MessageClass.SuccessMessage("Успех\nДанные пользователя обновлены");
                }

                MainDialogHost.IsOpen = false;
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                CleanTracker();
                await DialogClass.ShowConfirmDialog(
                    "Ошибка при сохранении",
                    $"{ex.Message}",
                    "Понятно",
                    "Отмена");
            }
        }

        private async void DeleteUserMenuItemButtonClick(object sender, RoutedEventArgs e)
        {
            var user = (sender as MenuItem)?.DataContext as Users ?? UsersDataGrid.SelectedItem as Users;
            if (user == null) return;

            if (user.UserId == AppData.CurrentUser.UserId)
            {
                MessageClass.WarningMessage("Предупреждение\nВы не можете удалить свою учетную запись");
                return;
            }

            bool isConfirmed = await DialogClass.ShowConfirmDialog(
                "Удаление",
                $"Вы точно хотите удалить пользователя '{user.Login}'?", 
                "Удалить",
                "Отмена");

            if (isConfirmed)
            {
                try
                {
                    AppData.db.Users.Remove(user);
                    await AppData.db.SaveChangesAsync();

                    MessageClass.SuccessMessage("Успех\nПользователь удален из системы");

                    await LoadDataAsync();
                }
                catch (Exception ex)
                {
                    CleanTracker();
                    await DialogClass.ShowConfirmDialog(
                        "Ошибка при удалении",
                        $"{ex.Message}",
                        "Понятно",
                        "Отмена");
                }
            }
        }

        private async void UnlockUserMenuItemButtonClick(object sender, RoutedEventArgs e)
        {
            var user = (sender as MenuItem)?.DataContext as Users ?? UsersDataGrid.SelectedItem as Users;

            if (user == null)
                return;

            bool isConfirmed = await DialogClass.ShowConfirmDialog(
                "Снятие блокировки",
                $"Аккаунт пользователя '{user.Login}' был заблокирован из-за неверного ввода пароля.\nСнять блокировку?",
                "Разблокировать",
                "Отмена");

            if (isConfirmed)
            {
                try
                {
                    user.FailedAttempts = 0;
                    user.LockoutEnd = null;

                    await AppData.db.SaveChangesAsync();

                    MessageClass.SuccessMessage("Успех\nУчетная запись разблокирована");

                    await LoadDataAsync();
                }
                catch (Exception ex)
                {
                    CleanTracker();
                    await DialogClass.ShowConfirmDialog(
                        "Ошибка при разблокировке",
                        $"Не удалось разблокировать пользователя: {ex.Message}",
                        "Понятно",
                        "Отмена");
                }
            }
        }

        private void PreviewMouseRightButtonDownDataGrid(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (sender is DataGridRow row)
            {
                row.Focus();
                if (UsersDataGrid.ContextMenu != null)
                {
                    var user = row.DataContext as Users;

                    var unlockMenuItem = UsersDataGrid.ContextMenu.Items.OfType<MenuItem>().FirstOrDefault(m => m.Name == "UnlockMenuItem");

                    if (unlockMenuItem != null)
                    {
                        if (user != null && user.IsLockedOut)
                        {
                            unlockMenuItem.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            unlockMenuItem.Visibility = Visibility.Collapsed;
                        }
                    }

                    UsersDataGrid.ContextMenu.PlacementTarget = row;
                    UsersDataGrid.ContextMenu.IsOpen = true;
                }
            }
        }

        private string textToHash(string text)
        {
            var bytes = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(text));
            return BitConverter.ToString(bytes).Replace("-", "").ToLower();
        }

        private void CleanTracker()
        {
            foreach (var entry in AppData.db.ChangeTracker.Entries().ToList())
            {
                if (entry.State == EntityState.Added)
                    entry.State = EntityState.Detached;
                else if (entry.State == EntityState.Modified || entry.State == EntityState.Deleted)
                    entry.State = EntityState.Unchanged;
            }
        }
    }
}
