using Microsoft.AspNetCore.Mvc;
using YemekhaneApi.Models;
using YemekhaneApi.Repositories;
using YemekhaneApi.Services;

namespace YemekhaneApi.Controllers;

[Route("api/yonetici")]
public class YoneticiController : BaseApiController
{
    private readonly YemekhaneRepository _repository;

    public YoneticiController(YemekhaneRepository repository, RedisService redisService)
        : base(redisService)
    {
        _repository = repository;
    }

    [HttpGet("ozet")]
    public async Task<IActionResult> RaporOzet()
    {
        var oturum = await AktifOturumGetirAsync();
        if (oturum == null)
            return Unauthorized(new { hata = "Yönetici paneline erişmek için giriş yapmalısınız." });

        if (!string.Equals(oturum.Rol, "yonetici", StringComparison.OrdinalIgnoreCase))
            return StatusCode(403, new { hata = "Bu ekran yalnızca yöneticiler içindir." });

        var veri = await _repository.YoneticiRaporlariniGetirAsync();
        return Ok(veri);
    }

    [HttpPost("menu")]
    public async Task<IActionResult> MenuOlusturVeyaGuncelle([FromBody] MenuKaydetIstekDto istek)
    {
        var oturum = await AktifOturumGetirAsync();
        if (oturum == null)
            return Unauthorized(new { hata = "Bu işlem için giriş yapmalısınız." });

        if (!string.Equals(oturum.Rol, "yonetici", StringComparison.OrdinalIgnoreCase))
            return StatusCode(403, new { hata = "Menü oluşturma ve düzenleme yetkisi yalnızca yöneticilere aittir." });

        try
        {
            var (menuId, yhId, tarih, ogunId) = await _repository.MenuKaydetVeyaGuncelleAsync(null, istek);

            // Aşama 3 kuralı: Yönetici o menüyü değiştirdiğinde Redis'teki menu:{yemekhane_id}:{tarih}:{ogun_id} kaydı silinir
            await RedisService.MenuOnbellekSilAsync(yhId, tarih, ogunId);

            return Ok(new
            {
                mesaj = $"Menü (#{menuId}) başarıyla kaydedildi ve 'menu:{yhId}:{tarih}:{ogunId}' Redis önbelleği temizlendi.",
                menuId,
                silinenOnbellekAnahtari = RedisService.MenuAnahtariOlustur(yhId, tarih, ogunId)
            });
        }
        catch (IsKuraliIstisnasi ex)
        {
            return BadRequest(new { hata = ex.Message });
        }
    }

    [HttpPut("menu/{id:int}")]
    public async Task<IActionResult> MenuGuncelle(int id, [FromBody] MenuKaydetIstekDto istek)
    {
        var oturum = await AktifOturumGetirAsync();
        if (oturum == null)
            return Unauthorized(new { hata = "Bu işlem için giriş yapmalısınız." });

        if (!string.Equals(oturum.Rol, "yonetici", StringComparison.OrdinalIgnoreCase))
            return StatusCode(403, new { hata = "Menü düzenleme yetkisi yalnızca yöneticilere aittir." });

        try
        {
            var (menuId, yhId, tarih, ogunId) = await _repository.MenuKaydetVeyaGuncelleAsync(id, istek);

            // Aşama 3 kuralı: Yönetici o menüyü değiştirdiğinde Redis'teki menu:{yemekhane_id}:{tarih}:{ogun_id} kaydı silinir
            await RedisService.MenuOnbellekSilAsync(yhId, tarih, ogunId);

            return Ok(new
            {
                mesaj = $"Menü (#{menuId}) güncellendi ve 'menu:{yhId}:{tarih}:{ogunId}' Redis önbelleği silindi.",
                menuId,
                silinenOnbellekAnahtari = RedisService.MenuAnahtariOlustur(yhId, tarih, ogunId)
            });
        }
        catch (IsKuraliIstisnasi ex)
        {
            return BadRequest(new { hata = ex.Message });
        }
    }

    [HttpPost("cache-temizle")]
    public async Task<IActionResult> TumMenuOnbellekleriniTemizle()
    {
        var oturum = await AktifOturumGetirAsync();
        if (oturum == null || !string.Equals(oturum.Rol, "yonetici", StringComparison.OrdinalIgnoreCase))
            return StatusCode(403, new { hata = "Yetkisiz işlem." });

        var silinen = await RedisService.TumMenuOnbellekleriniTemizleAsync();
        return Ok(new
        {
            mesaj = $"Redis üzerindeki {silinen} adet menü önbellek kaydı temizlendi.",
            silinenSayisi = silinen
        });
    }
}

