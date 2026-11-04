using System.Text.Json;
using StackExchange.Redis;
using YemekhaneApi.Models;

namespace YemekhaneApi.Services;

public class RedisService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    private readonly ILogger<RedisService> _logger;
    private static readonly TimeSpan OturumSuresi = TimeSpan.FromMinutes(30);

    public RedisService(IConfiguration configuration, ILogger<RedisService> logger)
    {
        _logger = logger;
        var redisConn = configuration["REDIS_CONNECTION_STRING"] ?? "redis:6379";
        var options = ConfigurationOptions.Parse(redisConn);
        options.AbortOnConnectFail = false;
        _redis = ConnectionMultiplexer.Connect(options);
        _db = _redis.GetDatabase();
    }

    public static string MenuAnahtariOlustur(int yemekhaneId, string tarih, int ogunId)
        => $"menu:{yemekhaneId}:{tarih}:{ogunId}";

    public static string OturumAnahtariOlustur(string token)
        => $"oturum:{token}";

    // ========================================================================
    // 1. OTURUM YÖNETİMİ (oturum:{token} - 30 dk, her istekte yenilenir)
    // ========================================================================

    public async Task<string> OturumOlusturAsync(KullaniciOturumDto kullanici)
    {
        var token = Guid.NewGuid().ToString("N");
        var key = OturumAnahtariOlustur(token);
        var json = JsonSerializer.Serialize(kullanici);

        await _db.StringSetAsync(key, json, OturumSuresi);
        _logger.LogInformation("[REDIS SESSION CREATE] Oturum oluşturuldu: {Key} (Kullanıcı: {Eposta}, TTL: 1800 sn)", key, kullanici.Eposta);
        return token;
    }

    public async Task<KullaniciOturumDto?> OturumGetirVeYenileAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var key = OturumAnahtariOlustur(token.Trim());
        var value = await _db.StringGetAsync(key);
        if (value.IsNullOrEmpty)
            return null;

        // Her istekte 30 dakikalık süreyi yenile (Sliding Expiration)
        await _db.KeyExpireAsync(key, OturumSuresi);
        return JsonSerializer.Deserialize<KullaniciOturumDto>(value!);
    }

    public async Task OturumGuncelleAsync(string token, KullaniciOturumDto kullanici)
    {
        var key = OturumAnahtariOlustur(token.Trim());
        var json = JsonSerializer.Serialize(kullanici);
        await _db.StringSetAsync(key, json, OturumSuresi);
    }

    public async Task OturumSilAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return;

        var key = OturumAnahtariOlustur(token.Trim());
        await _db.KeyDeleteAsync(key);
        _logger.LogInformation("[REDIS SESSION DELETE] Çıkış yapıldı, oturum silindi: {Key}", key);
    }

    // ========================================================================
    // 2. MENÜ ÖNBELLEĞİ (menu:{yemekhane_id}:{tarih}:{ogun_id} - gün sonuna kadar)
    // ========================================================================

    public async Task<MenuDetayDto?> MenuOnbellektenGetirAsync(int yemekhaneId, string tarih, int ogunId)
    {
        var key = MenuAnahtariOlustur(yemekhaneId, tarih, ogunId);
        var cached = await _db.StringGetAsync(key);

        if (cached.IsNullOrEmpty)
        {
            _logger.LogInformation("[REDIS CACHE MISS] İlk istek: '{Key}' Redis'te bulunamadı, veritabanından (mysql-replica) okunuyor.", key);
            return null;
        }

        _logger.LogInformation("[REDIS CACHE HIT] İstek Redis önbelleğinden karşılandı: '{Key}'", key);
        var dto = JsonSerializer.Deserialize<MenuDetayDto>(cached!);
        if (dto != null)
        {
            dto.OnbellekKaynagi = "REDIS";
        }
        return dto;
    }

    public async Task MenuOnbellegeYazAsync(int yemekhaneId, string tarih, int ogunId, MenuDetayDto menu)
    {
        var key = MenuAnahtariOlustur(yemekhaneId, tarih, ogunId);
        var simdi = DateTime.Now;
        var gunSonu = simdi.Date.AddDays(1).AddSeconds(-1);
        var kalanSure = gunSonu - simdi;
        if (kalanSure.TotalSeconds < 60)
        {
            kalanSure = TimeSpan.FromMinutes(30);
        }

        menu.OnbellekKaynagi = "DB";
        var json = JsonSerializer.Serialize(menu);
        await _db.StringSetAsync(key, json, kalanSure);
        _logger.LogInformation("[REDIS CACHE SET] Menü Redis önbelleğine yazıldı: '{Key}' (TTL: {Ttl} sn, gün sonuna kadar)", key, (int)kalanSure.TotalSeconds);
    }

    public async Task MenuOnbellekSilAsync(int yemekhaneId, string tarih, int ogunId)
    {
        var key = MenuAnahtariOlustur(yemekhaneId, tarih, ogunId);
        var silindi = await _db.KeyDeleteAsync(key);
        _logger.LogInformation("[REDIS CACHE INVALIDATE] Menü güncellendiği için önbellek kaydı silindi: '{Key}' (Silindi: {Silindi})", key, silindi);
    }

    public async Task<int> TumMenuOnbellekleriniTemizleAsync()
    {
        var endpoints = _redis.GetEndPoints();
        int silinenSayisi = 0;

        foreach (var endpoint in endpoints)
        {
            var server = _redis.GetServer(endpoint);
            var keys = server.Keys(pattern: "menu:*").ToArray();
            if (keys.Length > 0)
            {
                silinenSayisi += (int)await _db.KeyDeleteAsync(keys);
            }
        }

        _logger.LogInformation("[REDIS CACHE FLUSH] Eski yapıdaki {Count} adet 'menu:*' önbellek kaydı temizlendi.", silinenSayisi);
        return silinenSayisi;
    }
}

