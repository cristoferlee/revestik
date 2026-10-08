using Revestik.Api.Services.BankVouchers;

namespace Revestik.Api.Tests.Expenses;

public sealed class BancoNacionalBankVoucherEmailParserTests
{
    private readonly BancoNacionalBankVoucherEmailParser parser = new();

    [Fact]
    public void TryParse_RealBancoNacionalMastercardVoucher_ParsesExpectedValues()
    {
        const string body = """
            Estimado señor(a): 3102959852 SOCIEDAD DE RESPONSABILIDAD LIMIT
            Reciba un cordial saludo de parte del Banco Nacional.

            Por este medio le hacemos llegar el comprobante de Comprarealizada en AGOOGLE YOUTUBE         MOUNTAIN VIEW USA el 4 de Octubre de 2026a las 0:07 p.m.

            AGOOGLE YOUTUBE         MOUNTAIN VIEW USA
            Oct 4, 2026 - 0:07 p.m.
            MASTERCARD************6653
            NRO. AUT:
            655771
            REF:
            627718037615
            TOTAL:
            CRC 8390,00
            """;

        var parsed = parser.TryParse(
            "Voucher Digital",
            body,
            out var voucher);

        Assert.True(parsed);
        Assert.NotNull(voucher);
        Assert.Equal("Banco Nacional", voucher.Bank);
        Assert.Equal(
            "AGOOGLE YOUTUBE MOUNTAIN VIEW USA",
            voucher.MerchantName);
        Assert.Equal(8390.00m, voucher.Amount);
        Assert.Equal("CRC", voucher.Currency);
        Assert.Equal("MASTERCARD", voucher.CardBrand);
        Assert.Equal("6653", voucher.CardLastFour);
        Assert.Equal("655771", voucher.AuthorizationNumber);
        Assert.Equal("627718037615", voucher.ReferenceNumber);
        Assert.Equal(
            new DateTimeOffset(
                2026, 10, 4, 12, 7, 0,
                TimeSpan.FromHours(-6)),
            voucher.TransactionDate);
    }

    [Fact]
    public void TryParse_RealBancoNacionalVisaVoucher_ParsesExpectedValues()
    {
        const string body = """
            ASUPER SANTIAGO           ALAJUELA     CR
            Oct 4, 2026 - 0:08 p.m.
            VISA************0857
            NRO. AUT:
            514976
            REF:
            627712329803
            TOTAL:
            CRC 10585,00
            """;

        var parsed = parser.TryParse(
            "Voucher Digital",
            body,
            out var voucher);

        Assert.True(parsed);
        Assert.NotNull(voucher);
        Assert.Equal(
            "ASUPER SANTIAGO ALAJUELA CR",
            voucher.MerchantName);
        Assert.Equal(10585.00m, voucher.Amount);
        Assert.Equal("CRC", voucher.Currency);
        Assert.Equal("VISA", voucher.CardBrand);
        Assert.Equal("0857", voucher.CardLastFour);
        Assert.Equal("514976", voucher.AuthorizationNumber);
        Assert.Equal("627712329803", voucher.ReferenceNumber);
        Assert.Equal(
            new DateTimeOffset(
                2026, 10, 4, 12, 8, 0,
                TimeSpan.FromHours(-6)),
            voucher.TransactionDate);
    }

    [Fact]
    public void TryParse_LabeledPlainTextVoucher_RemainsSupported()
    {
        const string body = """
            Comercio: IKAMI HOME
            Fecha de transacción: 18/09/2026 11:29 AM
            Tarjeta: VISA ****8812
            Autorización: 284668
            Referencia: 626117158669
            Total: CRC 7,243.47
            """;

        var parsed = parser.TryParse(
            "Voucher Digital",
            body,
            out var voucher);

        Assert.True(parsed);
        Assert.NotNull(voucher);
        Assert.Equal("IKAMI HOME", voucher.MerchantName);
        Assert.Equal(7243.47m, voucher.Amount);
        Assert.Equal("CRC", voucher.Currency);
        Assert.Equal("VISA", voucher.CardBrand);
        Assert.Equal("8812", voucher.CardLastFour);
        Assert.Equal("284668", voucher.AuthorizationNumber);
        Assert.Equal("626117158669", voucher.ReferenceNumber);
    }

    [Fact]
    public void TryParse_HtmlLabeledVoucher_RemainsSupported()
    {
        const string body = """
            <table>
              <tr><td>Comercio</td><td>IKAMI HOME</td></tr>
              <tr><td>Fecha</td><td>18/09/2026 11:29 AM</td></tr>
              <tr><td>Tarjeta</td><td>VISA ****8812</td></tr>
              <tr><td>Autorización</td><td>284668</td></tr>
              <tr><td>Referencia</td><td>626117158669</td></tr>
              <tr><td>Total</td><td>CRC 7,243.47</td></tr>
            </table>
            """;

        var parsed = parser.TryParse(
            "Voucher Digital",
            body,
            out var voucher);

        Assert.True(parsed);
        Assert.NotNull(voucher);
        Assert.Equal("IKAMI HOME", voucher.MerchantName);
        Assert.Equal(7243.47m, voucher.Amount);
    }

    [Fact]
    public void TryParse_MissingRequiredFields_ReturnsFalse()
    {
        const string body = """
            VISA************0857
            NRO. AUT:
            514976
            """;

        var parsed = parser.TryParse(
            "Voucher Digital",
            body,
            out var voucher);

        Assert.False(parsed);
        Assert.Null(voucher);
    }
}
