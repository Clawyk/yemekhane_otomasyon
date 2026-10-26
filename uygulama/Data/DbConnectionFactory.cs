using MySqlConnector;

namespace YemekhaneApi.Data;

public class DbConnectionFactory
{
    private readonly string _primaryConnectionString;
    private readonly string _replicaConnectionString;
    private readonly ILogger<DbConnectionFactory> _logger;

    public DbConnectionFactory(IConfiguration configuration, ILogger<DbConnectionFactory> logger)
    {
        _primaryConnectionString = configuration["PRIMARY_CONNECTION_STRING"]
            ?? "Server=mysql-primary;Port=3306;Database=yemekhane;User=root;Password=root_pass_2026;CharSet=utf8mb4;AllowUserVariables=True;";
        _replicaConnectionString = configuration["REPLICA_CONNECTION_STRING"]
            ?? "Server=mysql-replica;Port=3306;Database=yemekhane;User=root;Password=root_pass_2026;CharSet=utf8mb4;AllowUserVariables=True;";
        _logger = logger;
    }

    /// <summary>
    /// Yazma işlemleri (Stored Procedure çağrıları, INSERT/UPDATE) ve
    /// rezervasyon sonrası anlık bakiye okuması için kaynak (mysql-primary) bağlantısı üretir.
    /// </summary>
    public async Task<MySqlConnection> OpenPrimaryConnectionAsync()
    {
        var conn = new MySqlConnection(_primaryConnectionString);
        await conn.OpenAsync();
        return conn;
    }

    /// <summary>
    /// Listeleme, menü sorgulama ve yönetici raporları için salt okunur replika (mysql-replica) bağlantısı üretir.
    /// Replika geçici olarak erişilemezse yüksek erişilebilirlik için kaynağa (primary) yedeklenir.
    /// </summary>
    public async Task<MySqlConnection> OpenReplicaConnectionAsync()
    {
        try
        {
            var conn = new MySqlConnection(_replicaConnectionString);
            await conn.OpenAsync();
            return conn;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[DB-FAILOVER] mysql-replica erişilemedi, okuma işlemi mysql-primary üzerinden yedekleniyor.");
            return await OpenPrimaryConnectionAsync();
        }
    }
}

