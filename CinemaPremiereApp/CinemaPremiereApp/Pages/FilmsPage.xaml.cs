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
        // Основной список фильмов из БД
        List<Films> allFilms = new List<Films>();

        // Переменные для пагинации
        int currentPage = 1;
        int itemsPerPage = 10;
        int totalPages = 1;

        public FilmsPage()
        {
            InitializeComponent();

            LoadData();
        }

        // Метод загрузки данных из БД
        public void LoadData()
        {
            // Загрузка фильмов в список
            allFilms = AppData.db.Films
                .Include(f => f.Genres)
                .Include(f => f.AgeRatings)
                .ToList();

            // Загрузка жанров в фильтр
            GenresListBox.ItemsSource = AppData.db.Genres
                .OrderBy(g => g.Name)
                .ToList();

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

        private void SortComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
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

        public void ApplyFilters()
        {
            if (FilmsDataGrid == null || SearchTextBox == null || PageInputTextBox == null)
                return;

            // Фильтруем весь список фильмов
            var filtered = allFilms.Where(film =>
            {
                // Собираем текст из поиска
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

                bool matchesSearch = string.IsNullOrWhiteSpace(searchText) ||
                    film.Title.ToLower().Contains(searchText);

                bool matchesAge = selectedAgeRatings.Count == 0 ||
                    (film.AgeRatings != null && selectedAgeRatings.Contains((int)film.AgeRatings.Name));

                bool matchesGenre = selectedGenresIds.Count == 0 ||
                    film.Genres.Any(g => selectedGenresIds.Contains(g.GenreId));

                return matchesSearch && matchesAge && matchesGenre;
            }).ToList();

            // Сортировка
            IEnumerable<Films> sorted = filtered;

            switch (SortComboBox.SelectedIndex)
            {
                case 1:
                    sorted = filtered.OrderByDescending(f => f.ReleaseDate);
                    break;
                case 2:
                    sorted = filtered.OrderBy(f => f.Title);
                    break;
                case 3:
                    sorted = filtered.OrderBy(f => f.AgeRatings.Name);
                    break;
                default:
                    sorted = filtered;
                    break;
            } 

            // Считаем страницы
            int filteredCount = filtered.Count;
            totalPages = (int)Math.Ceiling((double)filteredCount / itemsPerPage);

            if (totalPages < 1)
                totalPages = 1;

            if (currentPage > totalPages)
                currentPage = totalPages;

            // Пагинация вместе с сортировкой
            FilmsDataGrid.ItemsSource = sorted
                .Skip((currentPage - 1) * itemsPerPage)
                .Take(itemsPerPage)
                .ToList();

            // Счетчики
            PageInputTextBox.Text = currentPage.ToString();

            PageInfoTextBlock.Text = $"из {totalPages}";
            CounterTextBlock.Text = $"Найдено {filteredCount} из {allFilms.Count}";

            // Проверка на пустой список
            if (filteredCount == 0)
            {
                FilmsDataGrid.Visibility = Visibility.Collapsed;
                EmptyStackPanel.Visibility = Visibility.Visible;
            }
            else
            {
                FilmsDataGrid.Visibility = Visibility.Visible;
                EmptyStackPanel.Visibility = Visibility.Collapsed;
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
            if (PageSizeComboBox.SelectedItem == null || allFilms == null)
                return;

            var selectedItem = PageSizeComboBox.SelectedItem as ComboBoxItem;
            
            if (selectedItem != null)
            {
                itemsPerPage = Convert.ToInt32(selectedItem.Tag);

                currentPage = 1;

                ApplyFilters();
            }
        }

        private void ResetFiltersButtonClick(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Text = "";

            AgeRatingsListBox.SelectedItems.Clear();
            GenresListBox.SelectedItems.Clear();

            PageSizeComboBox.SelectedIndex = 1;
            SortComboBox.SelectedIndex = 0;

            currentPage = 1;

            ApplyFilters();
        }
    }
}
