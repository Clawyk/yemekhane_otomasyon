using System.Data;
using MySqlConnector;
using YemekhaneApi.Data;
using YemekhaneApi.Models;

namespace YemekhaneApi.Repositories;

public class IsKuraliIstisnasi : Exception
{
    public IsKuraliIstisnasi(string message) : base(message) { }
}

public class YemekhaneRepository
{
    private readonly DbConnectionFactory _dbFactory;
    private readonly ILogger<YemekhaneRepository> _logger;

    public YemekhaneRepository(DbConnectionFactory dbFactory, ILogger<YemekhaneRepository> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    // ========================================================================
    // 1. KULLANICI GİRİŞİ VE PROFİL (Okuma -> Replica, Güncel Bakiye -> Primary/Replica)
    // ========================================================================

    public async Task<(KullaniciOturumDto? Kullanici, string? SifreHash)> KullaniciEpostaIleGetirAsync(string eposta)
    {
        await using var conn = await _dbFactory.OpenReplicaConnectionAsync();
        const string sql = @"
            SELECT id, ad_soyad, eposta, sifre_hash, rol, ogrenci_no, bakiye
            FROM kullanicilar
            WHERE eposta = @eposta
            LIMIT 1;";

        await using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@eposta", eposta.Trim());

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return (null, null);

        var kullanici = new KullaniciOturumDto
        {
            Id = reader.GetInt32("id"),
            AdSoyad = reader.GetString("ad_soyad"),
            Eposta = reader.GetString("eposta"),
            Rol = reader.GetString("rol"),
            OgrenciNo = reader.IsDBNull(reader.GetOrdinal("ogrenci_no")) ? null : reader.GetString("ogrenci_no"),
            Bakiye = reader.GetDecimal("bakiye")
        };
        var sifreHash = reader.GetString("sifre_hash");
        await reader.CloseAsync();

        kullanici.AlerjenIdleri = await KullaniciAlerjenIdleriniGetirInternalAsync(conn, kullanici.Id);
        return (kullanici, sifreHash);
    }

    public async Task<KullaniciOturumDto?> KullaniciIdIleGetirAsync(int kullaniciId, bool kaynaktanOku = false)
    {
        await using var conn = kaynaktanOku
            ? await _dbFactory.OpenPrimaryConnectionAsync()
            : await _dbFactory.OpenReplicaConnectionAsync();

        const string sql = @"
            SELECT id, ad_soyad, eposta, rol, ogrenci_no, bakiye
            FROM kullanicilar
            WHERE id = @id
            LIMIT 1;";

        await using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", kullaniciId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        var kullanici = new KullaniciOturumDto
        {
            Id = reader.GetInt32("id"),
            AdSoyad = reader.GetString("ad_soyad"),
            Eposta = reader.GetString("eposta"),
            Rol = reader.GetString("rol"),
            OgrenciNo = reader.IsDBNull(reader.GetOrdinal("ogrenci_no")) ? null : reader.GetString("ogrenci_no"),
            Bakiye = reader.GetDecimal("bakiye")
        };
        await reader.CloseAsync();

        kullanici.AlerjenIdleri = await KullaniciAlerjenIdleriniGetirInternalAsync(conn, kullanici.Id);
        return kullanici;
    }

    private static async Task<List<int>> KullaniciAlerjenIdleriniGetirInternalAsync(MySqlConnection conn, int kullaniciId)
    {
        var liste = new List<int>();
        try
        {
            const string tabloVarSql = @"
                SELECT COUNT(*) FROM information_schema.TABLES
                WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'kullanici_alerjenleri';";
            await using var chkCmd = new MySqlCommand(tabloVarSql, conn);
            var varMi = Convert.ToInt32(await chkCmd.ExecuteScalarAsync());
            if (varMi == 0)
                return liste;

            const string sql = "SELECT alerjen_id FROM kullanici_alerjenleri WHERE kullanici_id = @uid ORDER BY alerjen_id;";
            await using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@uid", kullaniciId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                liste.Add(reader.GetInt32(0));
            }
        }
        catch
        {
            // Aşama 4 öncesi tablo henüz yoksa boş liste döner
        }
        return liste;
    }

    // ========================================================================
    // 2. REFERANS VERİLER (Yemekhaneler, Öğünler, Yemekler, Alerjenler -> Replica)
    // ========================================================================

    public async Task<object> ReferansVerileriGetirAsync()
    {
        await using var conn = await _dbFactory.OpenReplicaConnectionAsync();

        var yemekhaneler = new List<YemekhaneDto>();
        await using (var cmd = new MySqlCommand("SELECT id, ad, kapasite, konum FROM yemekhaneler WHERE aktif = 1 ORDER BY id;", conn))
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            while (await r.ReadAsync())
            {
                yemekhaneler.Add(new YemekhaneDto
                {
                    Id = r.GetInt32("id"),
                    Ad = r.GetString("ad"),
                    Kapasite = r.GetInt32("kapasite"),
                    Konum = r.GetString("konum")
                });
            }
        }

