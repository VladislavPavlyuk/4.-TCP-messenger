using System.Windows;
using System.Windows.Input;

namespace Client
{
    public partial class PasswordDialog : Window
    {
        public string Password => PasswordBox.Password;

        public PasswordDialog()
        {
            InitializeComponent();
            PasswordBox.Focus();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(PasswordBox.Password))
            {
                ErrorTextBlock.Text = "Please enter password";
                return;
            }
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                OkButton_Click(sender, e);
            }
        }
    }
}

