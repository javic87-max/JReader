using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using lector_de_libros.Models;
using lector_de_libros.Services;
using lector_de_libros.ViewModels;
using Microsoft.Win32;

namespace lector_de_libros
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel = new();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _viewModel;
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
                return;
            }

            int startOffset = _viewModel.ReadingPositionStore.GetPosition(filePath) ?? 0;
            NavigateToOffset(startOffset);
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
