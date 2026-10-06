using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Revestik.Api.Authorization;
using Revestik.Api.Services.ElectronicDocuments;
using Revestik.Api.Models.Identity;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Endpoints;

public static class ElectronicDocumentEndpoints
{
    private const long MaxXmlBytes = 5L * 1024L * 1024L;

    public static IEndpointRouteBuilder MapElectronicDocumentEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/purchases/received-documents")
            .WithTags("Received documents")
            .RequireAuthorization(PolicyNames.ManagePurchases);

        group.MapGet(
                "/",
                async (
                    [AsParameters] ElectronicDocumentListRequest request,
                    IElectronicDocumentService service,
                    CancellationToken cancellationToken) =>
                {
                    var errors = ValidateRequest(request);
                    return errors.Count > 0
                        ? Results.ValidationProblem(errors)
                        : Results.Ok(await service.GetPageAsync(request, cancellationToken));
                })
            .WithName("GetReceivedElectronicDocuments");

        group.MapGet(
                "/summary",
                async (
                    IElectronicDocumentService service,
                    CancellationToken cancellationToken) =>
                    Results.Ok(await service.GetSummaryAsync(cancellationToken)))
            .WithName("GetReceivedElectronicDocumentSummary");

        group.MapGet(
                "/categories",
                async (
                    bool? includeInactive,
                    IElectronicDocumentService service,
                    CancellationToken cancellationToken) =>
                    Results.Ok(await service.GetCategoriesAsync(
                        includeInactive == true,
                        cancellationToken)))
            .WithName("GetReceivedElectronicDocumentCategories");

        group.MapPost(
                "/categories",
                async (
                    ElectronicDocumentCategoryUpsertRequest request,
                    IElectronicDocumentService service,
                    CancellationToken cancellationToken) =>
                {
                    var errors = ValidateRequest(request);
                    if (errors.Count > 0)
                    {
                        return Results.ValidationProblem(errors);
                    }

                    try
                    {
                        var category = await service.CreateCategoryAsync(
                            request,
                            cancellationToken);
                        return Results.Created(
                            $"/api/purchases/received-documents/categories/{category.Id}",
                            category);
                    }
                    catch (InvalidOperationException exception)
                    {
                        return Results.Problem(
                            title: "Categoría inválida.",
                            detail: exception.Message,
                            statusCode: StatusCodes.Status409Conflict);
                    }
                })
            .RequireAuthorization(PolicyNames.ManageAccountingClassification)
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("CreateReceivedElectronicDocumentCategory");

        group.MapPut(
                "/categories/{id:int}",
                async (
                    int id,
                    ElectronicDocumentCategoryUpsertRequest request,
                    IElectronicDocumentService service,
                    CancellationToken cancellationToken) =>
                {
                    var errors = ValidateRequest(request);
                    if (errors.Count > 0)
                    {
                        return Results.ValidationProblem(errors);
                    }

                    try
                    {
                        var category = await service.UpdateCategoryAsync(
                            id,
                            request,
                            cancellationToken);
                        return category is null
                            ? Results.NotFound()
                            : Results.Ok(category);
                    }
                    catch (InvalidOperationException exception)
                    {
                        return Results.Problem(
                            title: "Categoría inválida.",
                            detail: exception.Message,
                            statusCode: StatusCodes.Status409Conflict);
                    }
                })
            .RequireAuthorization(PolicyNames.ManageAccountingClassification)
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("UpdateReceivedElectronicDocumentCategory");

        group.MapGet(
                "/learning-rules",
                async (
                    IElectronicDocumentService service,
                    CancellationToken cancellationToken) =>
                    Results.Ok(await service.GetLearningRulesAsync(cancellationToken)))
            .RequireAuthorization(PolicyNames.ManageAccountingClassification)
            .WithName("GetReceivedElectronicDocumentLearningRules");

