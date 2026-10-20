using System.Globalization;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Services.Projects;
using Revestik.Shared.Projects;

namespace Revestik.Api.Services.Projects.Pdf;

// Internal, read-only report. File bodies and physical storage paths are never rendered.
public sealed class ProjectReportPdfService(RevestikDbContext db, ProjectMilestoneService milestoneService)
{
    private static readonly CultureInfo Cr = CultureInfo.GetCultureInfo("es-CR");
    private const string Accent = "E98B19";
    private const string Ink = "233044";
    private const string Muted = "677489";
    private const string Pale = "F4F6F9";
    // Mismo recurso SVG usado en los PDF de Ventas y Cotizaciones.
    private static readonly string LogoPath =
        Path.Combine(AppContext.BaseDirectory, "Assets", "revestik.svg");

    public async Task<byte[]?> GenerateAsync(int projectId, CancellationToken ct)
    {
        var project = await db.Set<Project>().AsNoTracking().Include(x => x.Contacts)
            .SingleOrDefaultAsync(x => x.Id == projectId, ct);
        if (project is null) return null;

        var customer = await db.Customers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == project.CustomerId, ct);
        var responsible = await db.Users.AsNoTracking()
            .Where(x => x.Id == project.ResponsibleUserId)
            .Select(x => new { x.DisplayName, x.UserName }).SingleOrDefaultAsync(ct);
        var responsibleName = !string.IsNullOrWhiteSpace(responsible?.DisplayName)
            ? responsible.DisplayName : responsible?.UserName ?? "No disponible";

