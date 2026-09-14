# 🛡️ Access Request Hub MVP (Phase 1)

Access Request Hub adalah sistem *single source of truth* untuk pengajuan, approval berjenjang, dan audit trail permintaan akses aplikasi internal. Proyek ini dibangun dengan pendekatan **Clean Architecture**, **SQLite** (*zero-dependency*, mandiri tanpa Docker atau DBMS server eksternal), **Entity Framework Core**, **Razor Pages**, **Bootstrap 5**, **jQuery**, **SweetAlert2**, dan **DataTables**.

---

## 1. Arsitektur & Struktur Proyek

Proyek memisahkan tanggung jawab secara tegas menggunakan Clean Architecture:
```
Assessment_Agit/
├── src/
│   ├── Assessment_Agit.Domain/         # Entitas (User, ApplicationMaster, AccessRequest, AuditLog), Enums, Exceptions
│   ├── Assessment_Agit.Application/    # DTOs, Kontrak Interfaces, Validasi & Rule Engine, Concurrency & Idempotency logic
│   └── Assessment_Agit.Infrastructure/ # EF Core DbContext, SQLite mapping, Seeding otomatis, CurrentUserService
├── Assessment_Agit/                    # ASP.NET Core Razor Pages (Web UI), User Switcher, Minimal API Endpoints
├── tests/
│   └── Assessment_Agit.Tests/          # Automated Test Suite (xUnit + SQLite In-Memory)
├── PLAN.md                             # Problem understanding, arsitektur, trade-offs, log perubahan
├── AI_USAGE.md                         # Interaksi AI paling berpengaruh & "Three things AI got wrong"
├── REVIEW.md                           # Production readiness review, severity, limitations & deferred work
├── INTEGRITY.md                        # Deklarasi integritas engineering
└── README.md                           # Panduan setup, run, test, dan skenario demo
```

---

## 2. Prasyarat Sistem
- **.NET SDK**: .NET 10.0 (`dotnet --version` >= 10.0)
- **OS**: Windows, macOS, atau Linux (Cross-platform)
- **Database Engine**: **Tidak memerlukan instalasi database eksternal**. SQLite berjalan mandiri sebagai file lokal `access_hub.db`.

---

## 3. Cara Menjalankan Aplikasi (Setup & Run)

### A. Jalankan Web Application
Buka terminal/PowerShell di direktori root repositori:
```powershell
dotnet run --project Assessment_Agit/Assessment_Agit.csproj
```
Aplikasi akan otomatis:
1. Membangun database SQLite `access_hub.db`.
2. Menjalankan skema relational (`EnsureCreatedAsync`).
3. Mengisi *seed data* (5 demo users dan 2 master applications) secara otomatis.
4. Menjalankan web server pada URL lokal (misal: `http://localhost:5000` atau `https://localhost:7154`).

Buka URL tersebut pada browser favorit Anda.

---

## 4. Cara Menjalankan Automated Tests

Seluruh 7 skenario wajib assessment serta uji konkurensi dan idempotensi dapat dijalankan dengan perintah berikut:
```powershell
dotnet test tests/Assessment_Agit.Tests/Assessment_Agit.Tests.csproj
```

**Hasil Verifikasi Test Suite:**
- `Scenario1_StandardRequest_AliceToBob_BecomesApproved` : Standard Non-High-Risk flow.
- `Scenario2_ProductionRequest_HighRisk_RequiresCarolSystemOwnerApproval` : High-Risk Production flow (2-stage approval).
- `Scenario3_AdminAccess_HighRisk_EscalatesToDanaSystemOwner` : High-Risk Admin flow (2-stage approval).
- `Scenario4_UnauthorizedApproval_ThrowsForbiddenException` : Proteksi otorisasi server-side (403 Forbidden).
- `Scenario5_DuplicateSubmit_IsIdempotent_DoesNotCreateSecondRow` : Proteksi idempotensi `ClientRequestId`.
- `Scenario6_ConcurrentAction_OptimisticConcurrency_ThrowsConflict` : Proteksi optimistic concurrency (409 Conflict).
- `Scenario7_RejectedRequest_RequiresReason_BecomesTerminal` : Validasi rejection reason & terminal state.

