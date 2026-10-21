-- ============================================================================
-- Yemekhane Otomasyonu - Veritabanı Şeması (01_sema.sql)
-- İçerik: 12 Tablo, 3 View, 4 Stored Procedure
-- ============================================================================

SET NAMES utf8mb4;
SET CHARACTER SET utf8mb4;

CREATE DATABASE IF NOT EXISTS yemekhane
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_turkish_ci;

USE yemekhane;

-- Replikasyon kullanıcısı (GTID source-replica replikasyonu için)
CREATE USER IF NOT EXISTS 'repl_user'@'%' IDENTIFIED WITH mysql_native_password BY 'repl_pass_2026';
GRANT REPLICATION SLAVE, REPLICATION CLIENT ON *.* TO 'repl_user'@'%';
FLUSH PRIVILEGES;

-- ============================================================================
-- 1. TABLOLAR (12 Adet)
-- ============================================================================

-- 1) kullanicilar: öğrenci, personel, yönetici
-- Kural: bakiye negatif olamaz; öğrenci numarası yalnızca öğrencide bulunur
CREATE TABLE kullanicilar (
    id INT AUTO_INCREMENT PRIMARY KEY,
    ad_soyad VARCHAR(100) NOT NULL,
    eposta VARCHAR(120) NOT NULL UNIQUE,
    sifre_hash VARCHAR(255) NOT NULL,
    rol ENUM('ogrenci', 'personel', 'yonetici') NOT NULL,
    ogrenci_no VARCHAR(20) NULL UNIQUE,
    bakiye DECIMAL(10, 2) NOT NULL DEFAULT 0.00,
    olusturulma_tarihi DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT chk_bakiye_negatif_olamaz CHECK (bakiye >= 0.00),
    CONSTRAINT chk_ogrenci_no_kurali CHECK (
        (rol = 'ogrenci' AND ogrenci_no IS NOT NULL) OR
        (rol <> 'ogrenci' AND ogrenci_no IS NULL)
    )
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_turkish_ci;

-- 2) yemekhaneler: Merkez (400 kişi), Mühendislik (60 kişi)
-- Kural: kapasite rezervasyonu sınırlar
CREATE TABLE yemekhaneler (
    id INT AUTO_INCREMENT PRIMARY KEY,
    ad VARCHAR(100) NOT NULL UNIQUE,
    kapasite INT NOT NULL,
    konum VARCHAR(150) NOT NULL,
    aktif TINYINT(1) NOT NULL DEFAULT 1,
    CONSTRAINT chk_kapasite_pozitif CHECK (kapasite > 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_turkish_ci;

-- 3) ogunler: Kahvaltı, Öğle, Akşam
-- Kural: her öğünün rezervasyon son saati var (07:00, 10:00, 15:00)
CREATE TABLE ogunler (
    id INT AUTO_INCREMENT PRIMARY KEY,
    ad VARCHAR(50) NOT NULL UNIQUE,
    baslangic_saati TIME NOT NULL,
    bitis_saati TIME NOT NULL,
    rezervasyon_son_saat TIME NOT NULL,
    sira INT NOT NULL UNIQUE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_turkish_ci;

-- 4) yemek_kategorileri: 7 kategori, menüde kategori sırasıyla gösterilir
CREATE TABLE yemek_kategorileri (
    id INT AUTO_INCREMENT PRIMARY KEY,
    ad VARCHAR(80) NOT NULL UNIQUE,
    sira INT NOT NULL UNIQUE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_turkish_ci;

-- 5) yemekler: 52 yemek, kalori, vejetaryen bilgisi
CREATE TABLE yemekler (
    id INT AUTO_INCREMENT PRIMARY KEY,
    kategori_id INT NOT NULL,
    ad VARCHAR(120) NOT NULL UNIQUE,
    kalori INT NOT NULL,
    vejetaryen TINYINT(1) NOT NULL DEFAULT 0,
    CONSTRAINT chk_kalori_pozitif CHECK (kalori >= 0),
    CONSTRAINT fk_yemekler_kategori FOREIGN KEY (kategori_id)
        REFERENCES yemek_kategorileri(id) ON UPDATE CASCADE ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_turkish_ci;

-- 6) alerjenler: 14 alerjen
CREATE TABLE alerjenler (
    id INT AUTO_INCREMENT PRIMARY KEY,
    ad VARCHAR(80) NOT NULL UNIQUE,
    aciklama VARCHAR(255) NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_turkish_ci;

-- 7) yemek_alerjenleri: yemek-alerjen eşleşmesi (çoka çok ilişki)
CREATE TABLE yemek_alerjenleri (
    yemek_id INT NOT NULL,
    alerjen_id INT NOT NULL,
    PRIMARY KEY (yemek_id, alerjen_id),
    CONSTRAINT fk_ya_yemek FOREIGN KEY (yemek_id)
        REFERENCES yemekler(id) ON UPDATE CASCADE ON DELETE CASCADE,
    CONSTRAINT fk_ya_alerjen FOREIGN KEY (alerjen_id)
        REFERENCES alerjenler(id) ON UPDATE CASCADE ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_turkish_ci;

-- 8) menuler: yemekhane + tarih + öğün başına tek menü; öğrenci ve personel fiyatı ayrı
CREATE TABLE menuler (
    id INT AUTO_INCREMENT PRIMARY KEY,
    yemekhane_id INT NOT NULL,
    ogun_id INT NOT NULL,
    tarih DATE NOT NULL,
    ogrenci_fiyati DECIMAL(10, 2) NOT NULL,
    personel_fiyati DECIMAL(10, 2) NOT NULL,
    olusturulma_tarihi DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY uk_yemekhane_tarih_ogun (yemekhane_id, tarih, ogun_id),
    CONSTRAINT chk_fiyatlar_pozitif CHECK (ogrenci_fiyati >= 0 AND personel_fiyati >= 0),
    CONSTRAINT fk_menuler_yemekhane FOREIGN KEY (yemekhane_id)
        REFERENCES yemekhaneler(id) ON UPDATE CASCADE ON DELETE RESTRICT,
    CONSTRAINT fk_menuler_ogun FOREIGN KEY (ogun_id)
        REFERENCES ogunler(id) ON UPDATE CASCADE ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_turkish_ci;

-- 9) menu_yemekleri: menüdeki yemeklerin eşleşmesi
CREATE TABLE menu_yemekleri (
    menu_id INT NOT NULL,
    yemek_id INT NOT NULL,
    PRIMARY KEY (menu_id, yemek_id),
    CONSTRAINT fk_my_menu FOREIGN KEY (menu_id)
        REFERENCES menuler(id) ON UPDATE CASCADE ON DELETE CASCADE,
    CONSTRAINT fk_my_yemek FOREIGN KEY (yemek_id)
        REFERENCES yemekler(id) ON UPDATE CASCADE ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_turkish_ci;

-- 10) rezervasyonlar: aktif, kullanıldı, iptal; kişi başına menü başına tek kayıt
CREATE TABLE rezervasyonlar (
    id INT AUTO_INCREMENT PRIMARY KEY,
    kullanici_id INT NOT NULL,
    menu_id INT NOT NULL,
    durum ENUM('aktif', 'kullanildi', 'iptal') NOT NULL DEFAULT 'aktif',
    ucret DECIMAL(10, 2) NOT NULL,
    olusturulma_tarihi DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    guncellenme_tarihi DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uk_kullanici_menu (kullanici_id, menu_id),
    CONSTRAINT chk_ucret_pozitif CHECK (ucret >= 0),
    CONSTRAINT fk_rez_kullanici FOREIGN KEY (kullanici_id)
        REFERENCES kullanicilar(id) ON UPDATE CASCADE ON DELETE RESTRICT,
    CONSTRAINT fk_rez_menu FOREIGN KEY (menu_id)
        REFERENCES menuler(id) ON UPDATE CASCADE ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_turkish_ci;

-- 11) bakiye_hareketleri: yükleme, rezervasyon, iade
-- Kural: bakiye her zaman hareketlerin toplamına eşittir (tutar: yükleme/iade +, rezervasyon -)
CREATE TABLE bakiye_hareketleri (
    id INT AUTO_INCREMENT PRIMARY KEY,
    kullanici_id INT NOT NULL,
    tur ENUM('yukleme', 'rezervasyon', 'iade') NOT NULL,
    tutar DECIMAL(10, 2) NOT NULL,
    rezervasyon_id INT NULL,
    aciklama VARCHAR(255) NOT NULL,
    islem_tarihi DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_bh_kullanici FOREIGN KEY (kullanici_id)
        REFERENCES kullanicilar(id) ON UPDATE CASCADE ON DELETE RESTRICT,
    CONSTRAINT fk_bh_rezervasyon FOREIGN KEY (rezervasyon_id)
        REFERENCES rezervasyonlar(id) ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_turkish_ci;

-- 12) degerlendirmeler: menüye 1–5 puan ve yorum; kişi başına menü başına tek oy
CREATE TABLE degerlendirmeler (
    id INT AUTO_INCREMENT PRIMARY KEY,
    kullanici_id INT NOT NULL,
    menu_id INT NOT NULL,
    puan TINYINT NOT NULL,
    yorum VARCHAR(500) NULL,
    olusturulma_tarihi DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY uk_degerlendirme_kullanici_menu (kullanici_id, menu_id),
    CONSTRAINT chk_puan_araligi CHECK (puan BETWEEN 1 AND 5),
    CONSTRAINT fk_deg_kullanici FOREIGN KEY (kullanici_id)
        REFERENCES kullanicilar(id) ON UPDATE CASCADE ON DELETE RESTRICT,
    CONSTRAINT fk_deg_menu FOREIGN KEY (menu_id)
        REFERENCES menuler(id) ON UPDATE CASCADE ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_turkish_ci;

-- ============================================================================
-- 2. GÖRÜNÜMLER / VIEWS (3 Adet)
-- ============================================================================

-- 1) v_menu_detay: Menüdeki yemekleri kategori sırasıyla, kalori, vejetaryen ve alerjen bilgileriyle listeler
CREATE OR REPLACE VIEW v_menu_detay AS
SELECT
    m.id AS menu_id,
    m.tarih,
    m.yemekhane_id,
    yh.ad AS yemekhane_ad,
    yh.kapasite AS yemekhane_kapasite,
    m.ogun_id,
    o.ad AS ogun_ad,
    o.sira AS ogun_sira,
    o.baslangic_saati,
    o.bitis_saati,
    o.rezervasyon_son_saat,
    m.ogrenci_fiyati,
    m.personel_fiyati,
    y.id AS yemek_id,
    y.ad AS yemek_ad,
    y.kalori,
    y.vejetaryen,
    yk.id AS kategori_id,
    yk.ad AS kategori_ad,
    yk.sira AS kategori_sira,
    COALESCE(GROUP_CONCAT(DISTINCT a.ad ORDER BY a.id SEPARATOR ', '), '') AS alerjenler,
    COALESCE(GROUP_CONCAT(DISTINCT a.id ORDER BY a.id SEPARATOR ','), '') AS alerjen_idleri
FROM menuler m
INNER JOIN yemekhaneler yh ON yh.id = m.yemekhane_id
INNER JOIN ogunler o ON o.id = m.ogun_id
INNER JOIN menu_yemekleri my ON my.menu_id = m.id
INNER JOIN yemekler y ON y.id = my.yemek_id
INNER JOIN yemek_kategorileri yk ON yk.id = y.kategori_id
LEFT JOIN yemek_alerjenleri ya ON ya.yemek_id = y.id
LEFT JOIN alerjenler a ON a.id = ya.alerjen_id
GROUP BY
    m.id, m.tarih, m.yemekhane_id, yh.ad, yh.kapasite,
    m.ogun_id, o.ad, o.sira, o.baslangic_saati, o.bitis_saati, o.rezervasyon_son_saat,
    m.ogrenci_fiyati, m.personel_fiyati,
    y.id, y.ad, y.kalori, y.vejetaryen,
    yk.id, yk.ad, yk.sira;

-- 2) v_menu_doluluk: Menü bazında kapasite, kullanılan/aktif rezervasyon sayısı, boş yer ve toplam kalori
CREATE OR REPLACE VIEW v_menu_doluluk AS
SELECT
    m.id AS menu_id,
    m.tarih,
    m.yemekhane_id,
    yh.ad AS yemekhane_ad,
    yh.kapasite,
    m.ogun_id,
    o.ad AS ogun_ad,
    o.sira AS ogun_sira,
    o.rezervasyon_son_saat,
    m.ogrenci_fiyati,
    m.personel_fiyati,
    COALESCE(rez_ozet.dolu_koltuk, 0) AS dolu_sayisi,
    GREATEST(yh.kapasite - COALESCE(rez_ozet.dolu_koltuk, 0), 0) AS bos_yer_sayisi,
    ROUND((COALESCE(rez_ozet.dolu_koltuk, 0) / yh.kapasite) * 100, 1) AS doluluk_yuzdesi,
    COALESCE(kal_ozet.toplam_kalori, 0) AS toplam_kalori,
    COALESCE(kal_ozet.menu_vejetaryen, 1) AS menu_vejetaryen
