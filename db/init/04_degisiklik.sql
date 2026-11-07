-- ============================================================================
-- Yemekhane Otomasyonu - Aşama 4 Migration Dosyası (04_degisiklik.sql)
-- Özellik 1: Yarım Porsiyon (Öğle ve akşamda %60 ücret, kahvaltıda yasak,
--            geçmiş rezervasyonlar varsayılan olarak 'tam' porsiyon sayılır)
-- Özellik 2: Kullanıcı Alerjen Tercihleri (kullanici_alerjenleri tablosu)
-- ============================================================================

SET NAMES utf8mb4;
SET CHARACTER SET utf8mb4;

USE yemekhane;

-- 1) rezervasyonlar tablosuna porsiyon sütunu eklenmesi
-- Mevcut veriyi bozmadan tüm geçmiş/mevcut kayıtlar DEFAULT 'tam' olarak işaretlenir.
SET @sutun_var := (
    SELECT COUNT(*)
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = 'yemekhane'
      AND TABLE_NAME = 'rezervasyonlar'
      AND COLUMN_NAME = 'porsiyon'
);

SET @alter_sql := IF(
    @sutun_var = 0,
    'ALTER TABLE rezervasyonlar ADD COLUMN porsiyon ENUM(''tam'', ''yarim'') NOT NULL DEFAULT ''tam'' AFTER durum',
    'SELECT ''porsiyon sutunu zaten mevcut'' AS bilgi'
);

PREPARE stmt FROM @alter_sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- 2) Kullanıcıların profilde seçtiği alerjenleri tutan yeni tablo
CREATE TABLE IF NOT EXISTS kullanici_alerjenleri (
    kullanici_id INT NOT NULL,
    alerjen_id INT NOT NULL,
    olusturulma_tarihi DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (kullanici_id, alerjen_id),
    CONSTRAINT fk_ka_kullanici FOREIGN KEY (kullanici_id)
        REFERENCES kullanicilar(id) ON UPDATE CASCADE ON DELETE CASCADE,
    CONSTRAINT fk_ka_alerjen FOREIGN KEY (alerjen_id)
        REFERENCES alerjenler(id) ON UPDATE CASCADE ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_turkish_ci;

-- Örnek kullanıcı (Ahmet Yılmaz - id:1) için varsayılan alerjen tercihleri (Gluten ve Süt)
INSERT IGNORE INTO kullanici_alerjenleri (kullanici_id, alerjen_id) VALUES
(1, 1),
(1, 2),
(2, 6);

-- 3) sp_rezervasyon_yap saklı yordamının yarım porsiyon kurallarıyla güncellenmesi
-- Kurallar:
--   * Öğle ve akşam menülerinde yarım porsiyon seçilebilir, ücret tam fiyatın %60'ıdır.
--   * Kahvaltıda yarım porsiyon yoktur (SQLSTATE '45000' ile Türkçe hata döndürür).
DELIMITER $$

DROP PROCEDURE IF EXISTS sp_rezervasyon_yap$$
CREATE PROCEDURE sp_rezervasyon_yap(
    IN p_kullanici_id INT,
    IN p_menu_id INT,
    IN p_porsiyon VARCHAR(10)
)
BEGIN
    DECLARE v_rol VARCHAR(20);
    DECLARE v_bakiye DECIMAL(10, 2);
    DECLARE v_tarih DATE;
    DECLARE v_ogun_id INT;
    DECLARE v_ogun_ad VARCHAR(50);
    DECLARE v_son_saat TIME;
    DECLARE v_kapasite INT;
    DECLARE v_ogrenci_fiyati DECIMAL(10, 2);
    DECLARE v_personel_fiyati DECIMAL(10, 2);
    DECLARE v_tam_ucret DECIMAL(10, 2);
    DECLARE v_ucret DECIMAL(10, 2);
    DECLARE v_porsiyon VARCHAR(10);
    DECLARE v_dolu_sayisi INT DEFAULT 0;
    DECLARE v_mevcut_rez_id INT DEFAULT NULL;
    DECLARE v_mevcut_durum VARCHAR(20) DEFAULT NULL;
    DECLARE v_yeni_rez_id INT;

    SET v_porsiyon = COALESCE(NULLIF(LOWER(TRIM(p_porsiyon)), ''), 'tam');

    IF v_porsiyon NOT IN ('tam', 'yarim') THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Geçersiz porsiyon seçimi. Yalnızca tam veya yarım porsiyon seçilebilir.';
    END IF;

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

    -- Menü, yemekhane kapasitesi ve öğün kontrolü
    SELECT m.tarih, m.ogun_id, o.ad, o.rezervasyon_son_saat, yh.kapasite, m.ogrenci_fiyati, m.personel_fiyati
      INTO v_tarih, v_ogun_id, v_ogun_ad, v_son_saat, v_kapasite, v_ogrenci_fiyati, v_personel_fiyati
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

    -- Kahvaltıda yarım porsiyon yasağı
    IF (v_ogun_id = 1 OR v_ogun_ad = 'Kahvaltı') AND v_porsiyon = 'yarim' THEN
        ROLLBACK;
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Kahvaltı öğününde yarım porsiyon seçeneği bulunmamaktadır.';
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

    -- Role ve porsiyona göre fiyat belirleme (Yarım porsiyon = Tam fiyatın %60'ı)
    IF v_rol = 'ogrenci' THEN
        SET v_tam_ucret = v_ogrenci_fiyati;
    ELSE
        SET v_tam_ucret = v_personel_fiyati;
    END IF;

    IF v_porsiyon = 'yarim' THEN
        SET v_ucret = ROUND(v_tam_ucret * 0.60, 2);
    ELSE
        SET v_ucret = v_tam_ucret;
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
               porsiyon = v_porsiyon,
               ucret = v_ucret,
               olusturulma_tarihi = CURRENT_TIMESTAMP,
               guncellenme_tarihi = CURRENT_TIMESTAMP
         WHERE id = v_mevcut_rez_id;
        SET v_yeni_rez_id = v_mevcut_rez_id;
    ELSE
        INSERT INTO rezervasyonlar (kullanici_id, menu_id, durum, porsiyon, ucret)
        VALUES (p_kullanici_id, p_menu_id, 'aktif', v_porsiyon, v_ucret);
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
        CONCAT(
            DATE_FORMAT(v_tarih, '%d.%m.%Y'), ' ', v_ogun_ad,
            ' menüsü rezervasyonu (',
            IF(v_porsiyon = 'yarim', 'Yarım Porsiyon', 'Tam Porsiyon'),
            ')'
        )
    );

    COMMIT;

    SELECT
        v_yeni_rez_id AS rezervasyon_id,
        v_porsiyon AS porsiyon,
        v_ucret AS kesilen_ucret,
        (v_bakiye - v_ucret) AS guncel_bakiye;
END$$

DELIMITER ;
