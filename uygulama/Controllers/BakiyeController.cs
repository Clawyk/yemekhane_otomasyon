using Microsoft.AspNetCore.Mvc;
using YemekhaneApi.Models;
using YemekhaneApi.Repositories;
using YemekhaneApi.Services;

namespace YemekhaneApi.Controllers;

[Route("api/bakiye")]
public class BakiyeController : BaseApiController
{
    private readonly YemekhaneRepository _repository;

    public BakiyeController(YemekhaneRepository repository, RedisService redisService)
        : base(redisService)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<IActionResult> Getir([FromQuery] bool kaynaktan = false)
    {
        var oturum = await AktifOturumGetirAsync();
        if (oturum == null)
            return Unauthorized(new { hata = "Bakiye bilgilerinizi görüntülemek için giriş yapmalısınız." });

        var veri = await _repository.BakiyeVeHareketleriGetirAsync(oturum.Id, kaynaktanOku: kaynaktan);
        return Ok(veri);
    }

    [HttpPost("yukle")]
    public async Task<IActionResult> Yukle([FromBody] BakiyeYukleIstekDto istek)
    {
        var oturum = await AktifOturumGetirAsync();
        if (oturum == null)
            return Unauthorized(new { hata = "Bakiye yüklemek için giriş yapmalısınız." });

        try
        {
            var guncelBakiye = await _repository.BakiyeYukleAsync(oturum.Id, istek.Tutar, istek.Aciklama);
            oturum.Bakiye = guncelBakiye;

            var token = IstekTokenGetir();
            if (!string.IsNullOrWhiteSpace(token))
            {
                await RedisService.OturumGuncelleAsync(token, oturum);
            }

            return Ok(new
            {
                mesaj = $"{istek.Tutar:0.00} TL bakiye hesabınıza yüklendi.",
                guncelBakiye,
                bakiyeOkumaKaynagi = "mysql-primary"
            });
        }
        catch (IsKuraliIstisnasi ex)
        {
            return BadRequest(new { hata = ex.Message });
        }
    }
}