FROM menuler m
INNER JOIN yemekhaneler yh ON yh.id = m.yemekhane_id
INNER JOIN ogunler o ON o.id = m.ogun_id
LEFT JOIN (
    SELECT
        menu_id,
        COUNT(*) AS dolu_koltuk
    FROM rezervasyonlar
    WHERE durum IN ('aktif', 'kullanildi')
    GROUP BY menu_id
) rez_ozet ON rez_ozet.menu_id = m.id
LEFT JOIN (
    SELECT
        my.menu_id,
        SUM(y.kalori) AS toplam_kalori,
        MIN(y.vejetaryen) AS menu_vejetaryen
    FROM menu_yemekleri my
    INNER JOIN yemekler y ON y.id = my.yemek_id
    GROUP BY my.menu_id
) kal_ozet ON kal_ozet.menu_id = m.id;

-- 3) v_menu_puan: Menü bazında değerlendirme sayısı ve ortalama puan
CREATE OR REPLACE VIEW v_menu_puan AS
SELECT
    m.id AS menu_id,
    m.tarih,
    m.yemekhane_id,
    yh.ad AS yemekhane_ad,
    m.ogun_id,
    o.ad AS ogun_ad,
    COUNT(d.id) AS degerlendirme_sayisi,
     COALESCE(ROUND(AVG(d.puan), 2), 0.00) AS ortalama_puan
