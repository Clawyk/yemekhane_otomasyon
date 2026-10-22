-- ============================================================================
-- Yemekhane Otomasyonu - Örnek Veri (02_ornek_veri.sql)
-- İçerik: 30 kullanıcı, 7 kategori, 52 yemek, 14 alerjen, 84 menü,
--         365 rezervasyon, bakiye hareketleri ve değerlendirmeler.
-- Not: Menü ve işlem tarihleri kurulum gününe (CURDATE()) göre dinamik üretilir.
-- Tüm kullanıcıların varsayılan şifresi: 123456
-- ============================================================================

SET NAMES utf8mb4;
SET CHARACTER SET utf8mb4;

USE yemekhane;

-- 1) Yemekhaneler (2 adet: Merkez 400 kişi, Mühendislik 60 kişi)
INSERT INTO yemekhaneler (id, ad, kapasite, konum, aktif) VALUES
(1, 'Merkez Yemekhane', 400, 'Merkez Kampüs Öğrenci Yaşam Merkezi Binası', 1),
(2, 'Mühendislik Yemekhanesi', 60, 'Mühendislik Fakültesi B Blok Zemin Kat', 1);

-- 2) Öğünler (3 adet: Kahvaltı 07:00, Öğle 10:00, Akşam 15:00)
INSERT INTO ogunler (id, ad, baslangic_saati, bitis_saati, rezervasyon_son_saat, sira) VALUES
(1, 'Kahvaltı', '07:30:00', '09:30:00', '07:00:00', 1),
(2, 'Öğle',     '11:30:00', '14:00:00', '10:00:00', 2),
(3, 'Akşam',    '17:00:00', '19:30:00', '15:00:00', 3);

-- 3) Yemek Kategorileri (7 adet - menüde kategori sırasıyla gösterilir)
INSERT INTO yemek_kategorileri (id, ad, sira) VALUES
(1, 'Çorba', 1),
(2, 'Ana Yemek', 2),
(3, 'Yardımcı Yemek', 3),
(4, 'Salata ve Meze', 4),
(5, 'Tatlı ve Meyve', 5),
(6, 'Kahvaltılık', 6),
(7, 'İçecek', 7);

-- 4) Alerjenler (14 adet)
INSERT INTO alerjenler (id, ad, aciklama) VALUES
(1,  'Gluten',                 'Buğday, çavdar, arpa, yulaf ve türevleri'),
(2,  'Süt ve Süt Ürünleri',    'Laktoz, peynir, tereyağı, yoğurt, krema'),
(3,  'Yumurta',                'Yumurta ve yumurta içeren ürünler'),
(4,  'Soya',                   'Soya fasulyesi ve soya bazlı soslar'),
(5,  'Yer Fıstığı',            'Yer fıstığı ve ürünleri'),
(6,  'Sert Kabuklu Yemişler',  'Ceviz, fındık, badem, antep fıstığı'),
(7,  'Balık',                  'Balık ve balık ürünleri'),
(8,  'Kabuklu Deniz Ürünleri', 'Karides, yengeç, ıstakoz vb.'),
(9,  'Yumuşakçalar',           'Midye, kalamar, ahtapot vb.'),
(10, 'Kereviz',                'Kereviz sapı, kökü ve tohumu'),
(11, 'Hardal',                 'Hardal tohumu ve sosu'),
(12, 'Susam',                  'Susam tohumu, tahin ve susam yağı'),
(13, 'Kükürt Dioksit / Sülfit','Kurutulmuş meyveler ve koruyucu sülfitler'),
(14, 'Acı Bakla',              'Acı bakla unu ve tohumu');

