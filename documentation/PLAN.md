# PLAN.md - Access Request Hub MVP (Phase 1)

## 1. Problem Understanding
Perusahaan saat ini mengelola permintaan akses aplikasi internal melalui jalur informal (email/chat) yang menimbulkan masalah operasional:
- Kesulitan audit dan pelacakan status approval.
- Risiko duplikasi request (misal pengguna menekan submit berkali-kali).
- Race condition / concurrent action ketika dua approver melakukan tindakan hampir bersamaan terhadap request yang sama.
- Potensi pelanggaran pemisahan kewenangan (separation of duties), misalnya requester menyetujui request milik sendiri atau bukan approver yang berwenang.

**Tujuan MVP:**
Membangun sistem Access Request Hub yang berfungsi sebagai *single source of truth* untuk request, approval berjenjang, dan audit trail yang tidak dapat diubah (append-only), didukung proteksi idempotensi dan optimistic concurrency, serta berjalan mandiri (zero-dependency) secara lokal.

---

## 2. Architecture & Data Model Ringkas
Proyek ini mengadopsi prinsip **Clean Architecture** untuk memisahkan domain enterprise dari detail framework/database:

```
src/
  ├── Assessment_Agit.Domain/         # Entity, Value Objects, Enums, Domain Exceptions
  ├── Assessment_Agit.Application/    # Use cases, DTOs, Service Interfaces, Validation & Workflow Engine
  ├── Assessment_Agit.Infrastructure/ # EF Core DbContext (SQLite), Config, Migrations/Seeders
  └── Assessment_Agit.Web/            # ASP.NET Core Razor Pages, ViewModels, UI Switcher, jQuery
tests/
  └── Assessment_Agit.Tests/          # Unit & Integration Tests (xUnit + SQLite In-Memory)
```

### Data Model Utama:
- **User**: `Id`, `Email`, `FullName`, `Role`, `ManagerId` (Self-referencing FK untuk relasi hierarki).
- **ApplicationMaster**: `Id`, `Code`, `Name`, `SystemOwnerId` (FK ke User).
- **AccessRequest**:
  - `Id` (GUID)
  - `ClientRequestId` (UUID string, Unique Index untuk Idempotency)
  - `RequesterId` (FK ke User)
  - `ApplicationId` (FK ke ApplicationMaster)
  - `Environment` (Enum: `NonProduction`, `Production`)
  - `AccessLevel` (Enum: `Read`, `Admin`)
  - `Justification` (String, required)
  - `Status` (Enum: `PendingManager`, `PendingSystemOwner`, `Approved`, `Rejected`)
  - `RejectReason` (String, opsional saat pending, wajib saat rejected)
  - `PolicyVersion` (String, "v1")
  - `RowVersion` (Int/Byte[] Concurrency Token untuk Optimistic Concurrency)
  - `CreatedAt`, `UpdatedAt`
- **AuditLog**:
  - `Id` (GUID)
  - `AccessRequestId` (FK)
  - `Action` (Created, ManagerApproved, ManagerRejected, SystemOwnerApproved, SystemOwnerRejected)
  - `ActorId` (FK ke User)
  - `FromStatus`, `ToStatus`
  - `Details` / `Comments`
  - `Timestamp`

---

## 3. Implementation Order
1. **Repository Setup**: Inisialisasi Git, `.gitignore`, initial baseline commit, tag `assessment-start`.
2. **Domain Layer**: Enums, entity definitions, domain business exceptions.
3. **Application Layer**: DTOs, interface kontrak, rule engine (evaluasi high-risk, state transition machine, idempotency handler).
4. **Infrastructure Layer**: EF Core DbContext, SQLite provider, model configuration (unique constraint, concurrency token), seed data 5 demo user & 2 master aplikasi.
5. **Web Layer (Razor Pages + Bootstrap 5 + jQuery)**:
   - User Switcher dropdown di navbar untuk simulasi konteks requester/approver/auditor.
   - Form pembuatan request (`/Requests/Create`) dengan validasi client/server dan generator ClientRequestId.
   - Daftar request user (`/Requests/Index`) dan detail request (`/Requests/Detail`) dengan visual audit timeline.
   - Approval Inbox (`/Approvals/Index`) dengan pemfilteran berbasis role/assignment pengguna aktif.
6. **Automated Testing Suite**: 7 skenario wajib assessment (standard, high-risk prod, high-risk admin, unauthorized, duplicate submit, concurrency conflict, rejection).
7. **Evidence & Documentation**: `PLAN.md`, `AI_USAGE.md`, `REVIEW.md`, `INTEGRITY.md`, `README.md`.

---

## 4. Test Strategy
- **Test Runner**: xUnit dengan FluentAssertions atau standard xUnit Assertions.
- **Database Isolation**: In-memory SQLite (`Filename=:memory:`) dengan per-test connection lifecycle untuk memastikan isolasi penuh dan determinisme.
- **Coverage Skenario Wajib**:
  1. Standard Request: Alice -> Bob -> `Approved`.
  2. Production High-Risk: Alice -> Bob (`PendingSystemOwner`) -> Carol (`Approved`).
  3. Admin Access High-Risk: Alice -> Bob (`PendingSystemOwner`) -> Dana (`Approved`).
  4. Authorization Failure: Percobaan approval oleh user tidak berwenang / self-approval -> 403 Forbidden.
  5. Idempotency: Submission ulang `ClientRequestId` yang sama secara paralel atau berurutan -> tetap 1 record bisnis.
  6. Concurrency Conflict: Dua aksi approval simultan dengan concurrency token yang sama -> 1 berhasil, 1 conflict (409).
  7. Rejection Flow: Bob/Carol reject dengan alasan -> status `Rejected`, audit tercatat, terminal state.

