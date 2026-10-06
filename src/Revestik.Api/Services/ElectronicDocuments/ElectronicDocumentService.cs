using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Services.ElectronicDocuments;

public sealed class ElectronicDocumentService(RevestikDbContext dbContext)
    : IElectronicDocumentService
{
    public async Task<ElectronicDocumentListResponse> GetPageAsync(
        ElectronicDocumentListRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = dbContext.ElectronicDocuments.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(x =>
                x.IssuerName.Contains(search) ||
                x.IssuerIdentification.Contains(search) ||
                x.NumeroConsecutivo.Contains(search) ||
                x.Clave.Contains(search));
        }

        if (request.DocumentType.HasValue)
        {
            query = query.Where(x => x.DocumentType == request.DocumentType.Value);
        }

        if (request.ProcessingStatus.HasValue)
        {
            query = query.Where(x => x.ProcessingStatus == request.ProcessingStatus.Value);
        }

        if (request.CategoryId.HasValue)
        {
            query = query.Where(x => x.CategoryId == request.CategoryId.Value);
        }

        if (request.DateFrom.HasValue)
        {
            var from = new DateTimeOffset(
                request.DateFrom.Value.ToDateTime(TimeOnly.MinValue),
                TimeSpan.Zero);
            query = query.Where(x => x.FechaEmision >= from);
        }

        if (request.DateTo.HasValue)
        {
            var until = new DateTimeOffset(
                request.DateTo.Value.AddDays(1).ToDateTime(TimeOnly.MinValue),
                TimeSpan.Zero);
            query = query.Where(x => x.FechaEmision < until);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var skip = ((long)request.Page - 1) * request.PageSize;

        IReadOnlyList<ElectronicDocumentListItemResponse> items = skip >= totalCount
            ? []
            : await query
                .OrderByDescending(x => x.FechaEmision)
                .ThenByDescending(x => x.Id)
                .Skip((int)skip)
                .Take(request.PageSize)
                .Select(x => new ElectronicDocumentListItemResponse(
                    x.Id,
                    x.Clave,
                    x.DocumentType,
                    x.NumeroConsecutivo,
                    x.FechaEmision,
                    x.IssuerName,
                    x.IssuerIdentification,
                    x.CurrencyCode,
                    x.TotalDocument,
                    x.ProcessingStatus,
                    x.CategoryId,
                    x.Category != null ? x.Category.Name : null,
                    x.SupplierId,
                    x.PurchaseId,
                    x.HaciendaResponse != null))
                .ToListAsync(cancellationToken);

        return new ElectronicDocumentListResponse(
            items,
            request.Page,
            request.PageSize,
            totalCount);
    }

    public async Task<ElectronicDocumentSummaryResponse> GetSummaryAsync(
        CancellationToken cancellationToken)
    {
        var pending = await dbContext.ElectronicDocuments
            .CountAsync(x => x.ProcessingStatus == ElectronicDocumentProcessingStatus.Pending, cancellationToken);
        var processed = await dbContext.ElectronicDocuments
            .CountAsync(x => x.ProcessingStatus == ElectronicDocumentProcessingStatus.Processed, cancellationToken);
        var noAction = await dbContext.ElectronicDocuments
            .CountAsync(x => x.ProcessingStatus == ElectronicDocumentProcessingStatus.NoActionRequired, cancellationToken);
        var withHacienda = await dbContext.ElectronicDocuments
            .CountAsync(x => x.HaciendaResponse != null, cancellationToken);

        var classified = await dbContext.ElectronicDocuments
            .AsNoTracking()
            .Where(x =>
                x.ProcessingStatus == ElectronicDocumentProcessingStatus.Processed &&
                x.CategoryId != null)
            .Select(x => new
            {
                x.CurrencyCode,
                x.TotalDocument,
                DocumentNature = x.Category!.AccountingNature,
                Lines = x.Lines
                    .Select(line => new
                    {
                        line.TotalLine,
                        HasOverride = line.ClassificationCategoryId != null || line.OperationalDestination != null,
                        EffectiveNature = line.ClassificationCategory != null
                            ? line.ClassificationCategory.AccountingNature
                            : x.Category!.AccountingNature
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        var totals = new Dictionary<string, AccountingAccumulator>(StringComparer.OrdinalIgnoreCase);

        foreach (var document in classified)
        {
            var currency = string.IsNullOrWhiteSpace(document.CurrencyCode)
                ? "N/D"
                : document.CurrencyCode;

            if (!totals.TryGetValue(currency, out var accumulator))
            {
                accumulator = new AccountingAccumulator();
                totals[currency] = accumulator;
            }

            if (!document.Lines.Any(x => x.HasOverride))
            {
                accumulator.Add(document.DocumentNature, document.TotalDocument);
                continue;
            }

            var allocated = 0m;
            foreach (var line in document.Lines)
            {
                accumulator.Add(line.EffectiveNature, line.TotalLine);
                allocated += line.TotalLine;
            }

            accumulator.Unallocated += document.TotalDocument - allocated;
        }

        var accountingTotals = totals
            .OrderBy(x => x.Key)
            .Select(x => new ElectronicDocumentAccountingTotalResponse(
                x.Key,
                x.Value.Purchases,
                x.Value.Expenses,
                x.Value.DirectCosts,
                x.Value.Assets,
                x.Value.Unallocated))
            .ToList();

        var financialSource = await dbContext.ElectronicDocuments
            .AsNoTracking()
            .Where(x =>
                x.ProcessingStatus == ElectronicDocumentProcessingStatus.Processed &&
                x.CategoryId != null)
            .Select(x => new
            {
                x.CurrencyCode,
                x.TotalDocument,
                x.SaleConditionCode
            })
            .ToListAsync(cancellationToken);

        var financialTotals = financialSource
            .GroupBy(x => string.IsNullOrWhiteSpace(x.CurrencyCode) ? "N/D" : x.CurrencyCode)
            .OrderBy(group => group.Key)
            .Select(group => new ElectronicDocumentFinancialTotalResponse(
                group.Key,
                group.Where(x => x.SaleConditionCode == "01").Sum(x => x.TotalDocument),
                group.Where(x => x.SaleConditionCode == "02").Sum(x => x.TotalDocument),
                group.Where(x => x.SaleConditionCode != "01" && x.SaleConditionCode != "02").Sum(x => x.TotalDocument)))
            .ToList();

        return new ElectronicDocumentSummaryResponse(
            pending,
            processed,
            noAction,
            withHacienda,
            accountingTotals,
            financialTotals);
    }

    public async Task<ElectronicDocumentDetailResponse?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        await EnsureDefaultCategoriesAsync(cancellationToken);

        var document = await dbContext.ElectronicDocuments
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new ElectronicDocumentDetailResponse(
                x.Id,
                x.Clave,
                x.DocumentType,
                x.NumeroConsecutivo,
                x.FechaEmision,
                x.IssuerName,
                x.IssuerCommercialName,
                x.IssuerIdentificationType,
                x.IssuerIdentification,
                x.IssuerPhoneNumber,
                x.IssuerEmail,
                x.IssuerAddress,
                x.ReceiverName,
                x.ReceiverIdentificationType,
                x.ReceiverIdentification,
                x.SaleConditionCode,
                x.CreditTermDays,
                x.CurrencyCode,
                x.ExchangeRate,
                x.TotalTaxedServices,
                x.TotalExemptServices,
                x.TotalExoneratedServices,
                x.TotalNonSubjectServices,
                x.TotalTaxedGoods,
                x.TotalExemptGoods,
                x.TotalExoneratedGoods,
                x.TotalNonSubjectGoods,
                x.TotalTaxed,
                x.TotalExempt,
                x.TotalExonerated,
                x.TotalNonSubject,
                x.TotalSale,
                x.TotalDiscounts,
                x.TotalNetSale,
                x.TotalTax,
                x.TotalVatReturned,
                x.TotalOtherCharges,
                x.TotalDocument,
                x.ProcessingStatus,
                x.CategoryId,
                x.Category != null ? x.Category.Name : null,
                x.SupplierId,
                x.Supplier != null ? x.Supplier.Name : null,
                x.PurchaseId,
                x.ImportedAtUtc,
                x.ProcessedAtUtc,
                x.Lines
                    .OrderBy(line => line.LineNumber)
                    .Select(line => new ElectronicDocumentLineResponse(
                        line.Id,
                        line.LineNumber,
                        line.CabysCode,
                        line.CommercialCodeType,
                        line.CommercialCode,
                        line.Quantity,
                        line.UnitOfMeasure,
                        line.CommercialUnitOfMeasure,
                        line.Description,
                        line.UnitPrice,
                        line.GrossAmount,
                        line.Subtotal,
                        line.TaxableBase,
                        line.NetTax,
                        line.TotalLine,
                        line.Discounts
                            .Select(d => new ElectronicDocumentDiscountResponse(
                                d.Amount,
                                d.Code,
                                d.Nature))
                            .ToList(),
                        line.Taxes
                            .Select(t => new ElectronicDocumentTaxResponse(
                                t.TaxCode,
                                t.VatRateCode,
                                t.Rate,
                                t.Amount))
                            .ToList(),
                        null,
                        null,
                        null,
                        line.ClassificationCategoryId,
                        line.ClassificationCategory != null ? line.ClassificationCategory.Name : null,
                        line.ClassificationCategory != null
                            ? line.ClassificationCategory.AccountingNature
                            : (AccountingNature?)null,
                        line.OperationalDestination))
                    .ToList(),
                x.HaciendaResponse == null
                    ? null
                    : new HaciendaResponseDetailResponse(
                        x.HaciendaResponse.MessageCode,
                        x.HaciendaResponse.MessageStatus,
                        x.HaciendaResponse.MessageDetail,
                        x.HaciendaResponse.TotalTax,
                        x.HaciendaResponse.TotalInvoice,
                        x.HaciendaResponse.ReceivedAtUtc),
                x.IssuerEconomicActivityCode,
                x.ReceiverEconomicActivityCode,
                null,
                null,
                x.Category != null ? x.Category.AccountingNature : (AccountingNature?)null,
                x.OperationalDestination,
                null,
                null,
                null,
                null,
                false,
                x.Lines.Any(line =>
                    line.ClassificationCategoryId != null ||
                    line.OperationalDestination != null)))
            .SingleOrDefaultAsync(cancellationToken);

        if (document is null)
        {
            return null;
        }

        var cabysCodes = document.Lines
            .Select(x => x.CabysCode)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        var cabysItems = await dbContext.CabysItems
            .AsNoTracking()
            .Where(x => x.Catalog.IsCurrent && cabysCodes.Contains(x.Code))
            .Select(x => new CabysLookup(
                x.Code,
                x.Description,
                x.TaxReference,
                x.Category1Description,
                x.Category2Description,
                x.Category3Description,
                x.Category4Code,
                x.Category4Description,
                x.Category5Description,
                x.Category6Description,
                x.Category7Description,
                x.Category8Description))
            .ToListAsync(cancellationToken);

        var cabysByCode = cabysItems.ToDictionary(x => x.Code);

        var enrichedLines = document.Lines
            .Select(line =>
            {
                if (!cabysByCode.TryGetValue(line.CabysCode, out var cabys))
                {
                    return line;
                }

                var hierarchy = string.Join(
                    " > ",
                    new[]
                    {
                        cabys.Category1Description,
                        cabys.Category2Description,
                        cabys.Category3Description,
                        cabys.Category4Description,
                        cabys.Category5Description,
                        cabys.Category6Description,
                        cabys.Category7Description,
                        cabys.Category8Description
                    }.Where(value => !string.IsNullOrWhiteSpace(value)));

                return line with
                {
                    CabysOfficialDescription = cabys.Description,
                    CabysTaxReference = cabys.TaxReference,
                    CabysHierarchy = hierarchy
                };
            })
            .ToList();

        if (document.ProcessingStatus != ElectronicDocumentProcessingStatus.Pending)
        {
            return document with { Lines = enrichedLines };
        }

        // Older imports may already contain a preloaded category from the previous
        // issuer-only learning flow. Treat it as a suggestion until the user confirms.
        if (document.CategoryId.HasValue &&
            !string.IsNullOrWhiteSpace(document.CategoryName) &&
            document.CategoryAccountingNature.HasValue)
        {
            var existingCategory = await dbContext.ElectronicDocumentCategories
                .AsNoTracking()
                .Where(x => x.Id == document.CategoryId.Value)
                .Select(x => new
                {
                    x.DefaultOperationalDestination
                })
                .SingleAsync(cancellationToken);

            return document with
            {
                Lines = enrichedLines,
                SuggestedCategoryId = document.CategoryId,
                SuggestedCategoryLabel = document.CategoryName,
                SuggestedCategoryReason =
                    "Clasificación aprendida previamente para este emisor. Revísala antes de confirmar.",
                SuggestedAccountingNature = document.CategoryAccountingNature,
                SuggestedOperationalDestination =
                    document.OperationalDestination ?? existingCategory.DefaultOperationalDestination,
                SuggestedConfidence = ClassificationConfidence.Low
            };
        }

        var suggestion = await GetClassificationSuggestionAsync(
            document,
            cabysItems,
            cancellationToken);

        return suggestion is null
            ? document with { Lines = enrichedLines }
            : document with
            {
                Lines = enrichedLines,
                SuggestedCategoryId = suggestion.CategoryId,
                SuggestedCategoryLabel = suggestion.CategoryName,
                SuggestedCategoryReason = suggestion.Reason,
                SuggestedAccountingNature = suggestion.AccountingNature,
                SuggestedOperationalDestination = suggestion.OperationalDestination,
                SuggestedConfidence = suggestion.Confidence,
                SuggestedHasConflict = suggestion.HasConflict
            };
    }

    public async Task<byte[]?> GetOriginalXmlAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return await dbContext.ElectronicDocuments
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => x.OriginalXml)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ElectronicDocumentCategoryResponse>> GetCategoriesAsync(
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        await EnsureDefaultCategoriesAsync(cancellationToken);

        var query = dbContext.ElectronicDocumentCategories.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query
            .OrderBy(x => x.AccountingNature)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new ElectronicDocumentCategoryResponse(
                x.Id,
                x.Name,
                x.AccountingNature,
                x.SortOrder,
                x.IsSystemDefault,
                x.AllowsAutomaticSuggestion,
                x.IsActive,
                x.SystemKey,
                x.DefaultOperationalDestination))
            .ToListAsync(cancellationToken);
    }

    public async Task<ElectronicDocumentCategoryResponse> CreateCategoryAsync(
        ElectronicDocumentCategoryUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var category = new ElectronicDocumentCategory
        {
            Name = NormalizeCategoryName(request.Name),
            AccountingNature = request.AccountingNature,
            DefaultOperationalDestination = request.DefaultOperationalDestination,
            IsDefaultDestinationInitialized = true,
            SortOrder = request.SortOrder,
            IsSystemDefault = false,
            AllowsAutomaticSuggestion = request.AllowsAutomaticSuggestion,
            IsActive = request.IsActive,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.ElectronicDocumentCategories.Add(category);
        await SaveCategoryChangesAsync(cancellationToken);

        return MapCategory(category);
    }

    public async Task<ElectronicDocumentCategoryResponse?> UpdateCategoryAsync(
        int id,
        ElectronicDocumentCategoryUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var category = await dbContext.ElectronicDocumentCategories
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (category is null)
        {
            return null;
        }

        category.Name = NormalizeCategoryName(request.Name);
        category.AccountingNature = request.AccountingNature;
        category.DefaultOperationalDestination = request.DefaultOperationalDestination;
        category.IsDefaultDestinationInitialized = true;
        category.SortOrder = request.SortOrder;
        category.AllowsAutomaticSuggestion = request.AllowsAutomaticSuggestion;
        category.IsActive = request.IsActive;
        category.UpdatedAtUtc = DateTime.UtcNow;

        await SaveCategoryChangesAsync(cancellationToken);

        return MapCategory(category);
    }

    public async Task<ElectronicDocumentDetailResponse?> ClassifyAsync(
        int id,
        ElectronicDocumentClassifyRequest request,
        string userId,
        CancellationToken cancellationToken)
    {
        await EnsureActiveUserAsync(userId, cancellationToken);
        await EnsureDefaultCategoriesAsync(cancellationToken);

        var categoryIds = request.LineClassifications
            .Select(x => x.CategoryId)
            .Append(request.CategoryId)
            .Distinct()
            .ToArray();

        var categories = await dbContext.ElectronicDocumentCategories
            .Where(x => categoryIds.Contains(x.Id) && x.IsActive)
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        if (!categories.TryGetValue(request.CategoryId, out var documentCategory))
        {
            throw new InvalidOperationException(
                "La categoría seleccionada no existe o está inactiva.");
        }

        if (categories.Count != categoryIds.Length)
        {
            throw new InvalidOperationException(
                "Una de las categorías seleccionadas para las líneas no existe o está inactiva.");
        }

        var document = await dbContext.ElectronicDocuments
            .Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (document is null)
        {
            return null;
        }

        var lineIds = document.Lines.Select(x => x.Id).ToHashSet();
        if (request.LineClassifications.Any(x => !lineIds.Contains(x.LineId)))
        {
            throw new InvalidOperationException(
                "Una de las líneas seleccionadas no pertenece al documento.");
        }

        var now = DateTime.UtcNow;
        document.CategoryId = request.CategoryId;
        document.OperationalDestination =
            request.ProcessingStatus == ElectronicDocumentProcessingStatus.NoActionRequired
                ? OperationalDestination.NoAction
                : request.OperationalDestination;
        document.ProcessingStatus = request.ProcessingStatus;
        document.ProcessedByUserId = userId;
        document.ProcessedAtUtc = now;

        foreach (var line in document.Lines)
        {
            line.ClassificationCategoryId = null;
            line.OperationalDestination = null;
        }

        if (request.ProcessingStatus == ElectronicDocumentProcessingStatus.Processed &&
            request.SeparateByLine)
        {
            foreach (var lineRequest in request.LineClassifications)
            {
                var line = document.Lines.Single(x => x.Id == lineRequest.LineId);
                line.ClassificationCategoryId = lineRequest.CategoryId;
                line.OperationalDestination = lineRequest.OperationalDestination;
            }
        }

        await LearnClassificationAsync(
            document,
            documentCategory,
            request,
            now,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<ElectronicDocumentLearningRuleResponse>> GetLearningRulesAsync(
        CancellationToken cancellationToken)
    {
        var issuerRules = await dbContext.ElectronicDocumentClassificationRules
            .AsNoTracking()
            .OrderBy(x => x.IssuerIdentification)
            .Select(x => new ElectronicDocumentLearningRuleResponse(
                x.Id,
                ElectronicDocumentLearningRuleTypes.Issuer,
                x.IssuerIdentification,
                null,
                null,
                x.CategoryId,
                x.Category.Name,
                null,
                1,
                x.CreatedAtUtc,
                x.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        var cabysRules = await dbContext.ElectronicDocumentCabysClassificationRules
            .AsNoTracking()
            .OrderBy(x => x.IssuerIdentification)
            .ThenBy(x => x.CabysCode ?? x.CabysCategory4Code)
            .Select(x => new ElectronicDocumentLearningRuleResponse(
                x.Id,
                x.CabysCode != null
                    ? ElectronicDocumentLearningRuleTypes.ExactCabys
                    : ElectronicDocumentLearningRuleTypes.CabysBranch,
                x.IssuerIdentification,
                x.CabysCode,
                x.CabysCategory4Code,
                x.CategoryId,
                x.Category.Name,
                x.OperationalDestination,
                x.ConfirmationCount,
                x.CreatedAtUtc,
                x.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return issuerRules
            .Concat(cabysRules)
            .OrderBy(x => x.IssuerIdentification)
            .ThenBy(x => x.RuleType)
            .ThenBy(x => x.CabysCode ?? x.CabysCategory4Code)
            .ToList();
    }

    public async Task<ElectronicDocumentLearningRuleResponse?> UpdateLearningRuleAsync(
        ElectronicDocumentLearningRuleUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var category = await dbContext.ElectronicDocumentCategories
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == request.CategoryId && x.IsActive,
                cancellationToken);

        if (category is null)
        {
            throw new InvalidOperationException(
                "La categoría seleccionada no existe o está inactiva.");
        }

        var now = DateTime.UtcNow;

        if (request.RuleType == ElectronicDocumentLearningRuleTypes.Issuer)
        {
            var rule = await dbContext.ElectronicDocumentClassificationRules
                .Include(x => x.Category)
                .SingleOrDefaultAsync(x => x.Id == request.RuleId, cancellationToken);

            if (rule is null)
            {
                return null;
            }

            rule.CategoryId = request.CategoryId;
            rule.UpdatedAtUtc = now;
            await dbContext.SaveChangesAsync(cancellationToken);

            return new ElectronicDocumentLearningRuleResponse(
                rule.Id,
                ElectronicDocumentLearningRuleTypes.Issuer,
                rule.IssuerIdentification,
                null,
                null,
                rule.CategoryId,
                category.Name,
                null,
                1,
                rule.CreatedAtUtc,
                rule.UpdatedAtUtc);
        }

        var cabysRule = await dbContext.ElectronicDocumentCabysClassificationRules
            .Include(x => x.Category)
            .SingleOrDefaultAsync(x => x.Id == request.RuleId, cancellationToken);

        if (cabysRule is null)
        {
            return null;
        }

        if (!request.OperationalDestination.HasValue)
        {
            throw new InvalidOperationException(
                "Debe seleccionar un destino operativo para la regla CAByS.");
        }

        cabysRule.CategoryId = request.CategoryId;
        cabysRule.OperationalDestination = request.OperationalDestination.Value;
        cabysRule.ConfirmationCount = 1;
        cabysRule.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ElectronicDocumentLearningRuleResponse(
            cabysRule.Id,
            cabysRule.CabysCode is not null
                ? ElectronicDocumentLearningRuleTypes.ExactCabys
                : ElectronicDocumentLearningRuleTypes.CabysBranch,
            cabysRule.IssuerIdentification,
            cabysRule.CabysCode,
            cabysRule.CabysCategory4Code,
            cabysRule.CategoryId,
            category.Name,
            cabysRule.OperationalDestination,
            cabysRule.ConfirmationCount,
            cabysRule.CreatedAtUtc,
            cabysRule.UpdatedAtUtc);
    }

    public async Task<bool> DeleteLearningRuleAsync(
        string ruleType,
        int ruleId,
        CancellationToken cancellationToken)
    {
        if (ruleType == ElectronicDocumentLearningRuleTypes.Issuer)
        {
            var rule = await dbContext.ElectronicDocumentClassificationRules
                .SingleOrDefaultAsync(x => x.Id == ruleId, cancellationToken);
            if (rule is null)
            {
                return false;
            }

            dbContext.ElectronicDocumentClassificationRules.Remove(rule);
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }

        if (ruleType is not ElectronicDocumentLearningRuleTypes.ExactCabys
            and not ElectronicDocumentLearningRuleTypes.CabysBranch)
        {
            throw new InvalidOperationException("El tipo de regla aprendida no es válido.");
        }

        var cabysRule = await dbContext.ElectronicDocumentCabysClassificationRules
            .SingleOrDefaultAsync(x => x.Id == ruleId, cancellationToken);
        if (cabysRule is null)
        {
            return false;
        }

        dbContext.ElectronicDocumentCabysClassificationRules.Remove(cabysRule);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task EnsureDefaultCategoriesAsync(
        CancellationToken cancellationToken)
    {
        var categories = await dbContext.ElectronicDocumentCategories
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var changed = false;

        foreach (var definition in ElectronicDocumentCategoryDefaults.All)
        {
            var category = categories.FirstOrDefault(x =>
                string.Equals(
                    x.SystemKey,
                    definition.SystemKey,
                    StringComparison.OrdinalIgnoreCase));

            if (category is null)
            {
                category = categories.FirstOrDefault(x =>
                    x.SystemKey is null &&
                    string.Equals(
                        x.Name,
                        definition.Name,
                        StringComparison.OrdinalIgnoreCase));
            }

            if (category is null)
            {
                category = new ElectronicDocumentCategory
                {
                    Name = definition.Name,
                    SystemKey = definition.SystemKey,
                    AccountingNature = definition.AccountingNature,
                    DefaultOperationalDestination = definition.DefaultOperationalDestination,
                    IsDefaultDestinationInitialized = true,
                    SortOrder = definition.SortOrder,
                    IsSystemDefault = true,
                    AllowsAutomaticSuggestion = definition.AllowsAutomaticSuggestion,
                    IsActive = true,
                    CreatedAtUtc = now
                };

                dbContext.ElectronicDocumentCategories.Add(category);
                categories.Add(category);
                changed = true;
                continue;
            }

            if (category.SystemKey is null)
            {
                category.SystemKey = definition.SystemKey;
                category.AccountingNature = definition.AccountingNature;
                category.SortOrder = definition.SortOrder;
                category.IsSystemDefault = true;
                category.AllowsAutomaticSuggestion = definition.AllowsAutomaticSuggestion;
                category.UpdatedAtUtc = now;
                changed = true;
            }

            // Initialize the new default-destination field exactly once for categories
            // created by earlier blocks. After that, Administrator/Accountant edits are respected.
            if (!category.IsDefaultDestinationInitialized)
            {
                category.DefaultOperationalDestination = definition.DefaultOperationalDestination;
                category.IsDefaultDestinationInitialized = true;
                category.UpdatedAtUtc = now;
                changed = true;
            }
        }

        if (changed)
        {
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception)
                when (exception.InnerException is SqlException { Number: 2601 or 2627 })
            {
                dbContext.ChangeTracker.Clear();
            }
        }
    }

    private async Task EnsureActiveUserAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        var active = !string.IsNullOrWhiteSpace(userId) &&
            await dbContext.Users
                .AsNoTracking()
                .AnyAsync(x => x.Id == userId && x.IsActive, cancellationToken);

        if (!active)
        {
            throw new InvalidOperationException(
                "The authenticated user does not exist or is inactive.");
        }
    }

    private async Task SaveCategoryChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new InvalidOperationException(
                "Ya existe una categoría de documentos recibidos con ese nombre o identificador interno.",
                exception);
        }
    }

    private async Task LearnClassificationAsync(
        ElectronicDocument document,
        ElectronicDocumentCategory documentCategory,
        ElectronicDocumentClassifyRequest request,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var issuerRule = await dbContext.ElectronicDocumentClassificationRules
            .SingleOrDefaultAsync(
                x => x.IssuerIdentificationType == document.IssuerIdentificationType &&
                     x.IssuerIdentification == document.IssuerIdentification,
                cancellationToken);

        if (issuerRule is null)
        {
            dbContext.ElectronicDocumentClassificationRules.Add(
                new ElectronicDocumentClassificationRule
                {
                    IssuerIdentificationType = document.IssuerIdentificationType,
                    IssuerIdentification = document.IssuerIdentification,
                    CategoryId = documentCategory.Id,
                    CreatedAtUtc = now
                });
        }
        else
        {
            issuerRule.CategoryId = documentCategory.Id;
            issuerRule.UpdatedAtUtc = now;
        }

        if (request.ProcessingStatus != ElectronicDocumentProcessingStatus.Processed ||
            !request.OperationalDestination.HasValue)
        {
            return;
        }

        var cabysCodes = document.Lines
            .Select(x => x.CabysCode)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToArray();

        var branchByCode = await dbContext.CabysItems
            .AsNoTracking()
            .Where(x => x.Catalog.IsCurrent && cabysCodes.Contains(x.Code))
            .Select(x => new { x.Code, x.Category4Code })
            .ToDictionaryAsync(x => x.Code, x => x.Category4Code, cancellationToken);

        var lineOverrides = request.LineClassifications
            .ToDictionary(x => x.LineId);

        var learnedPairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in document.Lines)
        {
            var categoryId = request.CategoryId;
            var destination = request.OperationalDestination.Value;

            if (request.SeparateByLine && lineOverrides.TryGetValue(line.Id, out var lineOverride))
            {
                categoryId = lineOverride.CategoryId;
                destination = lineOverride.OperationalDestination;
            }

            if (string.IsNullOrWhiteSpace(line.CabysCode))
            {
                continue;
            }

            await UpsertCabysRuleAsync(
                BuildExactRuleKey(document, line.CabysCode),
                document,
                line.CabysCode,
                null,
                categoryId,
                destination,
                document.Id,
                now,
                cancellationToken);

            if (!branchByCode.TryGetValue(line.CabysCode, out var branch) ||
                string.IsNullOrWhiteSpace(branch))
            {
                continue;
            }

            var branchPairKey = $"{branch}|{categoryId}|{destination}";
            if (!learnedPairs.Add(branchPairKey))
            {
                continue;
            }

            await UpsertCabysRuleAsync(
                BuildBranchRuleKey(document, branch),
                document,
                null,
                branch,
                categoryId,
                destination,
                document.Id,
                now,
                cancellationToken);
        }
    }

    private async Task UpsertCabysRuleAsync(
        string ruleKey,
        ElectronicDocument document,
        string? cabysCode,
        string? cabysCategory4Code,
        int categoryId,
        OperationalDestination destination,
        int documentId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var rule = await dbContext.ElectronicDocumentCabysClassificationRules
            .SingleOrDefaultAsync(x => x.RuleKey == ruleKey, cancellationToken);

        if (rule is null)
        {
            dbContext.ElectronicDocumentCabysClassificationRules.Add(
                new ElectronicDocumentCabysClassificationRule
                {
                    RuleKey = ruleKey,
                    IssuerIdentificationType = document.IssuerIdentificationType,
                    IssuerIdentification = document.IssuerIdentification,
                    CabysCode = cabysCode,
                    CabysCategory4Code = cabysCategory4Code,
                    CategoryId = categoryId,
                    OperationalDestination = destination,
                    ConfirmationCount = 1,
                    LastConfirmedElectronicDocumentId = documentId,
                    CreatedAtUtc = now
                });
            return;
        }

        if (rule.CategoryId != categoryId || rule.OperationalDestination != destination)
        {
            rule.CategoryId = categoryId;
            rule.OperationalDestination = destination;
            rule.ConfirmationCount = 1;
        }
        else if (rule.LastConfirmedElectronicDocumentId != documentId)
        {
            rule.ConfirmationCount++;
        }

        rule.LastConfirmedElectronicDocumentId = documentId;
        rule.UpdatedAtUtc = now;
    }

    private async Task<ClassificationSuggestion?> GetClassificationSuggestionAsync(
        ElectronicDocumentDetailResponse document,
        IReadOnlyList<CabysLookup> cabysItems,
        CancellationToken cancellationToken)
    {
        var categories = await dbContext.ElectronicDocumentCategories
            .AsNoTracking()
            .Where(x => x.IsActive && x.AllowsAutomaticSuggestion)
            .ToListAsync(cancellationToken);

        var categoriesById = categories.ToDictionary(x => x.Id);
        var categoriesByKey = categories
            .Where(x => !string.IsNullOrWhiteSpace(x.SystemKey))
            .ToDictionary(x => x.SystemKey!, StringComparer.OrdinalIgnoreCase);

        var rules = await dbContext.ElectronicDocumentCabysClassificationRules
            .AsNoTracking()
            .Where(x =>
                x.IssuerIdentificationType == document.IssuerIdentificationType &&
                x.IssuerIdentification == document.IssuerIdentification &&
                x.Category.IsActive &&
                x.Category.AllowsAutomaticSuggestion)
            .ToListAsync(cancellationToken);

        var cabysByCode = cabysItems.ToDictionary(x => x.Code);
        var candidates = new List<SuggestionCandidate>();

        foreach (var line in document.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.CabysCode))
            {
                continue;
            }

            var exactRule = rules.FirstOrDefault(x =>
                string.Equals(x.CabysCode, line.CabysCode, StringComparison.Ordinal));

            if (exactRule is not null && categoriesById.TryGetValue(exactRule.CategoryId, out var exactCategory))
            {
                candidates.Add(new SuggestionCandidate(
                    exactCategory,
                    exactRule.OperationalDestination,
                    100,
                    "CAByS exacto aprendido anteriormente para este proveedor."));
                continue;
            }

            if (!cabysByCode.TryGetValue(line.CabysCode, out var cabys))
            {
                continue;
            }

            var systemKey = GetSystemCategoryKey(cabys.Category4Code);
            if (systemKey is not null && categoriesByKey.TryGetValue(systemKey, out var systemCategory))
            {
                candidates.Add(new SuggestionCandidate(
                    systemCategory,
                    systemCategory.DefaultOperationalDestination,
                    80,
                    GetSystemSuggestionReason(systemKey)));
                continue;
            }

            var branchRule = rules.FirstOrDefault(x =>
                x.ConfirmationCount >= 2 &&
                string.Equals(x.CabysCategory4Code, cabys.Category4Code, StringComparison.Ordinal));

            if (branchRule is not null && categoriesById.TryGetValue(branchRule.CategoryId, out var branchCategory))
            {
                candidates.Add(new SuggestionCandidate(
                    branchCategory,
                    branchRule.OperationalDestination,
                    70,
                    "Rama CAByS confirmada anteriormente para este proveedor."));
            }
        }

        if (candidates.Count > 0)
        {
            var groups = candidates
                .GroupBy(x => new { x.Category.Id, x.OperationalDestination })
                .Select(group => new
                {
                    Items = group.ToList(),
                    Count = group.Count(),
                    MaxPriority = group.Max(x => x.Priority)
                })
                .OrderByDescending(x => x.MaxPriority)
                .ThenByDescending(x => x.Count)
                .ToList();

            var selected = groups[0].Items
                .OrderByDescending(x => x.Priority)
                .First();
            var hasConflict = groups.Count > 1;

            var confidence = hasConflict
                ? ClassificationConfidence.Low
                : selected.Priority >= 80
                    ? ClassificationConfidence.High
                    : ClassificationConfidence.Medium;

            var reason = hasConflict
                ? $"{selected.Reason} Hay señales diferentes entre las líneas; revise la clasificación o separe por línea."
                : selected.Reason;

            return new ClassificationSuggestion(
                selected.Category.Id,
                selected.Category.Name,
                selected.Category.AccountingNature,
                selected.OperationalDestination ?? selected.Category.DefaultOperationalDestination,
                confidence,
                hasConflict,
                reason);
        }

        var issuerRule = await dbContext.ElectronicDocumentClassificationRules
            .AsNoTracking()
            .Where(x =>
                x.IssuerIdentificationType == document.IssuerIdentificationType &&
                x.IssuerIdentification == document.IssuerIdentification &&
                x.Category.IsActive &&
                x.Category.AllowsAutomaticSuggestion)
            .Select(x => new
            {
                x.Category.Id,
                x.Category.Name,
                x.Category.AccountingNature,
                x.Category.DefaultOperationalDestination
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (issuerRule is not null)
        {
            return new ClassificationSuggestion(
                issuerRule.Id,
                issuerRule.Name,
                issuerRule.AccountingNature,
                issuerRule.DefaultOperationalDestination,
                ClassificationConfidence.Low,
                false,
                "Proveedor clasificado anteriormente. Revise la sugerencia antes de confirmar.");
        }

        if (document.IssuerEconomicActivityCode.StartsWith("4730", StringComparison.Ordinal) &&
            categoriesByKey.TryGetValue("expense.fuel", out var fuelCategory))
        {
            return new ClassificationSuggestion(
                fuelCategory.Id,
                fuelCategory.Name,
                fuelCategory.AccountingNature,
                fuelCategory.DefaultOperationalDestination,
                ClassificationConfidence.Medium,
                false,
                "Actividad económica del emisor compatible con venta de combustible.");
        }

        return null;
    }

    private static string? GetSystemCategoryKey(string? category4Code) =>
        category4Code switch
        {
            "3331" or "3332" or "3334" or "3336" or "3337" => "expense.fuel",
            "6511" or "6512" or "6513" or "6521" or "6522" or "6531" or "6532" or "6791" => "expense.freight",
            "6911" or "6912" or "6921" or "6922" or "6923" => "expense.utilities",
            "8411" or "8412" or "8413" or "8414" or "8415" or "8419" or "8421" or "8422" or "8429" => "expense.telecommunications",
            "8711" or "8712" or "8713" or "8714" or "8715" or "8729" => "expense.maintenance",
            "8361" or "8362" or "8363" => "expense.marketing",
            "8211" or "8212" or "8213" or "8219" or "8221" or "8222" or "8231" or
            "8311" or "8312" or "8313" or "8314" or "8315" or "8316" or "8319" or
            "8321" or "8322" or "8323" or "8331" or "8332" or "8333" or
            "8341" or "8342" or "8343" or "8344" or "8391" or "8392" or "8393" or "8394" or "8395" or "8399" => "expense.professional",
            "7111" or "7112" or "7113" or "7114" or "7120" or
            "7151" or "7152" or "7153" or "7154" or "7155" or "7159" or "7170" => "expense.banking",
            "7211" or "7311" or "7312" or "7321" or "7322" or "7323" or "7324" or "7325" or "7326" or "7327" or "7329" => "expense.rent",
            "7131" or "7132" or "7133" or "7141" or "7142" or "7143" or "7161" or "7162" or "7163" or "7164" or "7169" => "expense.insurance",
            _ => null
        };

    private static string GetSystemSuggestionReason(string systemKey) =>
        systemKey switch
        {
            "expense.fuel" => "CAByS identificado como combustible.",
            "expense.freight" => "CAByS identificado como transporte o flete general.",
            "expense.utilities" => "CAByS identificado como servicio público.",
            "expense.telecommunications" => "CAByS identificado como telecomunicaciones o Internet.",
            "expense.maintenance" => "CAByS identificado como mantenimiento o reparación.",
            "expense.marketing" => "CAByS identificado como publicidad o mercadeo.",
            "expense.professional" => "CAByS identificado como servicio profesional o técnico.",
            "expense.banking" => "CAByS identificado como servicio bancario o comisión.",
            "expense.rent" => "CAByS identificado como alquiler o arrendamiento.",
            "expense.insurance" => "CAByS identificado como seguro.",
            _ => "Sugerencia basada en el CAByS."
        };

    private static string BuildExactRuleKey(ElectronicDocument document, string cabysCode) =>
        $"{document.IssuerIdentificationType}:{document.IssuerIdentification}:exact:{cabysCode}";

    private static string BuildBranchRuleKey(ElectronicDocument document, string category4Code) =>
        $"{document.IssuerIdentificationType}:{document.IssuerIdentification}:branch:{category4Code}";

    private static ElectronicDocumentCategoryResponse MapCategory(
        ElectronicDocumentCategory category) =>
        new(
            category.Id,
            category.Name,
            category.AccountingNature,
            category.SortOrder,
            category.IsSystemDefault,
            category.AllowsAutomaticSuggestion,
            category.IsActive,
            category.SystemKey,
            category.DefaultOperationalDestination);

    private static string NormalizeCategoryName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "La categoría es obligatoria.",
                nameof(name));
        }

        return name.Trim();
    }

    private sealed class AccountingAccumulator
    {
        public decimal Purchases { get; private set; }
        public decimal Expenses { get; private set; }
        public decimal DirectCosts { get; private set; }
        public decimal Assets { get; private set; }
        public decimal Unallocated { get; set; }

        public void Add(AccountingNature nature, decimal amount)
        {
            switch (nature)
            {
                case AccountingNature.Inventory:
                    Purchases += amount;
                    break;
                case AccountingNature.OperatingExpense:
                    Expenses += amount;
                    break;
                case AccountingNature.DirectCost:
                    DirectCosts += amount;
                    break;
                case AccountingNature.FixedAsset:
                    Assets += amount;
                    break;
                default:
                    Unallocated += amount;
                    break;
            }
        }
    }

    private sealed record CabysLookup(
        string Code,
        string Description,
        string TaxReference,
        string Category1Description,
        string Category2Description,
        string Category3Description,
        string Category4Code,
        string Category4Description,
        string Category5Description,
        string Category6Description,
        string Category7Description,
        string Category8Description);

    private sealed record SuggestionCandidate(
        ElectronicDocumentCategory Category,
        OperationalDestination? OperationalDestination,
        int Priority,
        string Reason);

    private sealed record ClassificationSuggestion(
        int CategoryId,
        string CategoryName,
        AccountingNature AccountingNature,
        OperationalDestination? OperationalDestination,
        ClassificationConfidence Confidence,
        bool HasConflict,
        string Reason);
}
