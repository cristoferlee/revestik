using System.Data;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;

namespace Revestik.Api.Services.Quotations;

public sealed class SqlQuotationNumberGenerator(
    RevestikDbContext dbContext)
    : IQuotationNumberGenerator
{
    public async Task<string> GenerateAsync(
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();

        var shouldCloseConnection =
            connection.State != ConnectionState.Open;

        if (shouldCloseConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();

            command.CommandText =
                "SELECT NEXT VALUE FOR dbo.QuotationNumberSequence;";

            var result = await command.ExecuteScalarAsync(
                cancellationToken);

            if (result is null ||
                result == DBNull.Value)
            {
                throw new InvalidOperationException(
                    "The quotation sequence did not return a value.");
            }

            var sequenceNumber = Convert.ToInt64(result);

            return $"COT-{sequenceNumber:D6}";
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }
}