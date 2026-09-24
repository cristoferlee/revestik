using System.Data;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;

namespace Revestik.Api.Services.Sales;

public sealed class SqlSaleNumberGenerator(
    RevestikDbContext dbContext)
    : ISaleNumberGenerator
{
    public async Task<string> GenerateAsync(
        CancellationToken cancellationToken)
    {
        var connection =
            dbContext.Database.GetDbConnection();

        var shouldCloseConnection =
            connection.State != ConnectionState.Open;

        if (shouldCloseConnection)
        {
            await connection.OpenAsync(
                cancellationToken);
        }

        try
        {
            await using var command =
                connection.CreateCommand();

            command.CommandText =
                "SELECT NEXT VALUE FOR dbo.SaleNumberSequence;";

            var result =
                await command.ExecuteScalarAsync(
                    cancellationToken);

            if (result is null ||
                result == DBNull.Value)
            {
                throw new InvalidOperationException(
                    "The sale sequence did not return a value.");
            }

            var sequenceNumber =
                Convert.ToInt64(result);

            return $"VEN-{sequenceNumber:D6}";
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