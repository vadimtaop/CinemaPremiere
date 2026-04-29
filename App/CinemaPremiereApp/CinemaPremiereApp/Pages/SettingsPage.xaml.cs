using CinemaPremiereApp.Ado;
using CinemaPremiereApp.Classes;
using CinemaPremiereApp.Properties;
using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
    /// Логика взаимодействия для SettingsPage.xaml
    /// </summary>
    public partial class SettingsPage : Page
    {
        private readonly PaletteHelper _paletteHelper = new PaletteHelper();

        public SettingsPage()
        {
            InitializeComponent();

            LoadProjectSettings();
            ApplyPermissions();
        }

        private void LoadProjectSettings()
        {
            // Загрузка цвета
            if (!string.IsNullOrEmpty(Settings.Default.PrimaryColor))
            {
                foreach (ComboBoxItem item in ColorComboBox.Items)
                {
                    if (item.Tag?.ToString() == Settings.Default.PrimaryColor)
                    {
                        ColorComboBox.SelectedItem = item;
                        break;
                    }
                }
            }

            // Загрузка пути к картинке
            if (string.IsNullOrEmpty(Settings.Default.CustomAuthImagePath))
            {
                AuthImagePathTextBlock.Text = "По умолчанию";
                AuthImagePathTextBlock.ToolTip = null;
            }
            else
            {
                // Показываем только имя, а в подсказке полный путь
                AuthImagePathTextBlock.Text = System.IO.Path.GetFileName(Settings.Default.CustomAuthImagePath);
                AuthImagePathTextBlock.ToolTip = Settings.Default.CustomAuthImagePath;
            }

            // Загрузка быстрых цен
            if (!string.IsNullOrEmpty(Settings.Default.TemplatePrices))
            {
                TemplatePricesTextBox.Text = Settings.Default.TemplatePrices;
            }
            else
            {
                TemplatePricesTextBox.Text = "250, 300, 350";
            }
        }

        private async void ColorComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ColorComboBox.SelectedItem is ComboBoxItem selectedItem
                && selectedItem.Tag != null)
            {
                // Извлекаем название цвета из Tag
                string colorName = selectedItem.Tag.ToString();

                try
                {
                    // Получаем текущую тему приложения
                    Theme theme = _paletteHelper.GetTheme();

                    if (Enum.TryParse(colorName, out MaterialDesignColor colorEnum))
                    {
                        Color selectedColor = SwatchHelper.Lookup[colorEnum];

                        theme.SetPrimaryColor(selectedColor);

                        // Применяем обновленную тему
                        _paletteHelper.SetTheme(theme);

                        // Сохраняем выбор в настройках проекта
                        Settings.Default.PrimaryColor = colorName;
                        Settings.Default.Save();
                    }
                }
                catch (Exception ex)
                {
                    await DialogClass.ShowConfirmDialog(
                        "Ошибка при попытке смены цветовой схемы",
                        $"{ex.Message}",
                        "Понятно",
                        "Отмена");
                }
            }
        }

        private async void ChangeAuthImageButtonClick(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();

            // Фильтр тольок для картинок
            openFileDialog.Filter = "Изображения (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png";
            openFileDialog.Title = "Выберите фоновое изображение";

            if (openFileDialog.ShowDialog() == true)
            {
                string sourceFile = openFileDialog.FileName;

                try
                {
                    // Определеяем безопасное место для хранения
                    string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    string targetFolder = System.IO.Path.Combine(appDataPath, "CinemaPremiereApp", "Resources");

                    // Создаем папку, если ее нет
                    if (!Directory.Exists(targetFolder))
                    {
                        Directory.CreateDirectory(targetFolder);
                    }

                    // Формируем целевой путь
                    string extension = System.IO.Path.GetExtension(sourceFile);
                    string targetFileName = $"auth_background{extension}";
                    string destinationFile = System.IO.Path.Combine(targetFolder, targetFileName);

                    // Копируем файл
                    File.Copy(sourceFile, destinationFile, overwrite: true);

                    // Сохраняем путь в настройки проекта
                    Settings.Default.CustomAuthImagePath = destinationFile;
                    Settings.Default.Save();

                    // Обновляем интерфейс
                    AuthImagePathTextBlock.Text = targetFileName;
                    AuthImagePathTextBlock.ToolTip = destinationFile;
                    AuthImagePathTextBlock.Opacity = 1.0;

                    MessageClass.SuccessMessage("Успех\nИзображение сохранено");
                }
                catch (Exception ex)
                {
                    await DialogClass.ShowConfirmDialog(
                        "Ошибка при попытке замены изображения",
                        $"{ex.Message}",
                        "Понятно",
                        "Отмена");
                }
            }
        }

        private void ResetAuthImageButtonClick(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(Settings.Default.CustomAuthImagePath))
            {
                // Просто очищаем путь в настройках
                Settings.Default.CustomAuthImagePath = string.Empty;
                Settings.Default.Save();

                // Обновляем интерфейс
                AuthImagePathTextBlock.Text = "По умолчанию";
                AuthImagePathTextBlock.ToolTip = null;
                AuthImagePathTextBlock.Opacity = 0.6;

                MessageClass.SuccessMessage("Успех\nИзображение восстановлено по умолчанию");
            }
        }

        private void TemplatePricesTextBoxLostFocus(object sender, RoutedEventArgs e)
        {
            string input = TemplatePricesTextBox.Text;
            var validPrices = new List<string>();

            // Разбиваем строку по запятым и пробелам
            var parts = input.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);

            // Фильтруем только числа и береме первые 6
            foreach (var part in parts)
            {
                // Если число - добавляем в список
                if (int.TryParse(part, out int price))
                {
                    validPrices.Add(price.ToString());
                }

                // Если набрали 6 штук - останавливаемся
                if (validPrices.Count == 6)
                    break;
            }

            // Собираем обратно красивую строку
            string result = string.Join(", ", validPrices);

            // Возвращаем отформатированную строку
            TemplatePricesTextBox.Text = result;

            // Сохраняем в настройки проекта
            Settings.Default.TemplatePrices = result;
            Settings.Default.Save();

            if (parts.Length > 6)
            {
                MessageClass.WarningMessage("Предупреждение\nВ шаблон можно добавить не более 6 цен");
            }
        }

        private async void ChangePasswordButtonClick(object sender, RoutedEventArgs e)
        {
            string oldPass = OldPasswordBox.Password.Trim();
            string newPass = NewPasswordBox.Password.Trim();
            string confirmPass = ConfirmPasswordBox.Password.Trim();

            // Проверка на пустоту
            if (string.IsNullOrWhiteSpace(oldPass) 
                || string.IsNullOrWhiteSpace(newPass)
                || string.IsNullOrWhiteSpace(confirmPass))
            {
                MessageClass.WarningMessage("Предупреждение\nЗаполните все поля");
                return;
            }

            // Проверка на совпадение нового пароля
            if (newPass != confirmPass)
            {
                MessageClass.WarningMessage("Предупреждение\nНовые пароли не совпадают");
                return;
            }

            try
            {
                int currentId = AppData.CurrentUser.UserId;
                var user = await AppData.db.Users.FirstOrDefaultAsync(u => u.UserId == currentId);

                if (user != null)
                {
                    // Хешируем старый пароль
                    string oldPassHash = textToHash(oldPass);

                    // Проверка старого пароля
                    if (user.Password != oldPassHash)
                    {
                        MessageClass.WarningMessage("Предупреждение\nСтарый пароль неверный");
                        return;
                    }

                    // Хешируем новый пароль
                    string newPassHash = textToHash(newPass);

                    // Сохранение нового пароля
                    user.Password = newPassHash;
                    await AppData.db.SaveChangesAsync();

                    // Очищаем поля
                    OldPasswordBox.Password = "";
                    NewPasswordBox.Password = "";
                    ConfirmPasswordBox.Password = "";

                    MessageClass.SuccessMessage("Успех\nПароль изменен");
                }
                else
                {
                    MessageClass.ErrorMessage("Ошибка\nПользователь не найден");
                }
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
                    "Ошибка при попытке смены пароля",
                    $"{ex.Message}",
                    "Понятно",
                    "Отмена");
            }
        }

        // Метод хеширования текста
        private string textToHash(string text)
        {
            var bytes = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(text));
            return BitConverter.ToString(bytes).Replace("-", "").ToLower();
        }

        private async void BackupDatabaseButtonClick(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog()
            {
                Filter = "Backup file (*.bak)|*.bak",
                FileName = $"CinemaPremiereDb_Backup_{DateTime.Now:dd_MM_yyyy_HH_mm}"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    Mouse.OverrideCursor = Cursors.Wait;

                    // Получаем имя БД из строки подключения
                    string dbName = AppData.db.Database.Connection.Database;

                    // Формируем SQL запрос
                    string sqlCommand = $"BACKUP DATABASE [{dbName}] TO DISK = '{saveFileDialog.FileName}' WITH FORMAT";

                    await Task.Run(() =>
                    {
                        AppData.db.Database.ExecuteSqlCommand(TransactionalBehavior.DoNotEnsureTransaction, sqlCommand);
                    });

                    MessageClass.SuccessMessage("Успех\nРезервная копия базы данных сохранена");
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("5(Отказано в доступе.)")
                        || ex.Message.Contains("Operating system error 5"))
                    {
                        await DialogClass.ShowConfirmDialog(
                            "Ошибка доступа",
                            "SQL-сервер не имеет прав для записи в эту папку.\n\n" +
                            "Рекомендации:\n" +
                            "1. Создайте папку C:\\Backups\n" +
                            "2. В свойствах папки (вкладка Безопасность) дайте доступ группе 'Все'\n" +
                            "3. Повторите попытку сохранения в эту папку",
                            "Понятно",
                            "Отмена");
                    }
                    else
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
                            "Ошибка при попытке сохранения базы данных",
                            $"{ex.Message}",
                            "Понятно",
                            "Отмена");
                    }
                }
                finally
                {
                    Mouse.OverrideCursor = null;
                }
            }
        }

        private async void RestoreDatabaseButtonClick(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog()
            {
                Filter = "Backup file (*.bak)|*.bak"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                bool confirm = await DialogClass.ShowConfirmDialog(
                    "Восстановление базы данных",
                    "Внимание! Все текущие данные будут заменены данными из бекапа. Продолжить?",
                    "Восстановить",
                    "Отмена");

                if (!confirm)
                    return;

                try
                {
                    Mouse.OverrideCursor = Cursors.Wait;

                    string dbName = AppData.db.Database.Connection.Database;

                    await Task.Run(() =>
                    {
                        // Переводим базу в режим одного пользователя и обрываем все соединения
                        // Восстанавливаем
                        // Возвращаем многопользовательский режим
                        string sqlCommand = $@"
                            USE [master];
                            ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                            RESTORE DATABASE [{dbName}] FROM DISK = '{openFileDialog.FileName}' WITH REPLACE;
                            ALTER DATABASE [{dbName}] SET MULTI_USER;";

                        AppData.db.Database.ExecuteSqlCommand(TransactionalBehavior.DoNotEnsureTransaction, sqlCommand);
                    });

                    MessageClass.SuccessMessage("Успех\nБаза данных успешно восстановлена");
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
                        "Ошибка при попытке восстановления базы данных",
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

        private void ApplyPermissions()
        {
            var user = AppData.CurrentUser;

            if (user == null)
                return;

            // 1 роль: Администратор
            CashierSettingsCard.Visibility = Visibility.Visible;
            DatabaseSettingsCard.Visibility = Visibility.Visible;

            // 2 роль: Методист
            if (user.RoleId == 2)
            {
                CashierSettingsCard.Visibility = Visibility.Collapsed;
                DatabaseSettingsCard.Visibility = Visibility.Collapsed;
            }

            // 3 роль: Кассир
            if (user.RoleId == 3)
            {
                DatabaseSettingsCard.Visibility = Visibility.Collapsed;
            }
        }
    }
}
