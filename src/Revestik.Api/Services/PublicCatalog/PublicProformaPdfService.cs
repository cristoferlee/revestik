using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Revestik.Api.Services.PublicCatalog;

// Only public, already-validated data is passed to this transient in-memory renderer.
// No file or database write is performed by this service.
public sealed record PublicProformaLine(
    int ProductId, string Name, string? Brand, decimal Quantity, string? SalesUnit,
    decimal? Price, string? Currency, byte[]? CoverImage);

public sealed class PublicProformaPdfService
{
    private const string Gold = "B8832D";
    private const string Ink = "253140";
    private const string Muted = "677282";
    private const string Pale = "F6F4F0";

    public byte[] Generate(string language, IReadOnlyList<PublicProformaLine> lines, DateTimeOffset generatedAtUtc)
    {
        if (lines is null || lines.Count is < 1 or > 50)
            throw new ArgumentException("The quotation requires 1-50 products.", nameof(lines));
        language = language is "en" or "de" ? language : "es";
        var locale = CultureInfo.GetCultureInfo(language switch
        {
            "en" => "en-US", "de" => "de-DE", _ => "es-CR"
        });
        var totals = lines.Where(line => line.Price.HasValue && (line.Currency is "CRC" or "USD"))
            .GroupBy(line => line.Currency!)
            .OrderBy(group => group.Key)
            .Select(group => new
            {
                Currency = group.Key,
                Amount = group.Sum(line => Math.Round(line.Price!.Value * line.Quantity, 2,
                    MidpointRounding.AwayFromZero))
            }).ToArray();
        var labels = language switch
        {
            "en" => new PdfLabels("PRELIMINARY QUOTATION", "Product selection", "Description", "Qty.", "Unit", "Unit price", "Estimated amount", "Price on request", "Estimated subtotal", "Tax and delivery have not been determined. All prices and availability are subject to confirmation. This is not an invoice or a final, binding quotation.", "Generated", "Page"),
            "de" => new PdfLabels("UNVERBINDLICHE PROFORMA", "Produktauswahl", "Beschreibung", "Menge", "Einheit", "Stückpreis", "Geschätzter Betrag", "Preis auf Anfrage", "Geschätzte Zwischensumme", "Steuern und Lieferung sind nicht festgelegt. Preise und Verfügbarkeit bedürfen der Bestätigung. Dies ist weder eine Rechnung noch ein verbindliches Angebot.", "Erstellt", "Seite"),
            _ => new PdfLabels("PROFORMA PRELIMINAR", "Selección de productos", "Descripción", "Cant.", "Unidad", "Precio unit.", "Importe estimado", "Precio por consultar", "Subtotal orientativo", "Impuestos y transporte pendientes de confirmar. Precios y disponibilidad sujetos a confirmación. Este documento no es factura ni cotización comercial aceptada.", "Generado", "Página")
        };
        var svgPath = Path.Combine(AppContext.BaseDirectory, "Assets", "revestik.svg");
        var logo = File.Exists(svgPath) ? File.ReadAllText(svgPath) : null;

        return Document.Create(document => document.Page(page =>
        {
            // US Letter, 8.5 x 11 in. 36pt = 0.5 in safe printable margins.
            page.Size(PageSizes.Letter);
            page.Margin(36);
            page.DefaultTextStyle(text => text.FontFamily("Arial").FontSize(9).FontColor(Ink));

            page.Header().Column(column =>
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem().Column(left =>
                    {
                        if (!string.IsNullOrWhiteSpace(logo))
                            left.Item().Width(145).Height(48).Svg(logo).FitArea();
                        else
                            left.Item().Text("REVESTIK").FontSize(20).Bold();
                        left.Item().PaddingTop(4).Text(labels.Selection).FontColor(Muted).FontSize(8);
                    });
                    row.ConstantItem(200).AlignRight().Column(right =>
                    {
                        right.Item().AlignRight().Text(labels.Title).FontSize(14).Bold().FontColor(Gold);
                        right.Item().AlignRight().Text($"{labels.Generated}: {generatedAtUtc.UtcDateTime:dd/MM/yyyy HH:mm} UTC").FontSize(8);
                    });
                });
                column.Item().PaddingTop(12).LineHorizontal(1).LineColor(Gold);
            });

            page.Content().PaddingVertical(14).Column(column =>
            {
                column.Spacing(14);
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(57);
                        columns.RelativeColumn(3.2f);
                        columns.RelativeColumn(0.8f);
                        columns.RelativeColumn(0.9f);
                        columns.RelativeColumn(1.35f);
                        columns.RelativeColumn(1.4f);
                    });
                    table.Header(header =>
                    {
                        HeaderCell(header.Cell(), "");
                        HeaderCell(header.Cell(), labels.Description);
                        HeaderCell(header.Cell(), labels.Quantity);
                        HeaderCell(header.Cell(), labels.Unit);
                        HeaderCell(header.Cell(), labels.UnitPrice);
                        HeaderCell(header.Cell(), labels.Amount);
                    });
                    foreach (var line in lines)
                    {
                        // Decode before composing the cell. Even if a file passed the
                        // catalog's structural checks, an invalid raster must not make
                        // the entire customer quotation fail.
                        var image = DecodeCoverImage(line.CoverImage);
                        if (image is not null)
                            table.Cell().Padding(4).Height(48).Image(image).FitArea();
                        else
                            table.Cell().Padding(4).AlignMiddle().Text("REVESTIK").FontSize(7).FontColor(Muted);

                        table.Cell().Element(c => Cell(c).Column(col =>
                        {
                            col.Item().Text(line.Name).SemiBold();
                            if (!string.IsNullOrWhiteSpace(line.Brand))
                                col.Item().Text(line.Brand).FontSize(7).FontColor(Muted);
                        }));
                        table.Cell().Element(c => Cell(c).AlignRight().Text(line.Quantity.ToString("0.###", locale)));
                        table.Cell().Element(c => Cell(c).Text(line.SalesUnit ?? "—"));
                        var hasPrice = line.Price.HasValue && (line.Currency is "CRC" or "USD");
                        table.Cell().Element(c => Cell(c).AlignRight().Text(hasPrice
                            ? Money(line.Price!.Value, line.Currency!, locale) : "—"));
                        table.Cell().Element(c => Cell(c).AlignRight().Text(hasPrice
                            ? Money(Math.Round(line.Price!.Value * line.Quantity, 2, MidpointRounding.AwayFromZero),
                                line.Currency!, locale) : labels.NoPrice));
                    }
                });

                if (totals.Length > 0)
                    column.Item().AlignRight().Width(265).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();
                            columns.ConstantColumn(100);
                        });
                        foreach (var total in totals)
                        {
                            table.Cell().Background(Pale).Padding(8)
                                .Text($"{labels.Subtotal} ({total.Currency})").SemiBold();
                            table.Cell().Background(Pale).Padding(8).AlignRight()
                                .Text(Money(total.Amount, total.Currency, locale)).Bold();
                        }
                    });

                column.Item().BorderTop(1).BorderColor(Colors.Grey.Lighten2).PaddingTop(9)
                    .Text(labels.Disclaimer).FontColor(Muted).FontSize(8);
            });

            page.Footer().Column(column =>
            {
                column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                column.Item().PaddingTop(7).Row(row =>
                {
                    row.RelativeItem().Text("REVESTIK · Costa Rica").FontSize(7).FontColor(Muted);
                    row.ConstantItem(92).AlignRight()
                        .DefaultTextStyle(x => x.FontSize(7).FontColor(Muted))
                        .Text(text =>
                        {
                            text.Span(labels.Page + " ");
                            text.CurrentPageNumber();
                            text.Span(" / ");
                            text.TotalPages();
                        });
                });
            });
        })).GeneratePdf();
    }

    private static Image? DecodeCoverImage(byte[]? bytes)
    {
        if (bytes is not { Length: > 0 }) return null;
        try
        {
            return Image.FromBinaryData(bytes);
        }
        catch (QuestPDF.Drawing.Exceptions.DocumentComposeException) { return null; }
        catch (ArgumentException) { return null; }
        catch (InvalidDataException) { return null; }
        catch (InvalidOperationException) { return null; }
    }

    private static IContainer Cell(IContainer container) =>
        container.BorderBottom(0.6f).BorderColor(Colors.Grey.Lighten2).Padding(5);

    private static void HeaderCell(IContainer container, string label) =>
        container.Background(Ink).Padding(5).Text(label).FontColor(Colors.White).Bold().FontSize(7);

    private static string Money(decimal amount, string currency, CultureInfo locale) =>
        (currency == "CRC" ? "₡" : "$") + amount.ToString("N2", locale);

    private sealed record PdfLabels(string Title, string Selection, string Description,
        string Quantity, string Unit, string UnitPrice, string Amount,
        string NoPrice, string Subtotal, string Disclaimer, string Generated, string Page);
}
