# Pucaci: Spesifikasi Main Menu (untuk Agent AI)

Dokumen ini adalah instruksi lengkap untuk membuat main menu game **Pucaci** di Unity. Baca seluruhnya sebelum mulai bekerja.

## 1. Konteks Proyek

- **Nama game:** Pucaci
- **Genre:** Co-op horor-komedi first-person, 1–4 pemain. Pemain adalah petugas ronda malam yang menjaga kampung dari hantu (pocong, tuyul, kuntilanak) sampai fajar. Hantu bereaksi terhadap suara pemain, dan komunikasi senyap memakai kode kentongan.
- **Tone:** Malam kampung yang gelap dan hangat, sedikit konyol. Bukan gore. Hindari simbol keagamaan yang sensitif.
- **Platform:** PC (Windows), target rilis Steam.
- **Bahasa UI:** Bahasa Indonesia.
- **Engine:** Unity 2022.3 LTS (cek `ProjectSettings/ProjectVersion.txt`), Universal Render Pipeline (URP).
- **Paket yang sudah ada:** Input System, Cinemachine, TextMeshPro (aktifkan/import TMP Essentials jika belum).
- **Lokasi repo:** folder proyek Unity saat ini (sudah memakai Git).

## 2. Aturan Kerja (Wajib Dipatuhi)

1. Semua file baru ditaruh di `Assets/_Game/`. **Jangan mengubah, memindahkan, atau menghapus** isi `Assets/Scenes`, `Assets/SharedAssets`, atau aset template Unity lain.
2. **Jangan memasang paket baru** (Package Manager atau Asset Store) tanpa bertanya dulu. Gunakan fitur bawaan Unity, UGUI, dan TextMeshPro.
3. Gunakan **Input System** baru. Pada `EventSystem`, pakai `InputSystemUIInputModule`, bukan `StandaloneInputModule`.
4. Jangan menambahkan kode networking. Fitur Host dan Join dibuat sebagai **stub** (lihat bagian 6).
5. Jangan memakai aset berhak cipta (font, musik, gambar) tanpa lisensi jelas. Pakai placeholder atau aset dengan lisensi bebas (misalnya font berlisensi OFL), dan catat sumbernya di `Assets/_Game/CREDITS_ASSETS.md`.
6. Tidak boleh ada path absolut di kode. Tidak boleh ada error atau warning merah di Console saat selesai.
7. Setiap kali selesai satu bagian, pastikan proyek compile dan scene bisa dimainkan (Play) sebelum lanjut.
8. Kalau ada keputusan yang tidak jelas, **pilih opsi paling sederhana** dan catat asumsinya di bagian akhir laporanmu.

## 3. Struktur Folder yang Diminta

```
Assets/_Game/
  Scenes/
    MainMenu.unity
  Scripts/
    UI/
      MainMenuController.cs
      MenuPanel.cs
      SettingsPanel.cs
      SettingsManager.cs
      ButtonFeedback.cs
      SceneFader.cs
      TitleFlicker.cs
      LampFlicker.cs
    Core/
      IMultiplayerLauncher.cs
      StubMultiplayerLauncher.cs
  Prefabs/UI/
  Audio/
    PucaciMixer.mixer
  CREDITS_ASSETS.md
```

Gunakan namespace `Pucaci.UI` dan `Pucaci.Core`.

## 4. Scene `MainMenu`

**Build Settings:** `MainMenu` harus di index 0. Scene permainan (nama diatur lewat field serialized `gameSceneName`, default `Prototype`) di index 1. Jika scene `Prototype` belum ada, tampilkan peringatan di Console saat tombol dipakai, bukan crash.

**Suasana latar** (sederhana, ringan, tanpa aset eksternal):

- Warna langit gelap biru kehitaman (`#0B0F1A`) dengan fog tipis.
- Satu lampu jalan hangat (`#FFB347`) dengan intensitas berkedip pelan lewat `LampFlicker`.
- Siluet sederhana dari primitif: pos ronda (kubus dan atap), tiang lampu, dan pagar. Boleh hanya Cube dan Cylinder berwarna gelap.
- Kamera diam dengan gerakan sway sangat pelan (opsional).
- Placeholder audio ambience (jangkrik) boleh dikosongkan, tapi siapkan `AudioSource` agar mudah diisi nanti.