        var events = await db.Set<ProjectEvent>().AsNoTracking().Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.OccurredOn).ThenByDescending(x => x.Id).ToListAsync(ct);
        var eventIds = events.Select(x => x.Id).ToList();
        var agreements = await db.Set<ProjectEventAgreement>().AsNoTracking()
            .Where(x => eventIds.Contains(x.ProjectEventId)).OrderBy(x => x.ProjectEventId).ToListAsync(ct);
        var eventTitles = events.ToDictionary(x => x.Id, x => x.Title);
        var tasks = await db.Set<ProjectTask>().AsNoTracking().Where(x => x.ProjectId == projectId)
            .OrderBy(x => x.IsCompleted).ThenBy(x => x.DueOn).ThenBy(x => x.Id).ToListAsync(ct);
        var payments = await db.Set<ProjectPayment>().AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.Status == "Active")
            .OrderBy(x => x.PaidOn).ThenBy(x => x.Id).ToListAsync(ct);
        var progress = await milestoneService.GetAsync(projectId, ct);
        var milestones = progress?.Milestones.Where(x => !x.IsExcluded).OrderBy(x => x.SortOrder).ToList();
        var documents = await db.Set<ProjectDocument>().AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.DeletedAtUtc == null)
            .OrderBy(x => x.Category).ThenBy(x => x.DisplayName).ToListAsync(ct);

        var paid = payments.Sum(x => x.Amount);
        var currency = project.CurrencyCode;
        var due = project.AgreedAmount - paid;
        var generated = DateTime.UtcNow;
        var percent = progress?.Percent;

        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(32);
            page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(9).FontColor(Ink));
            page.Header().Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(left =>
                    {
                        left.Item().Width(145).Height(50).Element(ComposeLogo);
                        left.Item().PaddingTop(4).Text("INFORME DE ESTADO DEL PROYECTO").FontSize(8).Bold().FontColor(Muted);
                    });
                    row.ConstantItem(130).AlignRight().Column(right =>
                    {
                        right.Item().AlignRight().Text(StatusLabel(project.Status)).Bold().FontColor(Accent);
                        right.Item().AlignRight().Text($"Generado: {generated:dd/MM/yyyy} UTC").FontSize(7).FontColor(Muted);
                    });
                });
                col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Accent);
            });
            page.Content().PaddingVertical(14).Column(col =>
            {
                col.Spacing(12);
                col.Item().Text(project.Name).FontSize(16).Bold();
                col.Item().Text($"Cliente: {customer?.Name ?? "No disponible"}   ·   Responsable: {responsibleName}").FontSize(9);
                col.Item().Text($"Inicio estimado: {Date(project.EstimatedStartDate)}   ·   Fin estimado: {Date(project.EstimatedEndDate)}").FontColor(Muted);
                if (!string.IsNullOrWhiteSpace(project.OtherSigns)) col.Item().Text("Ubicación: " + project.OtherSigns);
                if (!string.IsNullOrWhiteSpace(project.Description))
                {
                    Heading(col, "DESCRIPCIÓN DEL PROYECTO");
                    col.Item().Text(project.Description);
                }

                Heading(col, "RESUMEN EJECUTIVO");
                col.Item().Row(row =>
                {
                    Metric(row.RelativeItem(), "AVANCE DE HITOS", percent is decimal v ? v.ToString("N2", Cr) + " %" : "Sin activar");
                    Metric(row.RelativeItem(), "MONTO ACORDADO", project.AgreedAmount is decimal amount ? Money(amount, currency) : "No definido");
                    Metric(row.RelativeItem(), "ABONADO", Money(paid, currency));
                    Metric(row.RelativeItem(), "SALDO PENDIENTE", due is decimal value ? Money(value, currency) : "No definido");
                });

                Heading(col, "PLAN DE HITOS Y AVANCE");
                if (percent is decimal progressValue)
                {
                    var width = Math.Clamp(progressValue, 0m, 100m);
                    col.Item().Text($"Progreso del proyecto: {progressValue.ToString("N2", Cr)} %").SemiBold();
                    col.Item().Height(9).Row(row =>
                    {
                        if (width > 0) row.RelativeItem((float)width).Background(Accent);
                        if (width < 100) row.RelativeItem((float)(100 - width)).Background("E7EAF0");
                    });
                }
                else col.Item().Text("No hay un plan activo con progreso calculado.").FontColor(Muted);
                if (milestones is { Count: > 0 })
                {
                    foreach (var item in milestones)
                        Entry(col, item.IsCompleted ? "COMPLETADO" : "PENDIENTE", item.Name,
                            $"Peso: {item.Weight.ToString("N2", Cr)} %" +
                            (string.IsNullOrWhiteSpace(item.Description) ? "" : "  ·  " + item.Description));
                }
                else col.Item().Text("No hay hitos configurados.").FontColor(Muted);

                Heading(col, "SITUACIÓN FINANCIERA");
                col.Item().Text($"Total vigente abonado: {Money(paid, currency)}  ·  Saldo: {(due is decimal remaining ? Money(remaining, currency) : "No definido")}");
                if (payments.Count == 0) col.Item().Text("Sin abonos vigentes registrados.").FontColor(Muted);
                else
                {
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(85);
                            columns.RelativeColumn();
                            columns.ConstantColumn(120);
                        });
                        table.Header(header =>
                        {
                            TableHeader(header.Cell(), "FECHA");
                            TableHeader(header.Cell(), "REFERENCIA");
                            TableHeader(header.Cell(), "IMPORTE");
                        });
                        foreach (var p in payments)
                        {
                            TableValue(table.Cell(), Date(p.PaidOn));
                            TableValue(table.Cell(), string.IsNullOrWhiteSpace(p.Reference) ? "Sin referencia" : p.Reference);
                            TableValue(table.Cell(), Money(p.Amount, p.CurrencyCode));
                        }
                    });
                }

                Heading(col, "ACTIVIDADES Y ACUERDOS");
                if (events.Count == 0) col.Item().Text("No hay actividades registradas.").FontColor(Muted);
                foreach (var e in events)
                    Entry(col, Date(e.OccurredOn) + (e.IsVoided ? " · ANULADA" : ""), e.Title, e.Description);
                if (agreements.Count > 0)
                {
                    col.Item().Text("Acuerdos").Bold();
                    foreach (var a in agreements)
                        Entry(col, AgreementLabel(a.Status.ToString()), eventTitles.GetValueOrDefault(a.ProjectEventId, "Actividad no disponible"),
                            $"Aceptado por: {(!string.IsNullOrWhiteSpace(a.AcceptedBy) ? a.AcceptedBy : "Sin registrar")}" +
                            (string.IsNullOrWhiteSpace(a.Notes) ? "" : "  ·  " + a.Notes));
                }

                Heading(col, "TAREAS");
                if (tasks.Count == 0) col.Item().Text("No hay tareas registradas.").FontColor(Muted);
                foreach (var t in tasks)
                    Entry(col, t.IsCompleted ? "COMPLETADA" : "PENDIENTE", t.Title,
                        "Vence: " + Date(t.DueOn) + (string.IsNullOrWhiteSpace(t.Description) ? "" : "  ·  " + t.Description));

                Heading(col, "DOCUMENTACIÓN REGISTRADA");
                if (documents.Count == 0) col.Item().Text("No hay documentos activos registrados.").FontColor(Muted);
                foreach (var d in documents)
                    Entry(col, d.Category.ToString(), d.DisplayName, "Documento registrado en el expediente privado. No se adjunta su contenido.");

                col.Item().PaddingTop(5).Text("Este informe refleja el estado registrado al momento de su generación. No sustituye el cierre formal del proyecto.")
                    .FontSize(8).Italic().FontColor(Muted);
            });
            page.Footer().Column(footer =>
            {
                footer.Item().LineHorizontal(1).LineColor("D9DEE6");
                footer.Item().PaddingTop(5).Row(row =>
                {
                    row.RelativeItem().Text("Revestik · Expediente interno de consulta").FontSize(7).FontColor(Muted);
                    row.ConstantItem(110).AlignRight().Text(text =>
                    {
                        text.Span("Página ");
                        text.CurrentPageNumber();
                        text.Span(" de ");
                        text.TotalPages();
                    });
                });
            });
        })).GeneratePdf();
    }

    private static void ComposeLogo(IContainer container)
    {
        // Mantener el comportamiento de Cotizaciones: fallback legible si no existe el SVG.
        if (!File.Exists(LogoPath))
        {
            container.AlignMiddle().Text("REVESTIK")
                .FontSize(22).Bold().FontColor(Colors.Grey.Darken4);
            return;
        }

        var svg = File.ReadAllText(LogoPath);
        container.Svg(svg).FitArea();
    }

    private static string Date(DateOnly? value) => value?.ToString("dd/MM/yyyy", Cr) ?? "Sin fecha";
    private static string Date(DateOnly value) => value.ToString("dd/MM/yyyy", Cr);
    private static string Money(decimal value, string? currency) =>
        (currency == "CRC" ? "₡" : currency == "USD" ? "$" : (currency ?? "") + " ") + value.ToString("N2", Cr);
    private static string StatusLabel(ProjectStatus status) => status switch
    {
        ProjectStatus.Planning => "Planificación",
        ProjectStatus.PendingApproval => "Pendiente de aprobación",
        ProjectStatus.Active => "Activo",
        ProjectStatus.Paused => "Pausado",
        ProjectStatus.Completed => "Finalizado",
        ProjectStatus.Cancelled => "Cancelado",
        ProjectStatus.Archived => "Archivado",
        _ => "Estado no disponible"
    };
    private static string AgreementLabel(string status) => status switch
    {
        "Pending" => "Pendiente", "Fulfilled" => "Cumplido", "Modified" => "Modificado",
        "Unfulfilled" => "Incumplido", _ => "Sin estado"
    };
    private static void Heading(ColumnDescriptor col, string title) =>
        col.Item().PaddingTop(7).PaddingBottom(2).Text(title).FontSize(10).Bold().FontColor(Accent);
    private static void Metric(IContainer cell, string title, string value) => cell.PaddingRight(5).Background(Pale).Padding(8).Column(col =>
    {
        col.Item().Text(title).FontSize(6.5f).Bold().FontColor(Muted);
        col.Item().PaddingTop(4).Text(value).FontSize(10).SemiBold().FontColor(Ink);
    });
    private static void Entry(ColumnDescriptor col, string label, string title, string? detail)
    {
        col.Item().Column(item =>
        {
            item.Item().Text(label).FontSize(7).SemiBold().FontColor(Accent);
            item.Item().Text(title).Bold();
            if (!string.IsNullOrWhiteSpace(detail)) item.Item().Text(detail).FontColor(Muted);
            item.Item().PaddingBottom(4).LineHorizontal(0.5f).LineColor("E5E9EF");
        });
    }
    private static void TableHeader(IContainer cell, string text) =>
        cell.Background("E8EDF3").Padding(5).Text(text).Bold().FontSize(7);
    private static void TableValue(IContainer cell, string text) =>
        cell.BorderBottom(0.5f).BorderColor("E5E9EF").Padding(5).Text(text).FontSize(8);
}
