using CinemaPremiereApp.Ado;
using CinemaPremiereApp.Classes;
using CinemaPremiereApp.Properties;
using CinemaPremiereApp.Windows;
using System;
using System.Collections.Generic;
using System.Data.Entity;
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
    /// Логика взаимодействия для AuthPage.xaml
    /// </summary>
    public partial class AuthPage : Page
    {
        public AuthPage()
        {
            InitializeComponent();

            LoadCustomBackground();
        }

        private void LoadCustomBackground()
        {
            try
            {
                // Берем путь из настроек
                string savedPath = Settings.Default.CustomAuthImagePath;

                // Проверяем путь и существование файла
                if (!string.IsNullOrEmpty(savedPath) && File.Exists(savedPath))
                {
                    // Создаем источник изображения из файла
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(savedPath);

                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();

                    // Устанавливаем изображение
                    if (AuthImageBorder.Background is ImageBrush brush)
                    {
                        brush.ImageSource = bitmap;
                    }
                }
            }
            catch
            {

            }
        }

        private async void LoginButtonClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                string login = LoginTextBox.Text.Trim();
                string password = PasswordBox.Password.Trim();

                if (!IsFieldValid(login, "Логин"))
                    return;
                if (!IsFieldValid(password, "Пароль"))
                    return;

                var user = await AppData.db.Users.FirstOrDefaultAsync(u => u.Login == login);

                if (user != null)
                {
                    if (user.LockoutEnd >= DateTime.Now)
                    {
                        MessageClass.ErrorMessage($"Ошибка\nВы временно заблокированы. Повторите попытку позже");
                        return;
                    }

                    string passwordHash = await Task.Run(() =>
                        textToHash(password));

                    if (user.Password == passwordHash)
                    {
                        user.FailedAttempts = 0;
                        user.LockoutEnd = null;

                        await AppData.db.SaveChangesAsync();

                        AppData.CurrentUser = user;

                        MessageClass.SuccessMessage($"Успех\nДобро пожаловать в приложение!");

                        if (GeneralWindow.Instance != null)
                        {
                            GeneralWindow.Instance.MenuButton.Visibility = Visibility.Visible;
                        }

                        NavigationService.Navigate(new OrdersPage());
                    }
                    else
                    {
                        user.FailedAttempts++;

                        await AppData.db.SaveChangesAsync();

                        if (user.FailedAttempts >= 3)
                        {
                            user.LockoutEnd = DateTime.Now.AddMinutes(10);

                            AppData.db.SaveChanges();

                            MessageClass.ErrorMessage($"Ошибка\nПревышение допустымых попыток. Вы временно заблокированы. Повторите попытку позже");
                            return;
                        }

                        MessageClass.ErrorMessage($"Ошибка\nНеверный пароль. Повторите попытку");
                    }
                }
                else
                {
                    MessageClass.ErrorMessage($"Ошибка\nПользователь не найден");
                }
            }
            catch (Exception ex)
            {
                await DialogClass.ShowConfirmDialog(
                    "Ошибка в авторизации",
                    $"{ex.Message}",
                    "Понятно",
                    "Отмена");
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        // Метод валидации логина и пароля
        private bool IsFieldValid(string value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                MessageClass.WarningMessage($"Предупреждение\nВведите данные в поле '{fieldName}'");
                return false;
            }
            
            if (value.Length < 4)
            {
                MessageClass.WarningMessage($"Предупреждение\nДлина поля '{fieldName}' должна быть не менее 4-х символов");
                return false;
            }

            return true;
        }

        // Метод хеширования текста
        private string textToHash(string text)
        {
            var bytes = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(text));
            return BitConverter.ToString(bytes).Replace("-", "").ToLower();
        }
    }
}
