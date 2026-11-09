

const AppState = {
    token: localStorage.getItem("yemekhane_token") || null,
    kullanici: null,
    referanslar: {
        yemekhaneler: [],
        ogunler: [],
        yemekler: [],
        alerjenler: [],
        bugun: new Date().toISOString().slice(0, 10)
    },
    rezervasyonlar: [],
    yoneticiRapor: []
};

// Yardımcı: Bildirim göster
function showAlert(message, type = "error") {
    const banner = document.getElementById("alertBanner");
    const text = document.getElementById("alertText");
    banner.className = `alert-banner ${type === "success" ? "alert-success" : "alert-error"}`;
    text.textContent = message;
    banner.classList.remove("hidden");
    window.scrollTo({ top: 0, behavior: "smooth" });
}

function hideAlert() {
    document.getElementById("alertBanner").classList.add("hidden");
}

// Yardımcı: API Fetch sarmalayıcı
async function apiFetch(path, options = {}) {
    const headers = Object.assign({ "Content-Type": "application/json" }, options.headers || {});
    if (AppState.token) {
        headers["Authorization"] = `Bearer ${AppState.token}`;
    }

    const response = await fetch(path, Object.assign({}, options, { headers }));
    let data = null;
    try {
        data = await response.json();
    } catch (_) {
        data = null;
    }

    if (!response.ok) {
        if (response.status === 401 && path !== "/api/auth/login") {
            oturumuTemizle();
        }
        const hataMesaji = (data && (data.hata || data.message)) || `İstek hatası (${response.status})`;
        throw new Error(hataMesaji);
    }

    return data;
}

function formatTL(val) {
    return `${Number(val || 0).toFixed(2)} ₺`;
}

function rolEtiketi(rol) {
    if (rol === "ogrenci") return "Öğrenci";
    if (rol === "personel") return "Personel";
    if (rol === "yonetici") return "Yönetici";
    return rol || "";
}



function oturumuTemizle() {
    AppState.token = null;
    AppState.kullanici = null;
    localStorage.removeItem("yemekhane_token");
    document.getElementById("userHeaderBox").classList.add("hidden");
    document.getElementById("mainNav").classList.add("hidden");
    document.querySelectorAll(".screen").forEach(el => el.classList.add("hidden"));
    document.getElementById("screen-login").classList.remove("hidden");
}

function ustBilgiyiGuncelle() {
    if (!AppState.kullanici) return;
    document.getElementById("userHeaderBox").classList.remove("hidden");
    document.getElementById("mainNav").classList.remove("hidden");
    document.getElementById("headerUserName").textContent = AppState.kullanici.adSoyad;
    document.getElementById("headerUserRole").textContent = rolEtiketi(AppState.kullanici.rol);
    document.getElementById("headerUserBalance").textContent = formatTL(AppState.kullanici.bakiye);

    const yoneticiTab = document.getElementById("navTabYonetici");
    if (AppState.kullanici.rol === "yonetici") {
        yoneticiTab.classList.remove("hidden");
    } else {
        yoneticiTab.classList.add("hidden");
    }
}

async function ekranGoster(screenId) {
    hideAlert();
    document.querySelectorAll(".screen").forEach(el => el.classList.add("hidden"));
    const hedef = document.getElementById(screenId);
    if (hedef) hedef.classList.remove("hidden");

    document.querySelectorAll(".nav-tab").forEach(btn => {
        btn.classList.toggle("active", btn.dataset.screen === screenId);
    });

    try {
        if (screenId === "screen-gunun-menusu") {
            await gununMenusunuYukle();
        } else if (screenId === "screen-haftalik-menu") {
            await haftalikMenuyuYukle();
        } else if (screenId === "screen-rezervasyonlarim") {
            await rezervasyonlarimiYukle();
        } else if (screenId === "screen-bakiye") {
            await bakiyeSayfasiniYukle(false);
        } else if (screenId === "screen-degerlendirme") {
            await degerlendirmeSayfasiniYukle();
        } else if (screenId === "screen-profil") {
            await profilSayfasiniYukle();
        } else if (screenId === "screen-yonetici") {
            await yoneticiSayfasiniYukle();
        }
    } catch (err) {
        showAlert(err.message, "error");
    }
}

