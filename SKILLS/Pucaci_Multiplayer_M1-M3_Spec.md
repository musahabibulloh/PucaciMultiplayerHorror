# Pucaci: Spesifikasi Multiplayer Tahap Awal (Milestone 1–3)

Dokumen ini adalah instruksi untuk menambahkan multiplayer dasar ke game **Pucaci** memakai **Mirror**. Ini adalah **uji coba** untuk belajar dan membuktikan konsep, bukan fitur final. Baca seluruhnya sebelum mulai.

## 1. Konteks Proyek

- **Game:** Pucaci, co-op horor-komedi first-person, 1–4 pemain. Petugas ronda menjaga kampung dari hantu sampai fajar.
- **Engine:** Unity 2022.3 LTS (cek `ProjectSettings/ProjectVersion.txt`), URP, Input System, Cinemachine.
- **Scene gameplay:** `Gameplay` (berisi `Ground`, `Moonlight`, `Pos Ronda`, `PlayerCharacter`, `EventSystem`, `GameplayCanvas`, `StreetLamp_1..3`). Peta kampung final sedang dikerjakan orang lain dan akan menggantikan isi scene ini nanti.
- **Karakter:** hanya ada satu model karakter. **Semua pemain memakai prefab karakter yang sama untuk saat ini.**
- **Skrip yang sudah ada:** `PlayerCharacterController.cs` (gerak, lari, lompat, merunduk) dan `PlayerAnimatorController.controller` (parameter Animator seperti `Speed` dan `IsMoving`).
- **Kontrol yang sudah dipakai** (berdasarkan teks kontrol di layar, verifikasi lewat Input Actions): WASD, Shift Kiri, Spasi, Ctrl Kiri/C, F (senter), Q/E (menoleh ke belakang), 1/2/3, Esc.
- **Mirror:** diasumsikan sudah di-import dari Asset Store ke `Assets/Mirror`. Jika folder itu **tidak ada**, **berhenti dan beri tahu saya** (kamu tidak bisa mengunduhnya dari Asset Store sendiri). Jangan menulis ulang atau memodifikasi file Mirror.

## 2. Aturan Kerja (Wajib Dipatuhi)

1. **Langkah 0 adalah audit.** Sebelum mengubah apa pun, baca `PlayerCharacterController.cs`, struktur prefab/hierarchy `PlayerCharacter`, komponen kamera (Camera, Cinemachine, AudioListener), dan Input System yang dipakai. Laporkan temuanmu singkat (apa yang membaca input, di mana kamera berada, bagaimana parameter Animator diatur), lalu lanjut.
2. Kerjakan **satu milestone sekali jalan**. Setelah tiap milestone, pastikan proyek compile, jalankan kriteria selesai, lalu berhenti dan laporkan sebelum lanjut ke milestone berikutnya.
3. **Jangan memasang paket baru** tanpa bertanya. Mirror sudah ada, cukup itu.
4. **Jangan mengubah** scene lain, folder `Assets/Mirror`, atau aset sample/template Unity.
5. **Jangan menjalankan `git commit` atau `git push`.** Saya yang akan commit.
6. Semua skrip baru taruh di `Assets/_Game/Scripts/Network/`, dengan namespace `Pucaci.Network`. Prefab baru di `Assets/_Game/Prefabs/`.
7. Skrip yang sudah ada (`PlayerCharacterController`) boleh dimodifikasi seminimal mungkin. Buat perubahan kecil dan terarah, jangan menulis ulang seluruh file.
8. Tidak boleh ada error atau warning merah di Console. Tidak boleh ada path absolut di kode.
9. Jika ada keputusan yang tidak jelas, **pilih opsi paling sederhana** dan catat asumsinya di laporan.

## 3. Prinsip Desain (Pahami Dulu)

- **Host-client:** satu pemain menjadi host (server sekaligus pemain), pemain lain menjadi klien. Tidak ada dedicated server.
- **Gerakan pemain:** *client authority*. Tiap pemain mengontrol karakternya sendiri dan hasilnya dikirim ke yang lain. Ini paling sederhana untuk co-op kecil.
- **Hal penting yang harus dijaga:** setiap PC menjalankan **semua** salinan karakter. Hanya karakter milik pemain lokal (`isLocalPlayer`) yang boleh membaca input, memakai kamera, dan mengaktifkan UI. Salinan karakter pemain lain harus **pasif** (hanya menerima posisi dan animasi dari jaringan).
- **Event penting** (misalnya kentongan) memakai pola: pemain lokal mengirim `[Command]` ke server, server memeriksa dan menyiarkan lewat `[ClientRpc]` ke semua pemain.
- Hanya satu `NetworkManager` boleh ada. Perhatikan objek `DontDestroyOnLoad` agar tidak ada duplikat saat scene dimuat ulang.

