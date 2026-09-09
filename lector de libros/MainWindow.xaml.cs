using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Threading;
using lector_de_libros.Models;
using lector_de_libros.Services;
using lector_de_libros.ViewModels;
using Microsoft.Win32;

namespace lector_de_libros
{
    public partial class MainWindow : Window
    {
        // WPF's Language property defaults to en-US, which makes NVDA read the text with an
        // English voice regardless of the book's actual language unless it's set explicitly.
        // Spanish is the fallback for books that don't carry language metadata (most PDFs).
        private const string FallbackLanguageTag = "es-ES";

        private readonly MainViewModel _viewModel = new();

        // Guards against the startup check and a manual "Buscar actualizaciones…" click overlapping -
        // without this, both could independently find the same update and each pop its own dialog.
        private bool _isCheckingForUpdates;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _viewModel;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (_viewModel.ReopenLastBookOnStartup && _viewModel.GetLastOpenedFilePath() is { } lastFilePath)
            {
                await LoadBookAsync(lastFilePath);
            }

            if (_viewModel.CheckForUpdatesOnStartup)
            {
                _ = CheckForUpdatesAsync(silent: true);
            }
        }

        private async void CheckForUpdatesMenuItem_Click(object sender, RoutedEventArgs e)
        {
            await CheckForUpdatesAsync(silent: false);
        }

        /// <summary>
        /// Shared by the startup check and the manual menu item. In silent mode, a "no update" result
        /// or a network failure says nothing - only an actual update found is worth interrupting the
        /// user for, whether they asked for the check or not.
        /// </summary>
        private async Task CheckForUpdatesAsync(bool silent)
        {
            if (_isCheckingForUpdates)
            {
                return;
            }
            _isCheckingForUpdates = true;

            try
            {
                await RunUpdateCheckAsync(silent);
            }
            finally
            {
                _isCheckingForUpdates = false;
            }
        }

