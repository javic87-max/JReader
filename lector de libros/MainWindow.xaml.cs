using System.ComponentModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Documents;
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

            // WPF's RichTextBox doesn't reliably raise UI Automation caret-moved notifications on
            // arrow-key navigation (a known WPF limitation), so screen readers can lose track of the
            // caret even though it visually moves. Raising the event manually on every selection/caret
            // change forces NVDA/JAWS to pick it up.
            ReaderRichTextBox.SelectionChanged += (_, _) =>
            {
                AutomationPeer? peer = UIElementAutomationPeer.CreatePeerForElement(ReaderRichTextBox);
                peer?.RaiseAutomationEvent(AutomationEvents.TextPatternOnTextSelectionChanged);
            };
        }

        private void FocusReaderAfterLayout()
        {
            // Deferred via the dispatcher so it runs after layout/rendering settles (e.g. right after
            // the modal "Abrir archivo" dialog closes); calling Focus() synchronously at that point can
            // silently fail to raise the automation focus-changed event that screen readers rely on.
            Dispatcher.BeginInvoke(() =>
            {
                ReaderRichTextBox.Focus();
                Keyboard.Focus(ReaderRichTextBox);
            }, DispatcherPriority.ContextIdle);
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
            FocusReaderAfterLayout();
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
                FocusReaderAfterLayout();
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