-- 5) Yemekler (52 adet: kalori ve vejetaryen bilgisiyle)
INSERT INTO yemekler (id, kategori_id, ad, kalori, vejetaryen) VALUES
-- Kategori 1: Çorba (8 yemek)
(1,  1, 'Süzme Mercimek Çorbası',    185, 1),
(2,  1, 'Ezogelin Çorbası',          195, 1),
(3,  1, 'Yayla Çorbası',             165, 1),
(4,  1, 'Domates Çorbası',           150, 1),
(5,  1, 'Tarhana Çorbası',           175, 1),
(6,  1, 'Tavuk Suyu Çorbası',        190, 0),
(7,  1, 'Brokoli Çorbası',           140, 1),
(8,  1, 'Kremalı Mantar Çorbası',    170, 1),
-- Kategori 2: Ana Yemek (12 yemek)
(9,  2, 'Orman Kebabı',              380, 0),
(10, 2, 'İzmir Köfte',               395, 0),
(11, 2, 'Tavuk Sote',                320, 0),
(12, 2, 'Etli Nohut Yemeği',         350, 0),
(13, 2, 'Kıymalı Karnıyarık',        360, 0),
(14, 2, 'Fırında Somon Fileto',      340, 0),
(15, 2, 'Zeytinyağlı Taze Fasulye',  220, 1),
(16, 2, 'Sebzeli Güveç',             240, 1),
(17, 2, 'Kuru Fasulye (Etsiz)',      290, 1),
(18, 2, 'Mantarlı Tavuk Köri',       355, 0),
(19, 2, 'Hünkar Beğendi',            420, 0),
(20, 2, 'Zeytinyağlı Enginar',       210, 1),
-- Kategori 3: Yardımcı Yemek (8 yemek)
(21, 3, 'Şehriyeli Pirinç Pilavı',   260, 1),
(22, 3, 'Meyhane Pilavı (Bulgur)',   245, 1),
(23, 3, 'Fırın Makarna',             310, 1),
(24, 3, 'Peynirli Su Böreği',        330, 1),
(25, 3, 'Cevizli Erişte',            285, 1),
(26, 3, 'Patates Püresi',            215, 1),
(27, 3, 'Sebzeli Kuskus',            250, 1),
(28, 3, 'Domates Soslu Spagetti',    270, 1),
-- Kategori 4: Salata ve Meze (6 yemek)
(29, 4, 'Mevsim Salata',             85,  1),
(30, 4, 'Çoban Salata',              95,  1),
(31, 4, 'Cacık',                     115, 1),
(32, 4, 'Haydari',                   145, 1),
(33, 4, 'Humus',                     165, 1),
(34, 4, 'Közlenmiş Kırmızı Biber',   105, 1),
-- Kategori 5: Tatlı ve Meyve (7 yemek)
(35, 5, 'Fırın Sütlaç',              275, 1),
(36, 5, 'Cevizli Baklava',           390, 1),
(37, 5, 'Kemalpaşa Tatlısı',         335, 1),
(38, 5, 'Kazandibi',                 265, 1),
(39, 5, 'Mevsim Meyvesi (Elma/Muz)', 110, 1),
(40, 5, 'İrmik Helvası',             310, 1),
(41, 5, 'Kayısı Hoşafı',             155, 1),
-- Kategori 6: Kahvaltılık (7 yemek)
(42, 6, 'Beyaz Peynir ve Kaşar Tabağı', 230, 1),
(43, 6, 'Haşlanmış Yumurta',         155, 1),
(44, 6, 'Menemen',                   245, 1),
(45, 6, 'Siyah ve Yeşil Zeytin',     120, 1),
(46, 6, 'Bal ve Tereyağı Porsiyon',  195, 1),
(47, 6, 'Sıcak Simit / Poğaça',      280, 1),
(48, 6, 'Fındık Ezmesi ve Reçel',    210, 1),
-- Kategori 7: İçecek (4 yemek)
(49, 7, 'Ayran',                     90,  1),
(50, 7, 'Demleme Siyah Çay',         10,  1),
(51, 7, 'Taze Sıkma Portakal Suyu',  115, 1),
(52, 7, 'Şalgam Suyu',               45,  1);

