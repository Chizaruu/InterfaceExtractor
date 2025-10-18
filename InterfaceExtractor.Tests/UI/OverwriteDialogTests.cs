using FluentAssertions;
using InterfaceExtractor.UI;
using System;
using Xunit;

namespace InterfaceExtractor.Tests.UI
{
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