using Microsoft.AspNetCore.Mvc;
using YemekhaneApi.Models;
using YemekhaneApi.Repositories;
using YemekhaneApi.Services;

namespace YemekhaneApi.Controllers;

[Route("api/profil")]
public class ProfilController : BaseApiController
{
    private readonly YemekhaneRepository _repository;

    public ProfilController(YemekhaneRepository repository, RedisService redisService)
        : base(redisService)
    {
        _repository = repository;
    }

    [HttpGet("alerjenler")]
    public async Task<IActionResult> AlerjenleriGetir()
    {
        var oturum = await AktifOturumGetirAsync();
        if (oturum == null)
            return Unauthorized(new { hata = "Profil bilgilerinizi görüntülemek için giriş yapmalısınız." });

        var guncel = await _repository.KullaniciIdIleGetirAsync(oturum.Id, kaynaktanOku: false);
        return Ok(new
        {
            kullaniciId = oturum.Id,
            alerjenIdleri = guncel?.AlerjenIdleri ?? oturum.AlerjenIdleri
        });
    }

    [HttpPut("alerjenler")]
    public async Task<IActionResult> AlerjenleriGuncelle([FromBody] ProfilAlerjenGuncelleDto istek)
    {
        var oturum = await AktifOturumGetirAsync();
        if (oturum == null)
            return Unauthorized(new { hata = "Alerjen tercihlerinizi güncellemek için giriş yapmalısınız." });

        try
        {
            var guncelListe = await _repository.KullaniciAlerjenleriniGuncelleAsync(oturum.Id, istek.AlerjenIdleri);
            oturum.AlerjenIdleri = guncelListe;

            var token = IstekTokenGetir();
            if (!string.IsNullOrWhiteSpace(token))
            {
                await RedisService.OturumGuncelleAsync(token, oturum);
            }

            return Ok(new
            {
                mesaj = "Alerjen tercihleriniz güncellendi. Menülerde bu alerjenleri içeren yemekler uyarıyla işaretlenecektir.",
                alerjenIdleri = guncelListe
            });
        }
        catch (IsKuraliIstisnasi ex)
        {
            return BadRequest(new { hata = ex.Message });
        }
    }
}