-- 6) Yemek-Alerjen Eşleşmeleri (çoka çok ilişki)
INSERT INTO yemek_alerjenleri (yemek_id, alerjen_id) VALUES
-- Çorbalar
(1, 1), (1, 2),
(2, 1), (2, 2),
(3, 1), (3, 2), (3, 3),
(4, 1), (4, 2),
(5, 1), (5, 2),
(6, 1), (6, 10),
(7, 2),
(8, 1), (8, 2),
-- Ana Yemekler
(9, 10),
(10, 1), (10, 3),
(14, 7),
(16, 10),
(18, 2), (18, 11),
(19, 1), (19, 2),
(20, 10),
-- Yardımcı Yemekler
(21, 1), (21, 2),
(22, 1), (22, 2),
(23, 1), (23, 2), (23, 3),
(24, 1), (24, 2), (24, 3),
(25, 1), (25, 2), (25, 3), (25, 6),
(26, 2),
(27, 1), (27, 2),
(28, 1),
-- Salata ve Mezeler
(29, 11),
(31, 2),
(32, 2),
(33, 12),
-- Tatlılar
(35, 2),
(36, 1), (36, 2), (36, 3), (36, 6),
(37, 1), (37, 2), (37, 3),
(38, 1), (38, 2),
(40, 1), (40, 2), (40, 6),
(41, 13),
-- Kahvaltılıklar ve İçecekler
(42, 2),
(43, 3),
(44, 2), (44, 3),
(46, 2),
(47, 1), (47, 3), (47, 12),
(48, 5), (48, 6),
(49, 2);