---

## 5. 2-3 Important Trade-offs
1. **SQLite vs Server-based RDBMS (SQL Server / PostgreSQL)**:
   - *Trade-off*: SQLite memiliki concurrency locking level file, berbeda dengan row-level locking pada database server enterprise.
   - *Alasan Pilihan*: Memenuhi kriteria utama zero-dependency (portabel, dapat dijalankan langsung oleh asesor tanpa instalasi container/database terpisah). Proteksi concurrency tetap dijamin menggunakan optimistic concurrency token (`RowVersion`) dan unique database constraints.
2. **Simulated Session User Switcher vs Full Identity/OIDC**:
   - *Trade-off*: Tidak mengimplementasikan OAuth2/JWT token signing atau login page dengan password hashing.
   - *Alasan Pilihan*: Sesuai panduan assessment (Section 2.2 & 3.5), integrasi SSO/IdP nyata secara sengaja ditiadakan untuk efisiensi waktu. Sistem mengandalkan user switcher session-based namun *authorization enforcement* tetap 100% dieksekusi secara ketat di server-side (backend tidak mempercayai ID dari input bebas).
3. **Synchronous In-Transaction Audit Trail vs Asynchronous Event Outbox**:
   - *Trade-off*: Menulis audit log dalam transaksi database yang sama dengan mutasi status menambahkan sedikit overhead write latency dibandingkan publish-subscribe event broker.
   - *Alasan Pilihan*: Menjamin konsistensi atomik dan ketahanan audit (audit record tidak boleh hilang jika state berubah, dan rollback terjadi bersamaan jika terjadi kegagalan).

---

## 6. Perubahan Plan Selama Implementasi
Selama proses implementasi dan eksekusi test suite, beberapa penyesuaian dilakukan untuk memastikan *correctness* dan kepatuhan penuh terhadap spesifikasi:
1. **Urutan Evaluasi Optimistic Concurrency vs Terminal State**:
   - *Masalah*: Pada pengujian skenario konkurensi (Scenario 6), request yang telah diubah oleh transaksi pertama berpindah ke status terminal (`Approved`). Ketika transaksi kedua masuk membawa token versi lama (`RowVersion = 1`), pengecekan terminal state sempat mendahului pengecekan versi sehingga menghasilkan `DomainValidationException` (400) dan bukan `ConcurrencyConflictException` (409).
   - *Penyesuaian*: Pengecekan token konkurensi (`decision.RowVersion != request.RowVersion`) dipindahkan ke tahap paling awal sebelum evaluasi status. Hal ini memastikan bahwa setiap aksi terhadap *stale snapshot* data secara konsisten dilaporkan sebagai *concurrency conflict* (409 Conflict).
2. **Deterministic Seed Data untuk Uji Terisolasi**:
   - Demo users dan master applications dikonfigurasi dengan GUID deterministik tetap pada `DbInitializer` (`11111111-...` untuk Alice, `22222222-...` untuk Bob, dst.). Hal ini mempermudah automasi test xUnit dengan in-memory SQLite serta memudahkan pengujian visual bagi assessor melalui dropdown UI switcher.
3. **Penyediaan Minimal API Endpoints Paralel**:
   - Selain Razor Pages post handlers, endpoint minimal API (`/api/requests`, `/api/requests/{id}/approve`, `/api/requests/{id}/reject`, `/api/switch-user`) disediakan untuk memfasilitasi AJAX jQuery dari frontend sekaligus pengujian integrasi via HTTP client atau cURL secara transparan.
4. **Alur Validasi Tag Phase-1-Complete**:
   - Pembuatan Git tag `phase-1-complete` dieksekusi setelah seluruh validasi fungsional, UI, dan automated test suite terpenuhi 100%.
5. **Mitigasi SQLite DateTimeOffset OrderBy**:
   - EF Core SQLite tidak mendukung ekspresi tipe `DateTimeOffset` dalam klausa `ORDER BY`. Seluruh entity tanggal dimigrasikan ke `DateTime` (UTC), dan pengurutan riwayat request dipastikan dieksekusi secara aman via LINQ to Objects (`.ToListAsync()` lalu `.OrderByDescending()`).
6. **Modernisasi UI dengan Modal-First, SweetAlert2, DataTables, dan Breadcrumbs**:
   - Seluruh formulir tindakan (New Request, Approve confirmation, Reject dengan alasan wajib, dan Quick Decision di inbox) ditransformasikan menjadi Bootstrap Modal interaktif.
   - Mengadopsi SweetAlert2 yang di-host lokal di `wwwroot/lib/sweetalert2` untuk menggantikan alert default browser dengan toast dan feedback modal yang modern.
   - Mengintegrasikan DataTables Bootstrap 5 lokal di `wwwroot/lib/datatables` pada seluruh tabel (Dashboard Demo Users, My Requests, Approvals Inbox, dan Global Audit) dengan pagination, sorting, dan live search instan.
   - Menambahkan navigasi Breadcrumbs di seluruh halaman dan menghapus tombol shortcut header navbar demi kerapian arsitektur visual.
7. **Penanganan Requester Selain Alice (Pre-condition Validation)**:
   - Sesuai Business Rule 2.6 (semua request mewajibkan Manager approval dan manager hanya memproses direct report), backend menerapkan validasi pre-condition: pemohon wajib memiliki `ManagerId` yang valid di hirarki organisasi.
   - Jika pengguna tanpa atasan langsung (Bob, Carol, Dana, Erin) mencoba membuat request, backend langsung menolak dengan `DomainValidationException` (HTTP 400 Bad Request) dan UI menampilkan notifikasi hierarki edukatif, mencegah timbulnya *orphan request*.

