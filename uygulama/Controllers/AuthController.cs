using Microsoft.AspNetCore.Mvc;
using YemekhaneApi.Models;
using YemekhaneApi.Repositories;
using YemekhaneApi.Services;

namespace YemekhaneApi.Controllers;

[Route("api/auth")]
public class AuthController : BaseApiController
{
    private readonly YemekhaneRepository _repository;

    public AuthController(YemekhaneRepository repository, RedisService redisService)
        : base(redisService)
    {
        _repository = repository;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] GirisIstekDto istek)
    {
        if (string.IsNullOrWhiteSpace(istek.Eposta) || string.IsNullOrWhiteSpace(istek.Sifre))
        {
            return BadRequest(new { hata = "E-posta ve şifre alanları zorunludur." });
        }

        var (kullanici, sifreHash) = await _repository.KullaniciEpostaIleGetirAsync(istek.Eposta);
        if (kullanici == null || string.IsNullOrEmpty(sifreHash) || !BCrypt.Net.BCrypt.Verify(istek.Sifre, sifreHash))
        {
            return Unauthorized(new { hata = "E-posta adresi veya şifre hatalı." });
        }

        var token = await RedisService.OturumOlusturAsync(kullanici);
        return Ok(new GirisYanitDto
        {
            Token = token,
            Kullanici = kullanici
        });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var token = IstekTokenGetir();
        await RedisService.OturumSilAsync(token);
        return Ok(new { mesaj = "Oturum başarıyla sonlandırıldı." });
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me([FromQuery] bool kaynaktan = false)
    {
        var token = IstekTokenGetir();
        var oturum = await RedisService.OturumGetirVeYenileAsync(token);
        if (oturum == null)
        {
            return Unauthorized(new { hata = "Oturum süresi dolmuş veya geçersiz. Lütfen tekrar giriş yapın." });
        }

        var guncelKullanici = await _repository.KullaniciIdIleGetirAsync(oturum.Id, kaynaktanOku: kaynaktan);
        if (guncelKullanici != null && !string.IsNullOrWhiteSpace(token))
        {
            await RedisService.OturumGuncelleAsync(token, guncelKullanici);
            return Ok(guncelKullanici);
        }

        return Ok(oturum);
    }
}