        group.MapPut(
                "/learning-rules",
                async (
                    ElectronicDocumentLearningRuleUpdateRequest request,
                    IElectronicDocumentService service,
                    CancellationToken cancellationToken) =>
                {
                    var errors = ValidateRequest(request);
                    if (errors.Count > 0)
                    {
                        return Results.ValidationProblem(errors);
                    }

                    try
                    {
                        var rule = await service.UpdateLearningRuleAsync(
                            request,
                            cancellationToken);
                        return rule is null
                            ? Results.NotFound()
                            : Results.Ok(rule);
                    }
                    catch (InvalidOperationException exception)
                    {
                        return Results.Problem(
                            title: "Regla aprendida inválida.",
                            detail: exception.Message,
                            statusCode: StatusCodes.Status400BadRequest);
                    }
                })
            .RequireAuthorization(PolicyNames.ManageAccountingClassification)
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("UpdateReceivedElectronicDocumentLearningRule");

        group.MapDelete(
                "/learning-rules/{ruleType}/{ruleId:int}",
                async (
                    string ruleType,
                    int ruleId,
                    IElectronicDocumentService service,
                    CancellationToken cancellationToken) =>
                {
                    try
                    {
                        var deleted = await service.DeleteLearningRuleAsync(
                            ruleType,
                            ruleId,
                            cancellationToken);
                        return deleted ? Results.NoContent() : Results.NotFound();
                    }
                    catch (InvalidOperationException exception)
                    {
                        return Results.Problem(
                            title: "Regla aprendida inválida.",
                            detail: exception.Message,
                            statusCode: StatusCodes.Status400BadRequest);
                    }
                })
            .RequireAuthorization(PolicyNames.ManageAccountingClassification)
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("DeleteReceivedElectronicDocumentLearningRule");

        group.MapGet(
                "/{id:int}",
                async (
                    int id,
                    IElectronicDocumentService service,
                    CancellationToken cancellationToken) =>
                {
                    var document = await service.GetByIdAsync(
                        id,
                        cancellationToken);
                    return document is null
                        ? Results.NotFound()
                        : Results.Ok(document);
                })
            .WithName("GetReceivedElectronicDocumentById");

        group.MapGet(
                "/{id:int}/original",
                async (
                    int id,
                    IElectronicDocumentService service,
                    CancellationToken cancellationToken) =>
                {
                    var xml = await service.GetOriginalXmlAsync(
                        id,
                        cancellationToken);
                    return xml is null
                        ? Results.NotFound()
                        : Results.File(
                            xml,
                            "application/xml",
                            $"documento-recibido-{id}.xml");
                })
            .WithName("DownloadReceivedElectronicDocumentOriginal");

        group.MapPost(
                "/{id:int}/classify",
                async (
                    int id,
                    ElectronicDocumentClassifyRequest request,
                    ClaimsPrincipal user,
                    IElectronicDocumentService service,
                    CancellationToken cancellationToken) =>
                {
                    var errors = ValidateRequest(request);
                    if (errors.Count > 0)
                    {
                        return Results.ValidationProblem(errors);
                    }

                    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (string.IsNullOrWhiteSpace(userId))
                    {
                        return Results.Unauthorized();
                    }

                    var existing = await service.GetByIdAsync(id, cancellationToken);
                    if (existing is null)
                    {
                        return Results.NotFound();
                    }

                    if (existing.ProcessingStatus != ElectronicDocumentProcessingStatus.Pending &&
                        !user.IsInRole(RoleNames.Administrator) &&
                        !user.IsInRole(RoleNames.Accountant))
                    {
                        return Results.Forbid();
                    }

                    try
                    {
                        var document = await service.ClassifyAsync(
                            id,
                            request,
                            userId,
                            cancellationToken);
                        return document is null
                            ? Results.NotFound()
                            : Results.Ok(document);
                    }
                    catch (InvalidOperationException exception)
                    {
                        return Results.Problem(
                            title: "Clasificación inválida.",
                            detail: exception.Message,
                            statusCode: StatusCodes.Status400BadRequest);
                    }
                })
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("ClassifyReceivedElectronicDocument");

        group.MapPost("/import", StageInboxAsync)
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("StageReceivedElectronicDocument");

        group.MapGet(
                "/inbox",
                async (
                    IReceivedDocumentInboxService inboxService,
                    CancellationToken cancellationToken) =>
                    Results.Ok(new ReceivedDocumentInboxListResponse(
                        await inboxService.GetPendingAsync(cancellationToken))))
            .WithName("GetReceivedDocumentInbox");