-- 7) Kullanıcılar (30 adet: 22 öğrenci, 6 personel, 2 yönetici)
-- Şifre (tümü için): 123456 -> bcrypt: $2a$10$kRoRGMCENLr6O52xCW8Xteg26vlX6iDkAPjkVW2d/xeteHnpi3r46
INSERT INTO kullanicilar (id, ad_soyad, eposta, sifre_hash, rol, ogrenci_no, bakiye) VALUES
(1,  'Ahmet Yılmaz',      'ahmet.yilmaz@kampus.edu.tr',      '$2a$10$kRoRGMCENLr6O52xCW8Xteg26vlX6iDkAPjkVW2d/xeteHnpi3r46', 'ogrenci',  '20231001', 0.00),
(2,  'Zeynep Kaya',       'zeynep.kaya@kampus.edu.tr',       '$2a$10$kRoRGMCENLr6O52xCW8Xteg26vlX6iDkAPjkVW2d/xeteHnpi3r46', 'ogrenci',  '20231002', 0.00),
(3,  'Mehmet Demir',      'mehmet.demir@kampus.edu.tr',      '$2a$10$kRoRGMCENLr6O52xCW8Xteg26vlX6iDkAPjkVW2d/xeteHnpi3r46', 'ogrenci',  '20231003', 0.00),
(4,  'Elif Çelik',        'elif.celik@kampus.edu.tr',        '$2a$10$kRoRGMCENLr6O52xCW8Xteg26vlX6iDkAPjkVW2d/xeteHnpi3r46', 'ogrenci',  '20231004', 0.00),
(5,  'Mustafa Şahin',     'mustafa.sahin@kampus.edu.tr',     '$2a$10$kRoRGMCENLr6O52xCW8Xteg26vlX6iDkAPjkVW2d/xeteHnpi3r46', 'ogrenci',  '20231005', 0.00),
(6,  'Ayşe Yıldız',       'ayse.yildiz@kampus.edu.tr',       '$2a$10$kRoRGMCENLr6O52xCW8Xteg26vlX6iDkAPjkVW2d/xeteHnpi3r46', 'ogrenci',  '20231006', 0.00),
(7,  'Burak Özdemir',     'burak.ozdemir@kampus.edu.tr',     '$2a$10$kRoRGMCENLr6O52xCW8Xteg26vlX6iDkAPjkVW2d/xeteHnpi3r46', 'ogrenci',  '20231007', 0.00),
(8,  'Selin Arslan',      'selin.arslan@kampus.edu.tr',      '$2a$10$kRoRGMCENLr6O52xCW8Xteg26vlX6iDkAPjkVW2d/xeteHnpi3r46', 'ogrenci',  '20231008', 0.00),
(9,  'Emre Doğan',        'emre.dogan@kampus.edu.tr',        '$2a$10$kRoRGMCENLr6O52xCW8Xteg26vlX6iDkAPjkVW2d/xeteHnpi3r46', 'ogrenci',  '20231009', 0.00),
(10, 'Merve Kılıç',       'merve.kilic@kampus.edu.tr',       '$2a$10$kRoRGMCENLr6O52xCW8Xteg26vlX6iDkAPjkVW2d/xeteHnpi3r46', 'ogrenci',  '20231010', 0.00),
(11, 'Caner Aydın',       'caner.aydin@kampus.edu.tr',       '$2a$10$kRoRGMCENLr6O52xCW8Xteg26vlX6iDkAPjkVW2d/xeteHnpi3r46', 'ogrenci',  '20231011', 0.00),
(12, 'Büşra Öztürk',      'busra.ozturk@kampus.edu.tr',      '$2a$10$kRoRGMCENLr6O52xCW8Xteg26vlX6iDkAPjkVW2d/xeteHnpi3r46', 'ogrenci',  '20231012', 0.00),
(13, 'Furkan Koç',        'furkan.koc@kampus.edu.tr',        '$2a$10$kRoRGMCENLr6O52xCW8Xteg26vlX6iDkAPjkVW2d/xeteHnpi3r46', 'ogrenci',  '20231013', 0.00),
(14, 'Gizem Kurt',        'gizem.kurt@kampus.edu.tr',        '$2a$10$kRoRGMCENLr6O52xCW8Xteg26vlX6iDkAPjkVW2d/xeteHnpi3r46', 'ogrenci',  '20231014', 0.00),
(15, 'Hakan Özkan',       'hakan.ozkan@kampus.edu.tr',       '$2a$10$kRoRGMCENLr6O52xCW8Xteg26vlX6iDkAPjkVW2d/xeteHnpi3r46', 'ogrenci',  '20231015', 0.00),
(16, 'İrem Şimşek',       'irem.simsek@kampus.edu.tr',       '$2a$10$kRoRGMCENLr6O52xCW8Xteg26vlX6iDkAPjkVW2d/xeteHnpi3r46', 'ogrenci',  '20231016', 0.00),
(17, 'Kaan Polat',        'kaan.polat@kampus.edu.tr',        '$2a$10$kRoRGMCENLr6O52xCW8Xteg26vlX6iDkAPjkVW2d/xeteHnpi3r46', 'ogrenci',  '20231017', 0.00),
(18, 'Melis Çetin',       'melis.cetin@kampus.edu.tr',       '$2a$10$kRoRGMCENLr6O52xCW8Xteg26vlX6iDkAPjkVW2d/xeteHnpi3r46', 'ogrenci',  '20231018', 0.00),
(19, 'Oğuzhan Erdem',     'oguzhan.erdem@kampus.edu.tr',     '$2a$10$kRoRGMCENLr6O52xCW8Xteg26vlX6iDkAPjkVW2d/xeteHnpi3r46', 'ogrenci',  '20231019', 0.00),
(20, 'Pınar Aksoy',       'pinar.aksoy@kampus.edu.tr',       '$2a$10$kRoRGMCENLr6O52xCW8Xteg26vlX6iDkAPjkVW2d/xeteHnpi3r46', 'ogrenci',  '20231020', 0.00),
(21, 'Serkan Korkmaz',    'serkan.korkmaz@kampus.edu.tr',    '$2a$10$kRoRGMCENLr6O52xCW8Xteg26vlX6iDkAPjkVW2d/xeteHnpi3r46', 'ogrenci',  '20231021', 0.00),
(22, 'Tuğba Yavuz',       'tugba.yavuz@kampus.edu.tr',       '$2a$10$kRoRGMCENLr6O52xCW8Xteg26vlX6iDkAPjkVW2d/xeteHnpi3r46', 'ogrenci',  '20231022', 0.00),
(23, 'Dr. Kemal Güler',   'kemal.guler@kampus.edu.tr',       '$2a$10$kRoRGMCENLr6O52xCW8Xteg26vlX6iDkAPjkVW2d/xeteHnpi3r46', 'personel', NULL,       0.00),
(24, 'Doç. Dr. Derya Şen','derya.sen@kampus.edu.tr',         '$2a$10$kRoRGMCENLr6O52xCW8Xteg26vlX6iDkAPjkVW2d/xeteHnpi3r46', 'personel', NULL,       0.00),
(25, 'Prof. Dr. Tarık Acar','tarik.acar@kampus.edu.tr',      '$2a$10$kRoRGMCENLr6O52xCW8Xteg26vlX6iDkAPjkVW2d/xeteHnpi3r46', 'personel', NULL,       0.00),
(26, 'Arş. Gör. Sinan Tekin','sinan.tekin@kampus.edu.tr',    '$2a$10$kRoRGMCENLr6O52xCW8Xteg26vlX6iDkAPjkVW2d/xeteHnpi3r46', 'personel', NULL,       0.00),
(27, 'Uzman Nalan Bozkurt','nalan.bozkurt@kampus.edu.tr',    '$2a$10$kRoRGMCENLr6O52xCW8Xteg26vlX6iDkAPjkVW2d/xeteHnpi3r46', 'personel', NULL,       0.00),
(28, 'Memur Levent Uçar', 'levent.ucar@kampus.edu.tr',       '$2a$10$kRoRGMCENLr6O52xCW8Xteg26vlX6iDkAPjkVW2d/xeteHnpi3r46', 'personel', NULL,       0.00),
(29, 'Yönetici Murat Avcı','admin@kampus.edu.tr',            '$2a$10$kRoRGMCENLr6O52xCW8Xteg26vlX6iDkAPjkVW2d/xeteHnpi3r46', 'yonetici', NULL,       0.00),
(30, 'Yönetici Seda Karaca','seda.karaca@kampus.edu.tr',     '$2a$10$kRoRGMCENLr6O52xCW8Xteg26vlX6iDkAPjkVW2d/xeteHnpi3r46', 'yonetici', NULL,       0.00);