FROM menuler m
INNER JOIN yemekhaneler yh ON yh.id = m.yemekhane_id
INNER JOIN ogunler o ON o.id = m.ogun_id
LEFT JOIN degerlendirmeler d ON d.menu_id = m.id
GROUP BY m.id, m.tarih, m.yemekhane_id, yh.ad, m.ogun_id, o.ad;

-- ============================================================================
-- 3. SAKLI YORDAMLAR / STORED PROCEDURES (4 Adet)
-- Hata durumunda Türkçe mesajla SQLSTATE '45000' döndürür.
-- ============================================================================

DELIMITER $$

-- 1) sp_bakiye_yukle
DROP PROCEDURE IF EXISTS sp_bakiye_yukle$$
CREATE PROCEDURE sp_bakiye_yukle(
    IN p_kullanici_id INT,
    IN p_tutar DECIMAL(10, 2),
    IN p_aciklama VARCHAR(255)
)
BEGIN
    DECLARE v_mevcut_bakiye DECIMAL(10, 2);
    DECLARE v_kullanici_var INT DEFAULT 0;

    IF p_tutar IS NULL OR p_tutar <= 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Yüklenecek tutar 0 TL''den büyük olmalıdır.';
    END IF;

    IF p_tutar > 10000 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Tek seferde en fazla 10.000 TL bakiye yüklenebilir.';
    END IF;

    START TRANSACTION;

    SELECT COUNT(*), MAX(bakiye)
      INTO v_kullanici_var, v_mevcut_bakiye
      FROM kullanicilar
     WHERE id = p_kullanici_id
       FOR UPDATE;

    IF v_kullanici_var = 0 THEN
        ROLLBACK;
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Kullanıcı bulunamadı.';
    END IF;

    UPDATE kullanicilar
       SET bakiye = bakiye + p_tutar
     WHERE id = p_kullanici_id;

    INSERT INTO bakiye_hareketleri (kullanici_id, tur, tutar, rezervasyon_id, aciklama)
    VALUES (
        p_kullanici_id,
        'yukleme',
        p_tutar,
        NULL,
        COALESCE(NULLIF(TRIM(p_aciklama), ''), 'Bakiye yükleme işlemi')
    );

    COMMIT;

    SELECT
        id AS kullanici_id,
        bakiye AS guncel_bakiye
    FROM kullanicilar
    WHERE id = p_kullanici_id;
