using CinemaPremiereApp.Ado;
using CinemaPremiereApp.Classes;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Spreadsheet;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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

namespace CinemaPremiereApp.Pages
{
    /// <summary>
    /// Логика взаимодействия для FilmsPage.xaml
    /// </summary>
    public partial class FilmsPage : System.Windows.Controls.Page
    { 
        // Основной список фильмов из БД
        List<Films> allFilms = new List<Films>();

        // Переменные для пагинации
        int currentPage = 1;
        int itemsPerPage = 10;
        int totalPages = 1;

        // Временный путь для постера (в добавлении)
        string selectedImagePath = "";

        // Список для хранения строк, которые инвертировали за один клик
        private HashSet<Films> _processedFilms = new HashSet<Films>();

        // Переменная для редактирования
        private Films _editingFilm = null;

        // Переменная для хранения отфильтрованных строк до пагинации
        private List<Films> _filteredFilms;

        // Переменная для поиска
        private CancellationTokenSource _searchCts;

        // Переменная для сброса фильтров
        private bool _isResetting = false;

        public FilmsPage()
        {
            InitializeComponent();

            Dispatcher.BeginInvoke(new Action(async () => await LoadDataAsync()));
        }

        // Метод загрузки данных из БД
        public async Task LoadDataAsync()
        {
            // Загрузка фильмов в список
            allFilms = await AppData.db.Films
                .Include(f => f.Genres)
                .Include(f => f.AgeRatings)
                .OrderByDescending(f => f.FilmId)
                .ToListAsync();

            // Загрузка жанров для фильтра
            var genreList = await AppData.db.Genres
                .OrderBy(g => g.Name)
                .ToListAsync();
            GenresListBox.ItemsSource = genreList;

            // Загрузка жанров в диалоговое окно
            AddGenresListBox.ItemsSource = genreList;

            ApplyFilters();
        }

        private async void SearchTextBoxTextChanged(object sender, TextChangedEventArgs e)
        {
            // Отменяет предыдущую задачу поиска, если она еще не началась
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
                // Ничего не делаем
            }
            catch (Exception ex)
            {
                MessageClass.ErrorMessage($"Ошибка\n{ex.Message}");
            }
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

