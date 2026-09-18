using System.Windows;

namespace lector_de_libros
{
    public partial class SearchWindow : Window
    {
        public string SearchText { get; private set; } = string.Empty;

        public SearchWindow(string initialText)
        {
            InitializeComponent();
            SearchTextBox.Text = initialText;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Focus();
            SearchTextBox.SelectAll();
        }

        private void FindButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(SearchTextBox.Text))
            {
                return;
            }
            SearchText = SearchTextBox.Text;
            DialogResult = true;
        }
    }
}
