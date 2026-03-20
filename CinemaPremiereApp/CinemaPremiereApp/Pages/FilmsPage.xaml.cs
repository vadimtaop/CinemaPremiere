using CinemaPremiereApp.Ado;
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
    /// Логика взаимодействия для FilmsPage.xaml
    /// </summary>
    public partial class FilmsPage : Page
    {
        public FilmsPage()
        {
            InitializeComponent();

            LoadData();
        }

        public void LoadData()
        {
            FilmsDataGrid.ItemsSource = AppData.db.Films.ToList();
        }
    }
}
