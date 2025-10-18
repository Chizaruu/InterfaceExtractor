using FluentAssertions;
using InterfaceExtractor.UI;
using Xunit;

namespace InterfaceExtractor.Tests.UI
{
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
}