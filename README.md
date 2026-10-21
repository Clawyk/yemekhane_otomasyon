# Kampüs Yemekhane Otomasyonu – Dönem Ödevi

Bu proje, kampüs yemekhanesi için **menü, rezervasyon, bakiye, değerlendirme ve yönetici işlemlerini** tamamen Docker container'ları üzerinde yöneten katmanlı bir web uygulamasıdır.

- **Mimari Seçimi:** Web API (`ASP.NET Core 8.0 C#`) + Arayüz (`HTML5 + CSS3 + Vanilla JavaScript fetch`)
- **Veritabanı:** MySQL 8.0 GTID tabanlı Kaynak-Replika (`mysql-primary` yazma `:3307`, `mysql-replica` okuma `:3308`)
- **Önbellek ve Oturum:** Redis 7 (`:6379`)
- **Ön Yüz Sunucusu / Ters Vekil:** Apache 2.4 (`localhost:8080`)

---

## 1. Hızlı Kurulum ve Çalıştırma

Depo kök dizininde tek komutla tüm sistemi ayağa kaldırabilirsiniz:

```bash
docker compose up -d --build
```

Durumu kontrol etmek için:

```bash
docker compose ps
```

---

## 2. Bağlantı Bilgileri ve Örnek Hesaplar

| Servis | Container Adı | Dış Port | İç Port | Kullanıcı / Şifre |
|---|---|---|---|---|
| **Web Arayüzü (Apache)** | `frontend` | `http://localhost:8080` | `80` | — |
| **Web API (.NET 8)** | `uygulama` | *(Apache üzerinden `/api/`)* | `8080` | — |
| **MySQL Kaynak (Yazma)** | `mysql-primary` | `localhost:3307` | `3306` | `root` / `root_pass_2026` |
| **MySQL Replika (Okuma)** | `mysql-replica` | `localhost:3308` | `3306` | `root` / `root_pass_2026` |
| **Redis** | `redis` | `localhost:6379` | `6379` | — |

### Örnek Kullanıcı Hesapları (Tüm şifreler: `123456`)

| Rol | Ad Soyad | E-Posta | Şifre |
|---|---|---|---|
| **Öğrenci** | Ahmet Yılmaz | `ahmet.yilmaz@kampus.edu.tr` | `123456` |
| **Öğrenci** | Zeynep Kaya | `zeynep.kaya@kampus.edu.tr` | `123456` |
| **Personel** | Dr. Kemal Güler | `kemal.guler@kampus.edu.tr` | `123456` |
| **Yönetici** | Yönetici Murat Avcı | `admin@kampus.edu.tr` | `123456` |

---

## 3. Doğrulama Komutları

### Aşama 1 · Altyapı ve Replikasyon Doğrulaması

```bash
# 1. Tüm servislerin durumunu kontrol et
docker compose ps -a

# 2. Kaynakta bakiye yükle ve replikada görünmesini doğrula
docker exec mysql-primary mysql -uroot -proot_pass_2026 yemekhane -e "CALL sp_bakiye_yukle(1, 100.00, 'Aşama 1 replikasyon testi');"
docker exec mysql-replica mysql -uroot -proot_pass_2026 yemekhane -e "SELECT id, ad_soyad, bakiye FROM kullanicilar WHERE id = 1;"

# 3. Replikaya yazma denemesinin read-only hatasıyla reddedildiğini doğrula
docker exec mysql-replica mysql -uroot -proot_pass_2026 yemekhane -e "UPDATE kullanicilar SET bakiye = 9999 WHERE id = 1;"

# 4. Replika durdurulup başlatıldığında aradaki değişiklikleri yakaladığını doğrula
docker compose stop mysql-replica
docker exec mysql-primary mysql -uroot -proot_pass_2026 yemekhane -e "CALL sp_bakiye_yukle(2, 50.00, 'Replika kapalıyken yükleme');"
docker compose start mysql-replica
docker exec mysql-replica mysql -uroot -proot_pass_2026 yemekhane -e "SELECT id, ad_soyad, bakiye FROM kullanicilar WHERE id = 2;"
```

### Aşama 3 · Redis Önbellek ve Oturum Doğrulaması

```bash
# Redis içindeki tüm anahtarları görüntüle
docker exec redis redis-cli KEYS "*"

# Oturum TTL süresini görüntüle (1800 sn = 30 dk)
docker exec redis redis-cli TTL "oturum:<TOKEN>"
```

### Aşama 4 · Migration Doğrulaması

```bash
# Migration dosyasını kaynakta çalıştır
docker exec -i mysql-primary mysql -uroot -proot_pass_2026 yemekhane < db/04_degisiklik.sql

# Değişikliğin replikaya geçtiğini doğrula
docker exec mysql-replica mysql -uroot -proot_pass_2026 yemekhane -e "SHOW CREATE TABLE rezervasyonlar\G"
docker exec mysql-replica mysql -uroot -proot_pass_2026 yemekhane -e "SHOW CREATE TABLE kullanici_alerjenleri\G"
```

---

## 4. Sık Sorunlar ve Çözümleri

- **Port Çakışması (`8080`, `3307`, `3308`, `6379`):** Başka bir proje bu portları kullanıyorsa `docker ps` ile kontrol edip ilgili container'ı durdurun.
- **Veritabanını Sıfırlama:** Şemayı ve örnek verileri sıfırdan kurmak isterseniz `docker compose down -v && docker compose up -d --build` komutunu çalıştırın.