**Palet warna UI:**

| Peran | Warna |
|---|---|
| Latar panel | `#0B0F1A` (alpha 85%) |
| Teks utama | `#F2E8D5` |
| Aksen hangat (hover, judul) | `#FFB347` |
| Aksen bahaya (Keluar) | `#C0392B` |
| Tombol nonaktif | `#555555` |

## 5. Struktur Menu dan Layar

### 5.1 Menu Utama

Judul **PUCACI** besar di tengah-atas dengan efek kedip lampu ringan (`TitleFlicker`: alpha atau warna berfluktuasi, jangan terlalu cepat atau mengganggu). Subjudul kecil: "Ronda malam, jangan berisik."

Tombol, berurutan dari atas:

1. **Mabar** (membuka panel Mabar)
2. **Cara Bermain** (membuka panel Cara Bermain)
3. **Pengaturan** (membuka panel Pengaturan)
4. **Kredit** (membuka panel Kredit)
5. **Keluar** (konfirmasi dulu)

Pojok kiri bawah: nomor versi dari `Application.version`.

### 5.2 Panel Mabar

- Tombol **Buat Ruangan (Host)**.
- Input teks **Kode Ruangan** dan tombol **Gabung (Join)**.
- Tombol **Latihan Sendiri** yang langsung memuat scene permainan (single-player).
- Tombol **Kembali**.
- Host dan Join memanggil `IMultiplayerLauncher` (bagian 6). Karena masih stub, tampilkan pesan status kecil di panel: "Fitur online belum tersedia."

### 5.3 Panel Cara Bermain

Teks ringkas dalam Bahasa Indonesia, dibagi 4 poin singkat:

1. Tugasmu: keliling kampung dan isi buku absen ronda sampai fajar.
2. Hantu mendengar suaramu. Semakin keras bicara, semakin dekat mereka datang.
3. Pakai kentongan untuk kode senyap ke teman satu tim.
4. Pos ronda adalah tempat aman, tapi jangan terlalu ribut di sana.

Tombol **Kembali**.

### 5.4 Panel Pengaturan

Tab atau bagian bergulir dengan pengaturan berikut. Simpan semua lewat `PlayerPrefs` melalui `SettingsManager`, terapkan saat game dimulai.

**Audio** (lewat `AudioMixer` dengan parameter ter-expose):

- Volume Utama, Musik, Efek Suara, dan Suara Pemain (untuk voice chat nanti). Slider 0–1, dikonversi ke dB dengan `Mathf.Log10(Mathf.Max(value, 0.0001f)) * 20`.

**Mikrofon:**

- Dropdown pilihan perangkat dari `Microphone.devices`. Jika tidak ada mikrofon, tampilkan "Tidak ada mikrofon terdeteksi".
- Slider **Sensitivitas Suara** (0–1) yang disimpan sebagai nilai ambang. Nilai ini dipakai nanti oleh sistem deteksi suara untuk hantu.
- *(Nice to have)* Meter level mikrofon real-time di panel ini, agar pemain bisa melihat seberapa keras suaranya. Boleh dikerjakan di tahap akhir jika waktu cukup.

**Grafis:**

- Resolusi (dropdown dari `Screen.resolutions`, tanpa duplikat), mode layar (Fullscreen Window, Windowed), VSync, dan kualitas grafis (dari `QualitySettings.names`).

**Kontrol:**

- Slider sensitivitas mouse (disimpan saja, belum dipakai).

Tombol **Terapkan**, **Kembalikan Default**, dan **Kembali**.

### 5.5 Panel Kredit

Teks placeholder: "Pucaci dibuat oleh [nama kamu]". Daftar kredit dibuat mudah diedit lewat field teks di Inspector.

### 5.6 Dialog Konfirmasi Keluar

Panel kecil: "Yakin mau keluar?" dengan tombol **Ya** dan **Batal**. Di Editor, tombol Ya menghentikan play mode (`EditorApplication.isPlaying = false`), di build memanggil `Application.Quit()`.

## 6. Abstraksi Multiplayer (Stub)