async function referanslariYukle() {
    const data = await apiFetch("/api/menuler/referanslar");
    AppState.referanslar = data;

    const yhSeciciler = ["dailyYemekhane", "weeklyYemekhane", "adminYemekhane"];
    yhSeciciler.forEach(id => {
        const sel = document.getElementById(id);
        sel.innerHTML = data.yemekhaneler
            .map(y => `<option value="${y.id}">${y.ad} (Kapasite: ${y.kapasite})</option>`)
            .join("");
    });

    const ogunSeciciler = ["dailyOgun", "adminOgun"];
    ogunSeciciler.forEach(id => {
        const sel = document.getElementById(id);
        sel.innerHTML = data.ogunler
            .map(o => `<option value="${o.id}" ${o.id === 2 ? "selected" : ""}>${o.ad} (Son Saat: ${o.rezervasyonSonSaat})</option>`)
            .join("");
    });

    document.getElementById("dailyTarih").value = data.bugun;
    document.getElementById("weeklyBaslangic").value = data.bugun;
    document.getElementById("adminTarih").value = data.bugun;

    yoneticiYemekSecicisiniOlustur();
}



function kullaniciAlerjenUyarisiBul(yemek) {
    const kullaniciAlerjenler = (AppState.kullanici && AppState.kullanici.alerjenIdleri) || [];
    if (!yemek.alerjenIdleri || yemek.alerjenIdleri.length === 0 || kullaniciAlerjenler.length === 0) {
        return [];
    }
    const eslesenIdler = yemek.alerjenIdleri.filter(id => kullaniciAlerjenler.includes(id));
    if (eslesenIdler.length === 0) return [];

    return AppState.referanslar.alerjenler
        .filter(a => eslesenIdler.includes(a.id))
        .map(a => a.ad);
}

async function gununMenusunuYukle() {
    const yhId = document.getElementById("dailyYemekhane").value || 1;
    const tarih = document.getElementById("dailyTarih").value || AppState.referanslar.bugun;
    const ogunId = document.getElementById("dailyOgun").value || 2;
    const container = document.getElementById("dailyMenuContainer");
    const badge = document.getElementById("dailyCacheBadge");

    try {
        const menu = await apiFetch(`/api/menuler/gunun-menusu?yemekhaneId=${yhId}&tarih=${encodeURIComponent(tarih)}&ogunId=${ogunId}`);
        badge.textContent = menu.onbellekKaynagi === "REDIS"
            ? "⚡ Kaynak: Redis Önbelleği (HIT)"
            : "🗄️ Kaynak: MySQL Replika (MISS -> Redis'e Yazıldı)";

        const kullaniciFiyati = AppState.kullanici && AppState.kullanici.rol === "ogrenci"
            ? menu.ogrenciFiyati
            : menu.personelFiyati;

        const yemekKartlari = menu.yemekler.map(y => {
            const uyarilar = kullaniciAlerjenUyarisiBul(y);
            const tehlikeSinifi = uyarilar.length > 0 ? "dish-allergen-danger" : "";
            const uyariEtiketi = uyarilar.length > 0
                ? `<div class="badge badge-allergen-warn">⚠️ Alerjen Uyarısı: ${uyarilar.join(", ")}</div>`
                : "";

            return `
                <article class="dish-card ${tehlikeSinifi}">
                    <div>
                        <div class="dish-cat">${y.kategoriSira}. ${y.kategoriAd}</div>
                        <div class="dish-title">${y.yemekAd}</div>
                    </div>
                    <div class="dish-meta">
                        <span>${y.kalori} kcal</span>
                        ${y.vejetaryen ? `<span class="badge badge-veg">🌱 Vejetaryen</span>` : ""}
                    </div>
                    <div class="text-muted" style="font-size:0.8rem;">
                        <strong>Alerjenler:</strong> ${y.alerjenler || "Belirtilmedi"}
                    </div>
                    ${uyariEtiketi}
                </article>
            `;
        }).join("");

        container.innerHTML = `
            <div class="card">
                <div class="menu-summary-bar">
                    <div>
                        <strong>${menu.yemekhaneAd}</strong> · ${menu.tarih} · <strong>${menu.ogunAd}</strong>
                        <span class="text-muted">(Son Saat: ${menu.rezervasyonSonSaat})</span>
                    </div>
                    <div class="dish-meta">
                        <span><strong>Toplam Kalori:</strong> ${menu.toplamKalori} kcal</span>
                        ${menu.menuVejetaryen ? `<span class="badge badge-veg">🌱 Tamamı Vejetaryen Menü</span>` : ""}
                        <span><strong>Boş Yer:</strong> ${menu.bosYerSayisi} / ${menu.kapasite}</span>
                        <span><strong>Ücretiniz:</strong> ${formatTL(kullaniciFiyati)}</span>
                        <span>⭐ ${Number(menu.ortalamaPuan).toFixed(1)} (${menu.degerlendirmeSayisi} oy)</span>
                    </div>
                </div>
                <div class="dishes-grid">
                    ${yemekKartlari}
                </div>
            </div>
        `;
    } catch (err) {
        badge.textContent = "Kaynak: -";
        container.innerHTML = `<div class="card"><p class="text-muted">${err.message}</p></div>`;
    }
}


