using JdeClient.Core.XmlEngine.Models;

namespace JdeClient.Core.UnitTests.XmlEngine;

public class DataStructureTemplateTests
{
    [Test]
    public async Task Parse_ValidXml_BuildsTemplate()
    {
        // Arrange
        var xml = "\uFEFF\u0000<root szDescription=\"Template Desc\" xmlns=\"http://jde\">" +
                  "<Template>" +
                  "<Item ItemID=\"1\" DisplaySequence=\"1\" CopyWord=\"IN\" DDAlias=\"AL1\" FieldName=\"Field1\" />" +
                  "<Item ItemID=\"2\" DisplaySequence=\"2\" CopyWord=\"OUT\" DDAlias=\"AL2\" FieldName=\"Field2\" />" +
                  "<Item ItemID=\"\" DisplaySequence=\"3\" CopyWord=\"IN\" DDAlias=\"AL3\" FieldName=\"Field3\" />" +
                  "</Template>" +
                  "</root>";

        // Act
        var template = DataStructureTemplate.Parse("D0001", xml);

        // Assert
        await Assert.That(template.TemplateName).IsEqualTo("D0001");
        await Assert.That(template.Description).IsEqualTo("Template Desc");
        await Assert.That(template.ItemsById.Count).IsEqualTo(2);
        await Assert.That(template.TryGetItem("1") is not null).IsTrue();
        await Assert.That(template.TryGetItem("missing") is null).IsTrue();
    }

    [Test]
    public async Task TemplateItem_GetFormattedName_UsesAlias()
    {
        // Arrange
        var item = new DataStructureTemplateItem
        {
            Id = "1",
            DisplaySequence = "1",
            CopyWork = "IN",
            Alias = "AL1",
            FieldName = "Field1"
        };

        // Act
        var result = item.GetFormattedName();

        // Assert
        await Assert.That(result).IsEqualTo("Field1 [AL1]");
    }
}
