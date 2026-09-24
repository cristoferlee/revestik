using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Revestik.Api.Configuration;
using Revestik.Shared.Sales;

namespace Revestik.Api.Services.Sales.Pdf;

public sealed class SalePdfService(
    IOptions<CompanyOptions> companyOptions)
    : ISalePdfService
{
    private readonly CompanyOptions company = companyOptions.Value;
    private readonly string logoPath =
        Path.Combine(AppContext.BaseDirectory, "Assets", "revestik.svg");

    public byte[] Generate(SaleResponse sale)
    {
        ArgumentNullException.ThrowIfNull(sale);

        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);

                page.DefaultTextStyle(text => text
                    .FontFamily("Arial")
                    .FontSize(9)
                    .FontColor(Colors.Grey.Darken3));

                page.Header()
                    .Element(container =>
                        ComposeHeader(container, sale));

                page.Content()
                    .PaddingVertical(14)
                    .Element(container =>
                        ComposeContent(container, sale));

                page.Footer()
                    .AlignCenter()
                    .Text(text =>
                    {
                        text.Span(
                                company.Website.ToUpperInvariant())
                            .SemiBold();

                        text.Span(
                            "  •  Documento interno generado desde Revestik");
                    });
            });
        }).GeneratePdf();
    }

    private void ComposeHeader(
        IContainer container,
        SaleResponse sale)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item()
                        .Width(145)
                        .Height(50)
                        .Element(ComposeLogo);

                    left.Item()
                        .PaddingTop(4)
                        .Text(company.LegalName)
                        .SemiBold();

                    left.Item().Text(
                        $"Cédula jurídica: {company.IdentificationNumber}");

                    left.Item().Text(
                        $"Tel: {company.PhoneNumber}");

                    left.Item().Text(company.Email);
                });

                row.ConstantItem(180)
                    .AlignRight()
                    .Column(right =>
                    {
                        right.Item()
                            .AlignRight()
                            .Text("VENTA")
                            .FontSize(11)
                            .Bold();

                        right.Item()
                            .AlignRight()
                            .Text(
                                string.IsNullOrWhiteSpace(
                                    sale.SaleNumber)
                                    ? "BORRADOR"
                                    : sale.SaleNumber)
                            .FontSize(18)
                            .Bold()
                            .FontColor(
                                Colors.Orange.Darken2);

                        right.Item()
                            .AlignRight()
                            .Text(
                                $"Fecha: {FormatDate(GetDocumentDate(sale))}");

                        right.Item()
                            .AlignRight()
                            .Text(
                                $"Moneda: {sale.Currency}");

                        right.Item()
                            .AlignRight()
                            .Text(
                                FormatSaleStatus(
                                    sale.Status));
                    });
            });

            column.Item()
                .PaddingTop(8)
                .Text(company.Address);

            column.Item()
                .PaddingTop(8)
                .LineHorizontal(1)
                .LineColor(
                    Colors.Orange.Darken1);
        });
    }

    private void ComposeLogo(IContainer container)
    {
        if (!File.Exists(logoPath))
        {
            container
                .AlignMiddle()
                .Text("REVESTIK")
                .FontSize(22)
                .Bold()
                .FontColor(
                    Colors.Grey.Darken4);

            return;
        }

        var svg = File.ReadAllText(logoPath);

        container.Svg(svg).FitArea();
    }

    private static void ComposeContent(
        IContainer container,
        SaleResponse sale)
    {
        container.Column(column =>
        {
            column.Spacing(12);

            column.Item()
                .Element(c =>
                    ComposeCustomerAndTerms(
                        c,
                        sale));

            column.Item()
                .Element(c =>
                    ComposeLines(
                        c,
                        sale));

            if (sale.Charges.Count > 0)
            {
                column.Item()
                    .Element(c =>
                        ComposeCharges(
                            c,
                            sale));
            }

            column.Item()
                .AlignRight()
                .Width(275)
                .Element(c =>
                    ComposeTotals(
                        c,
                        sale));

            if (!string.IsNullOrWhiteSpace(
                    sale.Observations))
            {
                column.Item()
                    .Element(c =>
                        ComposeObservations(
                            c,
                            sale));
            }

            if (!string.IsNullOrWhiteSpace(
                    sale.SourceQuotationNumber))
            {
                column.Item()
                    .Text(
                        $"Origen: {sale.SourceQuotationNumber}")
                    .FontSize(8)
                    .FontColor(
                        Colors.Grey.Darken1);
            }
        });
    }

    private static void ComposeCustomerAndTerms(
        IContainer container,
        SaleResponse sale)
    {
        container.Row(row =>
        {
            row.RelativeItem()
                .Element(box =>
                {
                    box.Border(1)
                        .BorderColor(
                            Colors.Grey.Lighten2)
                        .Padding(10)
                        .Column(column =>
                        {
                            column.Item()
                                .Text("CLIENTE")
                                .Bold()
                                .FontSize(11);

                            column.Item()
                                .PaddingTop(5)
                                .Text(
                                    sale.CustomerName);

                            column.Item()
                                .Text(
                                    $"Identificación: {sale.CustomerIdentificationNumber}");

                            if (!string.IsNullOrWhiteSpace(
                                    sale.CustomerEmail))
                            {
                                column.Item()
                                    .Text(
                                        $"Correo: {sale.CustomerEmail}");
                            }

                            if (!string.IsNullOrWhiteSpace(
                                    sale.CustomerPhoneNumber))
                            {
                                column.Item()
                                    .Text(
                                        $"Teléfono: {sale.CustomerPhoneNumber}");
                            }
                        });
                });

            row.ConstantItem(12);

            row.RelativeItem()
                .Element(box =>
                {
                    box.Border(1)
                        .BorderColor(
                            Colors.Grey.Lighten2)
                        .Padding(10)
                        .Column(column =>
                        {
                            column.Item()
                                .Text("DETALLE DE VENTA")
                                .Bold()
                                .FontSize(11);

                            column.Item()
                                .PaddingTop(5)
                                .Text(
                                    $"Fecha: {FormatDate(GetDocumentDate(sale))}");

                            column.Item()
                                .Text(
                                    $"Moneda: {sale.Currency}");

                            column.Item()
                                .Text(
                                    $"Vendedor: {sale.CreatedByDisplayName}");
                        });
                });
        });
    }

    private static void ComposeLines(
        IContainer container,
        SaleResponse sale)
    {
        container.Column(column =>
        {
            column.Item()
                .Text("DETALLE")
                .Bold()
                .FontSize(11);

            column.Item()
                .PaddingTop(5)
                .Table(table =>
                {
                    table.ColumnsDefinition(
                        columns =>
                        {
                            columns.ConstantColumn(24);
                            columns.RelativeColumn(3.2f);
                            columns.RelativeColumn(1.2f);
                            columns.RelativeColumn(0.8f);
                            columns.RelativeColumn(1.25f);
                            columns.RelativeColumn(1.1f);
                            columns.RelativeColumn(1.25f);
                        });

                    table.Header(header =>
                    {
                        HeaderCell(
                            header.Cell(),
                            "#");

                        HeaderCell(
                            header.Cell(),
                            "Descripción");

                        HeaderCell(
                            header.Cell(),
                            "Unidad");

                        HeaderCell(
                            header.Cell(),
                            "Cant.");

                        HeaderCell(
                            header.Cell(),
                            "Precio");

                        HeaderCell(
                            header.Cell(),
                            "Desc.");

                        HeaderCell(
                            header.Cell(),
                            "Total");
                    });

                    for (var index = 0;
                         index < sale.Lines.Count;
                         index++)
                    {
                        var line =
                            sale.Lines[index];

                        BodyCell(
                            table.Cell(),
                            (index + 1).ToString());

                        BodyCell(
                            table.Cell(),
                            BuildLineDescription(
                                line));

                        BodyCell(
                            table.Cell(),
                            line.Unit);

                        BodyCell(
                            table.Cell(),
                            FormatQuantity(
                                line.Quantity),
                            true);

                        BodyCell(
                            table.Cell(),
                            FormatMoney(
                                line.UnitPrice,
                                sale.Currency),
                            true);

                        BodyCell(
                            table.Cell(),
                            FormatMoney(
                                line.LineDiscountAmount +
                                line.GeneralDiscountAmount,
                                sale.Currency),
                            true);

                        BodyCell(
                            table.Cell(),
                            FormatMoney(
                                line.TotalAmount,
                                sale.Currency),
                            true);
                    }
                });
        });
    }

    private static void ComposeCharges(
        IContainer container,
        SaleResponse sale)
    {
        container.Column(column =>
        {
            column.Item()
                .Text("OTROS CARGOS")
                .Bold()
                .FontSize(11);

            column.Item()
                .PaddingTop(5)
                .Table(table =>
                {
                    table.ColumnsDefinition(
                        columns =>
                        {
                            columns.RelativeColumn(1.2f);
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(1);
                        });

                    HeaderCell(
                        table.Cell(),
                        "Tipo");

                    HeaderCell(
                        table.Cell(),
                        "Detalle");

                    HeaderCell(
                        table.Cell(),
                        "Monto");

                    foreach (var charge in sale.Charges)
                    {
                        BodyCell(
                            table.Cell(),
                            FormatChargeType(
                                charge.Type));

                        BodyCell(
                            table.Cell(),
                            string.IsNullOrWhiteSpace(
                                charge.Description)
                                ? "—"
                                : charge.Description);

                        BodyCell(
                            table.Cell(),
                            FormatMoney(
                                charge.Amount,
                                sale.Currency),
                            true);
                    }
                });
        });
    }

    private static void ComposeTotals(
        IContainer container,
        SaleResponse sale)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.ConstantColumn(110);
            });

            TotalRow(
                table,
                "Subtotal",
                sale.Subtotal,
                sale.Currency);

            TotalRow(
                table,
                "Desc. líneas",
                sale.LineDiscountTotal,
                sale.Currency);

            if (sale.GeneralDiscountTotal > 0m)
            {
                TotalRow(
                    table,
                    "Desc. general",
                    sale.GeneralDiscountTotal,
                    sale.Currency);
            }

            TotalRow(
                table,
                "IVA incluido",
                sale.TaxTotal,
                sale.Currency);

            TotalRow(
                table,
                "Otros cargos",
                sale.ChargeTotal,
                sale.Currency);

            table.Cell()
                .Background(
                    Colors.Orange.Lighten4)
                .Padding(7)
                .Text("TOTAL")
                .Bold()
                .FontSize(11);

            table.Cell()
                .Background(
                    Colors.Orange.Lighten4)
                .Padding(7)
                .AlignRight()
                .Text(
                    FormatMoney(
                        sale.Total,
                        sale.Currency))
                .Bold()
                .FontSize(11);
        });
    }

    private static void ComposeObservations(
        IContainer container,
        SaleResponse sale)
    {
        container
            .BorderTop(1)
            .BorderColor(
                Colors.Grey.Lighten2)
            .PaddingTop(8)
            .Column(column =>
            {
                column.Item()
                    .Text("OBSERVACIONES")
                    .Bold();

                column.Item()
                    .PaddingTop(4)
                    .Text(
                        sale.Observations);
            });
    }

    private static void HeaderCell(
        IContainer container,
        string text) =>
        container
            .Background(
                Colors.Grey.Darken3)
            .Padding(5)
            .Text(text)
            .FontColor(Colors.White)
            .Bold()
            .FontSize(8);

    private static void BodyCell(
        IContainer container,
        string text,
        bool alignRight = false)
    {
        var cell = container
            .BorderBottom(1)
            .BorderColor(
                Colors.Grey.Lighten3)
            .Padding(5);

        if (alignRight)
        {
            cell.AlignRight()
                .Text(text)
                .FontSize(8);
        }
        else
        {
            cell.Text(text)
                .FontSize(8);
        }
    }

    private static void TotalRow(
        TableDescriptor table,
        string label,
        decimal amount,
        Currency currency)
    {
        table.Cell()
            .BorderBottom(1)
            .BorderColor(
                Colors.Grey.Lighten3)
            .Padding(6)
            .Text(label);

        table.Cell()
            .BorderBottom(1)
            .BorderColor(
                Colors.Grey.Lighten3)
            .Padding(6)
            .AlignRight()
            .Text(
                FormatMoney(
                    amount,
                    currency))
            .SemiBold();
    }

    private static string BuildLineDescription(
        SaleLineResponse line)
    {
        if (string.IsNullOrWhiteSpace(
                line.CabysCode))
        {
            return line.Description;
        }

        return $"{line.Description}\nCABYS: {line.CabysCode}";
    }

    private static DateTime? GetDocumentDate(
        SaleResponse sale) =>
        sale.IssuedAtUtc ?? sale.CreatedAtUtc;

    private static string FormatDate(
        DateTime? value) =>
        value.HasValue
            ? value.Value.ToString("dd/MM/yyyy")
            : "—";

    private static string FormatQuantity(
        decimal value) =>
        value.ToString("0.##");

    private static string FormatMoney(
        decimal value,
        Currency currency) =>
        currency == Currency.CRC
            ? $"₡{value:N2}"
            : $"${value:N2}";

    private static string FormatSaleStatus(
        SaleStatus status) => status switch
    {
        SaleStatus.Draft => "Borrador",
        SaleStatus.Issued => "Emitida",
        SaleStatus.Voided => "Anulada",
        _ => status.ToString()
    };

    private static string FormatChargeType(
        SaleChargeType type) => type switch
    {
        SaleChargeType.Service => "Servicio",
        SaleChargeType.Transport => "Transporte",
        SaleChargeType.Installation => "Instalación",
        SaleChargeType.Other => "Otro",
        _ => type.ToString()
    };
}