---

## 5. Demo Users & Peran (Seeded Data)

Sistem dilengkapi **User Switcher** pada bilah navigasi atas (kanan atas) yang memungkinkan assessor berganti perspektif secara instan:

| Pengguna | Email | Peran / Tanggung Jawab |
| :--- | :--- | :--- |
| **Alice** | `alice@example.local` | **Requester**. Bawahan langsung Bob. |
| **Bob** | `bob@example.local` | **Manager** untuk Alice. Memproses Manager Approval. |
| **Carol** | `carol@example.local` | **System Owner** untuk aplikasi **CRM**. |
| **Dana** | `dana@example.local` | **System Owner** untuk aplikasi **Finance Portal**. |
| **Erin** | `erin@example.local` | **Admin / Auditor**. Memantau seluruh request dan audit trail global. |

### Master Data Aplikasi:
- **CRM**: System Owner adalah **Carol**.
- **Finance Portal**: System Owner adalah **Dana**.

> ℹ️ **Catatan Hirarki Organisasi (Business Rule 2.6)**:
> Sesuai aturan bisnis wajib (Section 2.6: *"Semua request membutuhkan Manager approval terlebih dahulu"* dan *"Manager hanya boleh memproses direct report"*), pengguna wajib memiliki atasan langsung untuk mengajukan request. Dalam seeded data Phase 1, **Alice** melapor ke **Bob**. Jika persona lain yang tidak memiliki atasan langsung (Bob, Carol, Dana, Erin) mencoba membuat permohonan, backend secara proaktif menolaknya dengan pesan validasi edukatif guna mencegah *orphan request* (request tanpa approver sah).

---

## 6. Panduan Menjalankan 7 Skenario Demo Wajib

### Skenario 1: Standard Request (Non-High-Risk)
1. Pada User Switcher di navbar, pastikan aktif sebagai **Alice**.
2. Klik tombol **Submit Request** di Dashboard atau **New Access Request** di My Requests:
   - Form modal interaktif akan terbuka dengan navigasi Breadcrumbs.
   - Pilih Application: **CRM**.
   - Environment: **NonProduction**.
   - Access Level: **Read**.
   - Justification: "Akses membaca data CRM dev".
   - Klik **Submit Request**. SweetAlert2 akan menampilkan status keberhasilan, dan request berpindah ke `Pending Manager Approval`.
3. Ganti user ke **Bob** (Manager) melalui User Switcher.
4. Buka menu **Approvals Inbox** &rarr; klik **Approve** (via Quick Decision Modal) atau buka Detail request.
5. Klik **Approve Request** pada konfirmasi modal.
6. **Hasil**: Status langsung menjadi `Approved` (alur 1 tahap selesai) dan Audit Trail mencatat event `ManagerApproved`.

### Skenario 2: Production Request (High-Risk &rarr; 2-Stage Approval)
1. Switch ke **Alice** &rarr; Buka modal **New Request**:
   - Pilih Application: **CRM**.
   - Environment: **Production** *(Trigger High-Risk - notifikasi banner kuning muncul otomatis)*.
   - Access Level: **Read**.
   - Klik **Submit Request**.
2. Switch ke **Bob** &rarr; Buka **Approvals Inbox** &rarr; Buka detail request &rarr; Klik **Approve Request** pada modal konfirmasi.
3. **Hasil Tahap 1**: Status berpindah ke `Pending System Owner Approval` (menunggu Carol).
4. Switch ke **Carol** (System Owner CRM) &rarr; Buka **Approvals Inbox** &rarr; Buka detail request.
5. Klik **Approve Request** pada modal konfirmasi.
6. **Hasil Tahap 2**: Status final menjadi `Approved` dengan 3 log audit berurutan (`Created`, `ManagerApproved`, `SystemOwnerApproved`).

