using CinemaPremiereApp.Ado;
using CinemaPremiereApp.Classes;
using CinemaPremiereApp.Windows;
using System;
using System.Collections.Generic;
using System.Data.Entity;
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
        }

        private async void LoginButtonClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                string login = LoginTextBox.Text.Trim();
                string password = PasswordTextBox.Password.Trim();

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
                        PasswordHash(password));

                    if (user.Password == passwordHash)
                    {
                        user.FailedAttempts = 0;
                        user.LockoutEnd = null;

                        await AppData.db.SaveChangesAsync();

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

                        MessageClass.ErrorMessage($"Ошибка\nНеверный пароль\nПовторите попытку");
                    }
                }
                else
                {
                    MessageClass.ErrorMessage($"Ошибка\nПользователь не найден");
                }
            }
            catch (Exception ex)
            {
                MessageClass.ErrorMessage($"Ошибка\n{ex.Message}");
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
                MessageClass.ErrorMessage($"Ошибка\nВведите данные в поле '{fieldName}'");
                return false;
            }
            
            if (value.Length < 4)
            {
                MessageClass.ErrorMessage($"Ошибка\nДлина поля '{fieldName}' должна быть не менее 4-х символов");
                return false;
            }

            return true;
        }

        // Метод хеширования пароля
        private string PasswordHash(string password)
        {
            var bytes = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(password));
            return BitConverter.ToString(bytes).Replace("-", "").ToLower();
        }
    }
}
