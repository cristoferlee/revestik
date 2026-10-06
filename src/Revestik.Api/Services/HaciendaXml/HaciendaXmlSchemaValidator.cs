using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace Revestik.Api.Services.HaciendaXml;

public sealed class HaciendaXmlSchemaValidator : IHaciendaXmlSchemaValidator
{
    private const string XmlSchemaNamespace = "http://www.w3.org/2001/XMLSchema";
    private const string XmlDsigNamespace = "http://www.w3.org/2000/09/xmldsig#";
    private const long MaxCharactersInDocument = 10_485_760;

    private readonly string schemaDirectory;
    private readonly Dictionary<HaciendaXmlDocumentKind, XmlSchemaSet> compiledSchemas = [];
    private readonly object sync = new();

    public HaciendaXmlSchemaValidator(IWebHostEnvironment environment)
    {
        schemaDirectory = Path.Combine(
            environment.ContentRootPath,
            "Data",
            "Hacienda",
            "v4.4");
    }

    public HaciendaXmlValidationResult Validate(ReadOnlyMemory<byte> xml)
    {
        var descriptor = HaciendaXmlDocumentDetector.Detect(xml);
        var schemas = GetOrCompileSchema(descriptor);
        var issues = new List<string>();

        try
        {
            using var stream = new MemoryStream(xml.ToArray(), writable: false);
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MaxCharactersInDocument,
                ValidationType = ValidationType.Schema,
                Schemas = schemas,
                ValidationFlags = XmlSchemaValidationFlags.ReportValidationWarnings
            };

            settings.ValidationEventHandler += (_, args) =>
            {
                if (ShouldRejectSchemaIssue(args.Severity))
                    issues.Add(args.Message);
            };

            using var reader = XmlReader.Create(stream, settings);
            while (reader.Read()) { }
        }
        catch (XmlException exception)
        {
            throw new HaciendaXmlValidationException(
                $"El XML está malformado o contiene una construcción no permitida: {exception.Message}");
        }

        if (issues.Count > 0)
        {
            var summary = string.Join(" | ", issues.Take(5));
            throw new HaciendaXmlValidationException(
                $"El documento no cumple el XSD oficial Hacienda v4.4 para {descriptor.RootElement}: {summary}");
        }

        return new HaciendaXmlValidationResult(descriptor);
    }


    internal static bool ShouldRejectSchemaIssue(XmlSeverityType severity) =>
        severity == XmlSeverityType.Error;

    private XmlSchemaSet GetOrCompileSchema(HaciendaXmlDocumentDescriptor descriptor)
    {
        lock (sync)
        {
            if (compiledSchemas.TryGetValue(descriptor.Kind, out var cached))
                return cached;

            var documentSchemaPath = Path.Combine(schemaDirectory, descriptor.SchemaFileName);
            var signatureSchemaPath = Path.Combine(schemaDirectory, "xmldsig-core-schema.xsd");

            if (!File.Exists(documentSchemaPath) || !File.Exists(signatureSchemaPath))
            {
                throw new HaciendaXmlValidationException(
                    "Los XSD Hacienda v4.4 no están instalados. Ejecute scripts/Setup-HaciendaSchemas.ps1 antes de procesar documentos.");
            }

            var set = new XmlSchemaSet { XmlResolver = null };
            set.Add(ReadTrustedSchema(signatureSchemaPath, removeDtd: true));
            set.Add(ReadDocumentSchema(documentSchemaPath, descriptor.Namespace));
            set.Compile();

            compiledSchemas[descriptor.Kind] = set;
            return set;
        }
    }

    private static XmlSchema ReadTrustedSchema(string path, bool removeDtd)
    {
        var content = File.ReadAllText(path);
        if (removeDtd)
            content = RemoveDoctype(content);

        using var stringReader = new StringReader(content);
        using var reader = XmlReader.Create(
            stringReader,
            new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            });

        return XmlSchema.Read(reader, (_, args) =>
                   throw new HaciendaXmlValidationException(args.Message))
               ?? throw new HaciendaXmlValidationException($"No fue posible leer el esquema {Path.GetFileName(path)}.");
    }

    private static XmlSchema ReadDocumentSchema(string path, string expectedNamespace)
    {
        var content = File.ReadAllText(path);
        var xsd = XDocument.Parse(content, LoadOptions.PreserveWhitespace);
        var root = xsd.Root
                   ?? throw new HaciendaXmlValidationException($"El esquema {Path.GetFileName(path)} no tiene raíz.");

        var targetNamespace = (string?)root.Attribute("targetNamespace");
        if (!string.Equals(targetNamespace, expectedNamespace, StringComparison.Ordinal))
        {
            throw new HaciendaXmlValidationException(
                $"El XSD {Path.GetFileName(path)} no corresponde al namespace Hacienda esperado.");
        }

        XNamespace xs = XmlSchemaNamespace;
        foreach (var import in root.Elements(xs + "import")
                     .Where(element => string.Equals((string?)element.Attribute("namespace"), XmlDsigNamespace, StringComparison.Ordinal)))
        {
            // XMLDSIG is loaded independently from a pinned local file. Removing schemaLocation
            // prevents any network or file-system resolution requested by the XSD itself.
            import.Attribute("schemaLocation")?.Remove();
        }

        using var stringReader = new StringReader(xsd.ToString(SaveOptions.DisableFormatting));
        using var reader = XmlReader.Create(
            stringReader,
            new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            });

        return XmlSchema.Read(reader, (_, args) =>
                   throw new HaciendaXmlValidationException(args.Message))
               ?? throw new HaciendaXmlValidationException($"No fue posible leer el esquema {Path.GetFileName(path)}.");
    }

    private static string RemoveDoctype(string xml)
    {
        var start = xml.IndexOf("<!DOCTYPE", StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return xml;

        var bracketDepth = 0;
        var inQuote = false;
        char quote = '\0';

        for (var index = start; index < xml.Length; index++)
        {
            var current = xml[index];
            if (inQuote)
            {
                if (current == quote) inQuote = false;
                continue;
            }

            if (current is '\'' or '"')
            {
                inQuote = true;
                quote = current;
                continue;
            }

            if (current == '[') bracketDepth++;
            else if (current == ']') bracketDepth--;
            else if (current == '>' && bracketDepth <= 0)
                return xml.Remove(start, index - start + 1);
        }

        throw new HaciendaXmlValidationException("El XSD XMLDSIG contiene un DOCTYPE inválido.");
    }
}
