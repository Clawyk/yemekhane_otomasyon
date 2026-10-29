using Microsoft.AspNetCore.Mvc;
using YemekhaneApi.Repositories;
using YemekhaneApi.Services;

namespace YemekhaneApi.Controllers;

[Route("api/menuler")]
public class MenuController : BaseApiController
{
    private readonly YemekhaneRepository _repository;

    public MenuController(YemekhaneRepository repository, RedisService redisService)
        : base(redisService)
    {
        _repository = repository;
    }

    [HttpGet("referanslar")]
    public async Task<IActionResult> Referanslar()
    {
        await AktifOturumGetirAsync();
        var veri = await _repository.ReferansVerileriGetirAsync();
        return Ok(veri);
    }

    [HttpGet("gunun-menusu")]
    public async Task<IActionResult> GununMenusu(
        [FromQuery] int yemekhaneId = 1,
        [FromQuery] string? tarih = null,
        [FromQuery] int ogunId = 2)
    {
        await AktifOturumGetirAsync();

        var hedefTarih = string.IsNullOrWhiteSpace(tarih)
            ? DateTime.Today.ToString("yyyy-MM-dd")
            : tarih.Trim();

        // 1. Önce Redis önbelleğini kontrol et: menu:{yemekhane_id}:{tarih}:{ogun_id}
        var onbellekMenu = await RedisService.MenuOnbellektenGetirAsync(yemekhaneId, hedefTarih, ogunId);
        if (onbellekMenu != null)
        {
            if (DateTime.TryParse(onbellekMenu.Tarih, out var mTarih) &&
                TimeSpan.TryParse(onbellekMenu.RezervasyonSonSaat, out var sSaat))
            {
                onbellekMenu.SonSaatGectiMi = YemekhaneRepository.SonSaatGectiMiHesapla(mTarih, sSaat);
            }
            return Ok(onbellekMenu);
        }

        // 2. Redis'te yoksa replika veritabanından oku ve gün sonuna kadar Redis'e yaz
        var dbMenu = await _repository.TekMenuDetayGetirAsync(yemekhaneId, hedefTarih, ogunId);
        if (dbMenu == null)
        {
            return NotFound(new { hata = "Seçilen yemekhane, tarih ve öğün için tanımlı menü bulunamadı." });
        }

        await RedisService.MenuOnbellegeYazAsync(yemekhaneId, hedefTarih, ogunId, dbMenu);
        return Ok(dbMenu);
    }

    [HttpGet("haftalik")]
    public async Task<IActionResult> HaftalikMenu(
        [FromQuery] int yemekhaneId = 1,
        [FromQuery] string? baslangicTarih = null,
        [FromQuery] int gunSayisi = 7)
    {
        await AktifOturumGetirAsync();

        var hedefBaslangic = string.IsNullOrWhiteSpace(baslangicTarih)
            ? DateTime.Today.ToString("yyyy-MM-dd")
            : baslangicTarih.Trim();

        var menuler = await _repository.HaftalikMenuleriGetirAsync(yemekhaneId, hedefBaslangic, gunSayisi);
        return Ok(menuler);
    }
}