END$$

-- 2) sp_rezervasyon_yap
DROP PROCEDURE IF EXISTS sp_rezervasyon_yap$$
CREATE PROCEDURE sp_rezervasyon_yap(
    IN p_kullanici_id INT,
    IN p_menu_id INT
)
BEGIN
    DECLARE v_rol VARCHAR(20);
    DECLARE v_bakiye DECIMAL(10, 2);
    DECLARE v_tarih DATE;
    DECLARE v_ogun_ad VARCHAR(50);
    DECLARE v_son_saat TIME;
    DECLARE v_kapasite INT;
    DECLARE v_ogrenci_fiyati DECIMAL(10, 2);
    DECLARE v_personel_fiyati DECIMAL(10, 2);
    DECLARE v_ucret DECIMAL(10, 2);
    DECLARE v_dolu_sayisi INT DEFAULT 0;
    DECLARE v_mevcut_rez_id INT DEFAULT NULL;
    DECLARE v_mevcut_durum VARCHAR(20) DEFAULT NULL;
    DECLARE v_yeni_rez_id INT;

    START TRANSACTION;

    -- Kullanıcı kontrolü ve bakiye kilitleme
    SELECT rol, bakiye
      INTO v_rol, v_bakiye
      FROM kullanicilar
     WHERE id = p_kullanici_id
       FOR UPDATE;

    IF v_rol IS NULL THEN
        ROLLBACK;
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Kullanıcı bulunamadı.';
    END IF;

    -- Menü, yemekhane kapasitesi ve öğün son saati kontrolü
    SELECT m.tarih, o.ad, o.rezervasyon_son_saat, yh.kapasite, m.ogrenci_fiyati, m.personel_fiyati
      INTO v_tarih, v_ogun_ad, v_son_saat, v_kapasite, v_ogrenci_fiyati, v_personel_fiyati
      FROM menuler m
      INNER JOIN yemekhaneler yh ON yh.id = m.yemekhane_id
      INNER JOIN ogunler o ON o.id = m.ogun_id
     WHERE m.id = p_menu_id
       FOR UPDATE;

    IF v_tarih IS NULL THEN
        ROLLBACK;
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Seçilen menü bulunamadı.';
    END IF;

    -- Tarih ve son saat kontrolü
    IF v_tarih < CURDATE() THEN
        ROLLBACK;
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Geçmiş tarihli bir menüye rezervasyon yapılamaz.';
    END IF;

    IF v_tarih = CURDATE() AND CURTIME() > v_son_saat THEN
        ROLLBACK;
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Bu öğün için rezervasyon son saati geçmiştir.';
    END IF;

    -- Kullanıcının bu menüde mevcut rezervasyonu var mı?
    SELECT id, durum
      INTO v_mevcut_rez_id, v_mevcut_durum
      FROM rezervasyonlar
     WHERE kullanici_id = p_kullanici_id
       AND menu_id = p_menu_id
       FOR UPDATE;

    IF v_mevcut_rez_id IS NOT NULL AND v_mevcut_durum IN ('aktif', 'kullanildi') THEN
        ROLLBACK;
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Bu menü için zaten aktif veya kullanılmış bir rezervasyonunuz bulunmaktadır.';
    END IF;

    -- Kapasite kontrolü
    SELECT COUNT(*)
      INTO v_dolu_sayisi
      FROM rezervasyonlar
     WHERE menu_id = p_menu_id
       AND durum IN ('aktif', 'kullanildi');

    IF v_dolu_sayisi >= v_kapasite THEN
        ROLLBACK;
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Seçilen yemekhane ve öğün için kapasite dolmuştur.';
    END IF;

    -- Role göre fiyat belirleme
    IF v_rol = 'ogrenci' THEN
        SET v_ucret = v_ogrenci_fiyati;
    ELSE
        SET v_ucret = v_personel_fiyati;
    END IF;

    -- Bakiye kontrolü
    IF v_bakiye < v_ucret THEN
        ROLLBACK;
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Bakiye yetersiz.';
    END IF;

    -- Daha önce iptal edilmiş kayıt varsa yeniden aktifleştir, yoksa yeni ekle
    IF v_mevcut_rez_id IS NOT NULL AND v_mevcut_durum = 'iptal' THEN
        UPDATE rezervasyonlar
           SET durum = 'aktif',
               ucret = v_ucret,
               olusturulma_tarihi = CURRENT_TIMESTAMP,
               guncellenme_tarihi = CURRENT_TIMESTAMP
         WHERE id = v_mevcut_rez_id;
        SET v_yeni_rez_id = v_mevcut_rez_id;
    ELSE
        INSERT INTO rezervasyonlar (kullanici_id, menu_id, durum, ucret)
        VALUES (p_kullanici_id, p_menu_id, 'aktif', v_ucret);
        SET v_yeni_rez_id = LAST_INSERT_ID();
    END IF;

    -- Bakiyeyi düş ve hareket kaydı ekle
    UPDATE kullanicilar
       SET bakiye = bakiye - v_ucret
     WHERE id = p_kullanici_id;

    INSERT INTO bakiye_hareketleri (kullanici_id, tur, tutar, rezervasyon_id, aciklama)
    VALUES (
        p_kullanici_id,
        'rezervasyon',
        -v_ucret,
        v_yeni_rez_id,
        CONCAT(DATE_FORMAT(v_tarih, '%d.%m.%Y'), ' ', v_ogun_ad, ' menüsü rezervasyonu')
    );

    COMMIT;

    SELECT
        v_yeni_rez_id AS rezervasyon_id,
        v_ucret AS kesilen_ucret,
        (v_bakiye - v_ucret) AS guncel_bakiye;
