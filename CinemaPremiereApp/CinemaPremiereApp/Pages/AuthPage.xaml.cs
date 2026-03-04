using System;
using System.Collections.Generic;
using System.Linq;
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

        private void LoginButtonClick(object sender, RoutedEventArgs e)
        {
            try
            {
                string login = LoginTextBox.Text;
                string password = PasswordTextBox.Password;

                if (string.IsNullOrEmpty(login))
                {
                    MainSnackbar.MessageQueue.Enqueue(
                        content:"Ошибка\nВведите данные в поле 'Логин'",
                        null, null, null, false, true,
                        durationOverride: TimeSpan.FromSeconds(3));
                    return;
                }
                if (string.IsNullOrEmpty(password))
                {
                    MainSnackbar.MessageQueue.Enqueue(
                        content: "Ошибка\nВведите данные в поле 'Пароль'",
                        null, null, null, false, true,
                        durationOverride: TimeSpan.FromSeconds(3));
                    return;
                }
            }
            catch (Exception ex)
            {

            }
        }
    }
}