        private async Task RunUpdateCheckAsync(bool silent)
        {
            UpdateInfo? update;
            try
            {
                update = await new UpdateChecker().CheckForUpdateAsync();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                if (!silent)
                {
                    MessageBox.Show(this, $"No se pudo comprobar si hay actualizaciones: {ex.Message}", "Buscar actualizaciones", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                return;
            }

            if (update is null)
            {
                if (!silent)
                {
                    MessageBox.Show(this, "Ya tienes la última versión de J Reader.", "Buscar actualizaciones", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                return;
            }

            MessageBoxResult result = MessageBox.Show(
                this,
                $"Hay una nueva versión disponible: {update.TagName}\n\n{update.ReleaseNotes}\n\n¿Descargarla e instalarla ahora? Se cerrará J Reader durante la instalación.",
                "Actualización disponible",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            SaveCurrentPosition();
            Mouse.OverrideCursor = Cursors.Wait;
            AnnounceToScreenReader("Descargando actualización…");
            try
            {
                await new UpdateInstaller().DownloadAndApplyAsync(update);
                // On success, UpdateInstaller shuts the app down itself once the replacement is queued.
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
            {
                Mouse.OverrideCursor = null;
                MessageBox.Show(this, $"No se pudo instalar la actualización: {ex.Message}", "Error al actualizar", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowReadingProgress_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.CurrentBook is not { } book || book.Text.Length == 0)
            {
                MessageBox.Show(this, "No hay ningún libro abierto.", "Progreso de lectura", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            double percent = (double)ReaderTextBox.CaretIndex / book.Text.Length * 100;
            MessageBox.Show(this, $"Has leído el {percent:F0}% del libro.", "Progreso de lectura", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void OpenCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            OpenFileDialog dialog = new()
            {
                Filter = "Libros electrónicos (*.epub;*.pdf)|*.epub;*.pdf|EPUB (*.epub)|*.epub|PDF (*.pdf)|*.pdf",
                CheckFileExists = true,
            };
            if (dialog.ShowDialog(this) == true)
            {
                await LoadBookAsync(dialog.FileName);
            }
        }

        /// <summary>
        /// Parsing (especially a long PDF) can take a while, so this stays off the UI thread and
        /// shows a wait cursor instead of blocking the window - a frozen window otherwise looks
        /// crashed even though it's just slow.
        /// </summary>
        private async Task LoadBookAsync(string filePath)
        {
            SaveCurrentPosition();

            Mouse.OverrideCursor = Cursors.Wait;
            (bool success, string? errorMessage) result;
            try
            {
                result = await _viewModel.TryLoadBookAsync(filePath);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }

            if (!result.success)
            {
                MessageBox.Show(this, result.errorMessage, "No se pudo abrir el libro", MessageBoxButton.OK, MessageBoxImage.Error);
                _viewModel.RemoveRecentFile(filePath);
                return;
            }

            UpdateReaderLanguage();
            int startOffset = _viewModel.ReadingPositionStore.GetPosition(filePath) ?? 0;
            NavigateToOffset(startOffset);
        }

        /// <summary>
        /// Tags the reader with the book's language so NVDA's automatic language switching (when
        /// enabled) speaks it with the right voice instead of defaulting to English.
        /// </summary>
        private void UpdateReaderLanguage()
        {
            string languageTag = _viewModel.CurrentBook?.Language ?? FallbackLanguageTag;
            try
            {
                XmlLanguage language = XmlLanguage.GetLanguage(languageTag);
                _ = language.GetEquivalentCulture();
                ReaderTextBox.Language = language;
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                // Malformed or unrecognized language tag from the book's metadata (e.g. some PDF
                // producers - often docx-to-pdf converters - write POSIX-style tags like "es_419"
                // instead of the IETF-required "es-419", which XmlLanguage.GetLanguage rejects with
                // an ArgumentException rather than InvalidOperationException).
                ReaderTextBox.Language = XmlLanguage.GetLanguage(FallbackLanguageTag);
            }
        }

        private void CloseCommandBinding_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = _viewModel.CurrentBook is not null;
        }

        private void CloseCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            SaveCurrentPosition();
            _viewModel.CloseBook();
            UpdateReaderLanguage();
            CommandManager.InvalidateRequerySuggested();
            AnnounceToScreenReader("Libro cerrado");
        }

        /// <summary>
        /// One-off screen reader announcement via the UIA Notification event, for state changes
        /// (like closing the book) that aren't reflected by a focus move NVDA would otherwise report.
        /// </summary>
        private void AnnounceToScreenReader(string message)
        {
            AutomationPeer peer = UIElementAutomationPeer.FromElement(ReaderTextBox)
                ?? UIElementAutomationPeer.CreatePeerForElement(ReaderTextBox);
            peer?.RaiseNotificationEvent(
                AutomationNotificationKind.ActionCompleted,
                AutomationNotificationProcessing.ImportantMostRecent,
                message,
                nameof(AnnounceToScreenReader));
        }

        private async void RecentFileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem { Tag: RecentFile recent })
            {
                await LoadBookAsync(recent.FilePath);
            }
        }

        private async void Library_Click(object sender, RoutedEventArgs e)
        {
            LibraryWindow libraryWindow = new(_viewModel) { Owner = this };
            if (libraryWindow.ShowDialog() == true && libraryWindow.BookToOpen is { } filePath)
            {
                await LoadBookAsync(filePath);
            }
        }

        private void TocTreeView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key is Key.Enter or Key.Space && TocTreeView.SelectedItem is TocEntry entry)
            {
                NavigateToOffset(entry.CharacterOffset);
                e.Handled = true;
            }
        }

        private void TocTreeView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (TocTreeView.SelectedItem is TocEntry entry)
            {
                NavigateToOffset(entry.CharacterOffset);
            }
        }

        private void NavigateToOffset(int characterOffset)
        {
            // Deferred so it runs after layout has caught up with the current Text (important right
            // after loading a book, when Text was just set and line/caret geometry may still be stale).
            Dispatcher.BeginInvoke(() =>
            {
                int offset = Math.Clamp(characterOffset, 0, ReaderTextBox.Text.Length);
                ReaderTextBox.CaretIndex = offset;
                ReaderTextBox.ScrollToLine(ReaderTextBox.GetLineIndexFromCharacterIndex(offset));
                ReaderTextBox.Focus();
                Keyboard.Focus(ReaderTextBox);
            }, DispatcherPriority.ContextIdle);
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            SaveCurrentPosition();
        }

        private void SaveCurrentPosition()
        {
            if (_viewModel.CurrentBook is { } book)
            {
                _viewModel.ReadingPositionStore.SetPosition(book.FilePath, ReaderTextBox.CaretIndex);
            }
        }
    }
}
