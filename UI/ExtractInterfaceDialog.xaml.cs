using Microsoft.CodeAnalysis.CSharp;
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
            InterfaceNameTextBox.Text = $"{Constants.InterfacePrefix}{className}";
            Members = members;

            MembersListBox.ItemsSource = Members;

            // Select all by default
            foreach (var member in Members)
            {
                member.IsSelected = true;
            }

            // Update select all checkbox state
            UpdateSelectAllCheckBox();

            // Subscribe to property changes to update select all checkbox
            foreach (var member in Members)
            {
                member.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(MemberSelectionItem.IsSelected))
                    {
                        UpdateSelectAllCheckBox();
                    }
                };
            }

            InterfaceNameTextBox.Focus();
            InterfaceNameTextBox.SelectAll();
        }

        private void UpdateSelectAllCheckBox()
        {
            var selectedCount = Members.Count(m => m.IsSelected);

            if (selectedCount == 0)
            {
                SelectAllCheckBox.IsChecked = false;
            }
            else if (selectedCount == Members.Count)
            {
                SelectAllCheckBox.IsChecked = true;
            }
            else
            {
                SelectAllCheckBox.IsChecked = null; // Indeterminate state
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            InterfaceName = InterfaceNameTextBox.Text?.Trim();

            // Validate interface name
            if (string.IsNullOrWhiteSpace(InterfaceName))
            {
                MessageBox.Show(
                    "Please enter an interface name.",
                    Constants.ExtensionName,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                InterfaceNameTextBox.Focus();
                return;
            }

            // Check if valid C# identifier
            if (!SyntaxFacts.IsValidIdentifier(InterfaceName))
            {
                MessageBox.Show(
                    "The interface name is not a valid C# identifier.\n\n" +
                    "Names must start with a letter or underscore and contain only letters, digits, and underscores.",
                    Constants.ExtensionName,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                InterfaceNameTextBox.Focus();
                InterfaceNameTextBox.SelectAll();
                return;
            }

            // Check if it's a reserved keyword
            if (SyntaxFacts.GetKeywordKind(InterfaceName) != SyntaxKind.None)
            {
                MessageBox.Show(
                    $"'{InterfaceName}' is a C# reserved keyword and cannot be used as an interface name.",
                    Constants.ExtensionName,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                InterfaceNameTextBox.Focus();
                InterfaceNameTextBox.SelectAll();
                return;
            }

            // Warn if doesn't start with 'I'
            if (!InterfaceName.StartsWith(Constants.InterfacePrefix) || InterfaceName.Length < 2)
            {
                var result = MessageBox.Show(
                    $"Interface names typically start with '{Constants.InterfacePrefix}'. Do you want to continue?",
                    Constants.ExtensionName,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.No)
                {
                    InterfaceNameTextBox.Focus();
                    InterfaceNameTextBox.SelectAll();
                    return;
                }
            }

            // Validate at least one member is selected
            if (!Members.Any(m => m.IsSelected))
            {
                MessageBox.Show(
                    "Please select at least one member to include in the interface.",
                    Constants.ExtensionName,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
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
        }

        private void DeselectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var member in Members)
            {
                member.IsSelected = false;
            }
        }

        private void SelectAllCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            // Prevent recursion
            if (SelectAllCheckBox.IsChecked == null)
                return;

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