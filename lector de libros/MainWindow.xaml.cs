using System.ComponentModel;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
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

            // RichTextBox.Document is deliberately not data-bound in XAML: WPF disallows binding it
            // (throws XamlParseException at load time), so it's assigned directly here instead.
            ReaderRichTextBox.Document = _viewModel.CurrentBook!.Content;
            RestoreCurrentPosition();
            ReaderRichTextBox.Focus();
        }

        private void TocTreeView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key is Key.Enter or Key.Space && TocTreeView.SelectedItem is TocEntry entry)
            {
                NavigateToAnchor(entry.AnchorName);
                e.Handled = true;
            }
        }

        private void TocTreeView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (TocTreeView.SelectedItem is TocEntry entry)
            {
                NavigateToAnchor(entry.AnchorName);
            }
        }

        private void NavigateToAnchor(string anchorName)
        {
            if (ReaderRichTextBox.Document?.FindName(anchorName) is TextElement element)
            {
                element.BringIntoView();
                ReaderRichTextBox.Selection.Select(element.ContentStart, element.ContentStart);
                ReaderRichTextBox.Focus();
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            SaveCurrentPosition();
        }

        private void SaveCurrentPosition()
        {
            if (_viewModel.CurrentBook is { } book)
            {
                int offset = ReadingPositionStore.GetCharacterOffset(book.Content, ReaderRichTextBox.CaretPosition);
                _viewModel.ReadingPositionStore.SetPosition(book.FilePath, offset);
            }
        }

        private void RestoreCurrentPosition()
        {
            if (_viewModel.CurrentBook is not { } book)
            {
                return;
            }

            int? savedOffset = _viewModel.ReadingPositionStore.GetPosition(book.FilePath);
            if (savedOffset is int offset)
            {
                TextPointer position = ReadingPositionStore.GetPositionAtCharacterOffset(book.Content, offset);
                position.Paragraph?.BringIntoView();
                ReaderRichTextBox.Selection.Select(position, position);
            }
        }
    }
}
