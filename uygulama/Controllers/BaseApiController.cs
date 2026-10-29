using Microsoft.AspNetCore.Mvc;
using YemekhaneApi.Models;
using YemekhaneApi.Services;

namespace YemekhaneApi.Controllers;

[ApiController]
public abstract class BaseApiController : ControllerBase
{
    protected readonly RedisService RedisService;

    protected BaseApiController(RedisService redisService)
    {
        RedisService = redisService;
    }

    protected string? IstekTokenGetir()
    {
        if (Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var deger = authHeader.ToString();
            if (deger.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return deger.Substring("Bearer ".Length).Trim();
            if (!string.IsNullOrWhiteSpace(deger))
                return deger.Trim();
        }

        if (Request.Headers.TryGetValue("X-Session-Token", out var sessionHeader))
        {
            return sessionHeader.ToString().Trim();
        }

        return null;
    }

    protected async Task<KullaniciOturumDto?> AktifOturumGetirAsync()
    {
        var token = IstekTokenGetir();
        return await RedisService.OturumGetirVeYenileAsync(token);
    }
}

