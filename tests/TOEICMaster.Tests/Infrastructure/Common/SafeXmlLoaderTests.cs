using System.Text;
using System.Xml;
using TOEICMaster.Application.Common.Xml;

namespace TOEICMaster.Tests.Infrastructure.Common;

public sealed class SafeXmlLoaderTests
{
    [Fact]
    public void Load_rejects_xml_containing_doctype_declaration()
    {
        const string maliciousXml =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <!DOCTYPE test [
              <!ENTITY xxe SYSTEM "file:///etc/passwd">
            ]>
            <root>&xxe;</root>
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(maliciousXml));
        var exception = Assert.Throws<XmlException>(() => SafeXmlLoader.Load(stream));
        Assert.Contains("DTD is prohibited", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_parses_valid_xml_successfully()
    {
        const string validXml = """<root><item id="1">value</item></root>""";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(validXml));
        var doc = SafeXmlLoader.Load(stream);
        Assert.Equal("value", doc.Root?.Element("item")?.Value);
    }

    [Fact]
    public void Load_rejects_xml_exceeding_max_characters_in_document()
    {
        const string largeXml = """<root><item>012345678901234567890123456789</item></root>""";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(largeXml));
        var exception = Assert.Throws<XmlException>(() => SafeXmlLoader.Load(stream, maxCharactersInDocument: 20));
        Assert.NotNull(exception);
    }

    [Fact]
    public void Load_accepts_xml_within_max_characters_in_document()
    {
        const string validXml = """<root><item id="1">value</item></root>""";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(validXml));
        var doc = SafeXmlLoader.Load(stream, maxCharactersInDocument: 1000);
        Assert.Equal("value", doc.Root?.Element("item")?.Value);
    }
}
