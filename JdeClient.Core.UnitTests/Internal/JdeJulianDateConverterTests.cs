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

    [Test]
    public async Task ToDate_NegativeValue_ReturnsNull()
    {
        var date = JdeJulianDateConverter.ToDate(-1);
        await Assert.That(date is null).IsTrue();
    }

    [Test]
    public async Task ToDate_DddZero_ReturnsNull()
    {
        // 125000: c=1, yy=25, ddd=0 → null
        var date = JdeJulianDateConverter.ToDate(125000);
        await Assert.That(date is null).IsTrue();
    }

    [Test]
    public async Task ToDate_InvalidDayCausingException_ReturnsNull()
    {
        // 125999: c=1, yy=25, ddd=999 → DateTime(2025, 1, 1).AddDays(998)
        // That's valid. Let's try a value that causes a bad year.
        // 999999: c=9, yy=99, ddd=999 → year = 1900 + 900 + 99 = 2899
        // DateTime(2899, 1, 1).AddDays(998) — this may or may not work
        // Use an extremely large value to trigger exception
        // 9999999: c=99, yy=99, ddd=999 → year = 1900 + 9900 + 99 = 11899
        // DateTime only supports years up to 9999, so this throws
        var date = JdeJulianDateConverter.ToDate(9999999);
        await Assert.That(date is null).IsTrue();
    }
}