        public async void ApplyFilters()
        {
            if (_isResetting)
                return;

            // Сбрасываем выделение
            FilmsDataGrid?.UnselectAll();

            if (FilmsDataGrid == null || SearchTextBox == null || PageInputTextBox == null)
                return;

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

            int sortIndex = SortComboBox.SelectedIndex;
            int itemsPerPageLocal = itemsPerPage;
            int currentPageLocal = currentPage;

            var result = await Task.Run(() =>
            {
                // Фильтруем весь список фильмов
                var filtered = allFilms.Where(film =>
                {
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

                switch (sortIndex)
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
                        sorted = filtered.OrderByDescending(f => f.FilmId);
                        break;
                }

                // Сохраняем полный список фильмов для экспорта
                var sortedList = sorted.ToList();

                // Расчет пагинации
                int count = sortedList.Count;
                int tPages = (int)Math.Ceiling((double)count / itemsPerPageLocal);

                if (tPages < 1)
                    tPages = 1;

                // Корректируем текущую страницу, если она вылетела за пределы
                int cPage = currentPageLocal;

                if (cPage > tPages)
                    cPage = tPages;

                if (cPage < 1)
                    cPage = 1;

                // Берем только нужную страницу
                var pagedList = sortedList
                    .Skip((cPage - 1) * itemsPerPageLocal)
                    .Take(itemsPerPageLocal)
                    .ToList();

                return new
                {
                    Fulllist = sortedList,
                    PagedList = pagedList,
                    TotalCount = count,
                    TotalPages = tPages,
                    CorrectedPage = cPage
                };
            });

            // Вывод
            _filteredFilms = result.Fulllist;
            totalPages = result.TotalPages;
            currentPage = result.CorrectedPage;

            FilmsDataGrid.ItemsSource = result.PagedList;

            // Обновляем визуальные счетчики
            PageInputTextBox.Text = currentPage.ToString();
            PageInfoTextBlock.Text = $"из {totalPages}";
            CounterTextBlock.Text = $"Найдено {result.TotalCount} из {allFilms.Count}";

            // Проверка на пустой список
            if (result.TotalCount == 0)
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
            _isResetting = true;

            SearchTextBox.Text = "";

            AgeRatingsListBox.SelectedItems.Clear();
            GenresListBox.SelectedItems.Clear();

            PageSizeComboBox.SelectedIndex = 1;
            SortComboBox.SelectedIndex = 0;

            currentPage = 1;

            _isResetting = false;

            ApplyFilters();
        }

        private void AddFilmButtonClick(object sender, RoutedEventArgs e)
        {
            _editingFilm = null;
            selectedImagePath = null;

            // Очистка полей
            AddTitleTextBox.Text = "";
            AddReleaseDatePicker.SelectedDate = DateTime.Now;
            AddAgeRatingsListBox.SelectedIndex = -1;
            AddGenresListBox.UnselectAll();
            selectedImagePath = "";
            PosterPreviewImage.Source = new BitmapImage(new Uri("pack://application:,,,/Images/NoPhoto.png"));

            // Отображение диалога
            MainDialogHost.IsOpen = true;
        }

        private async void SaveFilmButtonClick(object sender, RoutedEventArgs e)
        {
            // Проверки на наличие названия и рейтинг
            if (string.IsNullOrWhiteSpace(AddTitleTextBox.Text))
            {
                MessageClass.ErrorMessage($"Ошибка\nВведите название фильма");
                return;
            }
            if (AddAgeRatingsListBox.SelectedItem == null)
            {
                MessageClass.ErrorMessage($"Ошибка\nВыберите возрастной рейтинг");
                return;
            }

            try
            {
                // Получаем выбранный возрастной рейтинг
                var selectedRatingItem = (ListBoxItem)AddAgeRatingsListBox.SelectedItem;
                int ratingValue = Convert.ToInt32(selectedRatingItem.Tag);

                // Ищем соответствующий объект в БД
                var ageRating = await AppData.db.AgeRatings.FirstOrDefaultAsync(r => r.Name == ratingValue);

                // Добавляение
                if (_editingFilm == null)
                {
                    // Содаем новый объект фильма
                    Films newFilm = new Films
                    {
                        Title = AddTitleTextBox.Text.Trim(),
                        ReleaseDate = AddReleaseDatePicker.SelectedDate ?? DateTime.Now,
                        AgeRatings = ageRating,
                    };

                    // Проверка выбрал ли пользователь постер
                    if (!string.IsNullOrEmpty(selectedImagePath))
                        newFilm.PosterFileName = await Task.Run(() => CopyPoster(selectedImagePath));

                    // Добавляем жанры
                    foreach (Genres selectedGenre in AddGenresListBox.SelectedItems)
                        newFilm.Genres.Add(selectedGenre);

                    // Добавляем фильм в контекст БД
                    AppData.db.Films.Add(newFilm);
                }
                // Редактирование
                else
                {
                    _editingFilm.Title = AddTitleTextBox.Text.Trim();
                    _editingFilm.ReleaseDate = AddReleaseDatePicker.SelectedDate ?? DateTime.Now;
                    _editingFilm.AgeRatings = ageRating;

                    // Проверка выбрал ли пользователь постер
                    if (!string.IsNullOrEmpty(selectedImagePath))
                        _editingFilm.PosterFileName = await Task.Run(() => CopyPoster(selectedImagePath));

                    // Обновление жанров
                    _editingFilm.Genres.Clear();

                    foreach (Genres selectedGenre in AddGenresListBox.SelectedItems)
                        _editingFilm.Genres.Add(selectedGenre);
                }
                // Сохраняем изменения в БД
                await AppData.db.SaveChangesAsync();

                string status = _editingFilm == null ? "добавлен" : "обновлен";

                // Закрываем диалоговое окно и обновляем данные
                MainDialogHost.IsOpen = false;
                _editingFilm = null;
                selectedImagePath = null;
                await LoadDataAsync();

                MessageClass.SuccessMessage($"Успех\nФильм {status}");
            }
            catch (Exception ex)
            {
                MessageClass.ErrorMessage($"Ошибка\n{ex.Message}");
            }
        }

        private string CopyPoster(string sourcePath)
        {
            // Формируем путь к папке постерами
            string folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", "Posters");

            // Если папки нет - создаем
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            // Генерируем и записываем уникальное имя файла
            string extension = Path.GetExtension(sourcePath);
            string uniqueFileName = Guid.NewGuid().ToString() + extension;
            string destPath = Path.Combine(folderPath, uniqueFileName);

            // Копируем файл в папку проекта
            File.Copy(sourcePath, destPath);

            return uniqueFileName;
        }

        private void AddPosterButtonClick(object sender, RoutedEventArgs e)
        {
            // Создаем окно диалога
            var openFileDialog = new Microsoft.Win32.OpenFileDialog();

            // Ставим фильтр для выбора файла
            openFileDialog.Filter = "Изображения (*.jpg; *png; *jpeg)|*jpg; *png; *jpeg;";

            if (openFileDialog.ShowDialog() == true)
            {
                // Сохраняем путь к файлу
                selectedImagePath = openFileDialog.FileName;

                // Вывод в превью
                PosterPreviewImage.Source = new BitmapImage(new Uri(selectedImagePath));
            }
        }

        private void EditFilmMenuItemButtonClick(object sender, RoutedEventArgs e)
        {
            if (_editingFilm == null)
                return;

            // Заполняем поля данными
            AddTitleTextBox.Text = _editingFilm.Title;
            AddReleaseDatePicker.SelectedDate = _editingFilm.ReleaseDate;

            // Сброс и выделение возрастного рейтинга
            AddAgeRatingsListBox.SelectedIndex = -1;

            if (_editingFilm.AgeRatings != null)
            {
                foreach (var element in AddAgeRatingsListBox.Items)
                {
                    if (element is ListBoxItem listItem)
                    {
                        if (listItem.Tag != null && listItem.Tag.ToString() == _editingFilm.AgeRatings.Name.ToString())
                        {
                            AddAgeRatingsListBox.SelectedItem = listItem;
                            break;
                        }
                    }
                }
            }

            // Выделяем жанры
            AddGenresListBox.UnselectAll();

            foreach (var genre in _editingFilm.Genres)
            {
                var genreInList = AddGenresListBox.Items.Cast<Genres>().FirstOrDefault(g => g.GenreId == genre.GenreId);
                if (genreInList != null)
                {
                    AddGenresListBox.SelectedItems.Add(genreInList);
                }
            }

            // Выделяем возрастной рейтинг
            var ratingItem = AddAgeRatingsListBox.Items.Cast<ListBoxItem>()
                .FirstOrDefault(i => i.Tag.ToString() == _editingFilm.AgeRatingId.ToString());
            if (ratingItem != null)
                ratingItem.IsSelected = true;

            // Загружаем постер (если есть)
            if (!string.IsNullOrEmpty(_editingFilm.PosterFileName))
            {
                string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "Images", "Posters", _editingFilm.PosterFileName);

                if (File.Exists(fullPath))
                {
                    PosterPreviewImage.Source = new BitmapImage(new Uri(fullPath));
                }
                else
                {
                    // Если файла почему-то нет физически
                    PosterPreviewImage.Source = new BitmapImage(new Uri("/Images/NoPhoto.png", UriKind.Relative));
                }
            }
            else
            {
                // Если в БД вообще нет имени файла
                PosterPreviewImage.Source = new BitmapImage(new Uri("/Images/NoPhoto.png", UriKind.Relative));
            }

            // Открываем окно
            MainDialogHost.IsOpen = true;
        }