## 4. Milestone 1: Dua Pemain Saling Terlihat

**Tujuan:** host dan klien sama-sama muncul di scene `Gameplay` dan bisa saling melihat bergerak (posisi dan rotasi tersinkron).

Tugas:

1. Buat objek `NetworkManager` di scene `Gameplay` dengan komponen **NetworkManager**, **KcpTransport**, dan **NetworkManagerHUD** (HUD hanya untuk debugging sementara).
2. Kosongkan field **Offline Scene** dan **Online Scene** (tidak perlu pindah scene untuk tes ini).
3. Ubah `PlayerCharacter` menjadi prefab di `Assets/_Game/Prefabs/PlayerCharacter.prefab`. Tambahkan **NetworkIdentity** dan komponen sinkron posisi (**NetworkTransformReliable** atau **NetworkTransformUnreliable**, mana yang tersedia di versi Mirror terpasang) dengan arah sinkron **Client To Server**, agar pemain lokal tidak terasa lag. Jika karakter memakai Rigidbody non-kinematic, aktifkan opsi update yang cocok (FixedUpdate) agar tidak patah-patah.
4. Isi **Player Prefab** pada NetworkManager dengan prefab tadi, lalu **hapus `PlayerCharacter` dari scene** (pemain akan di-spawn otomatis).
5. Buat 3–4 objek kosong dengan komponen **NetworkStartPosition** di dekat `Pos Ronda`, lalu atur NetworkManager agar memakai titik-titik itu (Round Robin).
6. Tambahkan skrip `PlayerNetworkSetup : NetworkBehaviour` pada prefab. Skrip ini:
   - Di `OnStartLocalPlayer()`: mengaktifkan input, kamera, AudioListener, dan UI milik pemain lokal.
   - Di pemain **non-lokal**: menonaktifkan `PlayerCharacterController` (atau minimal cegah membaca input), kamera, AudioListener, komponen `PlayerInput`/handler input, dan semua UI.
   - Sesuaikan dengan temuan audit. Jika kamera Cinemachine berada di luar prefab, buat agar **hanya pemain lokal** yang menjadi `Follow` target-nya saat spawn.
7. *(Nice to have)* Label teks kecil di atas kepala bertuliskan "Pemain {netId}" yang hanya terlihat pada karakter **pemain lain**, agar mudah membedakan saat semua memakai karakter yang sama. Label selalu menghadap kamera.

**Kriteria selesai:**

- [ ] Satu instance sebagai Host (Editor), satu lagi sebagai Client (build) lewat `localhost`, keduanya muncul di titik spawn berbeda.
- [ ] Keyboard di satu jendela **hanya** menggerakkan karakter di jendela itu.
- [ ] Gerakan dan rotasi tiap karakter terlihat mulus di jendela satunya.
- [ ] Hanya ada satu AudioListener dan satu kamera aktif per instance.
- [ ] Menutup klien tidak membuat error di host, dan sebaliknya.

## 5. Milestone 2: Animasi Tersinkron

**Tujuan:** karakter pemain lain terlihat berjalan, berlari, melompat, dan diam dengan animasi yang benar.

Tugas:

1. Tambahkan **NetworkAnimator** pada prefab dengan arah sinkron **Client To Server**, lalu pastikan parameter Animator yang dipakai skrip gerakan (misalnya `Speed`, `IsMoving`, dan parameter lompat/merunduk) ikut tersinkron.
2. Pastikan pemain non-lokal **tidak menjalankan** logika yang mengisi parameter Animator dari input (yang mengisi hanya pemain lokal). Parameter pemain lain datang dari jaringan.
3. Jika NetworkAnimator menimbulkan masalah (misalnya animasi telat atau trigger hilang), sebutkan di laporan dan tawarkan alternatif: sinkron nilai kecepatan dan status dengan `SyncVar`, lalu isi Animator secara lokal di setiap PC.

**Kriteria selesai:**

- [ ] Saat pemain A berlari, di layar pemain B karakter A terlihat berlari (bukan meluncur dalam pose diam).
- [ ] Lompat dan merunduk terlihat di pemain lain.
- [ ] Berhenti berjalan membuat animasi kembali ke diam di semua layar.
- [ ] Tidak ada error di Console terkait parameter Animator.