END$$

-- 3) sp_rezervasyon_iptal
DROP PROCEDURE IF EXISTS sp_rezervasyon_iptal$$
CREATE PROCEDURE sp_rezervasyon_iptal(
    IN p_rezervasyon_id INT,
    IN p_kullanici_id INT
)
BEGIN
    DECLARE v_sahip_id INT;
    DECLARE v_menu_id INT;
    DECLARE v_durum VARCHAR(20);
    DECLARE v_ucret DECIMAL(10, 2);
    DECLARE v_tarih DATE;
    DECLARE v_ogun_ad VARCHAR(50);
    DECLARE v_son_saat TIME;

    START TRANSACTION;

    SELECT r.kullanici_id, r.menu_id, r.durum, r.ucret, m.tarih, o.ad, o.rezervasyon_son_saat
      INTO v_sahip_id, v_menu_id, v_durum, v_ucret, v_tarih, v_ogun_ad, v_son_saat
      FROM rezervasyonlar r
      INNER JOIN menuler m ON m.id = r.menu_id
      INNER JOIN ogunler o ON o.id = m.ogun_id
     WHERE r.id = p_rezervasyon_id
       FOR UPDATE;

    IF v_sahip_id IS NULL OR v_sahip_id <> p_kullanici_id THEN
        ROLLBACK;
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Rezervasyon bulunamadı veya bu işlem için yetkiniz yok.';
    END IF;

    IF v_durum = 'kullanildi' THEN
        ROLLBACK;
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Kullanılmış bir rezervasyon iptal edilemez.';
    END IF;

    IF v_durum = 'iptal' THEN
        ROLLBACK;
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Bu rezervasyon zaten iptal edilmiş.';
    END IF;

    IF v_tarih < CURDATE() OR (v_tarih = CURDATE() AND CURTIME() > v_son_saat) THEN
        ROLLBACK;
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Öğün son saati geçtiği için rezervasyon iptal edilemez.';
    END IF;

    UPDATE rezervasyonlar
       SET durum = 'iptal',
           guncellenme_tarihi = CURRENT_TIMESTAMP
     WHERE id = p_rezervasyon_id;

    UPDATE kullanicilar
       SET bakiye = bakiye + v_ucret
     WHERE id = p_kullanici_id;

    INSERT INTO bakiye_hareketleri (kullanici_id, tur, tutar, rezervasyon_id, aciklama)
    VALUES (
        p_kullanici_id,
        'iade',
        v_ucret,
        p_rezervasyon_id,
        CONCAT(DATE_FORMAT(v_tarih, '%d.%m.%Y'), ' ', v_ogun_ad, ' rezervasyon iptal iadesi')
    );

    COMMIT;

    SELECT
        p_rezervasyon_id AS iptal_edilen_rezervasyon_id,
        v_ucret AS iade_tutari,
        bakiye AS guncel_bakiye
    FROM kullanicilar
    WHERE id = p_kullanici_id;
