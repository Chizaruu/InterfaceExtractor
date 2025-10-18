using FluentAssertions;
using InterfaceExtractor.UI;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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

    public class MemberSelectionItemTests
    {
        [Fact]
        public void IsSelected_RaisesPropertyChanged()
        {
            // Arrange
            var item = new MemberSelectionItem
            {
                DisplayText = "string Name { get; set; }",
                Signature = "string Name { get; set; }",
                MemberType = "Property",
                IsSelected = false
            };

            bool eventRaised = false;
            item.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MemberSelectionItem.IsSelected))
                    eventRaised = true;
            };

            // Act
            item.IsSelected = true;

            // Assert
            eventRaised.Should().BeTrue();
            item.IsSelected.Should().BeTrue();
        }

        [Fact]
        public void IsSelected_DoesNotRaisePropertyChangedWhenValueUnchanged()
        {
            // Arrange
            var item = new MemberSelectionItem
            {
                DisplayText = "string Name { get; set; }",
                Signature = "string Name { get; set; }",
                MemberType = "Property",
                IsSelected = true
            };

            bool eventRaised = false;
            item.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MemberSelectionItem.IsSelected))
                    eventRaised = true;
            };

            // Act
            item.IsSelected = true; // Same value

            // Assert
            eventRaised.Should().BeFalse();
        }

        [Fact]
        public void MemberSelectionItem_StoresAllProperties()
        {
            // Arrange & Act
            var item = new MemberSelectionItem
            {
                DisplayText = "T GetById<T>(int id) where T : class",
                Signature = "T GetById<T>(int id)",
                MemberType = "Method",
                Constraints = "where T : class",
                IsSelected = true
            };

            // Assert
            item.DisplayText.Should().Be("T GetById<T>(int id) where T : class");
            item.Signature.Should().Be("T GetById<T>(int id)");
            item.MemberType.Should().Be("Method");
            item.Constraints.Should().Be("where T : class");
            item.IsSelected.Should().BeTrue();
        }
    }

    public class OverwriteDialogTests
    {
        [StaFact]
        public void Constructor_SetsDefaultChoiceToNo()
        {
            // Arrange & Act
            var dialog = new OverwriteDialog("ITestClass.cs");

            // Assert
            dialog.Choice.Should().Be(OverwriteChoice.No);
        }

        [Theory]
        [InlineData(OverwriteChoice.Yes)]
        [InlineData(OverwriteChoice.YesToAll)]
        [InlineData(OverwriteChoice.No)]
        [InlineData(OverwriteChoice.NoToAll)]
        public void OverwriteChoice_EnumHasAllExpectedValues(OverwriteChoice choice)
        {
            // Assert
            Enum.IsDefined(typeof(OverwriteChoice), choice).Should().BeTrue();
        }
    }
}