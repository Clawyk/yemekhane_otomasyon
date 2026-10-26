namespace YemekhaneApi.Models;

public class KullaniciOturumDto
{
    public int Id { get; set; }
    public string AdSoyad { get; set; } = string.Empty;
    public string Eposta { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public string? OgrenciNo { get; set; }
    public decimal Bakiye { get; set; }
    public List<int> AlerjenIdleri { get; set; } = new();
}

public class GirisIstekDto
{
    public string Eposta { get; set; } = string.Empty;
    public string Sifre { get; set; } = string.Empty;
}

public class GirisYanitDto
{
    public string Token { get; set; } = string.Empty;
    public KullaniciOturumDto Kullanici { get; set; } = new();
}

public class YemekhaneDto
{
    public int Id { get; set; }
    public string Ad { get; set; } = string.Empty;
    public int Kapasite { get; set; }
    public string Konum { get; set; } = string.Empty;
}

public class OgunDto
{
    public int Id { get; set; }
    public string Ad { get; set; } = string.Empty;
    public string BaslangicSaati { get; set; } = string.Empty;
    public string BitisSaati { get; set; } = string.Empty;
    public string RezervasyonSonSaat { get; set; } = string.Empty;
    public int Sira { get; set; }
}

public class YemekDetayDto
{
    public int YemekId { get; set; }
    public string YemekAd { get; set; } = string.Empty;
    public int Kalori { get; set; }
    public bool Vejetaryen { get; set; }
    public int KategoriId { get; set; }
    public string KategoriAd { get; set; } = string.Empty;
    public int KategoriSira { get; set; }
    public string Alerjenler { get; set; } = string.Empty;
    public List<int> AlerjenIdleri { get; set; } = new();
}

public class MenuDetayDto
{
    public int MenuId { get; set; }
    public string Tarih { get; set; } = string.Empty;
    public int YemekhaneId { get; set; }
    public string YemekhaneAd { get; set; } = string.Empty;
    public int Kapasite { get; set; }
    public int OgunId { get; set; }
    public string OgunAd { get; set; } = string.Empty;
    public int OgunSira { get; set; }
    public string RezervasyonSonSaat { get; set; } = string.Empty;
    public decimal OgrenciFiyati { get; set; }
    public decimal PersonelFiyati { get; set; }
    public int DoluSayisi { get; set; }
    public int BosYerSayisi { get; set; }
    public decimal DolulukYuzdesi { get; set; }
    public int ToplamKalori { get; set; }
    public bool MenuVejetaryen { get; set; }
    public decimal OrtalamaPuan { get; set; }
    public int DegerlendirmeSayisi { get; set; }
    public bool SonSaatGectiMi { get; set; }
    public bool YarimPorsiyonSecilebilirMi { get; set; }
    public string OnbellekKaynagi { get; set; } = "DB";
    public List<YemekDetayDto> Yemekler { get; set; } = new();
}

public class RezervasyonOlusturDto
{
    public int MenuId { get; set; }
    public string Porsiyon { get; set; } = "tam";
}

public class RezervasyonKayitDto
{
    public int Id { get; set; }
    public int MenuId { get; set; }
    public string Tarih { get; set; } = string.Empty;
    public int YemekhaneId { get; set; }
    public string YemekhaneAd { get; set; } = string.Empty;
    public int OgunId { get; set; }
    public string OgunAd { get; set; } = string.Empty;
    public string RezervasyonSonSaat { get; set; } = string.Empty;
    public string Durum { get; set; } = string.Empty;
    public string Porsiyon { get; set; } = "tam";
    public decimal Ucret { get; set; }
    public string OlusturulmaTarihi { get; set; } = string.Empty;
    public bool IptalEdilebilirMi { get; set; }
    public bool DegerlendirildiMi { get; set; }
    public int? VerilenPuan { get; set; }
    public string? VerilenYorum { get; set; }
    public string YemekOzeti { get; set; } = string.Empty;
}

public class BakiyeHareketDto
{
    public int Id { get; set; }
    public string Tur { get; set; } = string.Empty;
    public decimal Tutar { get; set; }
    public int? RezervasyonId { get; set; }
    public string Aciklama { get; set; } = string.Empty;
    public string IslemTarihi { get; set; } = string.Empty;
}

public class BakiyeSayfaDto
{
    public decimal GuncelBakiye { get; set; }
    public decimal HareketlerToplami { get; set; }
    public string OkumaKaynagi { get; set; } = "mysql-replica";
    public List<BakiyeHareketDto> Hareketler { get; set; } = new();
}

public class BakiyeYukleIstekDto
{
    public decimal Tutar { get; set; }
    public string Aciklama { get; set; } = "Bakiye yükleme";
}

public class DegerlendirmeEkleDto
{
    public int MenuId { get; set; }
    public int Puan { get; set; }
    public string Yorum { get; set; } = string.Empty;
}

public class DegerlendirmeListeDto
{
    public int Id { get; set; }
    public int MenuId { get; set; }
    public string Tarih { get; set; } = string.Empty;
    public string YemekhaneAd { get; set; } = string.Empty;
    public string OgunAd { get; set; } = string.Empty;
    public string KullaniciAd { get; set; } = string.Empty;
    public int Puan { get; set; }
    public string Yorum { get; set; } = string.Empty;
    public string OlusturulmaTarihi { get; set; } = string.Empty;
}

public class AlerjenDto
{
    public int Id { get; set; }
    public string Ad { get; set; } = string.Empty;
    public string Aciklama { get; set; } = string.Empty;
}

public class ProfilAlerjenGuncelleDto
{
    public List<int> AlerjenIdleri { get; set; } = new();
}

public class MenuKaydetIstekDto
{
    public int YemekhaneId { get; set; }
    public int OgunId { get; set; }
    public string Tarih { get; set; } = string.Empty;
    public decimal OgrenciFiyati { get; set; }
    public decimal PersonelFiyati { get; set; }
    public List<int> YemekIdleri { get; set; } = new();
}

