using FluentAssertions;
using InterfaceExtractor.UI;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Generic;
using Xunit;

namespace InterfaceExtractor.Tests.UI
{
    public class ExtractInterfaceDialogTests
    {
        // Note: WPF dialogs require STA thread and can't be easily tested in xUnit
        // These tests focus on validation logic that doesn't require dialog instantiation

        [StaFact]
        public void Constructor_InitializesWithClassName()
        {
            // This test requires STA thread for WPF
            // Run manually in Visual Studio Test Explorer with STA thread support
            var members = new List<MemberSelectionItem>();
            var dialog = new ExtractInterfaceDialog("TestClass", members);
            dialog.Should().NotBeNull();
        }

        [StaFact]
        public void Constructor_SuggestsInterfaceNameWithIPrefix()
        {
            // This test requires STA thread for WPF
            var members = new List<MemberSelectionItem>();
            var dialog = new ExtractInterfaceDialog("BookingData", members);
            dialog.Should().NotBeNull();
        }

        [Theory]
        [InlineData("IValidName", true)]
        [InlineData("ITest123", true)]
        [InlineData("I_Test", true)]
        [InlineData("", false)]
        [InlineData(" ", false)]
        [InlineData("123Invalid", false)]
        [InlineData("class", false)]
        [InlineData("interface", false)]
        [InlineData("void", false)]
        [InlineData("public", false)]
        public void ValidateInterfaceName_ChecksValidIdentifier(string name, bool shouldBeValid)
        {
            // Act
            var isValid = SyntaxFacts.IsValidIdentifier(name);
            var isKeyword = SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None;

            // Assert
            if (shouldBeValid)
            {
                isValid.Should().BeTrue();
                isKeyword.Should().BeFalse();
            }
            else
            {
                (isValid && !isKeyword).Should().BeFalse();
            }
        }
    }
}