        private async void DeleteFilmMenuItemButtonClick(object sender, RoutedEventArgs e)
        {
            // Получаем выбранный фильм
            var film = (sender as MenuItem)?.DataContext as Films ?? FilmsDataGrid.SelectedItem as Films;

            if (film != null)
                await ExecuteDeleteAsync(new List<Films> { film });
        }

        private async void DeleteSelectionFilmsButtoncClick(object sender, RoutedEventArgs e)
        {
            var selected = FilmsDataGrid.SelectedItems.Cast<Films>().ToList();

            await ExecuteDeleteAsync(selected);
        }

        // Общий метод удаления
        private async Task ExecuteDeleteAsync(List<Films> filmsToDelete)
        {
            if (filmsToDelete == null || !filmsToDelete.Any())
                return;

            // Подтверждение пользователя
            string message = filmsToDelete.Count == 1
                ? $"Вы точно хотите удалить фильм \"{filmsToDelete[0].Title}\"?"
                : $"Вы точно хотите удалить выбранные фильмы ({filmsToDelete.Count} шт.)?";

            bool isConfirmed = await DialogClass.ShowConfirmDialog(
                "Удаление данных",
                message,
                "Удалить",
                "Отмена");

            if (isConfirmed)
            {
                try
                {
                    await Task.Run(() =>
                    {
                        // Удаление физического файла постера
                        foreach (var film in filmsToDelete)
                        {
                            if (!string.IsNullOrEmpty(film.PosterFileName))
                            {
                                // Собираем полный путь к картинке
                                string postersFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", "Posters");
                                string fullPath = Path.Combine(postersFolder, film.PosterFileName);

                                if (File.Exists(fullPath))
                                {
                                    try
                                    {
                                        File.Delete(fullPath);
                                    }
                                    catch
                                    {
                                        // Ничего не делаем
                                    }
                                }
                            }
                        }
                    });

                    // Удаление фильмов из базы
                    foreach (var film in filmsToDelete)
                        AppData.db.Films.Remove(film);

                    // Сохраняем все изменения в БД
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

        private void ClearSelectionButtonClick(object sender, RoutedEventArgs e)
        {
            FilmsDataGrid.UnselectAll();
        }

        private void FilmsDataGridSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int count = FilmsDataGrid.SelectedItems.Count;

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

        private void PreviewMouseLeftButtonDownDataGrid(object sender, MouseButtonEventArgs e)
        {
            // Находим строку, по которой кликнули
            DataGridRow row = sender as DataGridRow;

            if (row != null)
            {
                // Очищаем историю текущего выделения
                _processedFilms.Clear();

                var film = row.DataContext as Films;

                if (film != null)
                {
                    row.IsSelected = !row.IsSelected;
                    _processedFilms.Add(film);
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
                var film = row?.DataContext as Films;

                if (film != null && !_processedFilms.Contains(film))
                {
                    row.IsSelected = !row.IsSelected;
                    _processedFilms.Add(film);
                }
            }
        }

        private async void ExportToExcelButtonClick(object sender, RoutedEventArgs e)
        {
            // Собираем отфильтрованные данные
            if (_filteredFilms == null || !_filteredFilms.Any())
            {
                MessageClass.ErrorMessage($"Ошибка\nНет данных для экспорта");
                return;
            }

            // Создаем диалоговое окно сохранения
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog()
            {
                Filter = "Книга Excel (*.xlsx)|*.xlsx",
                FileName = $"Экспорт_фильмов_Excel_{DateTime.Now:dd_MM_yyyy}"
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
                            var worksheet = workbook.Worksheets.Add("Фильмы");

                            // Заполнеяем шапку (1 строка)
                            worksheet.Cell(1, 1).Value = "Название";
                            worksheet.Cell(1, 2).Value = "Жанры";
                            worksheet.Cell(1, 3).Value = "Возрастной рейтинг";
                            worksheet.Cell(1, 4).Value = "Дата выхода";

                            // Настраиваем стиль шапки
                            var headerRange = worksheet.Range("A1:D1");
                            headerRange.Style.Font.Bold = true;

                            // Заполняем строки данными
                            int currentRow = 2;
                            foreach (var f in _filteredFilms)
                            {
                                worksheet.Cell(currentRow, 1).Value = f.Title;
                                // Жанры склеиваем в одну строку через запятую
                                worksheet.Cell(currentRow, 2).Value = string.Join(", ", f.Genres.Select(g => g.Name));
                                worksheet.Cell(currentRow, 3).Value = f.AgeRatings?.Name.ToString() + "+";
                                worksheet.Cell(currentRow, 4).Value = f.ReleaseDate.ToShortDateString();

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
                    MessageClass.ErrorMessage($"Ошибка\n{ex.Message}");
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
            if (_filteredFilms == null || !_filteredFilms.Any())
            {
                MessageClass.ErrorMessage($"Ошибка\nНет данных для экспорта");
                return;
            }

            // Создаем диалоговое окно сохранения
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog()
            {
                Filter = "CSV файл (*.csv)|*.csv",
                FileName = $"Экспорт_фильмов_CSV_{DateTime.Now:dd_MM_yyyy}"
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
                        csv.AppendLine("FilmId;Title;AgeRatingId;PosterFileName;ReleaseDate;Genres");

                        // Заполняем данные
                        foreach (var f in _filteredFilms)
                        {
                            string genres = string.Join(", ", f.Genres.Select(g => g.Name));

                            // Формируем строку
                            string line = string.Format("{0};{1};{2};{3};{4};{5}",
                                f.FilmId,
                                f.Title,
                                f.AgeRatingId,
                                f.PosterFileName,
                                f.ReleaseDate.ToShortDateString(),
                                genres);

                            csv.AppendLine(line);
                        }

                        // Сохранение
                        File.WriteAllText(saveFileDialog.FileName, csv.ToString(), Encoding.UTF8);
                    });

                    MessageClass.SuccessMessage($"Успех\nДанные сохранены в формате CSV");
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
        }

        private async void ExportToJsonButtonClick(object sender, RoutedEventArgs e)
        {
            // Собираем отфильтрованные данные
            if (_filteredFilms == null || !_filteredFilms.Any())
            {
                MessageClass.ErrorMessage($"Ошибка\nНет данных для экспорта");
                return;
            }

            // Создаем диалоговое окно сохранения
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog()
            {
                Filter = "JSON файл (*.json)|*.json",
                FileName = $"Экспорт_фильмов_JSON_{DateTime.Now:dd_MM_yyyy}"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    Mouse.OverrideCursor = Cursors.Wait;

                    await Task.Run(() =>
                    {
                        // Создаем чистый список с нужными полями
                        var dataToExport = _filteredFilms.Select(f => new
                        {
                            f.FilmId,
                            f.Title,
                            f.PosterFileName,
                            f.ReleaseDate,
                            AgeRating = f.AgeRatings?.Name,
                            Genres = f.Genres.Select(g => g.Name).ToList()
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
                    MessageClass.ErrorMessage($"Ошибка\n{ex.Message}");
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
                        File.ReadAllLines(openFileDialog.FileName, Encoding.UTF8).Skip(1).ToList());

                    // Кэшируем данные из БД
                    var ratingsDict = await AppData.db.AgeRatings.ToDictionaryAsync(r => r.AgeRatingId);
                    var genresDict = await AppData.db.Genres.ToDictionaryAsync(g => g.Name);

                    foreach (var line in lines)
                    {
                        var parts = line.Split(';');

                        // Пропускаем битые строки
                        if (parts.Length < 6)
                            continue;
                        
                        // Получаем данные из строки
                        string title = parts[1].Trim();
                        int ageId = int.Parse(parts[2].Trim());
                        string poster = parts[3].Trim();
                        string dateRaw = parts[4].Trim();
                        string genresList = parts[5].Trim();

                        // Используем ParseExact, чтобы формат "день.месяц.год" всегда читался верно
                        DateTime date = DateTime.ParseExact(dateRaw, "dd.MM.yyyy",
                            System.Globalization.CultureInfo.InvariantCulture);

                        // Достаем рейтинг из словаря
                        ratingsDict.TryGetValue(ageId, out var dbRating);

                        // Создаем новый фильм
                        Films newFilm = new Films
                        {
                            Title = title,
                            ReleaseDate = date,
                            AgeRatings = dbRating,
                            PosterFileName = poster
                        };

                        // Обработка жанров
                        var genreNames = genresList.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var gName in genreNames)
                        {
                            if (genresDict.TryGetValue(gName.Trim(), out var dbGenre))
                                newFilm.Genres.Add(dbGenre);
                        }

                        AppData.db.Films.Add(newFilm);
                    }

                    await AppData.db.SaveChangesAsync();
                    await LoadDataAsync();

                    MessageClass.SuccessMessage($"Успех\nДанные загружены");
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

                // Запоминаем фильм, на который кликнули
                _editingFilm = row.DataContext as Films;

                // Открываем контекстное меню
                if (FilmsDataGrid.ContextMenu != null)
                {
                    FilmsDataGrid.ContextMenu.PlacementTarget = row;
                    FilmsDataGrid.ContextMenu.IsOpen = true;
                }
            }
        }
    }
}
