using JdeClient.Core;
using JdeClient.Core.Internal;
using JdeClient.Core.Interop;
using static JdeClient.Core.Interop.JdeStructures;

namespace JdeClient.Core.UnitTests.Internal;

public class SpecBusinessViewMetadataServiceTests
{
    [Test]
    public async Task NormalizeText_RemovesNullAndTrims()
    {
        var value = SpecBusinessViewMetadataService.NormalizeText("  ABC\0  ");
        await Assert.That(value).IsEqualTo("ABC");
    }

    [Test]
    public async Task NormalizeNid_UsesFallbackWhenEmpty()
    {
        var value = SpecBusinessViewMetadataService.NormalizeNid(new NID(string.Empty), "FALLBACK");
        await Assert.That(value).IsEqualTo("FALLBACK");
    }

    [Test]
    public async Task NormalizeNid_ReturnsValueWhenPresent()
    {
        var value = SpecBusinessViewMetadataService.NormalizeNid(new NID("TEST"), "FALLBACK");
        await Assert.That(value).IsEqualTo("TEST");
    }

    [Test]
    public async Task NormalizeText_Whitespace_ReturnsEmpty()
    {
        var value = SpecBusinessViewMetadataService.NormalizeText("   ");
        await Assert.That(value).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task FormatJoinOperator_MapsKnownAndUnknownValues()
    {
        await Assert.That(SpecBusinessViewMetadataService.FormatJoinOperator(0)).IsEqualTo("=");
        await Assert.That(SpecBusinessViewMetadataService.FormatJoinOperator(9)).IsEqualTo("op 9");
    }

    [Test]
    public async Task FormatJoinType_MapsKnownAndUnknownValues()
    {
        await Assert.That(SpecBusinessViewMetadataService.FormatJoinType(0)).IsEqualTo("Inner");
        await Assert.That(SpecBusinessViewMetadataService.FormatJoinType(9)).IsEqualTo("Type 9");
    }

    [Test]
    public async Task GetBusinessViewInfo_EmptyName_ReturnsNull()
    {
        var service = new SpecBusinessViewMetadataService(new HUSER(), new JdeClientOptions());
        var result = service.GetBusinessViewInfo(" ");
        await Assert.That(result is null).IsTrue();
    }
}
