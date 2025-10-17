using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace InterfaceExtractor.UI
{
    public partial class ExtractInterfaceDialog : Window
    {
        public string InterfaceName { get; private set; }
        public List<MemberSelectionItem> Members { get; private set; }

        public ExtractInterfaceDialog(string className, List<MemberSelectionItem> members)
        {
            InitializeComponent();

            ClassNameText.Text = className;
            InterfaceNameTextBox.Text = $"I{className}";
            Members = members;

            MembersListBox.ItemsSource = Members;

            // Select all by default
            foreach (var member in Members)
            {
                member.IsSelected = true;
            }

            InterfaceNameTextBox.Focus();
            InterfaceNameTextBox.SelectAll();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            InterfaceName = InterfaceNameTextBox.Text?.Trim();

            if (string.IsNullOrWhiteSpace(InterfaceName))
            {
                MessageBox.Show("Please enter an interface name.", "Interface Extractor",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!InterfaceName.StartsWith("I") || InterfaceName.Length < 2)
            {
                var result = MessageBox.Show(
                    "Interface names typically start with 'I'. Do you want to continue?",
                    "Interface Extractor",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.No)
                    return;
            }

            if (!Members.Any(m => m.IsSelected))
            {
                MessageBox.Show("Please select at least one member.", "Interface Extractor",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var member in Members)
            {
                member.IsSelected = true;
            }
            SelectAllCheckBox.IsChecked = true;
        }

        private void DeselectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var member in Members)
            {
                member.IsSelected = false;
            }
            SelectAllCheckBox.IsChecked = false;
        }

        private void SelectAllCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (SelectAllCheckBox.IsChecked == true)
            {
                SelectAll_Click(sender, e);
            }
            else
            {
                DeselectAll_Click(sender, e);
            }
        }
    }
}