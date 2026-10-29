using Microsoft.AspNetCore.Mvc;
using YemekhaneApi.Models;
using YemekhaneApi.Repositories;
using YemekhaneApi.Services;

namespace YemekhaneApi.Controllers;

[Route("api/rezervasyonlar")]
public class RezervasyonController : BaseApiController
{
    private readonly YemekhaneRepository _repository;

    public RezervasyonController(YemekhaneRepository repository, RedisService redisService)
        : base(redisService)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<IActionResult> Listele()
    {
        var oturum = await AktifOturumGetirAsync();
        if (oturum == null)
            return Unauthorized(new { hata = "Rezervasyonlarınızı görüntülemek için giriş yapmalısınız." });

        var liste = await _repository.KullaniciRezervasyonlariniGetirAsync(oturum.Id);
        return Ok(liste);
    }

    [HttpPost]
    public async Task<IActionResult> Olustur([FromBody] RezervasyonOlusturDto istek)
    {
        var oturum = await AktifOturumGetirAsync();
        if (oturum == null)
            return Unauthorized(new { hata = "Rezervasyon yapmak için lütfen giriş yapın." });

        try
        {
            var (rezId, kesilenUcret, guncelBakiye, porsiyon) = await _repository.RezervasyonYapAsync(
                oturum.Id, istek.MenuId, istek.Porsiyon);

            oturum.Bakiye = guncelBakiye;
            var token = IstekTokenGetir();
            if (!string.IsNullOrWhiteSpace(token))
            {
                await RedisService.OturumGuncelleAsync(token, oturum);
            }

            return Ok(new
            {
                mesaj = "Rezervasyonunuz başarıyla oluşturuldu.",
                rezervasyonId = rezId,
                porsiyon,
                kesilenUcret,
                guncelBakiye,
                bakiyeOkumaKaynagi = "mysql-primary"
            });
        }
        catch (IsKuraliIstisnasi ex)
        {
            return BadRequest(new { hata = ex.Message });
        }
    }

    [HttpPost("{id:int}/iptal")]
    public async Task<IActionResult> IptalEt(int id)
    {
        var oturum = await AktifOturumGetirAsync();
        if (oturum == null)
            return Unauthorized(new { hata = "Bu işlem için giriş yapmalısınız." });

        try
        {
            var (iadeTutari, guncelBakiye) = await _repository.RezervasyonIptalAsync(id, oturum.Id);

            oturum.Bakiye = guncelBakiye;
            var token = IstekTokenGetir();
            if (!string.IsNullOrWhiteSpace(token))
            {
                await RedisService.OturumGuncelleAsync(token, oturum);
            }

            return Ok(new
            {
                mesaj = $"Rezervasyon iptal edildi ve {iadeTutari:0.00} TL bakiyenize iade edildi.",
                iadeTutari,
                guncelBakiye,
                bakiyeOkumaKaynagi = "mysql-primary"
            });
        }
        catch (IsKuraliIstisnasi ex)
        {
            return BadRequest(new { hata = ex.Message });
        }
    }

    [HttpPost("{id:int}/turnike")]
    public async Task<IActionResult> TurnikeGecis(int id)
    {
        var oturum = await AktifOturumGetirAsync();
        if (oturum == null)
            return Unauthorized(new { hata = "Bu işlem için giriş yapmalısınız." });

        try
        {
            await _repository.TurnikeGecisAsync(id);
            return Ok(new
            {
                mesaj = "Turnike geçişi onaylandı. Rezervasyon durumu 'kullanıldı' olarak güncellendi."
            });
        }
        catch (IsKuraliIstisnasi ex)
        {
            return BadRequest(new { hata = ex.Message });
        }
    }
}