async function haftalikMenuyuYukle() {
    const yhId = document.getElementById("weeklyYemekhane").value || 1;
    const baslangic = document.getElementById("weeklyBaslangic").value || AppState.referanslar.bugun;
    const grid = document.getElementById("weeklyMenuGrid");

    const menuler = await apiFetch(`/api/menuler/haftalik?yemekhaneId=${yhId}&baslangicTarih=${encodeURIComponent(baslangic)}&gunSayisi=7`);
    if (!menuler || menuler.length === 0) {
        grid.innerHTML = `<div class="card"><p class="text-muted">Seçilen tarih aralığında menü bulunamadı.</p></div>`;
        return;
    }

    const ogrenciMi = AppState.kullanici && AppState.kullanici.rol === "ogrenci";

    grid.innerHTML = menuler.map(m => {
        const tamFiyat = ogrenciMi ? m.ogrenciFiyati : m.personelFiyati;
        const yarimFiyat = (Number(tamFiyat) * 0.60).toFixed(2);
        const pasifMi = m.sonSaatGectiMi || m.bosYerSayisi <= 0;
        const dugmeMetni = m.sonSaatGectiMi
            ? "Son Saat Geçti (Pasif)"
            : (m.bosYerSayisi <= 0 ? "Kapasite Dolu" : "Rezervasyon Yap");

        const yemekListesiHtml = m.yemekler.map(y => {
            const uyarilar = kullaniciAlerjenUyarisiBul(y);
            const uyariSimge = uyarilar.length > 0
                ? `<span class="badge badge-allergen-warn" title="Alerjen Uyarısı: ${uyarilar.join(", ")}">⚠️ ${uyarilar.join(", ")}</span>`
                : "";
            return `
                <li>
                    <span><strong>${y.kategoriAd}:</strong> ${y.yemekAd} ${uyariSimge}</span>
                    <span class="text-muted">${y.kalori} kcal</span>
                </li>
            `;
        }).join("");

        const porsiyonSecimHtml = m.yarimPorsiyonSecilebilirMi
            ? `
                <div class="portion-selector">
                    <label><input type="radio" name="porsiyon_${m.menuId}" value="tam" checked> Tam (${formatTL(tamFiyat)})</label>
                    <label><input type="radio" name="porsiyon_${m.menuId}" value="yarim"> Yarım %60 (${yarimFiyat} ₺)</label>
                </div>
            `
            : `
                <div class="portion-selector">
                    <span>Kahvaltı: Yalnızca Tam Porsiyon (${formatTL(tamFiyat)})</span>
                </div>
            `;

        return `
            <article class="weekly-card">
                <div>
                    <div class="weekly-card-header">
                        <div>
                            <strong>${m.tarih} · ${m.ogunAd}</strong>
                            <div class="text-muted" style="font-size:0.78rem;">${m.yemekhaneAd} · Son Saat: ${m.rezervasyonSonSaat}</div>
                        </div>
                        <span class="badge ${m.bosYerSayisi > 0 ? "badge-status-aktif" : "badge-allergen-warn"}">
                            Boş: ${m.bosYerSayisi}/${m.kapasite}
                        </span>
                    </div>
                    <ul class="weekly-dishes-list" style="margin-top:0.65rem;">
                        ${yemekListesiHtml}
                    </ul>
                </div>

                <div class="weekly-footer">
                    <div style="display:flex; justify-content:space-between; font-size:0.82rem;" class="text-muted">
                        <span>Toplam: ${m.toplamKalori} kcal</span>
                        <span>Rol Ücreti (${rolEtiketi(AppState.kullanici.rol)}): <strong>${formatTL(tamFiyat)}</strong></span>
                    </div>
                    ${porsiyonSecimHtml}
                    <button type="button"
                            class="btn btn-primary btn-block btn-rezervasyon-yap"
                            data-menu-id="${m.menuId}"
                            ${pasifMi ? "disabled" : ""}>
                        ${dugmeMetni}
                    </button>
                </div>
            </article>
        `;
    }).join("");

    grid.querySelectorAll(".btn-rezervasyon-yap").forEach(btn => {
        btn.addEventListener("click", async () => {
            const menuId = Number(btn.dataset.menuId);
            const seciliRadio = document.querySelector(`input[name="porsiyon_${menuId}"]:checked`);
            const porsiyon = seciliRadio ? seciliRadio.value : "tam";
            await rezervasyonYap(menuId, porsiyon);
        });
    });
}