## 6. Milestone 3: Event Kentongan Tersinkron

**Tujuan:** membuktikan pola `[Command]` dan `[ClientRpc]`. Pemain menekan tombol, **semua** pemain mendengar bunyi kentongan dari posisi pemain itu.

Tugas:

1. Buat skrip `Kentongan : NetworkBehaviour` pada prefab pemain.
2. Tombol: gunakan **`R`** lewat Input Action yang bisa diubah di Inspector (pastikan tidak bentrok dengan kontrol yang sudah ada). Hanya pemain lokal yang membaca tombol ini.
3. Alur:
   - Pemain lokal menekan tombol, memanggil `[Command] CmdPukulKentongan()`.
   - Server memeriksa cooldown (default **0.6 detik**, field di Inspector). Jika lolos, server memanggil `[ClientRpc] RpcBunyiKentongan()`.
   - Di setiap klien, `RpcBunyiKentongan()` memutar suara 3D (spatial blend 1) di posisi pemain pengirim, dan menulis log: `Kentongan dari pemain {netId}`.
4. Aset audio: **jangan memakai aset berhak cipta**. Sediakan field `AudioClip` opsional. Jika kosong, hasilkan bunyi "tok" sederhana lewat `AudioClip.Create` (placeholder) agar bisa diuji tanpa aset.
5. Tambahkan umpan balik visual debug: teks kecil di layar yang muncul 1 detik, "Kentongan! (pemain {netId})", agar bisa diverifikasi walau audio dimatikan.

Pola kode yang diharapkan (contoh, sesuaikan dengan versi Mirror yang terpasang):

```csharp
using Mirror;
using UnityEngine;

namespace Pucaci.Network
{
    public class Kentongan : NetworkBehaviour
    {
        [SerializeField] float cooldown = 0.6f;
        double nextAllowedTime;

        void Update()
        {
            if (!isLocalPlayer) return;
            // baca tombol (Input System), lalu:
            // CmdPukulKentongan();
        }

        [Command]
        void CmdPukulKentongan()
        {
            if (NetworkTime.time < nextAllowedTime) return;
            nextAllowedTime = NetworkTime.time + cooldown;
            RpcBunyiKentongan();
        }

        [ClientRpc]
        void RpcBunyiKentongan()
        {
            // putar suara 3D di posisi pemain ini + log + teks debug
        }
    }
}
```

**Kriteria selesai:**

- [ ] Pemain A menekan `R`, pemain B (dan A sendiri) mendengar bunyi dan melihat teks debug.
- [ ] Menekan `R` berulang sangat cepat tidak membanjiri (cooldown bekerja di server).
- [ ] Event hanya berasal dari pemain yang menekan, bukan dari semua pemain.
- [ ] Tidak ada error di Console.

## 7. Di Luar Cakupan (Jangan Dikerjakan Sekarang)

- Sinkron senter (tombol F), kondisi jongkok, atau status lain di luar yang disebutkan.
- Voice chat dan proximity voice.
- Hantu (AI), timer fajar, absen ronda.
- Integrasi main menu, Steam lobby, dan relay.
- Memindahkan host jika host keluar.
- Skin atau model karakter berbeda untuk tiap pemain.

**Catatan untuk tahap berikutnya (jangan dikerjakan sekarang):** saat main menu dihubungkan nanti, tombol **Latihan Sendiri** sebaiknya memanggil `StartHost()` (host tanpa klien lain), sehingga mode single-player dan multiplayer memakai jalur kode yang sama. Tombol **Host** dan **Join** akan lewat antarmuka `IMultiplayerLauncher` yang sudah disiapkan di proyek.

## 8. Cara Menguji

1. Tambahkan scene `Gameplay` ke **Build Settings**.
2. **Build and Run** satu salinan sebagai klien.
3. Di Editor, tekan Play dan klik **Host** di HUD. Di build, isi alamat `localhost` lalu klik **Client**.
4. Ikuti kriteria selesai tiap milestone.

## 9. Laporan Akhir yang Diminta (Setiap Milestone)

1. Temuan audit (hanya untuk milestone 1): ringkas struktur karakter, kamera, dan input yang ada.
2. Daftar file yang dibuat atau diubah.
3. Asumsi yang kamu ambil.
4. Kriteria selesai yang **sudah terpenuhi** dan yang **belum atau gagal diuji**.
5. Masalah atau pertanyaan yang butuh keputusan saya.
6. Langkah singkat untuk saya mengujinya.
