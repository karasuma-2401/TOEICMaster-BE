using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace TOEICMaster.Application.Common.Xml;

public static class SafeXmlLoader
{
    private static readonly XmlReaderSettings SafeSettings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null
    };

    public static XDocument Load(Stream stream, LoadOptions options = LoadOptions.None)
    {
        using var reader = XmlReader.Create(stream, SafeSettings);
        return XDocument.Load(reader, options);
    }
}