async function rezervasyonYap(menuId, porsiyon) {
    hideAlert();
    try {
        const sonuc = await apiFetch("/api/rezervasyonlar", {
            method: "POST",
            body: JSON.stringify({ menuId, porsiyon })
        });

        // Rezervasyondan hemen sonra kaynak sunucudan (mysql-primary) okunan güncel bakiyeyi yansıt
        AppState.kullanici.bakiye = sonuc.guncelBakiye;
        ustBilgiyiGuncelle();

        showAlert(
            `${sonuc.mesaj} Kesilen Ücret: ${formatTL(sonuc.kesilenUcret)} (${sonuc.porsiyon === "yarim" ? "Yarım Porsiyon" : "Tam Porsiyon"}). Güncel Bakiye (${sonuc.bakiyeOkumaKaynagi}): ${formatTL(sonuc.guncelBakiye)}`,
            "success"
        );
        await haftalikMenuyuYukle();
    } catch (err) {
        showAlert(err.message, "error");
    }
}



async function rezervasyonlarimiYukle() {
    const container = document.getElementById("reservationsList");
    const liste = await apiFetch("/api/rezervasyonlar");
    AppState.rezervasyonlar = liste;

    if (!liste || liste.length === 0) {
        container.innerHTML = `<p class="text-muted">Henüz kayıtlı bir rezervasyonunuz bulunmamaktadır.</p>`;
        return;
    }

    container.innerHTML = liste.map(r => {
        const porsiyonEtiket = r.porsiyon === "yarim" ? "Yarım Porsiyon (%60)" : "Tam Porsiyon";
        const iptalButon = r.iptalEdilebilirMi
            ? `<button type="button" class="btn btn-danger btn-sm btn-rez-iptal" data-id="${r.id}">İptal Et ve İade Al</button>`
            : "";
        const turnikeButon = r.durum === "aktif"
            ? `<button type="button" class="btn btn-success btn-sm btn-rez-turnike" data-id="${r.id}" title="sp_turnike_gecis çağrısı ile durumu 'kullanıldı' yapar">Turnikeden Geç (Simüle Et)</button>`
            : "";

        return `
            <div class="rez-item">
                <div>
                    <div>
                        <strong>#${r.id} · ${r.tarih} · ${r.ogunAd}</strong>
                        <span class="badge badge-status-${r.durum}">${r.durum.toUpperCase()}</span>
                        <span class="badge badge-cache">${porsiyonEtiket}</span>
                    </div>
                    <div class="text-muted" style="font-size:0.84rem; margin-top:0.25rem;">
                        ${r.yemekhaneAd} · Ücret: <strong>${formatTL(r.ucret)}</strong> · Son İptal Saati: ${r.rezervasyonSonSaat}
                    </div>
                    <div class="text-muted" style="font-size:0.8rem; margin-top:0.2rem;">
                        Menü: ${r.yemekOzeti}
                    </div>
                </div>
                <div class="rez-actions">
                    ${turnikeButon}
                    ${iptalButon}
                </div>
            </div>
        `;
    }).join("");

    container.querySelectorAll(".btn-rez-iptal").forEach(btn => {
        btn.addEventListener("click", async () => {
            try {
                const id = Number(btn.dataset.id);
                const yanit = await apiFetch(`/api/rezervasyonlar/${id}/iptal`, { method: "POST" });
                AppState.kullanici.bakiye = yanit.guncelBakiye;
                ustBilgiyiGuncelle();
                showAlert(yanit.mesaj, "success");
                await rezervasyonlarimiYukle();
            } catch (err) {
                showAlert(err.message, "error");
            }
        });
    });

    container.querySelectorAll(".btn-rez-turnike").forEach(btn => {
        btn.addEventListener("click", async () => {
            try {
                const id = Number(btn.dataset.id);
                const yanit = await apiFetch(`/api/rezervasyonlar/${id}/turnike`, { method: "POST" });
                showAlert(yanit.mesaj, "success");
                await rezervasyonlarimiYukle();
            } catch (err) {
                showAlert(err.message, "error");
            }
        });
    });
}