        group.MapPost(
                "/inbox/{inboxId:guid}/accept",
                async (
                    Guid inboxId,
                    ClaimsPrincipal user,
                    IReceivedDocumentInboxService inboxService,
                    CancellationToken cancellationToken) =>
                {
                    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (string.IsNullOrWhiteSpace(userId))
                        return Results.Unauthorized();

                    try
                    {
                        return Results.Ok(await inboxService.AcceptAsync(
                            inboxId,
                            userId,
                            cancellationToken));
                    }
                    catch (KeyNotFoundException)
                    {
                        return Results.NotFound();
                    }
                    catch (ReceivedDocumentInboxException exception)
                    {
                        return Results.Problem(
                            title: "No se pudo aceptar el documento.",
                            detail: exception.Message,
                            statusCode: StatusCodes.Status409Conflict);
                    }
                })
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("AcceptReceivedDocumentInboxItem");

        group.MapPost(
                "/inbox/{inboxId:guid}/reject",
                async (
                    Guid inboxId,
                    IReceivedDocumentInboxService inboxService,
                    CancellationToken cancellationToken) =>
                {
                    var rejected = await inboxService.RejectAsync(inboxId, cancellationToken);
                    return rejected ? Results.NoContent() : Results.NotFound();
                })
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("RejectReceivedDocumentInboxItem");

        return endpoints;
    }

    private static async Task<IResult> StageInboxAsync(
        HttpRequest request,
        IReceivedDocumentInboxService inboxService,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
            return Results.BadRequest(new { error = "Debe enviar un archivo XML mediante multipart/form-data." });

        var form = await request.ReadFormAsync(cancellationToken);
        if (form.Files.Count != 1)
            return Results.BadRequest(new { error = "Debe adjuntar exactamente un archivo XML." });

        var file = form.Files[0];
        if (file.Length <= 0)
            return Results.BadRequest(new { error = "El archivo XML está vacío." });

        if (file.Length > MaxXmlBytes)
            return Results.BadRequest(new { error = "El archivo XML no puede superar 5 MB." });

        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream((int)file.Length);
        await stream.CopyToAsync(buffer, cancellationToken);

        try
        {
            var staged = await inboxService.StageAsync(
                buffer.ToArray(),
                file.FileName,
                "Manual",
                cancellationToken);

            return Results.Created(
                $"/api/purchases/received-documents/inbox/{staged.Id}",
                staged);
        }
        catch (ReceivedDocumentInboxException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

    private static async Task<IResult> ImportAsync(
        HttpRequest request,
        ClaimsPrincipal user,
        IElectronicDocumentImportService importService,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return Results.BadRequest(new
            {
                error = "Debe enviar un archivo XML mediante multipart/form-data."
            });
        }

        var form = await request.ReadFormAsync(cancellationToken);
        if (form.Files.Count != 1)
        {
            return Results.BadRequest(new
            {
                error = "Debe adjuntar exactamente un archivo XML."
            });
        }

        var file = form.Files[0];
        if (file.Length <= 0)
        {
            return Results.BadRequest(new { error = "El archivo XML está vacío." });
        }

        if (file.Length > MaxXmlBytes)
        {
            return Results.BadRequest(new
            {
                error = "El archivo XML no puede superar 5 MB."
            });
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Results.Unauthorized();
        }

        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream((int)file.Length);
        await stream.CopyToAsync(buffer, cancellationToken);

        try
        {
            var result = await importService.ImportAsync(
                buffer.ToArray(),
                userId,
                cancellationToken);

            return Results.Created(
                $"/api/purchases/received-documents/{result.ElectronicDocumentId ?? result.Id}",
                result);
        }
        catch (DuplicateElectronicDocumentException exception)
        {
            return Results.Conflict(new
            {
                error = exception.Message,
                existingId = exception.ExistingId
            });
        }
        catch (InvalidElectronicDocumentReceiverException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (ElectronicDocumentXmlException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

    private static Dictionary<string, string[]> ValidateRequest<TRequest>(
        TRequest request)
        where TRequest : class
    {
        var validationResults = new List<ValidationResult>();

        Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            validationResults,
            validateAllProperties: true);

        return validationResults
            .SelectMany(result =>
                result.MemberNames
                    .DefaultIfEmpty("request")
                    .Select(memberName => new
                    {
                        MemberName = memberName,
                        ErrorMessage = result.ErrorMessage ?? "Invalid value."
                    }))
            .GroupBy(error => error.MemberName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).ToArray());
    }
}