Buat antarmuka agar nanti bisa diganti implementasi sungguhan tanpa mengubah UI:

```csharp
namespace Pucaci.Core
{
    public interface IMultiplayerLauncher
    {
        void Host();
        void Join(string roomCode);
        bool IsAvailable { get; }
    }
}
```

`StubMultiplayerLauncher` mengimplementasikannya dengan `IsAvailable = false` dan hanya menulis log. `MainMenuController` memegang referensi lewat field serialized atau inisialisasi sederhana, bukan singleton global yang ribet.

## 7. Perilaku dan Detail UI

- **Navigasi:** semua tombol bisa dioperasikan dengan mouse, keyboard (panah, Enter), dan gamepad. Atur navigasi `Selectable` dengan benar dan pilih tombol pertama saat panel dibuka.
- **Tombol Esc/B:** kembali ke panel sebelumnya. Di menu utama, Esc membuka konfirmasi keluar.
- **Umpan balik tombol (`ButtonFeedback`):** efek hover dan klik sederhana (skala sedikit membesar, warna berubah ke aksen). **Jangan memakai paket tween eksternal**, cukup coroutine atau `Update`. Sediakan field `AudioClip` opsional untuk suara hover dan klik (boleh kosong).
- **Transisi:** `SceneFader` memakai `CanvasGroup` hitam untuk fade out sebelum `SceneManager.LoadSceneAsync`, dan fade in saat menu muncul.
- **Resolusi layar:** Canvas `Scale With Screen Size`, referensi 1920x1080, match 0.5. UI harus tampak baik di 16:9 dan 16:10.
- **Font:** TextMeshPro. Pakai font bawaan sebagai placeholder, atau font berlisensi bebas yang dicatat di `CREDITS_ASSETS.md`.
- **Kursor:** terlihat dan tidak terkunci di menu. Saat masuk game, kursor dikunci oleh sistem permainan (bukan tugas menu ini).

## 8. Kriteria Selesai (Checklist)

- [ ] Scene `MainMenu` ada, di index 0 Build Settings.
- [ ] Semua panel (Menu Utama, Mabar, Cara Bermain, Pengaturan, Kredit, Konfirmasi Keluar) berfungsi dan bisa berpindah dengan benar.
- [ ] Tombol **Latihan Sendiri** memuat scene permainan dengan fade (atau menampilkan peringatan jelas jika scene belum ada).
- [ ] Host dan Join memanggil `IMultiplayerLauncher` stub dan menampilkan pesan status.
- [ ] Pengaturan tersimpan dan terpulihkan setelah game dibuka ulang (cek dengan keluar lalu Play lagi).
- [ ] Dropdown mikrofon terisi dari perangkat nyata atau menampilkan pesan jika tidak ada.
- [ ] Navigasi keyboard dan gamepad bekerja; Esc berfungsi.
- [ ] Console bersih dari error dan warning merah.
- [ ] Tidak ada perubahan pada `Assets/Scenes` dan `Assets/SharedAssets`.
- [ ] `CREDITS_ASSETS.md` mencatat semua aset pihak ketiga (atau menyatakan "tidak ada").

## 9. Urutan Pengerjaan yang Disarankan

1. Buat folder, scene `MainMenu`, `EventSystem` (Input System), dan Canvas dasar.
2. Buat Menu Utama dan perpindahan panel (`MenuPanel`, `MainMenuController`).
3. Buat Panel Mabar, Cara Bermain, Kredit, dan Konfirmasi Keluar.
4. Buat `SettingsManager` dan Panel Pengaturan (audio dulu, lalu mikrofon, grafis, kontrol).
5. Tambahkan `SceneFader`, `ButtonFeedback`, `TitleFlicker`, dan `LampFlicker`.
6. Tambahkan latar 3D sederhana dan pencahayaan.
7. Uji semua poin di checklist, lalu rapikan.

## 10. Laporan Akhir yang Diminta

Setelah selesai, berikan ringkasan singkat:

1. Daftar file yang dibuat.
2. Asumsi yang kamu ambil.
3. Hal yang belum selesai atau perlu keputusan saya.
4. Langkah menjalankan dan mengujinya di Unity.