### Skenario 3: Admin Access Request (High-Risk &rarr; Finance Portal)
1. Switch ke **Alice** &rarr; Buka **New Request**:
   - Pilih Application: **Finance Portal**.
   - Environment: **NonProduction**.
   - Access Level: **Admin** *(Trigger High-Risk)*.
   - Klik **Submit Request**.
2. Switch ke **Bob** &rarr; Approve request tersebut. Status berpindah ke `Pending System Owner Approval` (menunggu Dana).
3. Switch ke **Dana** (System Owner Finance Portal) &rarr; Buka **Approvals Inbox** &rarr; Approve request.
4. **Hasil**: Status final menjadi `Approved`.

### Skenario 4: Unauthorized Approval (Server-Side 403 Forbidden)
1. Buat request baru sebagai **Alice**.
2. Tetap sebagai **Alice**, coba approve request milik sendiri:
   - Panel tindakan di UI dinonaktifkan secara otomatis.
   - Jika memanggil endpoint approve langsung melalui cURL/Postman, backend mengembalikan status `403 Forbidden` (*"Requesters are strictly prohibited from approving their own access requests"*).
3. Switch ke **Carol** saat request masih menunggu Manager Approval (Bob):
   - Jika Carol mencoba approve, backend menolak dengan `403 Forbidden` (*"Only the requester's direct manager is authorized to approve this stage"*).

### Skenario 5: Duplicate Submit Idempotency
1. Pada form **New Request**, masukkan data pengajuan.
2. Klik tombol **Simulate Duplicate Submit (Idempotency)**.
3. Klik tombol tersebut berulang kali.
4. **Hasil**: Sistem merespons dengan ID request yang sama persis tanpa menduplikasi baris di database (Unique index constraint & atomic check).

### Skenario 6: Concurrent Action (Optimistic Concurrency 409 Conflict)
1. Buka request pending yang dapat diapprove.
2. Pada panel tindakan, klik tombol **Simulate Stale Action (409 Conflict)**.
3. Tombol ini sengaja mengirimkan snapshot versi usang (`RowVersion` lama).
4. **Hasil**: Sistem menampilkan pesan banner `[409 Conflict Detected] This request has already been modified or approved by another user or session`.

### Skenario 7: Rejected Request (Mandatory Reason & Terminal State)
1. Buat request baru sebagai **Alice**.
2. Switch ke **Bob** &rarr; Buka detail request &rarr; Klik tombol merah **Reject Request**.
3. Kosongkan alasan penolakan &rarr; sistem memvalidasi bahwa alasan wajib diisi.
4. Masukkan alasan: "Akses ditolak karena belum ada approval dari kepala divisi".
5. Konfirmasi penolakan &rarr; **Hasil**: Status berubah menjadi `Rejected` (terminal), alasan penolakan tersimpan dan ditampilkan di halaman, serta tidak ada aksi lanjutan yang diizinkan.

---

## 7. Evidence Deliverables Checklist

- [x] **Git Repository History**: Tag `assessment-start` dibuat sebelum implementasi inti.
- [x] **PLAN.md**: Problem understanding, arsitektur, trade-offs, dan catatan penyesuaian.
- [x] **AI_USAGE.md**: Ringkasan interaksi AI berdampak dan bagian "Three things AI got wrong".
- [x] **REVIEW.md**: Production readiness self-review matrix, limitasi, dan deferred work.
- [x] **INTEGRITY.md**: Deklarasi integritas kepemilikan kode.
- [x] **README.md**: Petunjuk setup, run, test, dan demo walkthrough.
- [x] **Automated Tests**: 100% lulus (7/7 skenario).
- [ ] **Tag `phase-1-complete`**: Ditangguhkan sesuai instruksi pengguna untuk pengecekan manual terlebih dahulu sebelum di-tag.