async function bakiyeSayfasiniYukle(kaynaktan = false) {
    const veri = await apiFetch(`/api/bakiye?kaynaktan=${kaynaktan}`);
    AppState.kullanici.bakiye = veri.guncelBakiye;
    ustBilgiyiGuncelle();

    document.getElementById("balanceCurrentAmount").textContent = formatTL(veri.guncelBakiye);
    document.getElementById("balanceSumCheck").textContent =
        `Hareketlerin Toplamı: ${formatTL(veri.hareketlerToplami)} (Bakiye = Hareketler Toplamı Kuralı Doğrulandı)`;
    document.getElementById("balanceSourceBadge").textContent = `Okuma: ${veri.okumaKaynagi}`;

    const rows = veri.hareketler.map(h => {
        const artiMi = Number(h.tutar) >= 0;
        return `
            <tr>
                <td>#${h.id}</td>
                <td><span class="badge ${artiMi ? "badge-veg" : "badge-status-aktif"}">${h.tur.toUpperCase()}</span></td>
                <td style="font-weight:700; color:${artiMi ? "#15803d" : "#b91c1c"};">
                    ${artiMi ? "+" : ""}${formatTL(h.tutar)}
                </td>
                <td>${h.aciklama}</td>
                <td>${h.islemTarihi}</td>
            </tr>
        `;
    }).join("");

    document.getElementById("balanceHistoryContainer").innerHTML = `
        <table class="data-table">
            <thead>
                <tr>
                    <th>ID</th>
                    <th>Tür</th>
                    <th>Tutar</th>
                    <th>Açıklama</th>
                    <th>Tarih</th>
                </tr>
            </thead>
            <tbody>${rows}</tbody>
        </table>
    `;
}



async function degerlendirmeSayfasiniYukle() {
    const [rezListe, degerlendirmeler] = await Promise.all([
        apiFetch("/api/rezervasyonlar"),
        apiFetch("/api/degerlendirmeler")
    ]);

    const kullanilmisUygun = rezListe.filter(r => r.durum === "kullanildi" && !r.degerlendirildiMi);
    const select = document.getElementById("ratingMenuSelect");

    if (kullanilmisUygun.length === 0) {
        select.innerHTML = `<option value="">Değerlendirilecek kullanılmış menü bulunmuyor</option>`;
    } else {
        select.innerHTML = kullanilmisUygun
            .map(r => `<option value="${r.menuId}">${r.tarih} · ${r.yemekhaneAd} · ${r.ogunAd} (${r.yemekOzeti})</option>`)
            .join("");
    }

    const listEl = document.getElementById("myRatingsList");
    if (!degerlendirmeler || degerlendirmeler.length === 0) {
        listEl.innerHTML = `<p class="text-muted">Henüz bir değerlendirmeniz bulunmuyor.</p>`;
    } else {
        listEl.innerHTML = degerlendirmeler.map(d => `
            <div class="rating-item">
                <div style="display:flex; justify-content:space-between;">
                    <strong>${d.tarih} · ${d.yemekhaneAd} (${d.ogunAd})</strong>
                    <span>${"⭐".repeat(d.puan)} (${d.puan}/5)</span>
                </div>
                <p style="margin-top:0.3rem;">${d.yorum || "<em>Yorum yazılmadı</em>"}</p>
                <small class="text-muted">${d.olusturulmaTarihi}</small>
            </div>
        `).join("");
    }
}


