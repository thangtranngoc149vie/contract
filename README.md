# FISA Contracts API

Triển khai dịch vụ Web API (.NET 8 + Dapper + PostgreSQL) phục vụ màn hình **Tạo Hợp đồng** theo tài liệu đặc tả "FISA — Contracts Create APIs v1".

## Kiến trúc tổng quan
- **Contracts.Api**: ASP.NET Core Web API, cung cấp các endpoint `/api/v1/*`.
- **Contracts.Api.Tests**: Bộ kiểm thử integration sử dụng `WebApplicationFactory` và Testcontainers (PostgreSQL).
- **migration_contracts_v1.sql**: script tạo schema tối thiểu cho tính năng hợp đồng (bảng `contracts`, `outbox_events`, mock `projects`, `bid_packages`).

## Yêu cầu hệ thống
- .NET SDK 8.0
- Docker (để chạy PostgreSQL trong Testcontainers) hoặc một PostgreSQL 14+ cài đặt sẵn.

## Thiết lập cơ sở dữ liệu
1. Khởi chạy PostgreSQL (mặc định `Host=localhost;Port=5432;Username=postgres;Password=postgres`).
2. Chạy script migration:
   ```bash
   psql "host=localhost port=5432 dbname=postgres user=postgres password=postgres" \
     -f migration_contracts_v1.sql
   ```
3. (Tuỳ chọn) tạo database `contracts` riêng và cập nhật chuỗi kết nối trong `src/Contracts.Api/appsettings.json` nếu cần.

## Chạy dịch vụ API
```bash
dotnet restore
DOTNET_ENVIRONMENT=Development dotnet run --project src/Contracts.Api
```

API lắng nghe theo `appsettings` (mặc định `https://localhost:7249` và `http://localhost:5249`). Swagger UI khả dụng ở `/swagger` trong môi trường Development.

### RBAC giả lập
Dịch vụ sử dụng header:
- `X-User-Id`: UUID của người dùng.
- `X-Roles`: danh sách role dạng CSV (ví dụ `Contract.Create`).

Endpoint `POST /api/v1/contracts` yêu cầu role `Contract.Create`. Các endpoint khác mở quyền đọc.

## Kiểm thử
Bộ kiểm thử integration sẽ khởi tạo PostgreSQL tạm bằng Testcontainers (yêu cầu Docker).
```bash
dotnet test
```

Các bài test bao gồm:
- Tạo hợp đồng thành công và ghi outbox event.
- Chặn tạo trùng mã trong cùng dự án.
- Xác thực sai range ngày trả về HTTP 422.

## Bộ endpoint chính
| Method | Endpoint | Mô tả |
|--------|----------|-------|
| GET | `/api/v1/contracts/meta` | Metadata form hợp đồng (enum, default, constraints). |
| GET | `/api/v1/projects` | Tìm kiếm dự án với phân trang, filter `keyword`. |
| GET | `/api/v1/bid-packages` | Tìm kiếm gói thầu theo `project_id`. |
| HEAD | `/api/v1/contracts/exists` | Kiểm tra trùng mã hợp đồng trong cùng dự án. |
| POST | `/api/v1/contracts/validate` | Validate server-side cho draft. |
| POST | `/api/v1/contracts` | Tạo hợp đồng, ghi outbox, trả ETag.

## Sample HTTP
```
### Metadata
GET http://localhost:5249/api/v1/contracts/meta

### Tìm kiếm dự án
GET http://localhost:5249/api/v1/projects?keyword=toa&page=1&page_size=10

### Kiểm tra trùng mã
HEAD http://localhost:5249/api/v1/contracts/exists?project_id={{projectId}}&code=CTR001

### Validate draft
POST http://localhost:5249/api/v1/contracts/validate
Content-Type: application/json

{
  "scope_type": "project",
  "project_id": "{{projectId}}",
  "code": "CTR001",
  "name": "Hop dong demo",
  "value_vnd": 1000000,
  "warranty_value_vnd": 0,
  "start_date": "2025-01-01",
  "end_date": "2025-02-01",
  "payment_schedule": []
}

### Tạo hợp đồng
POST http://localhost:5249/api/v1/contracts
Content-Type: application/json
X-User-Id: 00000000-0000-0000-0000-000000000001
X-Roles: Contract.Create

{
  "scope_type": "project",
  "project_id": "{{projectId}}",
  "code": "CTR001",
  "name": "Hop dong demo",
  "value_vnd": 1000000,
  "warranty_value_vnd": 0,
  "start_date": "2025-01-01",
  "end_date": "2025-02-01",
  "payment_schedule": []
}
```

## Bộ sưu tập HTTP
Xem thêm tệp `contracts.http` để gửi nhanh qua VS Code / Rider HTTP Client.
