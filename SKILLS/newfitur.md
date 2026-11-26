## Tugas
Buat fitur inventory untuk game ini. Di dalam game akan ada item yang bisa dipegang dan digunakan player.

Sebelum menulis kode, baca dulu struktur proyek dan konvensi kode yang sudah ada, lalu ikuti gaya yang sama. Karena ini game multiplayer, logika penting (pakai item, damage, efek proteksi) harus dijalankan/divalidasi di server, dan hasilnya disinkronkan ke semua player.

## Aset
Semua model item sudah ada di folder models/items:
kentongan, keris, sesajen (versi baru), totem/jimat, tulup.

## Aturan Inventory
- Setiap player punya 2 slot inventory, jadi bisa menyimpan maksimal 2 item.
- Player hanya boleh memegang (equip) 1 item pada satu waktu. Item yang dipegang adalah salah satu item yang ada di slot inventory.
- Untuk berganti item, player menekan tombol 1 atau 2 sesuai nomor slot.
- Pengecualian: karakter Pak Mamat punya 4 slot, jadi bisa menyimpan 4 item (tombol 1-4).
- Jumlah slot harus dibuat sebagai konfigurasi per karakter, bukan angka yang ditulis langsung di kode, supaya mudah diubah.
- Tangani kasus: inventory penuh, slot kosong, dan menekan tombol slot yang kosong.

## Mekanik Item yang Bisa Dipakai

1. Kentongan
   Dipakai untuk memberi tahu player lain bahwa ada hantu di sekitar pemakainya. Player lain yang berada dalam jangkauan harus menerima peringatan (misalnya suara dan/atau indikator UI).

2. Keris
   Senjata jarak dekat untuk menyerang hantu. Keris TIDAK bisa melukai hantu saat hantu sedang menghilang (tidak terlihat/tidak bisa disentuh). Serangan harus gagal atau tidak memberi damage pada kondisi itu.

3. Totem/Jimat
   Melindungi pemegangnya dari serangan hantu. Selama player memegang (equip) jimat, player kebal terhadap serangan hantu. Kekebalan hilang saat jimat tidak lagi dipegang.

4. Tulup
   Senjata jarak jauh. Player meniup tulup untuk menembakkan peluru ke arah hantu. Pelurunya adalah bawang putih. Buat proyektil dengan deteksi tabrakan terhadap hantu, serta cooldown antar tembakan.

## Sesajen (BUKAN item inventory)
Model sesajen baru ini hanya pengganti model sesajen lama yang kurang bagus. Sesajen tidak bisa diambil, disimpan, atau dipakai player seperti item di atas. Cukup ganti model sesajen lama dengan model baru di tempat sesajen yang sudah ada, tanpa mengubah logika sesajen yang sekarang.

## Yang Perlu Dihasilkan
- Sistem inventory (data slot, equip/switch lewat tombol, batas slot per karakter).
- UI inventory sederhana yang menampilkan slot dan item yang sedang dipegang.
- Implementasi keempat item di atas sesuai mekaniknya.
- Penggantian model sesajen lama dengan yang baru.
- Nilai seperti jangkauan, cooldown, dan damage disimpan sebagai konfigurasi agar mudah di-balance.

## Catatan
Jika ada bagian yang ambigu, pilih solusi paling sederhana yang konsisten dengan aturan di atas, lalu tuliskan asumsi yang kamu ambil di akhir pekerjaan.