# REVIEW.md - Production Readiness Self-Review & Deferred Work

Dokumen ini memuat *self-review* menyeluruh terhadap kesiapan rilis produksi (production readiness), mitigasi risiko teknis, batasan sistem, dan pekerjaan yang ditangguhkan (*deferred work*).

---

## 1. Production Readiness Review Matrix

| # | Finding / Area | Severity | Action Taken | Status | Evidence |
| :--- | :--- | :--- | :--- | :--- | :--- |
| 1 | **Optimistic Concurrency Collision** | **High** | Menambahkan `[ConcurrencyCheck] int RowVersion` pada entitas `AccessRequest`, mengecek kecocokan versi di awal alur approval, dan meningkatkan versi atomik saat mutasi. | **Resolved** | `Scenario6_ConcurrentAction_OptimisticConcurrency_ThrowsConflict` lulus test; demonstrasi tombol simulasi stale action di UI mengembalikan HTTP 409. |
| 2 | **Idempotency & Race on Duplicate Submit** | **High** | Menerapkan `ClientRequestId` unik dengan Unique Index di SQLite (`HasIndex().IsUnique()`), serta pengecekan deterministik di layer aplikasi untuk mengembalikan entitas yang sudah dibuat jika parameter identik. | **Resolved** | `Scenario5_DuplicateSubmit_IsIdempotent_DoesNotCreateSecondRow` lulus test; verifikasi database hanya menyimpan 1 baris bisnis. |
| 3 | **Privilege Escalation / Client Impersonation** | **High** | Menghapus `RequesterId` dari payload `CreateAccessRequestDto`. Identitas requester murni di-resolve dari sesi server melalui `ICurrentUserService`. | **Resolved** | `AccessRequestService.cs` baris 33; DTO `CreateAccessRequestDto` tidak memiliki properti identitas requester. |
| 4 | **Self-Approval Violation** | **Medium** | Menerapkan aturan eksplisit di backend: jika `request.RequesterId == currentUser.Id`, approval/rejection langsung ditolak dengan `ForbiddenException` (HTTP 403). | **Resolved** | `Scenario4_UnauthorizedApproval_ThrowsForbiddenException` membuktikan requester tidak dapat menyetujui request sendiri. |
| 5 | **Mandatory Rejection Reason** | **Medium** | Validasi server-side bahwa `RejectReason` tidak boleh kosong atau hanya berupa spasi saat aksi penolakan dilakukan. | **Resolved** | `Scenario7_RejectedRequest_RequiresReason_BecomesTerminal` menguji kegagalan saat reason kosong dan keberhasilan saat reason diisi. |
| 6 | **Audit Trail Immutability** | **Medium** | Audit log disimpan secara atomik dalam transaksi yang sama dengan perubahan status (`AppDbContext.AuditLogs`). Foreign key menggunakan mode cascade delete hanya jika parent dihapus, dan record audit tidak menyediakan endpoint update/delete. | **Resolved** | Verifikasi entitas `AuditLog.cs` dan event timeline di halaman `/Requests/Detail`. |
| 7 | **Zero-Dependency Portability** | **Low** | Menggunakan provider `Microsoft.EntityFrameworkCore.Sqlite` dengan auto-creation (`EnsureCreatedAsync`) dan seeding deterministik (`DbInitializer`), sehingga aplikasi dapat langsung dijalankan pada mesin baru tanpa instalasi DBMS terpisah. | **Resolved** | Konfigurasi di `Program.cs` dan verifikasi koneksi `Data Source=access_hub.db`. |
| 8 | **Zero-CDN Offline Frontend Dependencies** | **Low** | Mengunduh paket SweetAlert2 dan DataTables Bootstrap 5 langsung ke `wwwroot/lib/` (`sweetalert2` dan `datatables`) untuk memastikan UI berjalan 100% offline tanpa dependensi CDN eksternal. | **Resolved** | Folder `wwwroot/lib/sweetalert2` dan `wwwroot/lib/datatables`. |
| 9 | **Modal-First UX, DataTables & Breadcrumbs** | **Low** | Seluruh formulir dikemas dalam modal dialog, seluruh tabel dilengkapi DataTables (sorting, pagination, live filter), dan breadcrumbs dinamis dipasang untuk navigasi konsisten. | **Resolved** | Template `_Layout.cshtml`, `Detail.cshtml`, dan `Approvals/Index.cshtml`. |
| 10 | **Manager Pre-condition Constraint for Non-Alice Users** | **Medium** | Backend menolak pengajuan request dari user tanpa assigned direct manager di hirarki organisasi (`currentUser.ManagerId == null`), mencegah terbentuknya *orphan requests* yang melanggar aturan Manager Approval. | **Resolved** | `AccessRequestService.cs` baris 29 & unit test `Scenario8_RequesterWithoutManager_ThrowsDomainValidationException`. |

---

## 2. Known Limitations

1. **SQLite Database Locking Under Extreme Write Concurrency**:
   - *Batasan*: SQLite menggunakan file-level locking untuk transaksi write. Untuk volume ribuan transaksi serentak per detik, SQLite akan mengalami lock contention.
   - *Mitigasi di Phase 1*: SQLite dipilih untuk memastikan *zero-dependency* lokal sesuai kriteria assessment. Untuk arsitektur enterprise skala besar, provider dapat dialihkan ke SQL Server atau PostgreSQL via satu baris konfigurasi di `DependencyInjection.cs` tanpa mengubah layer Domain maupun Application.
2. **Simulated Authentication Session**:
   - *Batasan*: Sistem menggunakan User Switcher berbasis session/cookie lokal untuk keperluan demo personas, bukan token JWT yang di-sign secara kriptografis atau OIDC provider nyata.
   - *Catatan Kepatuhan*: Hal ini sesuai dengan instruksi assessment Section 2.2 & 3.5 (*"Authentication boleh disimulasikan... integrasi SSO/OIDC nyata sengaja tidak diminta"*).
3. **In-Memory Distributed Cache**:
   - *Batasan*: Sesi pengguna disimpan menggunakan `AddDistributedMemoryCache` lokal yang tidak terdistribusi lintas node (single-instance).

---

## 3. Deferred Work (Phase 2 Roadmap)

Pekerjaan berikut secara sengaja ditangguhkan (*deferred*) agar tetap berada dalam timebox 4-8 jam dan memprioritaskan *correctness* serta *engineering quality* (Section 1):
1. **Real-time Push Notifications (SignalR / WebSockets)**:
   - Mengirimkan update real-time ke halaman Approvals Inbox ketika ada request baru tanpa perlu manual page refresh.
2. **Advanced Multi-condition Search & Server-Side Pagination**:
   - Pagination untuk ribuan record audit trail dan request history.
3. **Change Request Phase 2 Integration**:
   - Mengimplementasikan policy versioning dinamis (misal: v2 rules) sesuai instruksi Section 5: *"Jangan mengimplementasikan Phase 2 sebelum dokumen Change Request Phase 2 diberikan assessor"*.
4. **Email / Webhook Dispatcher**:
   - Integrasi pengiriman notifikasi email ke manajer dan system owner menggunakan background worker / outbox pattern.

