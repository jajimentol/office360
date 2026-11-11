using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Oracle.ManagedDataAccess.Client;

namespace Ofis360.Infrastructure.Persistence;

/// <summary>
/// Her acilan Oracle baglantisinda CLIENT_IDENTIFIER'i istegin kullanicisina ayarlar; RESERVATION_STATUS_HISTORY
/// trigger'lari aktoru buradan okur. ODP.NET baglanti havuza donerken ClientId'yi kendisi sifirlar (2026-11-08'de ayni
/// SID ile dogrulandi); kullanici yoksa (oturumsuz istek, seed, arka plan) deger yine de acikca bosaltilir.
/// </summary>
public sealed class ClientIdentifierInterceptor(Func<string?> actorId) : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) => Apply(connection);

    public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        Apply(connection);
        return Task.CompletedTask;
    }

    private void Apply(DbConnection connection)
    {
        if (connection is OracleConnection oracle)
        {
            oracle.ClientId = actorId();
        }
    }
}