async function profilSayfasiniYukle() {
    const k = AppState.kullanici;
    document.getElementById("profileInfoBox").innerHTML = `
        <div><strong>Ad Soyad:</strong> ${k.adSoyad}</div>
        <div><strong>E-Posta:</strong> ${k.eposta}</div>
        <div><strong>Rol:</strong> ${rolEtiketi(k.rol)}</div>
        ${k.ogrenciNo ? `<div><strong>Öğrenci No:</strong> ${k.ogrenciNo}</div>` : ""}
    `;

    const seciliIdler = k.alerjenIdleri || [];
    const container = document.getElementById("allergenCheckboxes");
    container.innerHTML = AppState.referanslar.alerjenler.map(a => {
        const checked = seciliIdler.includes(a.id) ? "checked" : "";
        return `
            <label class="checkbox-pill">
                <input type="checkbox" name="userAllergen" value="${a.id}" ${checked}>
                <div>
                    <strong>${a.ad}</strong>
                    <div class="text-muted" style="font-size:0.75rem;">${a.aciklama}</div>
                </div>
            </label>
        `;
    }).join("");
}



function yoneticiYemekSecicisiniOlustur(seciliIdler = []) {
    const container = document.getElementById("adminDishesContainer");
    if (!container) return;
    const varsayilan = seciliIdler.length > 0 ? seciliIdler : [1, 9, 21, 29];

    container.innerHTML = AppState.referanslar.yemekler.map(y => {
        const checked = varsayilan.includes(y.id) ? "checked" : "";
        return `
            <label style="display:flex; align-items:center; gap:0.5rem; font-size:0.84rem;">
                <input type="checkbox" name="adminDishItem" value="${y.id}" ${checked}>
                <span><strong>[${y.kategoriAd}]</strong> ${y.ad} (${y.kalori} kcal ${y.vejetaryen ? "· 🌱" : ""})</span>
            </label>
        `;
    }).join("");
}

async function yoneticiSayfasiniYukle() {
    const veri = await apiFetch("/api/yonetici/ozet");
    AppState.yoneticiRapor = veri.menuRaporlari || [];

    const rows = AppState.yoneticiRapor.map(m => `
        <tr>
            <td>#${m.menuId}</td>
            <td>${m.tarih}</td>
            <td>${m.yemekhaneAd}</td>
            <td>${m.ogunAd}</td>
            <td>${m.doluSayisi} / ${m.kapasite} (%${m.dolulukYuzdesi})</td>
            <td>⭐ ${Number(m.ortalamaPuan).toFixed(2)} (${m.degerlendirmeSayisi})</td>
            <td>${formatTL(m.ogrenciFiyati)} / ${formatTL(m.personelFiyati)}</td>
            <td>
                <button type="button" class="btn btn-secondary btn-xs btn-edit-menu" data-id="${m.menuId}">Düzenle</button>
            </td>
        </tr>
    `).join("");

    const tableContainer = document.getElementById("adminReportTable");
    tableContainer.innerHTML = `
        <table class="data-table">
            <thead>
                <tr>
                    <th>ID</th>
                    <th>Tarih</th>
                    <th>Yemekhane</th>
                    <th>Öğün</th>
                    <th>Doluluk</th>
                    <th>Ortalama Puan</th>
                    <th>Öğr / Per</th>
                    <th>İşlem</th>
                </tr>
            </thead>
            <tbody>${rows}</tbody>
        </table>
    `;

    tableContainer.querySelectorAll(".btn-edit-menu").forEach(btn => {
        btn.addEventListener("click", () => {
            const id = Number(btn.dataset.id);
            const m = AppState.yoneticiRapor.find(x => x.menuId === id);
            if (!m) return;

            document.getElementById("adminMenuId").value = m.menuId;
            document.getElementById("adminYemekhane").value = m.yemekhaneId;
            document.getElementById("adminOgun").value = m.ogunId;
            document.getElementById("adminTarih").value = m.tarih;
            document.getElementById("adminOgrenciFiyat").value = m.ogrenciFiyati;
            document.getElementById("adminPersonelFiyat").value = m.personelFiyati;
            yoneticiYemekSecicisiniOlustur(m.yemekIdleri || []);
            document.getElementById("btnSaveAdminMenu").textContent = `Menü #${m.menuId} Güncelle ve Redis Kaydını Sil`;
            window.scrollTo({ top: 0, behavior: "smooth" });
        });
    });
}



