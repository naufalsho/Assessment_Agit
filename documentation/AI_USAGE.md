# AI_USAGE.md - Engineering Provenance & AI Interaction Summary

Dokumen ini mendokumentasikan peran asistensi AI (Antigravity AI Assistant berbasis Google Gemini) dalam proses arsitektur, implementasi, dan refactoring sistem Access Request Hub MVP.

---

## 1. Top Impactful AI Interactions

### Interaksi 1: Strategi Idempotensi pada Pembuatan Request
- **Ask**: Bagaimana merancang proteksi idempotensi untuk endpoint create request agar tahan terhadap *concurrent retry* dan duplikasi jaringan di level database dan aplikasi?
- **AI Suggestion**: Menggunakan header `Idempotency-Key` atau field payload `ClientRequestId` (UUID), dikombinasikan dengan unique index di EF Core pada tabel `AccessRequests`, serta menangani `DbUpdateException` saat terjadi race condition simultan.
- **Decision**: **Accepted with refinement**.
- **Why**: Mengadopsi saran index unik pada `ClientRequestId`, tetapi menyempurnakan logika domain: jika request dengan `ClientRequestId` yang sama dikirim ulang dengan atribut yang identik, sistem mengembalikan data request yang sudah ada (HTTP 200/201 idempotent result); namun jika payload-nya berbeda, sistem menolaknya dengan `DomainValidationException` untuk mencegah *tampering*.

### Interaksi 2: Optimistic Concurrency Control di SQLite
- **Ask**: Bagaimana mengimplementasikan optimistic concurrency control yang andal di SQLite mengingat SQLite tidak memiliki native `rowversion`/`timestamp` auto-incrementing binary columns seperti SQL Server?
- **AI Suggestion**: Menambahkan properti `[ConcurrencyCheck] public int RowVersion { get; set; } = 1;` pada entity `AccessRequest` dan menaikkan versinya (`RowVersion++`) pada setiap mutasi approval/rejection.
- **Decision**: **Accepted**.
- **Why**: Cara ini 100% *zero-dependency*, portabel lintas SQLite, PostgreSQL, dan SQL Server, serta bekerja secara deterministik di level EF Core maupun perbandingan manual di layer aplikasi.

### Interaksi 3: Server-Side Authorization Boundary vs UI Hiding
- **Ask**: Bagaimana merancang sistem otorisasi simulasi persona demo tanpa mengabaikan batasan keamanan server-side?
- **AI Suggestion**: Buat user switcher di frontend yang mengirimkan identitas requester di payload form JSON/POST untuk setiap aksi approval.
- **Decision**: **Rejected**.
- **Why**: Mengirimkan identitas requester atau approver secara bebas dalam payload request melanggar prinsip *security boundary* (Section 2.6: *"Requester diambil dari current/seeded user, bukan dipercaya dari payload bebas. Authorization wajib server-side"*). Keputusan yang diambil adalah mengelola konteks user aktif melalui `ICurrentUserService` berbasis sesi/cookie server-side (`X-User-Id` untuk API dan session untuk Razor Pages), dan memvalidasi hirarki manager serta kepemilikan sistem secara ketat di backend.

### Interaksi 4: Urutan Pengecekan Concurrency vs Terminal State
- **Ask**: Mengapa test `Scenario6_ConcurrentAction_OptimisticConcurrency_ThrowsConflict` gagal dengan `DomainValidationException` ("Cannot process request in terminal state") bukannya `ConcurrencyConflictException`?
- **AI Suggestion**: AI awalnya menyarankan untuk mengabaikan terminal check jika status sudah terminal dan langsung throw concurrency error hanya pada branch tertentu.
- **Decision**: **Changed**.
- **Why**: Analisis kode menunjukkan bahwa pengecekan `decision.RowVersion != request.RowVersion` harus ditempatkan **sebelum** evaluasi terminal state atau aturan bisnis lainnya. Jika user melakukan submit terhadap snapshot versi lama yang telah berubah di database, anomali tersebut secara fundamental adalah *stale data conflict* (HTTP 409) terlepas dari apakah status yang baru itu terminal atau bukan.

### Interaksi 5: Modal-First Form Interaction, Local SweetAlert2 & DataTables Integration
- **Ask**: Bagaimana merancang interaksi formulir (pengajuan request, approval, dan penolakan) agar intuitif, mencegah perpindahan halaman yang tidak perlu, dan memastikan alert maupun tabel interaktif (DataTables) tersimpan secara lokal tanpa ketergantungan CDN?
- **AI Suggestion**: Menggunakan CDN eksternal untuk SweetAlert2 dan DataTables serta meletakkan tombol submit form di halaman terpisah.
- **Decision**: **Changed**.
- **Why**: Sesuai prinsip *zero-dependency* dan privasi internal, file SweetAlert2 dan DataTables Bootstrap 5 diunduh langsung ke dalam direktori lokal `Assessment_Agit/wwwroot/lib/` (`sweetalert2` dan `datatables`) tanpa mengandalkan koneksi CDN saat runtime. Seluruh formulir diintegrasikan ke dalam Bootstrap Modal (`#globalNewRequestModal`, `#approveModal`, `#rejectModal`, dan quick modals di inbox) disertai navigasi Breadcrumbs dan tabel interaktif dengan search/pagination instan.

---

## 2. Three Things AI Got Wrong

1. **PowerShell Command Separator Syntax**:
   - *Error*: AI mencoba menjalankan chained commands menggunakan token `&&` (`git init && git add . && ...`), yang merupakan syntax Linux/bash atau cmd, menyebabkan parser error pada PowerShell di Windows: `The token '&&' is not a valid statement separator in this version`.
   - *Koreksi Pengembang*: Perintah dikoreksi menggunakan pemisah statement PowerShell yang valid (`;`).

2. **Urutan Guard Clauses pada Approval Workflow**:
   - *Error*: AI meletakkan validasi status terminal sebelum validasi `RowVersion`. Ketika dua approver menekan tombol approve pada saat bersamaan untuk request non-high-risk, request pertama langsung mengubah status menjadi `Approved` (terminal). Request kedua yang datang dengan `RowVersion` basi justru tertahan di pengecekan terminal state dan melempar `DomainValidationException` (HTTP 400), bukan `ConcurrencyConflictException` (HTTP 409).
   - *Koreksi Pengembang*: Logika divalidasi ulang: verifikasi *concurrency token snapshot* harus selalu dievaluasi terlebih dahulu sebelum mengevaluasi aturan bisnis status.

3. **Ketergantungan pada Payload Input untuk Identitas Requester**:
   - *Error*: Draft awal controller/handler yang disarankan AI menyertakan field `RequesterId` di dalam DTO `CreateAccessRequestDto`, membuka celah bagi user untuk menyamar sebagai pengguna lain (*privilege escalation*).
   - *Koreksi Pengembang*: Field `RequesterId` dihapus dari DTO publik; identitas pengguna aktif wajib diambil langsung dari `ICurrentUserService` yang terautentikasi di level server.

