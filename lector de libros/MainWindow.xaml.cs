using System.ComponentModel;
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

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _viewModel;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (_viewModel.ReopenLastBookOnStartup && _viewModel.GetLastOpenedFilePath() is { } lastFilePath)
            {
                LoadBook(lastFilePath);
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

        private void OpenCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            OpenFileDialog dialog = new()
            {
                Filter = "Libros electrónicos (*.epub;*.pdf)|*.epub;*.pdf|EPUB (*.epub)|*.epub|PDF (*.pdf)|*.pdf",
                CheckFileExists = true,
            };
            if (dialog.ShowDialog(this) == true)
            {
                LoadBook(dialog.FileName);
            }
        }

        private void LoadBook(string filePath)
        {
            SaveCurrentPosition();

            if (!_viewModel.TryLoadBook(filePath, out string? errorMessage))
            {
                MessageBox.Show(this, errorMessage, "No se pudo abrir el libro", MessageBoxButton.OK, MessageBoxImage.Error);
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
            catch (InvalidOperationException)
            {
                // Malformed or unrecognized language tag from the book's metadata.
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

        private void RecentFileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem { Tag: RecentFile recent })
            {
                LoadBook(recent.FilePath);
            }
        }

        private void Library_Click(object sender, RoutedEventArgs e)
        {
            LibraryWindow libraryWindow = new(_viewModel) { Owner = this };
            if (libraryWindow.ShowDialog() == true && libraryWindow.BookToOpen is { } filePath)
            {
                LoadBook(filePath);
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
