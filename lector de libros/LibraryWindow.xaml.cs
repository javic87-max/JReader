using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using lector_de_libros.Models;
using lector_de_libros.ViewModels;
using Microsoft.Win32;

namespace lector_de_libros
{
    public partial class LibraryWindow : Window
    {
        private readonly MainViewModel _viewModel;

        public string? BookToOpen { get; private set; }

        public LibraryWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = viewModel;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateButtonStates();
            LibraryListBox.Focus();
        }

        private void LibraryListBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
            UpdateButtonStates();

        private void UpdateButtonStates()
        {
            bool hasSelection = LibraryListBox.SelectedItem is not null;
            OpenButton.IsEnabled = hasSelection;
            RemoveButton.IsEnabled = hasSelection;
        }

        private void LibraryListBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key is Key.Enter && LibraryListBox.SelectedItem is LibraryBook book)
            {
                OpenSelected(book);
                e.Handled = true;
            }
        }

        private void LibraryListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (LibraryListBox.SelectedItem is LibraryBook book)
            {
                OpenSelected(book);
            }
        }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            if (LibraryListBox.SelectedItem is LibraryBook book)
            {
                OpenSelected(book);
            }
        }

        private void OpenSelected(LibraryBook book)
        {
            BookToOpen = book.FilePath;
            DialogResult = true;
            Close();
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new()
            {
                Filter = "Libros electrónicos (*.epub;*.pdf)|*.epub;*.pdf|EPUB (*.epub)|*.epub|PDF (*.pdf)|*.pdf",
                CheckFileExists = true,
            };
            if (dialog.ShowDialog(this) == true)
            {
                _viewModel.AddToLibrary(dialog.FileName);
            }
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (LibraryListBox.SelectedItem is LibraryBook book)
            {
                _viewModel.RemoveFromLibrary(book.FilePath);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
