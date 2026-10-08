using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Services.BankVouchers;
using Revestik.Shared.Expenses;

namespace Revestik.Api.Tests.Expenses;

public sealed class BankVoucherIngestionServiceTests
{
    [Fact]
    public async Task ImportAsync_ValidVoucher_PersistsAsNeedsReview()
    {
        await using var dbContext = CreateDb();
        var service = CreateService(dbContext);

        var result = await service.ImportAsync(
            "gmail-message-1",
            "Voucher Digital",
            [ValidVoucherBody],
            CancellationToken.None);

        Assert.Equal(BankVoucherImportOutcome.Imported, result);

        var voucher = Assert.Single(dbContext.BankVouchers);
        Assert.Equal("IKAMI HOME", voucher.MerchantName);
        Assert.Equal(7243.47m, voucher.Amount);
        Assert.Equal("CRC", voucher.Currency);
        Assert.Equal("gmail-message-1", voucher.GmailMessageId);
        Assert.Equal(BankVoucherStatus.NeedsReview, voucher.Status);
        Assert.Null(voucher.MatchedElectronicDocumentId);
    }

    [Fact]
    public async Task ImportAsync_SameGmailMessageId_ReturnsDuplicate()
    {
        await using var dbContext = CreateDb();
        var service = CreateService(dbContext);

        var first = await service.ImportAsync(
            "gmail-message-1",
            "Voucher Digital",
            [ValidVoucherBody],
            CancellationToken.None);

        var second = await service.ImportAsync(
            "gmail-message-1",
            "Voucher Digital",
            [ValidVoucherBody],
            CancellationToken.None);

        Assert.Equal(BankVoucherImportOutcome.Imported, first);
        Assert.Equal(BankVoucherImportOutcome.Duplicate, second);
        Assert.Single(dbContext.BankVouchers);
    }

    [Fact]
    public async Task ImportAsync_UnrecognizedMessage_DoesNotPersist()
    {
        await using var dbContext = CreateDb();
        var service = CreateService(dbContext);

        var result = await service.ImportAsync(
            "gmail-message-2",
            "Correo normal",
            ["Este correo no contiene un voucher bancario."],
            CancellationToken.None);

        Assert.Equal(BankVoucherImportOutcome.Unrecognized, result);
        Assert.Empty(dbContext.BankVouchers);
    }

    private static BankVoucherIngestionService CreateService(
        RevestikDbContext dbContext) =>
        new(
            dbContext,
            new BancoNacionalBankVoucherEmailParser());

    private static RevestikDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<RevestikDbContext>()
            .UseInMemoryDatabase(
                $"BankVoucherIngestionTests-{Guid.NewGuid()}")
            .Options);

    private const string ValidVoucherBody = """
        Voucher Digital
        Comercio: IKAMI HOME
        Fecha de transacción: 18/09/2026 11:29 a.m.
        Tarjeta: VISA ****8812
        Autorización: 284668
        Referencia: 626117158669
        Total: CRC 7,243.47
        """;
}
