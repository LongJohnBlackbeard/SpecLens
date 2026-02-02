using JdeClient.Core.Internal;

namespace JdeClient.Core.UnitTests.Internal;

public class JdeJulianDateConverterTests
{
    [Test]
    public async Task ToDate_Invalid_ReturnsNullAndEmpty()
    {
        // Act
        var date = JdeJulianDateConverter.ToDate(0);
        var text = JdeJulianDateConverter.ToDateString(0);

        // Assert
        await Assert.That(date is null).IsTrue();
        await Assert.That(text).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task ToDate_Valid_ReturnsExpectedDate()
    {
        // Arrange
        const int jdeJulian = 125001; // 2025-01-01

        // Act
        var date = JdeJulianDateConverter.ToDate(jdeJulian);
        var text = JdeJulianDateConverter.ToDateString(jdeJulian);

        // Assert
        await Assert.That(date is not null).IsTrue();
        await Assert.That(date!.Value.Year).IsEqualTo(2025);
        await Assert.That(date.Value.Month).IsEqualTo(1);
        await Assert.That(date.Value.Day).IsEqualTo(1);
        await Assert.That(text).IsEqualTo("2025-01-01");
    }
}