-- 8) 84 Menü, Menü Yemekleri, 365 Rezervasyon, Bakiye Hareketleri ve Değerlendirmeleri
-- Kurulum gününe göre (CURDATE() - 6 gün ile CURDATE() + 7 gün arası = 14 gün * 2 yemekhane * 3 öğün = 84 menü)
-- üretmek için geçici kurulum yordamı:
DELIMITER $$

DROP PROCEDURE IF EXISTS sp_kurulum_verisi_uret$$
CREATE PROCEDURE sp_kurulum_verisi_uret()
BEGIN
    DECLARE v_gun INT DEFAULT -6;
    DECLARE v_yh INT;
    DECLARE v_ogun INT;
    DECLARE v_tarih DATE;
    DECLARE v_menu_id INT DEFAULT 0;
    DECLARE v_ogr_fiyat DECIMAL(10, 2);
    DECLARE v_per_fiyat DECIMAL(10, 2);

    DECLARE v_corba_id INT;
    DECLARE v_ana_id INT;
    DECLARE v_yardimci_id INT;
    DECLARE v_ek_id INT;

    DECLARE v_k_id INT;
    DECLARE v_rol VARCHAR(20);
    DECLARE v_ucret DECIMAL(10, 2);
    DECLARE v_durum VARCHAR(20);
    DECLARE v_rez_sayac INT DEFAULT 0;
    DECLARE v_rez_id INT;
    DECLARE v_hedef_rez INT;
    DECLARE v_i INT;

    -- A) 84 Menüyü ve her menünün 4 yemeğini oluştur
    WHILE v_gun <= 7 DO
        SET v_tarih = DATE_ADD(CURDATE(), INTERVAL v_gun DAY);
        SET v_yh = 1;

        WHILE v_yh <= 2 DO
            SET v_ogun = 1;

            WHILE v_ogun <= 3 DO
                IF v_ogun = 1 THEN
                    SET v_ogr_fiyat = 20.00;
                    SET v_per_fiyat = 45.00;
                ELSEIF v_ogun = 2 THEN
                    SET v_ogr_fiyat = 30.00;
                    SET v_per_fiyat = 75.00;
                ELSE
                    SET v_ogr_fiyat = 35.00;
                    SET v_per_fiyat = 85.00;
                END IF;

                INSERT INTO menuler (yemekhane_id, ogun_id, tarih, ogrenci_fiyati, personel_fiyati)
                VALUES (v_yh, v_ogun, v_tarih, v_ogr_fiyat, v_per_fiyat);

                SET v_menu_id = LAST_INSERT_ID();

                IF v_ogun = 1 THEN
                    -- Kahvaltı menüsü: 3 kahvaltılık + 1 içecek
                    INSERT INTO menu_yemekleri (menu_id, yemek_id) VALUES
                    (v_menu_id, 42 + MOD(v_menu_id, 2)),
                    (v_menu_id, 44 + MOD(v_menu_id, 2)),
                    (v_menu_id, 46 + MOD(v_menu_id, 3)),
                    (v_menu_id, 50 + MOD(v_menu_id, 2));
                ELSE
                    -- Öğle / Akşam menüsü: 1 Çorba + 1 Ana Yemek + 1 Yardımcı Yemek + 1 Salata/Tatlı
                    SET v_corba_id    = 1  + MOD(v_menu_id + v_yh, 8);
                    SET v_ana_id      = 9  + MOD(v_menu_id * 3 + v_ogun, 12);
                    SET v_yardimci_id = 21 + MOD(v_menu_id + v_ogun, 8);
                    IF MOD(v_menu_id, 2) = 0 THEN
                        SET v_ek_id = 29 + MOD(v_menu_id, 6);
                    ELSE
                        SET v_ek_id = 35 + MOD(v_menu_id, 7);
                    END IF;

                    INSERT INTO menu_yemekleri (menu_id, yemek_id) VALUES
                    (v_menu_id, v_corba_id),
                    (v_menu_id, v_ana_id),
                    (v_menu_id, v_yardimci_id),
                    (v_menu_id, v_ek_id);
                END IF;

                SET v_ogun = v_ogun + 1;
            END WHILE;

            SET v_yh = v_yh + 1;
        END WHILE;

        SET v_gun = v_gun + 1;
    END WHILE;

    -- B) Tüm kullanıcılara başlangıç bakiye yüklemesi ekle (bakiye = hareketlerin toplamı kuralı)
    SET v_k_id = 1;
    WHILE v_k_id <= 30 DO
        INSERT INTO bakiye_hareketleri (kullanici_id, tur, tutar, rezervasyon_id, aciklama, islem_tarihi)
        VALUES (
            v_k_id,
            'yukleme',
            750.00,
            NULL,
            'Dönem başı bakiye yüklemesi',
            DATE_SUB(NOW(), INTERVAL 10 DAY)
        );

        UPDATE kullanicilar SET bakiye = 750.00 WHERE id = v_k_id;
        SET v_k_id = v_k_id + 1;
    END WHILE;

    -- C) 84 menü üzerinde toplam 365 rezervasyon oluştur
    -- İlk 29 menüde 5'er rezervasyon (145), kalan 55 menüde 4'er rezervasyon (220) -> 145 + 220 = 365 rezervasyon
    SET v_menu_id = 1;
    WHILE v_menu_id <= 84 DO
        SELECT tarih, ogrenci_fiyati, personel_fiyati
          INTO v_tarih, v_ogr_fiyat, v_per_fiyat
          FROM menuler
         WHERE id = v_menu_id;

        IF v_menu_id <= 29 THEN
            SET v_hedef_rez = 5;
        ELSE
            SET v_hedef_rez = 4;
        END IF;

        SET v_i = 0;
        WHILE v_i < v_hedef_rez DO
            SET v_k_id = 1 + MOD((v_menu_id * 5) + (v_i * 7), 28); -- 1..28 arası öğrenci ve personel
            SELECT rol INTO v_rol FROM kullanicilar WHERE id = v_k_id;

            IF v_rol = 'ogrenci' THEN
                SET v_ucret = v_ogr_fiyat;
            ELSE
                SET v_ucret = v_per_fiyat;
            END IF;

            IF v_tarih < CURDATE() THEN
                IF MOD(v_menu_id + v_i, 9) = 0 THEN
                    SET v_durum = 'iptal';
                ELSE
                    SET v_durum = 'kullanildi';
                END IF;
            ELSE
                IF MOD(v_menu_id + v_i, 11) = 0 THEN
                    SET v_durum = 'iptal';
                ELSE
                    SET v_durum = 'aktif';
                END IF;
            END IF;

            INSERT INTO rezervasyonlar (kullanici_id, menu_id, durum, ucret, olusturulma_tarihi, guncellenme_tarihi)
            VALUES (
                v_k_id,
                v_menu_id,
                v_durum,
                v_ucret,
                DATE_SUB(CONCAT(v_tarih, ' 08:00:00'), INTERVAL 1 DAY),
                CONCAT(v_tarih, ' 12:30:00')
            );

            SET v_rez_id = LAST_INSERT_ID();
            SET v_rez_sayac = v_rez_sayac + 1;

            -- Rezervasyon kesintisi hareketi
            INSERT INTO bakiye_hareketleri (kullanici_id, tur, tutar, rezervasyon_id, aciklama, islem_tarihi)
            VALUES (
                v_k_id,
                'rezervasyon',
                -v_ucret,
                v_rez_id,
                CONCAT(DATE_FORMAT(v_tarih, '%d.%m.%Y'), ' menü rezervasyonu (#', v_menu_id, ')'),
                DATE_SUB(CONCAT(v_tarih, ' 08:00:00'), INTERVAL 1 DAY)
            );

            -- İptal ise iade hareketi ekle, değilse bakiyeden düş
            IF v_durum = 'iptal' THEN
                INSERT INTO bakiye_hareketleri (kullanici_id, tur, tutar, rezervasyon_id, aciklama, islem_tarihi)
                VALUES (
                    v_k_id,
                    'iade',
                    v_ucret,
                    v_rez_id,
                    CONCAT(DATE_FORMAT(v_tarih, '%d.%m.%Y'), ' rezervasyon iptal iadesi (#', v_menu_id, ')'),
                    DATE_SUB(CONCAT(v_tarih, ' 09:00:00'), INTERVAL 1 DAY)
                );
            ELSE
                UPDATE kullanicilar
                   SET bakiye = bakiye - v_ucret
                 WHERE id = v_k_id;
            END IF;

            -- Geçmişte kullanılmış menüler için örnek değerlendirme ekle
            IF v_durum = 'kullanildi' AND MOD(v_rez_sayac, 2) = 0 THEN
                INSERT INTO degerlendirmeler (kullanici_id, menu_id, puan, yorum, olusturulma_tarihi)
                VALUES (
                    v_k_id,
                    v_menu_id,
                    3 + MOD(v_rez_sayac, 3), -- 3, 4 veya 5 puan
                    ELT(1 + MOD(v_rez_sayac, 5),
                        'Yemekler sıcak ve lezzetliydi, teşekkürler.',
                        'Porsiyonlar doyurucuydu, çorba çok güzeldi.',
                        'Ana yemek başarılıydı ancak sıra biraz uzundu.',
                        'Fiyat/performans açısından gayet memnun kaldım.',
                        'Menü dengeli ve hijyenikti, elinize sağlık.'
                    ),
                    CONCAT(v_tarih, ' 14:15:00')
                );
            END IF;

            SET v_i = v_i + 1;
        END WHILE;

        SET v_menu_id = v_menu_id + 1;
    END WHILE;
END$$

DELIMITER ;

CALL sp_kurulum_verisi_uret();
DROP PROCEDURE IF EXISTS sp_kurulum_verisi_uret;