        var ogunler = new List<OgunDto>();
        await using (var cmd = new MySqlCommand("SELECT id, ad, baslangic_saati, bitis_saati, rezervasyon_son_saat, sira FROM ogunler ORDER BY sira;", conn))
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            while (await r.ReadAsync())
            {
                ogunler.Add(new OgunDto
                {
                    Id = r.GetInt32("id"),
                    Ad = r.GetString("ad"),
                    BaslangicSaati = r.GetTimeSpan("baslangic_saati").ToString(@"hh\:mm"),
                    BitisSaati = r.GetTimeSpan("bitis_saati").ToString(@"hh\:mm"),
                    RezervasyonSonSaat = r.GetTimeSpan("rezervasyon_son_saat").ToString(@"hh\:mm"),
                    Sira = r.GetInt32("sira")
                });
            }
        }

        var yemekler = new List<object>();
        const string yemekSql = @"
            SELECT y.id, y.ad, y.kalori, y.vejetaryen, yk.id AS kategori_id, yk.ad AS kategori_ad, yk.sira
            FROM yemekler y
            INNER JOIN yemek_kategorileri yk ON yk.id = y.kategori_id
            ORDER BY yk.sira, y.ad;";
        await using (var cmd = new MySqlCommand(yemekSql, conn))
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            while (await r.ReadAsync())
            {
                yemekler.Add(new
                {
                    id = r.GetInt32("id"),
                    ad = r.GetString("ad"),
                    kalori = r.GetInt32("kalori"),
                    vejetaryen = r.GetBoolean("vejetaryen"),
                    kategoriId = r.GetInt32("kategori_id"),
                    kategoriAd = r.GetString("kategori_ad")
                });
            }
        }

        var alerjenler = await TumAlerjenleriGetirInternalAsync(conn);

        return new
        {
            yemekhaneler,
            ogunler,
            yemekler,
            alerjenler,
            bugun = DateTime.Today.ToString("yyyy-MM-dd")
        };
    }

    private static async Task<List<AlerjenDto>> TumAlerjenleriGetirInternalAsync(MySqlConnection conn)
    {
        var alerjenler = new List<AlerjenDto>();
        await using var cmd = new MySqlCommand("SELECT id, ad, COALESCE(aciklama, '') AS aciklama FROM alerjenler ORDER BY id;", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            alerjenler.Add(new AlerjenDto
            {
                Id = r.GetInt32("id"),
                Ad = r.GetString("ad"),
                Aciklama = r.GetString("aciklama")
            });
        }
        return alerjenler;
    }

    // ========================================================================
    // 3. GÜNÜN MENÜSÜ VE HAFTALIK MENÜ (v_menu_detay, v_menu_doluluk, v_menu_puan -> Replica)
    // ========================================================================

    public async Task<MenuDetayDto?> TekMenuDetayGetirAsync(int yemekhaneId, string tarih, int ogunId)
    {
        await using var conn = await _dbFactory.OpenReplicaConnectionAsync();
        _logger.LogInformation("[DB READ - REPLICA] v_menu_doluluk, v_menu_puan ve v_menu_detay sorgulanıyor (Yemekhane:{Yh}, Tarih:{Tarih}, Öğün:{Ogun})",
            yemekhaneId, tarih, ogunId);

        const string ustBilgiSql = @"
            SELECT
                d.menu_id,
                d.tarih,
                d.yemekhane_id,
                d.yemekhane_ad,
                d.kapasite,
                d.ogun_id,
                d.ogun_ad,
                d.ogun_sira,
                d.rezervasyon_son_saat,
                d.ogrenci_fiyati,
                d.personel_fiyati,
                d.dolu_sayisi,
                d.bos_yer_sayisi,
                d.doluluk_yuzdesi,
                d.toplam_kalori,
                d.menu_vejetaryen,
                COALESCE(p.ortalama_puan, 0) AS ortalama_puan,
                COALESCE(p.degerlendirme_sayisi, 0) AS degerlendirme_sayisi
            FROM v_menu_doluluk d
            LEFT JOIN v_menu_puan p ON p.menu_id = d.menu_id
            WHERE d.yemekhane_id = @yhId
              AND d.tarih = @tarih
              AND d.ogun_id = @ogunId
            LIMIT 1;";

        MenuDetayDto? menu = null;
        await using (var cmd = new MySqlCommand(ustBilgiSql, conn))
        {
            cmd.Parameters.AddWithValue("@yhId", yemekhaneId);
            cmd.Parameters.AddWithValue("@tarih", tarih);
            cmd.Parameters.AddWithValue("@ogunId", ogunId);

            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                var menuTarih = r.GetDateTime("tarih");
                var sonSaat = r.GetTimeSpan("rezervasyon_son_saat");
                var ogunIdVal = r.GetInt32("ogun_id");
                var ogunAdVal = r.GetString("ogun_ad");

                menu = new MenuDetayDto
                {
                    MenuId = r.GetInt32("menu_id"),
                    Tarih = menuTarih.ToString("yyyy-MM-dd"),
                    YemekhaneId = r.GetInt32("yemekhane_id"),
                    YemekhaneAd = r.GetString("yemekhane_ad"),
                    Kapasite = r.GetInt32("kapasite"),
                    OgunId = ogunIdVal,
                    OgunAd = ogunAdVal,
                    OgunSira = r.GetInt32("ogun_sira"),
                    RezervasyonSonSaat = sonSaat.ToString(@"hh\:mm"),
                    OgrenciFiyati = r.GetDecimal("ogrenci_fiyati"),
                    PersonelFiyati = r.GetDecimal("personel_fiyati"),
                    DoluSayisi = Convert.ToInt32(r["dolu_sayisi"]),
                    BosYerSayisi = Convert.ToInt32(r["bos_yer_sayisi"]),
                    DolulukYuzdesi = Convert.ToDecimal(r["doluluk_yuzdesi"]),
                    ToplamKalori = Convert.ToInt32(r["toplam_kalori"]),
                    MenuVejetaryen = Convert.ToInt32(r["menu_vejetaryen"]) == 1,
                    OrtalamaPuan = Convert.ToDecimal(r["ortalama_puan"]),
                    DegerlendirmeSayisi = Convert.ToInt32(r["degerlendirme_sayisi"]),
                    SonSaatGectiMi = SonSaatGectiMiHesapla(menuTarih, sonSaat),
                    YarimPorsiyonSecilebilirMi = ogunIdVal != 1 && !ogunAdVal.Equals("Kahvaltı", StringComparison.OrdinalIgnoreCase)
                };
            }
        }

        if (menu == null)
            return null;

        const string yemeklerSql = @"
            SELECT
                yemek_id,
                yemek_ad,
                kalori,
                vejetaryen,
                kategori_id,
                kategori_ad,
                kategori_sira,
                alerjenler,
                alerjen_idleri
            FROM v_menu_detay
            WHERE menu_id = @menuId
            ORDER BY kategori_sira, yemek_id;";

        await using (var cmd = new MySqlCommand(yemeklerSql, conn))
        {
            cmd.Parameters.AddWithValue("@menuId", menu.MenuId);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var idStr = r.GetString("alerjen_idleri");
                var alerjenIdList = string.IsNullOrWhiteSpace(idStr)
                    ? new List<int>()
                    : idStr.Split(',', StringSplitOptions.RemoveEmptyEntries)
                           .Select(s => int.TryParse(s.Trim(), out var v) ? v : 0)
                           .Where(v => v > 0)
                           .ToList();

                menu.Yemekler.Add(new YemekDetayDto
                {
                    YemekId = r.GetInt32("yemek_id"),
                    YemekAd = r.GetString("yemek_ad"),
                    Kalori = r.GetInt32("kalori"),
                    Vejetaryen = r.GetBoolean("vejetaryen"),
                    KategoriId = r.GetInt32("kategori_id"),
                    KategoriAd = r.GetString("kategori_ad"),
                    KategoriSira = r.GetInt32("kategori_sira"),
                    Alerjenler = r.GetString("alerjenler"),
                    AlerjenIdleri = alerjenIdList
                });
            }
        }

        return menu;
    }

    public async Task<List<MenuDetayDto>> HaftalikMenuleriGetirAsync(int yemekhaneId, string baslangicTarih, int gunSayisi = 7)
    {
        await using var conn = await _dbFactory.OpenReplicaConnectionAsync();
        if (!DateTime.TryParse(baslangicTarih, out var baslangic))
        {
            baslangic = DateTime.Today;
        }
        var bitis = baslangic.AddDays(gunSayisi - 1);

        const string sql = @"
            SELECT
                d.menu_id,
                d.tarih,
                d.yemekhane_id,
                d.yemekhane_ad,
                d.kapasite,
                d.ogun_id,
                d.ogun_ad,
                d.ogun_sira,
                d.rezervasyon_son_saat,
                d.ogrenci_fiyati,
                d.personel_fiyati,
                d.dolu_sayisi,
                d.bos_yer_sayisi,
                d.doluluk_yuzdesi,
                d.toplam_kalori,
                d.menu_vejetaryen,
                COALESCE(p.ortalama_puan, 0) AS ortalama_puan,
                COALESCE(p.degerlendirme_sayisi, 0) AS degerlendirme_sayisi
            FROM v_menu_doluluk d
            LEFT JOIN v_menu_puan p ON p.menu_id = d.menu_id
            WHERE d.yemekhane_id = @yhId
              AND d.tarih BETWEEN @baslangic AND @bitis
            ORDER BY d.tarih ASC, d.ogun_sira ASC;";

        var menuler = new List<MenuDetayDto>();
        var menuDict = new Dictionary<int, MenuDetayDto>();

        await using (var cmd = new MySqlCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue("@yhId", yemekhaneId);
            cmd.Parameters.AddWithValue("@baslangic", baslangic.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@bitis", bitis.ToString("yyyy-MM-dd"));

            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var menuTarih = r.GetDateTime("tarih");
                var sonSaat = r.GetTimeSpan("rezervasyon_son_saat");
                var ogunIdVal = r.GetInt32("ogun_id");
                var ogunAdVal = r.GetString("ogun_ad");

                var dto = new MenuDetayDto
                {
                    MenuId = r.GetInt32("menu_id"),
                    Tarih = menuTarih.ToString("yyyy-MM-dd"),
                    YemekhaneId = r.GetInt32("yemekhane_id"),
                    YemekhaneAd = r.GetString("yemekhane_ad"),
                    Kapasite = r.GetInt32("kapasite"),
                    OgunId = ogunIdVal,
                    OgunAd = ogunAdVal,
                    OgunSira = r.GetInt32("ogun_sira"),
                    RezervasyonSonSaat = sonSaat.ToString(@"hh\:mm"),
                    OgrenciFiyati = r.GetDecimal("ogrenci_fiyati"),
                    PersonelFiyati = r.GetDecimal("personel_fiyati"),
                    DoluSayisi = Convert.ToInt32(r["dolu_sayisi"]),
                    BosYerSayisi = Convert.ToInt32(r["bos_yer_sayisi"]),
                    DolulukYuzdesi = Convert.ToDecimal(r["doluluk_yuzdesi"]),
                    ToplamKalori = Convert.ToInt32(r["toplam_kalori"]),
                    MenuVejetaryen = Convert.ToInt32(r["menu_vejetaryen"]) == 1,
                    OrtalamaPuan = Convert.ToDecimal(r["ortalama_puan"]),
                    DegerlendirmeSayisi = Convert.ToInt32(r["degerlendirme_sayisi"]),
                    SonSaatGectiMi = SonSaatGectiMiHesapla(menuTarih, sonSaat),
                    YarimPorsiyonSecilebilirMi = ogunIdVal != 1 && !ogunAdVal.Equals("Kahvaltı", StringComparison.OrdinalIgnoreCase)
                };
                menuler.Add(dto);
                menuDict[dto.MenuId] = dto;
            }
        }

        if (menuler.Count == 0)
            return menuler;

        var idListesi = string.Join(",", menuDict.Keys);
        var detaySql = $@"
            SELECT
                menu_id,
                yemek_id,
                yemek_ad,
                kalori,
                vejetaryen,
                kategori_id,
                kategori_ad,
                kategori_sira,
                alerjenler,
                alerjen_idleri
            FROM v_menu_detay
            WHERE menu_id IN ({idListesi})
            ORDER BY menu_id, kategori_sira, yemek_id;";

        await using (var cmd = new MySqlCommand(detaySql, conn))
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            while (await r.ReadAsync())
            {
                var mId = r.GetInt32("menu_id");
                if (!menuDict.TryGetValue(mId, out var mDto))
                    continue;

                var idStr = r.GetString("alerjen_idleri");
                var alerjenIdList = string.IsNullOrWhiteSpace(idStr)
                    ? new List<int>()
                    : idStr.Split(',', StringSplitOptions.RemoveEmptyEntries)
                           .Select(s => int.TryParse(s.Trim(), out var v) ? v : 0)
                           .Where(v => v > 0)
                           .ToList();

                mDto.Yemekler.Add(new YemekDetayDto
                {
                    YemekId = r.GetInt32("yemek_id"),
                    YemekAd = r.GetString("yemek_ad"),
                    Kalori = r.GetInt32("kalori"),
                    Vejetaryen = r.GetBoolean("vejetaryen"),
                    KategoriId = r.GetInt32("kategori_id"),
                    KategoriAd = r.GetString("kategori_ad"),
                    KategoriSira = r.GetInt32("kategori_sira"),
                    Alerjenler = r.GetString("alerjenler"),
                    AlerjenIdleri = alerjenIdList
                });
            }
        }

        return menuler;
    }

    public static bool SonSaatGectiMiHesapla(DateTime menuTarih, TimeSpan sonSaat)
    {
        var bugun = DateTime.Today;
        if (menuTarih.Date < bugun)
            return true;
        if (menuTarih.Date > bugun)
            return false;
        return DateTime.Now.TimeOfDay > sonSaat;
    }

    // ========================================================================
    // 4. REZERVASYON İŞLEMLERİ (Stored Procedure -> Primary, Listeleme -> Replica)
    // ========================================================================

    public async Task<(int RezervasyonId, decimal KesilenUcret, decimal GuncelBakiye, string Porsiyon)> RezervasyonYapAsync(
        int kullaniciId, int menuId, string porsiyon = "tam")
    {
        await using var conn = await _dbFactory.OpenPrimaryConnectionAsync();

        try
        {
            // sp_rezervasyon_yap parametre sayısını kontrol et (Aşama 2/3: 2 parametre, Aşama 4: 3 parametre)
            const string paramSayisiSql = @"
                SELECT COUNT(*) FROM information_schema.PARAMETERS
                WHERE SPECIFIC_SCHEMA = DATABASE()
                  AND SPECIFIC_NAME = 'sp_rezervasyon_yap';";
            await using var paramCmd = new MySqlCommand(paramSayisiSql, conn);
            var paramSayisi = Convert.ToInt32(await paramCmd.ExecuteScalarAsync());

            await using var cmd = new MySqlCommand("sp_rezervasyon_yap", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@p_kullanici_id", kullaniciId);
            cmd.Parameters.AddWithValue("@p_menu_id", menuId);

            var temizPorsiyon = string.IsNullOrWhiteSpace(porsiyon) ? "tam" : porsiyon.Trim().ToLowerInvariant();
            if (paramSayisi >= 3)
            {
                cmd.Parameters.AddWithValue("@p_porsiyon", temizPorsiyon);
            }

            _logger.LogInformation("[DB WRITE - PRIMARY] CALL sp_rezervasyon_yap({KullaniciId}, {MenuId}, {Porsiyon})",
                kullaniciId, menuId, temizPorsiyon);

            int rezId = 0;
            decimal kesilenUcret = 0;
            decimal guncelBakiye = 0;

            await using (var r = await cmd.ExecuteReaderAsync())
            {
                if (await r.ReadAsync())
                {
                    rezId = Convert.ToInt32(r["rezervasyon_id"]);
                    kesilenUcret = Convert.ToDecimal(r["kesilen_ucret"]);
                    guncelBakiye = Convert.ToDecimal(r["guncel_bakiye"]);
                }
            }

            // Kural: Rezervasyondan hemen sonra gösterilen bakiye replikasyon gecikmesinden
            // etkilenmemesi için doğrudan kaynak sunucudan (mysql-primary) okunur.
            await using var bakiyeCmd = new MySqlCommand("SELECT bakiye FROM kullanicilar WHERE id = @id;", conn);
            bakiyeCmd.Parameters.AddWithValue("@id", kullaniciId);
            var kaynakBakiyeObj = await bakiyeCmd.ExecuteScalarAsync();
            if (kaynakBakiyeObj != null && kaynakBakiyeObj != DBNull.Value)
            {
                guncelBakiye = Convert.ToDecimal(kaynakBakiyeObj);
            }

            _logger.LogInformation("[DB READ - PRIMARY] Rezervasyon sonrası güncel bakiye kaynaktan (mysql-primary) okundu: {Bakiye} TL", guncelBakiye);
            return (rezId, kesilenUcret, guncelBakiye, temizPorsiyon);
        }
        catch (MySqlException ex) when (ex.SqlState == "45000")
        {
            _logger.LogWarning("[SP BUSINESS RULE - 45000] sp_rezervasyon_yap hatası: {Message}", ex.Message);
            throw new IsKuraliIstisnasi(ex.Message);
        }
    }

    public async Task<(decimal IadeTutari, decimal GuncelBakiye)> RezervasyonIptalAsync(int rezervasyonId, int kullaniciId)
    {
        await using var conn = await _dbFactory.OpenPrimaryConnectionAsync();

        try
        {
            await using var cmd = new MySqlCommand("sp_rezervasyon_iptal", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@p_rezervasyon_id", rezervasyonId);
            cmd.Parameters.AddWithValue("@p_kullanici_id", kullaniciId);

            _logger.LogInformation("[DB WRITE - PRIMARY] CALL sp_rezervasyon_iptal({RezId}, {KullaniciId})", rezervasyonId, kullaniciId);

            decimal iade = 0;
            decimal guncelBakiye = 0;
            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                iade = Convert.ToDecimal(r["iade_tutari"]);
                guncelBakiye = Convert.ToDecimal(r["guncel_bakiye"]);
            }

            return (iade, guncelBakiye);
        }
        catch (MySqlException ex) when (ex.SqlState == "45000")
        {
            _logger.LogWarning("[SP BUSINESS RULE - 45000] sp_rezervasyon_iptal hatası: {Message}", ex.Message);
            throw new IsKuraliIstisnasi(ex.Message);
        }
    }

    public async Task TurnikeGecisAsync(int rezervasyonId)
    {
        await using var conn = await _dbFactory.OpenPrimaryConnectionAsync();

        try
        {
            await using var cmd = new MySqlCommand("sp_turnike_gecis", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@p_rezervasyon_id", rezervasyonId);

            _logger.LogInformation("[DB WRITE - PRIMARY] CALL sp_turnike_gecis({RezId})", rezervasyonId);
            await cmd.ExecuteNonQueryAsync();
        }
        catch (MySqlException ex) when (ex.SqlState == "45000")
        {
            _logger.LogWarning("[SP BUSINESS RULE - 45000] sp_turnike_gecis hatası: {Message}", ex.Message);
            throw new IsKuraliIstisnasi(ex.Message);
        }
    }

    public async Task<List<RezervasyonKayitDto>> KullaniciRezervasyonlariniGetirAsync(int kullaniciId)
    {
        await using var conn = await _dbFactory.OpenReplicaConnectionAsync();

        // Aşama 4 porsiyon sütunu var mı kontrol et
        const string porsiyonVarSql = @"
            SELECT COUNT(*) FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'rezervasyonlar'
              AND COLUMN_NAME = 'porsiyon';";
        await using var chkCmd = new MySqlCommand(porsiyonVarSql, conn);
        var porsiyonSutunuVar = Convert.ToInt32(await chkCmd.ExecuteScalarAsync()) > 0;
        var porsiyonSecim = porsiyonSutunuVar ? "r.porsiyon" : "'tam' AS porsiyon";

        var sql = $@"
            SELECT
                r.id,
                r.menu_id,
                m.tarih,
                m.yemekhane_id,
                yh.ad AS yemekhane_ad,
                m.ogun_id,
                o.ad AS ogun_ad,
                o.rezervasyon_son_saat,
                r.durum,
                {porsiyonSecim},
                r.ucret,
                r.olusturulma_tarihi,
                d.id AS degerlendirme_id,
                d.puan AS verilen_puan,
                d.yorum AS verilen_yorum,
                COALESCE((
                    SELECT GROUP_CONCAT(y.ad ORDER BY y.kategori_id SEPARATOR ', ')
                    FROM menu_yemekleri my
                    INNER JOIN yemekler y ON y.id = my.yemek_id
                    WHERE my.menu_id = m.id
                ), '') AS yemek_ozeti
            FROM rezervasyonlar r
            INNER JOIN menuler m ON m.id = r.menu_id
            INNER JOIN yemekhaneler yh ON yh.id = m.yemekhane_id
            INNER JOIN ogunler o ON o.id = m.ogun_id
            LEFT JOIN degerlendirmeler d ON d.menu_id = r.menu_id AND d.kullanici_id = r.kullanici_id
            WHERE r.kullanici_id = @uid
            ORDER BY m.tarih DESC, o.sira DESC, r.id DESC;";

        var liste = new List<RezervasyonKayitDto>();
        await using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@uid", kullaniciId);

        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            var menuTarih = r.GetDateTime("tarih");
            var sonSaat = r.GetTimeSpan("rezervasyon_son_saat");
            var durum = r.GetString("durum");
            var sonSaatGecti = SonSaatGectiMiHesapla(menuTarih, sonSaat);

            liste.Add(new RezervasyonKayitDto
            {
                Id = r.GetInt32("id"),
                MenuId = r.GetInt32("menu_id"),
                Tarih = menuTarih.ToString("yyyy-MM-dd"),
                YemekhaneId = r.GetInt32("yemekhane_id"),
                YemekhaneAd = r.GetString("yemekhane_ad"),
                OgunId = r.GetInt32("ogun_id"),
                OgunAd = r.GetString("ogun_ad"),
                RezervasyonSonSaat = sonSaat.ToString(@"hh\:mm"),
                Durum = durum,
                Porsiyon = r.GetString("porsiyon"),
                Ucret = r.GetDecimal("ucret"),
                OlusturulmaTarihi = r.GetDateTime("olusturulma_tarihi").ToString("yyyy-MM-dd HH:mm"),
                IptalEdilebilirMi = durum == "aktif" && !sonSaatGecti,
                DegerlendirildiMi = !r.IsDBNull(r.GetOrdinal("degerlendirme_id")),
                VerilenPuan = r.IsDBNull(r.GetOrdinal("verilen_puan")) ? null : r.GetInt32("verilen_puan"),
                VerilenYorum = r.IsDBNull(r.GetOrdinal("verilen_yorum")) ? null : r.GetString("verilen_yorum"),
                YemekOzeti = r.GetString("yemek_ozeti")
            });
        }

        return liste;
    }

    // ========================================================================
    // 5. BAKİYE VE HAREKET GEÇMİŞİ (sp_bakiye_yukle -> Primary, Liste -> Replica/Primary)
    // ========================================================================

    public async Task<BakiyeSayfaDto> BakiyeVeHareketleriGetirAsync(int kullaniciId, bool kaynaktanOku = false)
    {
        await using var conn = kaynaktanOku
            ? await _dbFactory.OpenPrimaryConnectionAsync()
            : await _dbFactory.OpenReplicaConnectionAsync();

        var sonuc = new BakiyeSayfaDto
        {
            OkumaKaynagi = kaynaktanOku ? "mysql-primary" : "mysql-replica"
        };

        await using (var cmd = new MySqlCommand("SELECT bakiye FROM kullanicilar WHERE id = @uid;", conn))
        {
            cmd.Parameters.AddWithValue("@uid", kullaniciId);
            var bakiyeObj = await cmd.ExecuteScalarAsync();
            sonuc.GuncelBakiye = bakiyeObj != null && bakiyeObj != DBNull.Value ? Convert.ToDecimal(bakiyeObj) : 0m;
        }

        const string hareketSql = @"
            SELECT id, tur, tutar, rezervasyon_id, aciklama, islem_tarihi
            FROM bakiye_hareketleri
            WHERE kullanici_id = @uid
            ORDER BY islem_tarihi DESC, id DESC;";

        await using (var cmd = new MySqlCommand(hareketSql, conn))
        {
            cmd.Parameters.AddWithValue("@uid", kullaniciId);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var tutar = r.GetDecimal("tutar");
                sonuc.HareketlerToplami += tutar;
                sonuc.Hareketler.Add(new BakiyeHareketDto
                {
                    Id = r.GetInt32("id"),
                    Tur = r.GetString("tur"),
                    Tutar = tutar,
                    RezervasyonId = r.IsDBNull(r.GetOrdinal("rezervasyon_id")) ? null : r.GetInt32("rezervasyon_id"),
                    Aciklama = r.GetString("aciklama"),
                    IslemTarihi = r.GetDateTime("islem_tarihi").ToString("yyyy-MM-dd HH:mm")
                });
            }
        }

        return sonuc;
    }

    public async Task<decimal> BakiyeYukleAsync(int kullaniciId, decimal tutar, string aciklama)
    {
        await using var conn = await _dbFactory.OpenPrimaryConnectionAsync();

        try
        {
            await using var cmd = new MySqlCommand("sp_bakiye_yukle", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@p_kullanici_id", kullaniciId);
            cmd.Parameters.AddWithValue("@p_tutar", tutar);
            cmd.Parameters.AddWithValue("@p_aciklama", string.IsNullOrWhiteSpace(aciklama) ? "Online bakiye yükleme" : aciklama.Trim());

            _logger.LogInformation("[DB WRITE - PRIMARY] CALL sp_bakiye_yukle({KullaniciId}, {Tutar})", kullaniciId, tutar);

            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                return Convert.ToDecimal(r["guncel_bakiye"]);
            }

            return 0m;
        }
        catch (MySqlException ex) when (ex.SqlState == "45000")
        {
            _logger.LogWarning("[SP BUSINESS RULE - 45000] sp_bakiye_yukle hatası: {Message}", ex.Message);
            throw new IsKuraliIstisnasi(ex.Message);
        }
    }

    // ========================================================================
    // 6. DEĞERLENDİRME İŞLEMLERİ (Kullanılmış menüye 1-5 puan ve yorum)
    // ========================================================================

    public async Task DegerlendirmeEkleAsync(int kullaniciId, int menuId, int puan, string yorum)
    {
        if (puan < 1 || puan > 5)
            throw new IsKuraliIstisnasi("Puan 1 ile 5 arasında olmalıdır.");

        await using var conn = await _dbFactory.OpenPrimaryConnectionAsync();

        // Kullanıcının bu menü için 'kullanildi' durumunda rezervasyonu var mı kontrol et
        const string kontrolSql = @"
            SELECT COUNT(*) FROM rezervasyonlar
            WHERE kullanici_id = @uid AND menu_id = @mid AND durum = 'kullanildi';";
        await using (var chkCmd = new MySqlCommand(kontrolSql, conn))
        {
            chkCmd.Parameters.AddWithValue("@uid", kullaniciId);
            chkCmd.Parameters.AddWithValue("@mid", menuId);
            var kullanildiSayisi = Convert.ToInt32(await chkCmd.ExecuteScalarAsync());
            if (kullanildiSayisi == 0)
            {
                throw new IsKuraliIstisnasi("Yalnızca kullanılmış (turnikeden geçilmiş) menüler için değerlendirme yapılabilir.");
            }
        }

        // Daha önce değerlendirme yapılmış mı?
        const string mevcutSql = @"
            SELECT COUNT(*) FROM degerlendirmeler
            WHERE kullanici_id = @uid AND menu_id = @mid;";
        await using (var mevCmd = new MySqlCommand(mevcutSql, conn))
        {
            mevCmd.Parameters.AddWithValue("@uid", kullaniciId);
            mevCmd.Parameters.AddWithValue("@mid", menuId);
            var mevcut = Convert.ToInt32(await mevCmd.ExecuteScalarAsync());
            if (mevcut > 0)
            {
                throw new IsKuraliIstisnasi("Bu menü için daha önce değerlendirme yaptınız (kişi başına tek oy).");
            }
        }

        const string insertSql = @"
            INSERT INTO degerlendirmeler (kullanici_id, menu_id, puan, yorum)
            VALUES (@uid, @mid, @puan, @yorum);";
        await using var insCmd = new MySqlCommand(insertSql, conn);
        insCmd.Parameters.AddWithValue("@uid", kullaniciId);
        insCmd.Parameters.AddWithValue("@mid", menuId);
        insCmd.Parameters.AddWithValue("@puan", puan);
        insCmd.Parameters.AddWithValue("@yorum", string.IsNullOrWhiteSpace(yorum) ? DBNull.Value : yorum.Trim());

        await insCmd.ExecuteNonQueryAsync();
        _logger.LogInformation("[DB WRITE - PRIMARY] Yeni menü değerlendirmesi kaydedildi (Kullanıcı:{Uid}, Menü:{Mid}, Puan:{Puan})",
            kullaniciId, menuId, puan);
    }

    public async Task<List<DegerlendirmeListeDto>> SonDegerlendirmeleriGetirAsync(int kullaniciId)
    {
        await using var conn = await _dbFactory.OpenReplicaConnectionAsync();
        const string sql = @"
            SELECT
                d.id,
                d.menu_id,
                m.tarih,
                yh.ad AS yemekhane_ad,
                o.ad AS ogun_ad,
                k.ad_soyad AS kullanici_ad,
                d.puan,
                COALESCE(d.yorum, '') AS yorum,
                d.olusturulma_tarihi
            FROM degerlendirmeler d
            INNER JOIN menuler m ON m.id = d.menu_id
            INNER JOIN yemekhaneler yh ON yh.id = m.yemekhane_id
            INNER JOIN ogunler o ON o.id = m.ogun_id
            INNER JOIN kullanicilar k ON k.id = d.kullanici_id
            WHERE d.kullanici_id = @uid
            ORDER BY d.olusturulma_tarihi DESC;";

        var liste = new List<DegerlendirmeListeDto>();
        await using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@uid", kullaniciId);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            liste.Add(new DegerlendirmeListeDto
            {
                Id = r.GetInt32("id"),
                MenuId = r.GetInt32("menu_id"),
                Tarih = r.GetDateTime("tarih").ToString("yyyy-MM-dd"),
                YemekhaneAd = r.GetString("yemekhane_ad"),
                OgunAd = r.GetString("ogun_ad"),
                KullaniciAd = r.GetString("kullanici_ad"),
                Puan = r.GetInt32("puan"),
                Yorum = r.GetString("yorum"),
                OlusturulmaTarihi = r.GetDateTime("olusturulma_tarihi").ToString("yyyy-MM-dd HH:mm")
            });
        }
        return liste;
    }

    // ========================================================================
    // 7. YÖNETİCİ PANELİ (Menü oluşturma/düzenleme -> Primary, Raporlar -> Replica)
    // ========================================================================

    public async Task<object> YoneticiRaporlariniGetirAsync()
    {
        await using var conn = await _dbFactory.OpenReplicaConnectionAsync();

        // Menü bazında doluluk ve ortalama puan (v_menu_doluluk + v_menu_puan)
        const string raporSql = @"
            SELECT
                d.menu_id,
                d.tarih,
                d.yemekhane_id,
                d.yemekhane_ad,
                d.kapasite,
                d.ogun_id,
                d.ogun_ad,
                d.ogun_sira,
                d.ogrenci_fiyati,
                d.personel_fiyati,
                d.dolu_sayisi,
                d.bos_yer_sayisi,
                d.doluluk_yuzdesi,
                d.toplam_kalori,
                COALESCE(p.degerlendirme_sayisi, 0) AS degerlendirme_sayisi,
                COALESCE(p.ortalama_puan, 0) AS ortalama_puan,
                COALESCE((
                    SELECT GROUP_CONCAT(y.ad ORDER BY y.kategori_id SEPARATOR ', ')
                    FROM menu_yemekleri my
                    INNER JOIN yemekler y ON y.id = my.yemek_id
                    WHERE my.menu_id = d.menu_id
                ), '') AS yemekler_ozet,
                COALESCE((
                    SELECT GROUP_CONCAT(my.yemek_id ORDER BY my.yemek_id SEPARATOR ',')
                    FROM menu_yemekleri my
                    WHERE my.menu_id = d.menu_id
                ), '') AS yemek_idleri
            FROM v_menu_doluluk d
            LEFT JOIN v_menu_puan p ON p.menu_id = d.menu_id
            ORDER BY d.tarih DESC, d.yemekhane_id ASC, d.ogun_sira ASC;";

        var menuRaporlari = new List<object>();
        await using (var cmd = new MySqlCommand(raporSql, conn))
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            while (await r.ReadAsync())
            {
                var yIdStr = r.GetString("yemek_idleri");
                var yemekIdList = string.IsNullOrWhiteSpace(yIdStr)
                    ? new List<int>()
                    : yIdStr.Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(x => int.TryParse(x, out var v) ? v : 0)
                            .Where(v => v > 0)
                            .ToList();

                menuRaporlari.Add(new
                {
                    menuId = r.GetInt32("menu_id"),
                    tarih = r.GetDateTime("tarih").ToString("yyyy-MM-dd"),
                    yemekhaneId = r.GetInt32("yemekhane_id"),
                    yemekhaneAd = r.GetString("yemekhane_ad"),
                    kapasite = r.GetInt32("kapasite"),
                    ogunId = r.GetInt32("ogun_id"),
                    ogunAd = r.GetString("ogun_ad"),
                    ogrenciFiyati = r.GetDecimal("ogrenci_fiyati"),
                    personelFiyati = r.GetDecimal("personel_fiyati"),
                    doluSayisi = Convert.ToInt32(r["dolu_sayisi"]),
                    bosYerSayisi = Convert.ToInt32(r["bos_yer_sayisi"]),
                    dolulukYuzdesi = Convert.ToDecimal(r["doluluk_yuzdesi"]),
                    toplamKalori = Convert.ToInt32(r["toplam_kalori"]),
                    degerlendirmeSayisi = Convert.ToInt32(r["degerlendirme_sayisi"]),
                    ortalamaPuan = Convert.ToDecimal(r["ortalama_puan"]),
                    yemeklerOzet = r.GetString("yemekler_ozet"),
                    yemekIdleri = yemekIdList
                });
            }
        }

        // Turnike geçiş testi için aktif rezervasyonlar
        const string aktifRezSql = @"
            SELECT
                r.id,
                k.ad_soyad,
                k.rol,
                m.tarih,
                yh.ad AS yemekhane_ad,
                o.ad AS ogun_ad,
                r.durum,
                r.ucret
            FROM rezervasyonlar r
            INNER JOIN kullanicilar k ON k.id = r.kullanici_id
            INNER JOIN menuler m ON m.id = r.menu_id
            INNER JOIN yemekhaneler yh ON yh.id = m.yemekhane_id
            INNER JOIN ogunler o ON o.id = m.ogun_id
            WHERE r.durum = 'aktif'
            ORDER BY m.tarih ASC, r.id DESC
            LIMIT 25;";

        var aktifRezervasyonlar = new List<object>();
        await using (var cmd = new MySqlCommand(aktifRezSql, conn))
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            while (await r.ReadAsync())
            {
                aktifRezervasyonlar.Add(new
                {
                    id = r.GetInt32("id"),
                    adSoyad = r.GetString("ad_soyad"),
                    rol = r.GetString("rol"),
                    tarih = r.GetDateTime("tarih").ToString("yyyy-MM-dd"),
                    yemekhaneAd = r.GetString("yemekhane_ad"),
                    ogunAd = r.GetString("ogun_ad"),
                    durum = r.GetString("durum"),
                    ucret = r.GetDecimal("ucret")
                });
            }
        }

        return new
        {
            menuRaporlari,
            aktifRezervasyonlar
        };
    }

    public async Task<(int MenuId, int YemekhaneId, string Tarih, int OgunId)> MenuKaydetVeyaGuncelleAsync(
        int? menuId, MenuKaydetIstekDto istek)
    {
        if (istek.YemekIdleri == null || istek.YemekIdleri.Distinct().Count() == 0)
            throw new IsKuraliIstisnasi("Menü için en az bir yemek seçilmelidir.");

        if (istek.OgrenciFiyati < 0 || istek.PersonelFiyati < 0)
            throw new IsKuraliIstisnasi("Öğrenci ve personel fiyatları negatif olamaz.");

        await using var conn = await _dbFactory.OpenPrimaryConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();

        try
        {
            int hedefMenuId;

            if (menuId.HasValue && menuId.Value > 0)
            {
                hedefMenuId = menuId.Value;
                const string updateSql = @"
                    UPDATE menuler
                       SET yemekhane_id = @yhId,
                           ogun_id = @ogunId,
                           tarih = @tarih,
                           ogrenci_fiyati = @ogrFiyat,
                           personel_fiyati = @perFiyat
                     WHERE id = @id;";
                await using var upCmd = new MySqlCommand(updateSql, conn, tx);
                upCmd.Parameters.AddWithValue("@yhId", istek.YemekhaneId);
                upCmd.Parameters.AddWithValue("@ogunId", istek.OgunId);
                upCmd.Parameters.AddWithValue("@tarih", istek.Tarih);
                upCmd.Parameters.AddWithValue("@ogrFiyat", istek.OgrenciFiyati);
                upCmd.Parameters.AddWithValue("@perFiyat", istek.PersonelFiyati);
                upCmd.Parameters.AddWithValue("@id", hedefMenuId);
                await upCmd.ExecuteNonQueryAsync();
            }
            else
            {
                // Aynı yemekhane + tarih + öğün için menü varsa onu güncelle, yoksa yeni oluştur
                const string mevcutSql = @"
                    SELECT id FROM menuler
                    WHERE yemekhane_id = @yhId AND tarih = @tarih AND ogun_id = @ogunId
                    LIMIT 1 FOR UPDATE;";
                await using var mevCmd = new MySqlCommand(mevcutSql, conn, tx);
                mevCmd.Parameters.AddWithValue("@yhId", istek.YemekhaneId);
                mevCmd.Parameters.AddWithValue("@tarih", istek.Tarih);
                mevCmd.Parameters.AddWithValue("@ogunId", istek.OgunId);
                var mevcutIdObj = await mevCmd.ExecuteScalarAsync();

                if (mevcutIdObj != null && mevcutIdObj != DBNull.Value)
                {
                    hedefMenuId = Convert.ToInt32(mevcutIdObj);
                    const string updateSql = @"
                        UPDATE menuler
                           SET ogrenci_fiyati = @ogrFiyat,
                               personel_fiyati = @perFiyat
                         WHERE id = @id;";
                    await using var upCmd = new MySqlCommand(updateSql, conn, tx);
                    upCmd.Parameters.AddWithValue("@ogrFiyat", istek.OgrenciFiyati);
                    upCmd.Parameters.AddWithValue("@perFiyat", istek.PersonelFiyati);
                    upCmd.Parameters.AddWithValue("@id", hedefMenuId);
                    await upCmd.ExecuteNonQueryAsync();
                }
                else
                {
                    const string insertSql = @"
                        INSERT INTO menuler (yemekhane_id, ogun_id, tarih, ogrenci_fiyati, personel_fiyati)
                        VALUES (@yhId, @ogunId, @tarih, @ogrFiyat, @perFiyat);";
                    await using var insCmd = new MySqlCommand(insertSql, conn, tx);
                    insCmd.Parameters.AddWithValue("@yhId", istek.YemekhaneId);
                    insCmd.Parameters.AddWithValue("@ogunId", istek.OgunId);
                    insCmd.Parameters.AddWithValue("@tarih", istek.Tarih);
                    insCmd.Parameters.AddWithValue("@ogrFiyat", istek.OgrenciFiyati);
                    insCmd.Parameters.AddWithValue("@perFiyat", istek.PersonelFiyati);
                    await insCmd.ExecuteNonQueryAsync();
                    hedefMenuId = (int)insCmd.LastInsertedId;
                }
            }

            await using (var delCmd = new MySqlCommand("DELETE FROM menu_yemekleri WHERE menu_id = @mid;", conn, tx))
            {
                delCmd.Parameters.AddWithValue("@mid", hedefMenuId);
                await delCmd.ExecuteNonQueryAsync();
            }

            foreach (var yemekId in istek.YemekIdleri.Distinct())
            {
                await using var myCmd = new MySqlCommand("INSERT INTO menu_yemekleri (menu_id, yemek_id) VALUES (@mid, @yid);", conn, tx);
                myCmd.Parameters.AddWithValue("@mid", hedefMenuId);
                myCmd.Parameters.AddWithValue("@yid", yemekId);
                await myCmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
            _logger.LogInformation("[DB WRITE - PRIMARY] Menü kaydedildi/güncellendi (MenuId:{MenuId}, Yemekhane:{Yh}, Tarih:{Tarih}, Öğün:{Ogun})",
                hedefMenuId, istek.YemekhaneId, istek.Tarih, istek.OgunId);

            return (hedefMenuId, istek.YemekhaneId, istek.Tarih, istek.OgunId);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ========================================================================
    // 8. AŞAMA 4 · KULLANICI ALERJEN TERCİHLERİ (Primary / Replica)
    // ========================================================================

    public async Task<List<int>> KullaniciAlerjenleriniGuncelleAsync(int kullaniciId, List<int> alerjenIdleri)
    {
        await using var conn = await _dbFactory.OpenPrimaryConnectionAsync();

        const string tabloVarSql = @"
            SELECT COUNT(*) FROM information_schema.TABLES
            WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'kullanici_alerjenleri';";
        await using (var chkCmd = new MySqlCommand(tabloVarSql, conn))
        {
            var varMi = Convert.ToInt32(await chkCmd.ExecuteScalarAsync());
            if (varMi == 0)
                throw new IsKuraliIstisnasi("Alerjen tercihleri tablosu henüz oluşturulmamış (04_degisiklik.sql migration bekleniyor).");
        }

        await using var tx = await conn.BeginTransactionAsync();
        try
        {
            await using (var delCmd = new MySqlCommand("DELETE FROM kullanici_alerjenleri WHERE kullanici_id = @uid;", conn, tx))
            {
                delCmd.Parameters.AddWithValue("@uid", kullaniciId);
                await delCmd.ExecuteNonQueryAsync();
            }

            var temizListe = (alerjenIdleri ?? new List<int>()).Distinct().Where(x => x > 0).OrderBy(x => x).ToList();
            foreach (var alerjenId in temizListe)
            {
                await using var insCmd = new MySqlCommand("INSERT INTO kullanici_alerjenleri (kullanici_id, alerjen_id) VALUES (@uid, @aid);", conn, tx);
                insCmd.Parameters.AddWithValue("@uid", kullaniciId);
                insCmd.Parameters.AddWithValue("@aid", alerjenId);
                await insCmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
            _logger.LogInformation("[DB WRITE - PRIMARY] Kullanıcı ({Uid}) alerjen tercihleri güncellendi: [{Ids}]",
                kullaniciId, string.Join(",", temizListe));
            return temizListe;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}

