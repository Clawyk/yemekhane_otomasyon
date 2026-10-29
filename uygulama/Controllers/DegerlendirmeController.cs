using Microsoft.AspNetCore.Mvc;
using YemekhaneApi.Models;
using YemekhaneApi.Repositories;
using YemekhaneApi.Services;

namespace YemekhaneApi.Controllers;

[Route("api/degerlendirmeler")]
public class DegerlendirmeController : BaseApiController
{
    private readonly YemekhaneRepository _repository;

    public DegerlendirmeController(YemekhaneRepository repository, RedisService redisService)
        : base(redisService)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<IActionResult> Listele()
    {
        var oturum = await AktifOturumGetirAsync();
        if (oturum == null)
            return Unauthorized(new { hata = "Değerlendirmeleri görüntülemek için giriş yapmalısınız." });

        var liste = await _repository.SonDegerlendirmeleriGetirAsync(oturum.Id);
        return Ok(liste);
    }

    [HttpPost]
    public async Task<IActionResult> Ekle([FromBody] DegerlendirmeEkleDto istek)
    {
        var oturum = await AktifOturumGetirAsync();
        if (oturum == null)
            return Unauthorized(new { hata = "Değerlendirme yapmak için giriş yapmalısınız." });

        try
        {
            await _repository.DegerlendirmeEkleAsync(oturum.Id, istek.MenuId, istek.Puan, istek.Yorum);
            return Ok(new { mesaj = "Değerlendirmeniz başarıyla kaydedildi. Teşekkür ederiz!" });
        }
        catch (IsKuraliIstisnasi ex)
        {
            return BadRequest(new { hata = ex.Message });
        }
    }
}