document.addEventListener("DOMContentLoaded", async () => {
    document.getElementById("alertClose").addEventListener("click", hideAlert);

    // Hızlı giriş butonları
    document.querySelectorAll(".quick-login").forEach(btn => {
        btn.addEventListener("click", () => {
            document.getElementById("loginEmail").value = btn.dataset.email;
            document.getElementById("loginPassword").value = "123456";
        });
    });

    // Giriş formu
    document.getElementById("loginForm").addEventListener("submit", async e => {
        e.preventDefault();
        hideAlert();
        try {
            const eposta = document.getElementById("loginEmail").value;
            const sifre = document.getElementById("loginPassword").value;
            const yanit = await apiFetch("/api/auth/login", {
                method: "POST",
                body: JSON.stringify({ eposta, sifre })
            });

            AppState.token = yanit.token;
            AppState.kullanici = yanit.kullanici;
            localStorage.setItem("yemekhane_token", yanit.token);

            ustBilgiyiGuncelle();
            await referanslariYukle();
            await ekranGoster("screen-gunun-menusu");
        } catch (err) {
            showAlert(err.message, "error");
        }
    });

    // Çıkış butonu
    document.getElementById("btnLogout").addEventListener("click", async () => {
        try {
            await apiFetch("/api/auth/logout", { method: "POST" });
        } catch (_) {}
        oturumuTemizle();
    });

    // Navigasyon sekmeleri
    document.querySelectorAll(".nav-tab").forEach(btn => {
        btn.addEventListener("click", () => ekranGoster(btn.dataset.screen));
    });

    // Günün menüsü filtreleme
    document.getElementById("btnLoadDailyMenu").addEventListener("click", () => gununMenusunuYukle());

    // Haftalık menü filtreleme
    document.getElementById("btnLoadWeeklyMenu").addEventListener("click", () => haftalikMenuyuYukle());

    // Rezervasyon yenileme
    document.getElementById("btnRefreshReservations").addEventListener("click", () => rezervasyonlarimiYukle());

    // Hızlı bakiye tutarları
    document.querySelectorAll(".quick-amount").forEach(btn => {
        btn.addEventListener("click", () => {
            document.getElementById("topupAmount").value = btn.dataset.amount;
        });
    });

    // Bakiye yükleme formu
    document.getElementById("topupForm").addEventListener("submit", async e => {
        e.preventDefault();
        hideAlert();
        try {
            const tutar = Number(document.getElementById("topupAmount").value);
            const aciklama = document.getElementById("topupNote").value;
            const yanit = await apiFetch("/api/bakiye/yukle", {
                method: "POST",
                body: JSON.stringify({ tutar, aciklama })
            });
            showAlert(yanit.mesaj, "success");
            await bakiyeSayfasiniYukle(true);
        } catch (err) {
            showAlert(err.message, "error");
        }
    });

    // Değerlendirme formu
    document.getElementById("ratingForm").addEventListener("submit", async e => {
        e.preventDefault();
        hideAlert();
        try {
            const menuId = Number(document.getElementById("ratingMenuSelect").value);
            if (!menuId) {
                showAlert("Değerlendirilecek kullanılmış bir menü seçmelisiniz.", "error");
                return;
            }
            const puan = Number(document.getElementById("ratingScore").value);
            const yorum = document.getElementById("ratingComment").value;

            const yanit = await apiFetch("/api/degerlendirmeler", {
                method: "POST",
                body: JSON.stringify({ menuId, puan, yorum })
            });
            document.getElementById("ratingComment").value = "";
            showAlert(yanit.mesaj, "success");
            await degerlendirmeSayfasiniYukle();
        } catch (err) {
            showAlert(err.message, "error");
        }
    });

    // Alerjen tercihleri formu
    document.getElementById("allergenForm").addEventListener("submit", async e => {
        e.preventDefault();
        hideAlert();
        try {
            const secililer = Array.from(document.querySelectorAll('input[name="userAllergen"]:checked'))
                .map(cb => Number(cb.value));

            const yanit = await apiFetch("/api/profil/alerjenler", {
                method: "PUT",
                body: JSON.stringify({ alerjenIdleri: secililer })
            });
            AppState.kullanici.alerjenIdleri = yanit.alerjenIdleri;
            showAlert(yanit.mesaj, "success");
        } catch (err) {
            showAlert(err.message, "error");
        }
    });

    // Yönetici form sıfırlama
    document.getElementById("btnResetAdminForm").addEventListener("click", () => {
        document.getElementById("adminMenuId").value = "";
        document.getElementById("btnSaveAdminMenu").textContent = "Menüyü Kaydet ve Önbelleği Yenile";
        yoneticiYemekSecicisiniOlustur([1, 9, 21, 29]);
    });

    // Yönetici menü kaydetme/güncelleme
    document.getElementById("adminMenuForm").addEventListener("submit", async e => {
        e.preventDefault();
        hideAlert();
        try {
            const menuId = document.getElementById("adminMenuId").value;
            const yemekIdleri = Array.from(document.querySelectorAll('input[name="adminDishItem"]:checked'))
                .map(cb => Number(cb.value));

            const govde = {
                yemekhaneId: Number(document.getElementById("adminYemekhane").value),
                ogunId: Number(document.getElementById("adminOgun").value),
                tarih: document.getElementById("adminTarih").value,
                ogrenciFiyati: Number(document.getElementById("adminOgrenciFiyat").value),
                personelFiyati: Number(document.getElementById("adminPersonelFiyat").value),
                yemekIdleri
            };

            const url = menuId ? `/api/yonetici/menu/${menuId}` : "/api/yonetici/menu";
            const method = menuId ? "PUT" : "POST";

            const yanit = await apiFetch(url, {
                method,
                body: JSON.stringify(govde)
            });

            showAlert(yanit.mesaj, "success");
            await yoneticiSayfasiniYukle();
        } catch (err) {
            showAlert(err.message, "error");
        }
    });

    // Yönetici tüm menü önbelleğini temizleme
    document.getElementById("btnFlushMenuCache").addEventListener("click", async () => {
        try {
            const yanit = await apiFetch("/api/yonetici/cache-temizle", { method: "POST" });
            showAlert(yanit.mesaj, "success");
        } catch (err) {
            showAlert(err.message, "error");
        }
    });

    // Sayfa yenilendiğinde veya uygulama container'ı yeniden başladığında Redis'teki oturumu koru
    const urlParams = new URLSearchParams(window.location.search);
    const demoScreen = urlParams.get("demoScreen");

    if (demoScreen) {
        if (demoScreen === "screen-login") {
            oturumuTemizle();
            return;
        }
        try {
            const demoEmail = demoScreen === "screen-yonetici"
                ? "admin@kampus.edu.tr"
                : "ahmet.yilmaz@kampus.edu.tr";
            const yanit = await apiFetch("/api/auth/login", {
                method: "POST",
                body: JSON.stringify({ eposta: demoEmail, sifre: "123456" })
            });
            AppState.token = yanit.token;
            AppState.kullanici = yanit.kullanici;
            localStorage.setItem("yemekhane_token", yanit.token);
            ustBilgiyiGuncelle();
            await referanslariYukle();
            await ekranGoster(demoScreen);
            return;
        } catch (_) {}
    }

    if (AppState.token) {
        try {
            const kullanici = await apiFetch("/api/auth/me");
            AppState.kullanici = kullanici;
            ustBilgiyiGuncelle();
            await referanslariYukle();
            await ekranGoster("screen-gunun-menusu");
        } catch (_) {
            oturumuTemizle();
        }
    }
});