END$$

-- 4) sp_turnike_gecis
DROP PROCEDURE IF EXISTS sp_turnike_gecis$$
CREATE PROCEDURE sp_turnike_gecis(
    IN p_rezervasyon_id INT
)
BEGIN
    DECLARE v_durum VARCHAR(20);
    DECLARE v_tarih DATE;

    START TRANSACTION;

    SELECT r.durum, m.tarih
      INTO v_durum, v_tarih
      FROM rezervasyonlar r
      INNER JOIN menuler m ON m.id = r.menu_id
     WHERE r.id = p_rezervasyon_id
       FOR UPDATE;

    IF v_durum IS NULL THEN
        ROLLBACK;
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Rezervasyon kaydı bulunamadı.';
    END IF;

    IF v_durum = 'iptal' THEN
        ROLLBACK;
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'İptal edilmiş rezervasyon ile turnikeden geçiş yapılamaz.';
    END IF;

    IF v_durum = 'kullanildi' THEN
        ROLLBACK;
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Bu rezervasyon daha önce kullanılmıştır.';
    END IF;

    UPDATE rezervasyonlar
       SET durum = 'kullanildi',
           guncellenme_tarihi = CURRENT_TIMESTAMP
     WHERE id = p_rezervasyon_id;

    COMMIT;

    SELECT
        p_rezervasyon_id AS rezervasyon_id,
        'kullanildi' AS yeni_durum;
END$$

DELIMITER ;

