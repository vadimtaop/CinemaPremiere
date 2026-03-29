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
using System.Data.Entity;
using System.ComponentModel;

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

        // Метод загрузки данных из БД
        public void LoadData()
        {
            // Загрузка фильмов в таблицу
            FilmsDataGrid.ItemsSource = AppData.db.Films
                .Include(f => f.Genres)
                .Include(f => f.AgeRatings)
                .ToList();

            // Загрузка жанров в фильтр
            GenresListBox.ItemsSource = AppData.db.Genres.ToList();

            ApplyFilters();
        }

        private void SearchTextBoxTextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void AgeRatingsListBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void GenresListBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        public void ApplyFilters()
        {
            var view = CollectionViewSource.GetDefaultView(FilmsDataGrid.ItemsSource);

            if (view == null)
                return;

            string searchText = SearchTextBox.Text.ToLower().Trim();

            // Собираем выбранные возрасты
            var selectedAgeRatings = AgeRatingsListBox.SelectedItems
                .Cast<ListBoxItem>()
                .Select(x => Convert.ToInt32(x.Tag))
                .ToList();

            // Собираем выбранные жанры
            var selectedGenresIds = GenresListBox.SelectedItems
                .Cast<Genres>()
                .Select(x => x.GenreId)
                .ToList();

            view.Filter = (obj) =>
            {
                var film = obj as Films;
                if (film == null)
                    return false;

                bool matchesSearch = string.IsNullOrWhiteSpace(searchText) || 
                    film.Title.ToLower().Contains(searchText);

                bool matchesAge = selectedAgeRatings.Count == 0 || 
                    (film.AgeRatings != null && selectedAgeRatings.Contains((int)film.AgeRatings.Name));

                bool matchesGenre = selectedGenresIds.Count == 0 ||
                    film.Genres.Any(g => selectedGenresIds.Contains(g.GenreId));

                return matchesSearch && matchesAge && matchesGenre;
            };

            UpdateCounter(view);
        }

        // Метод счетчика
        private void UpdateCounter(ICollectionView view)
        {
            // Отфильтрованное количество
            int filteredCount = view.Cast<object>().Count();

            // Общее количество
            int totalCount = (FilmsDataGrid.ItemsSource as List<Films>)?.Count ?? 0;

            // Вывод в текст
            CounterTextBlock.Text = $"Найдено фильмов: {filteredCount} из {totalCount}";
        }
    }
}
