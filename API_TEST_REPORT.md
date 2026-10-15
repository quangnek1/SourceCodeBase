# Báo cáo test API

- Thời gian chạy: 2026-10-15 21:26:59
- AerationSterilize: `http://localhost:5000/api/v1` (DB `SourceBase`) — đăng nhập, cấp token, phân quyền, Product
- CommonServices: `http://localhost:5001/api/v1` (DB `CommonServices`) — master data, chỉ kiểm tra token của Aeration
- Redis: container `aeration-redis` (cache + danh sách token đã thu hồi)
- Script: `tests/ApiTests/run_api_tests.py` (chạy lại được, tự dọn dữ liệu test cũ)

## Tổng kết

- Tổng số test: **177** — ✅ đạt **177**, ❌ lỗi **0**, ℹ️ ghi nhận 2
- Ký hiệu: ✅ đúng như mong đợi, ❌ sai, ℹ️ ghi nhận hành vi (không tính đạt/lỗi)

## 0. Chuẩn bị dữ liệu mẫu

Role test (bảng `AppRoles` + `Permissions`, AccessLevel: 0 None, 1 Own, 2 Department, 3 All):

- **T Product Manager** (`R_MANAGER`): PRODUCT.VIEW:3, PRODUCT.CREATE:3, PRODUCT.UPDATE:3, PRODUCT.DELETE:3, MASTER_DATA.VIEW:3, LANGUAGE.VIEW:3, REGION.VIEW:3, CURRENCY.VIEW:3, COUNTRY.VIEW:3, CITY.VIEW:3, BANK.VIEW:3
- **T Department Staff** (`R_DEPT`): PRODUCT.VIEW:2, PRODUCT.CREATE:2, PRODUCT.UPDATE:2, PRODUCT.DELETE:1
- **T Own Staff** (`R_OWN`): PRODUCT.VIEW:1, PRODUCT.CREATE:1, PRODUCT.UPDATE:1, PRODUCT.DELETE:1
- **T Master Data Viewer** (`R_VIEWER`): MASTER_DATA.VIEW:3, LANGUAGE.VIEW:3, REGION.VIEW:3, CURRENCY.VIEW:3, COUNTRY.VIEW:3, CITY.VIEW:3, BANK.VIEW:3
- **T No Access** (`R_NONE`): PRODUCT.VIEW:0, BANK.VIEW:0

Phòng ban: A = `d0000000-0000-0000-0000-00000000000a`, B = `d0000000-0000-0000-0000-00000000000b`. Mật khẩu mọi user test: `Test@123`.

User test (tạo qua API register, sau đó gán phòng + role bằng SQL):

- `t_manager`: Phòng A, PRODUCT mức All + xem master data
- `t_staff_a1`: Phòng A, PRODUCT VIEW/CREATE/UPDATE mức Department, DELETE mức Own
- `t_staff_a2`: Phòng A, giống t_staff_a1
- `t_staff_b1`: Phòng B, giống t_staff_a1
- `t_own_a1`: Phòng A, PRODUCT mức Own
- `t_own_a2`: Phòng A, PRODUCT mức Own
- `t_viewer`: Phòng B, chỉ xem master data, không có quyền PRODUCT
- `t_noaccess`: PRODUCT.VIEW và BANK.VIEW nhưng AccessLevel = None
- `t_nodept`: Không thuộc phòng nào, PRODUCT mức Department
- `t_multi`: Phòng B, 2 role Own + Department (lấy mức cao nhất)

| # | API | User | Kịch bản | Mong đợi | Thực tế | KQ |
|---|---|---|---|---|---|---|
| 1 | `POST :5000/auth/register` | (anonymous) | Đăng ký t_manager | 200 | 200 | ✅ |
| 2 | `POST :5000/auth/register` | (anonymous) | Đăng ký t_staff_a1 | 200 | 200 | ✅ |
| 3 | `POST :5000/auth/register` | (anonymous) | Đăng ký t_staff_a2 | 200 | 200 | ✅ |
| 4 | `POST :5000/auth/register` | (anonymous) | Đăng ký t_staff_b1 | 200 | 200 | ✅ |
| 5 | `POST :5000/auth/register` | (anonymous) | Đăng ký t_own_a1 | 200 | 200 | ✅ |
| 6 | `POST :5000/auth/register` | (anonymous) | Đăng ký t_own_a2 | 200 | 200 | ✅ |
| 7 | `POST :5000/auth/register` | (anonymous) | Đăng ký t_viewer | 200 | 200 | ✅ |
| 8 | `POST :5000/auth/register` | (anonymous) | Đăng ký t_noaccess | 200 | 200 | ✅ |
| 9 | `POST :5000/auth/register` | (anonymous) | Đăng ký t_nodept | 200 | 200 | ✅ |
| 10 | `POST :5000/auth/register` | (anonymous) | Đăng ký t_multi | 200 | 200 | ✅ |
| 11 | `SQL` | - | Gán phòng ban + role cho 10 user test | OK | OK | ✅ |
| 12 | `Redis` | - | Xoá cache quyền + cache product cũ | OK | xoá 12 key | ✅ |

## 1. Đăng nhập / token (AerationSterilize)

| # | API | User | Kịch bản | Mong đợi | Thực tế | KQ |
|---|---|---|---|---|---|---|
| 13 | `POST :5000/auth/register` | (anonymous) | Đăng ký trùng username | 400 | 400 (Bad Request) | ✅ |
| 14 | `POST :5000/auth/login` | admin | Sai mật khẩu | 4xx | 400 | ✅ |
| 15 | `POST :5000/auth/login` | khong_ton_tai | User không tồn tại | 4xx | 400 | ✅ |
| 16 | `POST :5000/auth/login` | admin | Đăng nhập, xem claim trong token | 200 + token | 200; department_id=None; BANK.VIEW:3, PRODUCT.ACTIVATE:3, PRODUCT.CREATE:3, PRODUCT.DELETE:3, PRODUCT.INACTIVATE:3, PRODUCT.UPDATE:3, PRODUCT.VIEW:3 | ✅ |
| 17 | `POST :5000/auth/login` | t_manager | Đăng nhập, xem claim trong token | 200 + token | 200; department_id=d0000000-0000-0000-0000-00000000000a; BANK.VIEW:3, PRODUCT.CREATE:3, PRODUCT.DELETE:3, PRODUCT.UPDATE:3, PRODUCT.VIEW:3 | ✅ |
| 18 | `POST :5000/auth/login` | t_staff_a1 | Đăng nhập, xem claim trong token | 200 + token | 200; department_id=d0000000-0000-0000-0000-00000000000a; PRODUCT.CREATE:2, PRODUCT.DELETE:1, PRODUCT.UPDATE:2, PRODUCT.VIEW:2 | ✅ |
| 19 | `POST :5000/auth/login` | t_staff_a2 | Đăng nhập, xem claim trong token | 200 + token | 200; department_id=d0000000-0000-0000-0000-00000000000a; PRODUCT.CREATE:2, PRODUCT.DELETE:1, PRODUCT.UPDATE:2, PRODUCT.VIEW:2 | ✅ |
| 20 | `POST :5000/auth/login` | t_staff_b1 | Đăng nhập, xem claim trong token | 200 + token | 200; department_id=d0000000-0000-0000-0000-00000000000b; PRODUCT.CREATE:2, PRODUCT.DELETE:1, PRODUCT.UPDATE:2, PRODUCT.VIEW:2 | ✅ |
| 21 | `POST :5000/auth/login` | t_own_a1 | Đăng nhập, xem claim trong token | 200 + token | 200; department_id=d0000000-0000-0000-0000-00000000000a; PRODUCT.CREATE:1, PRODUCT.DELETE:1, PRODUCT.UPDATE:1, PRODUCT.VIEW:1 | ✅ |
| 22 | `POST :5000/auth/login` | t_own_a2 | Đăng nhập, xem claim trong token | 200 + token | 200; department_id=d0000000-0000-0000-0000-00000000000a; PRODUCT.CREATE:1, PRODUCT.DELETE:1, PRODUCT.UPDATE:1, PRODUCT.VIEW:1 | ✅ |
| 23 | `POST :5000/auth/login` | t_viewer | Đăng nhập, xem claim trong token | 200 + token | 200; department_id=d0000000-0000-0000-0000-00000000000b; BANK.VIEW:3 | ✅ |
| 24 | `POST :5000/auth/login` | t_noaccess | Đăng nhập, xem claim trong token | 200 + token | 200; department_id=d0000000-0000-0000-0000-00000000000a; BANK.VIEW:0, PRODUCT.VIEW:0 | ✅ |
| 25 | `POST :5000/auth/login` | t_nodept | Đăng nhập, xem claim trong token | 200 + token | 200; department_id=None; PRODUCT.CREATE:2, PRODUCT.DELETE:1, PRODUCT.UPDATE:2, PRODUCT.VIEW:2 | ✅ |
| 26 | `POST :5000/auth/login` | t_multi | Đăng nhập, xem claim trong token | 200 + token | 200; department_id=d0000000-0000-0000-0000-00000000000b; PRODUCT.CREATE:2, PRODUCT.DELETE:1, PRODUCT.UPDATE:2, PRODUCT.VIEW:2 | ✅ |
| 27 | `JWT` | admin | Quyền cũ của admin sau migration FixPermissionAccessLevel | PRODUCT.VIEW:3 | có | ✅ |
| 28 | `JWT` | t_staff_a1 | Token có department_id của phòng A | d0000000-0000-0000-0000-00000000000a | d0000000-0000-0000-0000-00000000000a | ✅ |
| 29 | `JWT` | t_multi | 2 role Own + Department → lấy mức cao nhất | PRODUCT.VIEW:2 | ['PRODUCT.VIEW:2'] | ✅ |
| 30 | `JWT` | t_nodept | User không có phòng → không có claim department_id | không có | không có | ✅ |
| 31 | `JWT` | admin | Issuer / Audience | AerationSterilize | AerationSterilize / AerationSterilize | ✅ |
| 32 | `GET :5000/auth/me` | (anonymous) | Không có token | 401 | 401 | ✅ |
| 33 | `GET :5000/auth/me` | t_staff_a1 | Có token | 200 | 200 | ✅ |
| 34 | `POST :5000/auth/refresh-token` | (anonymous) | Không có cookie refreshToken | 401 | 401 (Unauthorized) | ✅ |
| 35 | `POST :5000/auth/refresh-token` | t_own_a1 | Refresh bằng cookie | 200; access token mới | 200; nhận access token mới | ✅ |
| 36 | `JWT` | t_own_a1 | Token sau refresh vẫn có department_id + quyền | d0000000-0000-0000-0000-00000000000a; PRODUCT.VIEW:1 | d0000000-0000-0000-0000-00000000000a; ['PRODUCT.VIEW:1'] | ✅ |
| 37 | `POST :5000/auth/refresh-token` | t_own_a1 | Refresh token mới trả trong body, KHÔNG set lại cookie | - | cookie mới: không | ℹ️ |
| 38 | `POST :5000/auth/refresh-token` | t_own_a1 | Dùng lại refresh token cũ (đã bị thu hồi) | 4xx | 400 | ✅ |

## 2. Product — chặn theo quyền chức năng ([Permission])

| # | API | User | Kịch bản | Mong đợi | Thực tế | KQ |
|---|---|---|---|---|---|---|
| 39 | `GET :5000/products?sortColumn=name` | (anonymous) | Không có token | 401 | 401 | ✅ |
| 40 | `POST :5000/products` | (anonymous) | Tạo product không token (trước đây KHÔNG bị chặn) | 401 | 401 | ✅ |
| 41 | `PUT :5000/products/aaaaaaaa-0001-0000-0000-000000000001` | (anonymous) | Sửa product không token (trước đây KHÔNG bị chặn) | 401 | 401 | ✅ |
| 42 | `DELETE :5000/products/aaaaaaaa-0001-0000-0000-000000000001` | (anonymous) | Xoá product không token (trước đây KHÔNG bị chặn) | 401 | 401 | ✅ |
| 43 | `GET :5000/products?sortColumn=name` | (token rác) | Token không hợp lệ | 401 | 401 | ✅ |
| 44 | `GET :5000/products?sortColumn=name` | t_viewer | Không có quyền PRODUCT.VIEW | 403 | 403 | ✅ |
| 45 | `POST :5000/products` | t_viewer | Không có quyền PRODUCT.CREATE | 403 | 403 | ✅ |
| 46 | `GET :5000/products?sortColumn=name` | t_noaccess | Có PRODUCT.VIEW nhưng AccessLevel = None | 403 | 403 | ✅ |
| 47 | `POST :5000/products` | t_manager | Validation: tên rỗng | 400 | 400 (One or more validation errors occurred.) | ✅ |
| 48 | `POST :5000/products` | t_manager | Validation: giá âm | 400 | 400 (One or more validation errors occurred.) | ✅ |

## 3. Product — phân quyền theo dữ liệu (AccessLevel)

Mỗi user tạo 1 product. Sau đó từng user xem / sửa / xoá để kiểm tra phạm vi All / Department / Own.

| # | API | User | Kịch bản | Mong đợi | Thực tế | KQ |
|---|---|---|---|---|---|---|
| 49 | `POST :5000/products` | t_manager | Tạo TEST-M | 200 | 200 | ✅ |
| 50 | `POST :5000/products` | t_staff_a1 | Tạo TEST-A1 | 200 | 200 | ✅ |
| 51 | `POST :5000/products` | t_staff_a2 | Tạo TEST-A2 | 200 | 200 | ✅ |
| 52 | `POST :5000/products` | t_staff_b1 | Tạo TEST-B1 | 200 | 200 | ✅ |
| 53 | `POST :5000/products` | t_own_a1 | Tạo TEST-O1 | 200 | 200 | ✅ |
| 54 | `POST :5000/products` | t_own_a2 | Tạo TEST-O2 | 200 | 200 | ✅ |
| 55 | `POST :5000/products` | t_multi | Tạo TEST-MULTI | 200 | 200 | ✅ |
| 56 | `SQL Product` | - | CreatedBy / DepartmentId được gán theo user tạo | đúng cho 7 product | đúng cho 7 product | ✅ |
| 57 | `GET :5000/products?searchTerm=TEST-&sortColumn=name&pageSize=100` | admin | Danh sách, phạm vi All (cùng query → cache key tách theo phạm vi) | 200; TEST-A1, TEST-A2, TEST-B1, TEST-M, TEST-MULTI, TEST-O1, TEST-O2 | 200; TEST-A1, TEST-A2, TEST-B1, TEST-M, TEST-MULTI, TEST-O1, TEST-O2 | ✅ |
| 58 | `GET :5000/products?searchTerm=TEST-&sortColumn=name&pageSize=100` | t_manager | Danh sách, phạm vi All (cùng query → cache key tách theo phạm vi) | 200; TEST-A1, TEST-A2, TEST-B1, TEST-M, TEST-MULTI, TEST-O1, TEST-O2 | 200; TEST-A1, TEST-A2, TEST-B1, TEST-M, TEST-MULTI, TEST-O1, TEST-O2 | ✅ |
| 59 | `GET :5000/products?searchTerm=TEST-&sortColumn=name&pageSize=100` | t_staff_a1 | Danh sách, phạm vi Department A (cùng query → cache key tách theo phạm vi) | 200; TEST-A1, TEST-A2, TEST-M, TEST-O1, TEST-O2 | 200; TEST-A1, TEST-A2, TEST-M, TEST-O1, TEST-O2 | ✅ |
| 60 | `GET :5000/products?searchTerm=TEST-&sortColumn=name&pageSize=100` | t_staff_b1 | Danh sách, phạm vi Department B (cùng query → cache key tách theo phạm vi) | 200; TEST-B1, TEST-MULTI | 200; TEST-B1, TEST-MULTI | ✅ |
| 61 | `GET :5000/products?searchTerm=TEST-&sortColumn=name&pageSize=100` | t_own_a1 | Danh sách, phạm vi Own (cùng query → cache key tách theo phạm vi) | 200; TEST-O1 | 200; TEST-O1 | ✅ |
| 62 | `GET :5000/products?searchTerm=TEST-&sortColumn=name&pageSize=100` | t_own_a2 | Danh sách, phạm vi Own (cùng query → cache key tách theo phạm vi) | 200; TEST-O2 | 200; TEST-O2 | ✅ |
| 63 | `GET :5000/products?searchTerm=TEST-&sortColumn=name&pageSize=100` | t_multi | Danh sách, phạm vi Department B (max Own/Dept) (cùng query → cache key tách theo phạm vi) | 200; TEST-B1, TEST-MULTI | 200; TEST-B1, TEST-MULTI | ✅ |
| 64 | `GET :5000/products?searchTerm=TEST-&sortColumn=name&pageSize=100` | t_nodept | Danh sách, phạm vi Department nhưng không có phòng (cùng query → cache key tách theo phạm vi) | 200; (rỗng) | 200; (rỗng) | ✅ |
| 65 | `GET :5000/products?sortColumn=name&pageSize=100` | admin | Danh sách không lọc tên: thấy cả product seed (không có chủ) | 200; có product seed | 200; 11 product | ✅ |
| 66 | `GET :5000/products?sortColumn=name&pageSize=100` | t_staff_a1 | Danh sách không lọc tên: KHÔNG thấy product seed (không thuộc phòng A) | 200; không có product seed | 200; 5 product | ✅ |
| 67 | `GET :5000/products/6764095b-aed8-4f41-e410-08df2ac856ec` | t_staff_a2 | Xem product của đồng nghiệp cùng phòng A | 200 | 200 | ✅ |
| 68 | `GET :5000/products/6764095b-aed8-4f41-e410-08df2ac856ec` | t_staff_b1 | Xem product phòng A từ phòng B | 404 | 404 | ✅ |
| 69 | `GET :5000/products/c2d55207-618b-4994-e413-08df2ac856ec` | t_own_a1 | Xem product của chính mình | 200 | 200 | ✅ |
| 70 | `GET :5000/products/cd73cdeb-d735-47de-e414-08df2ac856ec` | t_own_a1 | Xem product của người khác (mức Own) | 404 | 404 | ✅ |
| 71 | `GET :5000/products/6764095b-aed8-4f41-e410-08df2ac856ec` | t_nodept | User không có phòng xem product phòng A | 404 | 404 | ✅ |
| 72 | `GET :5000/products/aaaaaaaa-0001-0000-0000-000000000001` | t_manager | Mức All xem product seed | 200 | 200 | ✅ |
| 73 | `GET :5000/products/aaaaaaaa-0001-0000-0000-000000000001` | t_staff_a1 | Mức Department xem product seed (không có phòng) | 404 | 404 | ✅ |
| 74 | `GET :5000/products/77af628f-c7af-442e-819b-ba4c8610ee1b` | admin | Id không tồn tại | 404 | 404 | ✅ |
| 75 | `PUT :5000/products/c2d55207-618b-4994-e413-08df2ac856ec` | t_own_a2 | Sửa product của người khác (mức Own) | 404 | 404 | ✅ |
| 76 | `PUT :5000/products/c2d55207-618b-4994-e413-08df2ac856ec` | t_own_a1 | Sửa product của chính mình | 200 | 200 | ✅ |
| 77 | `PUT :5000/products/6764095b-aed8-4f41-e410-08df2ac856ec` | t_staff_b1 | Sửa product phòng A từ phòng B | 404 | 404 | ✅ |
| 78 | `PUT :5000/products/6764095b-aed8-4f41-e410-08df2ac856ec` | t_staff_a2 | Sửa product của đồng nghiệp cùng phòng (UPDATE mức Department) | 200 | 200 | ✅ |
| 79 | `GET :5000/products/6764095b-aed8-4f41-e410-08df2ac856ec` | t_staff_a1 | Xem lại sau khi sửa (cache đã bị xoá) | 200; TEST-A1-UPD | 200; TEST-A1-UPD | ✅ |
| 80 | `DELETE :5000/products/6764095b-aed8-4f41-e410-08df2ac856ec` | t_staff_a2 | Xoá product của đồng nghiệp (DELETE chỉ mức Own) | 404 | 404 | ✅ |
| 81 | `DELETE :5000/products/6764095b-aed8-4f41-e410-08df2ac856ec` | t_staff_b1 | Xoá product phòng khác | 404 | 404 | ✅ |
| 82 | `DELETE :5000/products/6764095b-aed8-4f41-e410-08df2ac856ec` | t_staff_a1 | Xoá product của chính mình | 200 | 200 | ✅ |
| 83 | `DELETE :5000/products/c2d55207-618b-4994-e413-08df2ac856ec` | t_own_a2 | Xoá product của người khác (mức Own) | 404 | 404 | ✅ |
| 84 | `DELETE :5000/products/c2d55207-618b-4994-e413-08df2ac856ec` | t_manager | Mức All xoá product của người khác | 200 | 200 | ✅ |
| 85 | `GET :5000/products?searchTerm=TEST-&sortColumn=name&pageSize=100` | t_staff_a1 | Danh sách phòng A sau khi xoá | 200; TEST-A2, TEST-M, TEST-O2 | 200; TEST-A2, TEST-M, TEST-O2 | ✅ |

## 4. Đổi quyền khi user đang đăng nhập

Quyền nằm trong token (60 phút) và danh sách quyền được cache trên Redis 30 phút (`aeration:perm:{userId}`).

| # | API | User | Kịch bản | Mong đợi | Thực tế | KQ |
|---|---|---|---|---|---|---|
| 86 | `SQL` | - | Cấp thêm PRODUCT.VIEW mức All cho role Master Data Viewer | OK | OK | ✅ |
| 87 | `GET :5000/products?sortColumn=name` | t_viewer | Token cũ (chưa có quyền mới) | 403 | 403 | ✅ |
| 88 | `POST :5000/auth/refresh-token → GET :5000/products` | t_viewer | Refresh ngay sau khi đổi quyền (quyền cũ còn trong cache Redis) | - | refresh 200, products 403 (vẫn dùng quyền cũ trong cache) | ℹ️ |
| 89 | `Redis` | - | Xoá cache quyền của t_viewer | OK | xoá 1 key | ✅ |
| 90 | `GET :5000/products?sortColumn=name` | t_viewer | Refresh lại sau khi xoá cache → có quyền mới | 200 | 200 | ✅ |

## 5. CommonServices — xác thực bằng token của Aeration

| # | API | User | Kịch bản | Mong đợi | Thực tế | KQ |
|---|---|---|---|---|---|---|
| 91 | `GET :5001/banks` | (anonymous) | Không có token | 401 | 401 | ✅ |
| 92 | `POST :5001/auth/login` | admin | Login cũ của CommonServices đã bị bỏ | 404 | 404 | ✅ |
| 93 | `GET :5001/banks` | admin (token kiểu cũ) | Token ký bằng secret/issuer cũ của CommonServices | 401 | 401 | ✅ |
| 94 | `GET :5001/banks` | admin | Token hết hạn | 401 | 401 | ✅ |
| 95 | `GET :5001/banks` | admin | Token bị sửa payload (sai chữ ký) | 401 | 401 | ✅ |
| 96 | `GET :5001/banks` | admin | Token của Aeration | 200 | 200 | ✅ |

## 6. CommonServices — quyền chức năng

| # | API | User | Kịch bản | Mong đợi | Thực tế | KQ |
|---|---|---|---|---|---|---|
| 97 | `GET :5001/languages` | t_viewer | Xem languages (có quyền VIEW) | 200 | 200 | ✅ |
| 98 | `GET :5001/regions` | t_viewer | Xem regions (có quyền VIEW) | 200 | 200 | ✅ |
| 99 | `GET :5001/currencies` | t_viewer | Xem currencies (có quyền VIEW) | 200 | 200 | ✅ |
| 100 | `GET :5001/countries` | t_viewer | Xem countries (có quyền VIEW) | 200 | 200 | ✅ |
| 101 | `GET :5001/cities` | t_viewer | Xem cities (có quyền VIEW) | 200 | 200 | ✅ |
| 102 | `GET :5001/banks` | t_viewer | Xem banks (có quyền VIEW) | 200 | 200 | ✅ |
| 103 | `POST :5001/banks` | t_viewer | Tạo bank khi chỉ có quyền VIEW | 403 | 403 | ✅ |
| 104 | `GET :5001/banks` | t_staff_a1 | Không có quyền BANK.VIEW | 403 | 403 | ✅ |
| 105 | `GET :5001/banks` | t_noaccess | BANK.VIEW nhưng AccessLevel = None | 403 | 403 | ✅ |
| 106 | `GET :5001/masterdata/banks` | (anonymous) | MasterData không token | 401 | 401 | ✅ |
| 107 | `GET :5001/masterdata/languages` | t_staff_a1 | MasterData/languages: mọi user đã đăng nhập | 200 | 200 | ✅ |
| 108 | `GET :5001/masterdata/languages/native` | t_staff_a1 | MasterData/languages/native: mọi user đã đăng nhập | 200 | 200 | ✅ |
| 109 | `GET :5001/masterdata/regions` | t_staff_a1 | MasterData/regions: mọi user đã đăng nhập | 200 | 200 | ✅ |
| 110 | `GET :5001/masterdata/currencies` | t_staff_a1 | MasterData/currencies: mọi user đã đăng nhập | 200 | 200 | ✅ |
| 111 | `GET :5001/masterdata/countries` | t_staff_a1 | MasterData/countries: mọi user đã đăng nhập | 200 | 200 | ✅ |
| 112 | `GET :5001/masterdata/cities` | t_staff_a1 | MasterData/cities: mọi user đã đăng nhập | 200 | 200 | ✅ |
| 113 | `GET :5001/masterdata/banks` | t_staff_a1 | MasterData/banks: mọi user đã đăng nhập | 200 | 200 | ✅ |

## 7. CommonServices — CRUD master data (admin)

Mã dữ liệu test có hậu tố `212659` để chạy lại không bị trùng (xoá là xoá mềm, mã vẫn giữ unique).

| # | API | User | Kịch bản | Mong đợi | Thực tế | KQ |
|---|---|---|---|---|---|---|
| 114 | `POST :5001/regions` | admin | Tạo region | 200 | 200 | ✅ |
| 115 | `GET :5001/regions/774ed3c1-2335-4283-a4b3-00b76f64fbf9` | admin | Xem region theo id | 200 | 200 | ✅ |
| 116 | `PUT :5001/regions/774ed3c1-2335-4283-a4b3-00b76f64fbf9` | admin | Sửa region | 200; Vùng test 212659 (sửa) | 200; Vùng test 212659 (sửa) | ✅ |
| 117 | `PUT :5001/regions/774ed3c1-2335-4283-a4b3-00b76f64fbf9/deactivate` | admin | Ngừng kích hoạt region | 200 | 200 | ✅ |
| 118 | `GET :5001/regions/list?status=0` | admin | Danh sách region trạng thái Inactive | 200; có bản ghi vừa ngừng | 200; có | ✅ |
| 119 | `PUT :5001/regions/774ed3c1-2335-4283-a4b3-00b76f64fbf9/activate` | admin | Kích hoạt lại region | 200 | 200 | ✅ |
| 120 | `GET :5001/regions?pageSize=100` | admin | Danh sách phân trang region | 200; có bản ghi mới | 200; 2 bản ghi | ✅ |
| 121 | `POST :5001/currencies` | admin | Tạo currency | 200 | 200 | ✅ |
| 122 | `GET :5001/currencies/db77cf67-3a73-492d-9abb-5f9ce4fa3842` | admin | Xem currency theo id | 200 | 200 | ✅ |
| 123 | `PUT :5001/currencies/db77cf67-3a73-492d-9abb-5f9ce4fa3842` | admin | Sửa currency | 200; Tiền test 212659 (sửa) | 200; Tiền test 212659 (sửa) | ✅ |
| 124 | `PUT :5001/currencies/db77cf67-3a73-492d-9abb-5f9ce4fa3842/deactivate` | admin | Ngừng kích hoạt currency | 200 | 200 | ✅ |
| 125 | `GET :5001/currencies/list?status=0` | admin | Danh sách currency trạng thái Inactive | 200; có bản ghi vừa ngừng | 200; có | ✅ |
| 126 | `PUT :5001/currencies/db77cf67-3a73-492d-9abb-5f9ce4fa3842/activate` | admin | Kích hoạt lại currency | 200 | 200 | ✅ |
| 127 | `GET :5001/currencies?pageSize=100` | admin | Danh sách phân trang currency | 200; có bản ghi mới | 200; 3 bản ghi | ✅ |
| 128 | `POST :5001/countries` | admin | Tạo country | 200 | 200 | ✅ |
| 129 | `GET :5001/countries/327d55e2-7231-46a7-8ddd-57600965c3f4` | admin | Xem country theo id | 200 | 200 | ✅ |
| 130 | `PUT :5001/countries/327d55e2-7231-46a7-8ddd-57600965c3f4` | admin | Sửa country | 200; Nước test 212659 (sửa) | 200; Nước test 212659 (sửa) | ✅ |
| 131 | `PUT :5001/countries/327d55e2-7231-46a7-8ddd-57600965c3f4/deactivate` | admin | Ngừng kích hoạt country | 200 | 200 | ✅ |
| 132 | `GET :5001/countries/list?status=0` | admin | Danh sách country trạng thái Inactive | 200; có bản ghi vừa ngừng | 200; có | ✅ |
| 133 | `PUT :5001/countries/327d55e2-7231-46a7-8ddd-57600965c3f4/activate` | admin | Kích hoạt lại country | 200 | 200 | ✅ |
| 134 | `GET :5001/countries?pageSize=100` | admin | Danh sách phân trang country | 200; có bản ghi mới | 200; 2 bản ghi | ✅ |
| 135 | `POST :5001/cities` | admin | Tạo city | 200 | 200 | ✅ |
| 136 | `GET :5001/cities/c3396519-b798-43af-84f7-1ebf72de29cd` | admin | Xem city theo id | 200 | 200 | ✅ |
| 137 | `PUT :5001/cities/c3396519-b798-43af-84f7-1ebf72de29cd` | admin | Sửa city | 200; Thành phố test 212659 (sửa) | 200; Thành phố test 212659 (sửa) | ✅ |
| 138 | `PUT :5001/cities/c3396519-b798-43af-84f7-1ebf72de29cd/deactivate` | admin | Ngừng kích hoạt city | 200 | 200 | ✅ |
| 139 | `GET :5001/cities/list?status=0` | admin | Danh sách city trạng thái Inactive | 200; có bản ghi vừa ngừng | 200; có | ✅ |
| 140 | `PUT :5001/cities/c3396519-b798-43af-84f7-1ebf72de29cd/activate` | admin | Kích hoạt lại city | 200 | 200 | ✅ |
| 141 | `GET :5001/cities?pageSize=100` | admin | Danh sách phân trang city | 200; có bản ghi mới | 200; 3 bản ghi | ✅ |
| 142 | `POST :5001/languages` | admin | Tạo language | 200 | 200 | ✅ |
| 143 | `GET :5001/languages/e298efa5-fa8b-491e-b724-de57155d41eb` | admin | Xem language theo id | 200 | 200 | ✅ |
| 144 | `PUT :5001/languages/e298efa5-fa8b-491e-b724-de57155d41eb` | admin | Sửa language | 200; Ngôn ngữ test 212659 (sửa) | 200; Ngôn ngữ test 212659 (sửa) | ✅ |
| 145 | `PUT :5001/languages/e298efa5-fa8b-491e-b724-de57155d41eb/deactivate` | admin | Ngừng kích hoạt language | 200 | 200 | ✅ |
| 146 | `GET :5001/languages/list?status=0` | admin | Danh sách language trạng thái Inactive | 200; có bản ghi vừa ngừng | 200; có | ✅ |
| 147 | `PUT :5001/languages/e298efa5-fa8b-491e-b724-de57155d41eb/activate` | admin | Kích hoạt lại language | 200 | 200 | ✅ |
| 148 | `GET :5001/languages?pageSize=100` | admin | Danh sách phân trang language | 200; có bản ghi mới | 200; 4 bản ghi | ✅ |
| 149 | `POST :5001/banks` | admin | Tạo bank | 200 | 200 | ✅ |
| 150 | `GET :5001/banks/f6af65d7-1cb1-4ffd-88f1-5d0cb076917b` | admin | Xem bank theo id | 200 | 200 | ✅ |
| 151 | `PUT :5001/banks/f6af65d7-1cb1-4ffd-88f1-5d0cb076917b` | admin | Sửa bank | 200; Ngân hàng test 212659 (sửa) | 200; Ngân hàng test 212659 (sửa) | ✅ |
| 152 | `PUT :5001/banks/f6af65d7-1cb1-4ffd-88f1-5d0cb076917b/deactivate` | admin | Ngừng kích hoạt bank | 200 | 200 | ✅ |
| 153 | `GET :5001/banks/list?status=0` | admin | Danh sách bank trạng thái Inactive | 200; có bản ghi vừa ngừng | 200; có | ✅ |
| 154 | `PUT :5001/banks/f6af65d7-1cb1-4ffd-88f1-5d0cb076917b/activate` | admin | Kích hoạt lại bank | 200 | 200 | ✅ |
| 155 | `GET :5001/banks?pageSize=100` | admin | Danh sách phân trang bank | 200; có bản ghi mới | 200; 2 bản ghi | ✅ |
| 156 | `GET :5001/banks/list` | admin | Accept-Language: en-US → tên tiếng Anh | 200; Test bank 212659 (edited) | 200; Test bank 212659 (edited) | ✅ |
| 157 | `SQL Banks` | admin | CreatedBy lấy từ token Aeration (user id của admin) | 33333333-3333-3333-3333-333333333333 | 33333333-3333-3333-3333-333333333333 | ✅ |
| 158 | `POST :5001/banks` | admin | Tạo bank trùng mã | 400 | 400 (Bad Request) | ✅ |
| 159 | `POST :5001/banks` | admin | Validation: mã + tên rỗng | 400 | 400 (One or more validation errors occurred.) | ✅ |
| 160 | `POST :5001/cities` | admin | Tạo city với country không tồn tại | 404 | 404 | ✅ |
| 161 | `DELETE :5001/cities/c3396519-b798-43af-84f7-1ebf72de29cd` | admin | Xoá city | 200 | 200 | ✅ |
| 162 | `GET :5001/cities/c3396519-b798-43af-84f7-1ebf72de29cd` | admin | Xem city sau khi xoá | 404 | 404 | ✅ |
| 163 | `DELETE :5001/countries/327d55e2-7231-46a7-8ddd-57600965c3f4` | admin | Xoá country | 200 | 200 | ✅ |
| 164 | `GET :5001/countries/327d55e2-7231-46a7-8ddd-57600965c3f4` | admin | Xem country sau khi xoá | 404 | 404 | ✅ |
| 165 | `DELETE :5001/currencies/db77cf67-3a73-492d-9abb-5f9ce4fa3842` | admin | Xoá currency | 200 | 200 | ✅ |
| 166 | `GET :5001/currencies/db77cf67-3a73-492d-9abb-5f9ce4fa3842` | admin | Xem currency sau khi xoá | 404 | 404 | ✅ |
| 167 | `DELETE :5001/regions/774ed3c1-2335-4283-a4b3-00b76f64fbf9` | admin | Xoá region | 200 | 200 | ✅ |
| 168 | `GET :5001/regions/774ed3c1-2335-4283-a4b3-00b76f64fbf9` | admin | Xem region sau khi xoá | 404 | 404 | ✅ |
| 169 | `DELETE :5001/languages/e298efa5-fa8b-491e-b724-de57155d41eb` | admin | Xoá language | 200 | 200 | ✅ |
| 170 | `GET :5001/languages/e298efa5-fa8b-491e-b724-de57155d41eb` | admin | Xem language sau khi xoá | 404 | 404 | ✅ |
| 171 | `DELETE :5001/banks/f6af65d7-1cb1-4ffd-88f1-5d0cb076917b` | admin | Xoá bank | 200 | 200 | ✅ |
| 172 | `GET :5001/banks/f6af65d7-1cb1-4ffd-88f1-5d0cb076917b` | admin | Xem bank sau khi xoá | 404 | 404 | ✅ |

## 8. Logout — thu hồi token trên cả 2 service

| # | API | User | Kịch bản | Mong đợi | Thực tế | KQ |
|---|---|---|---|---|---|---|
| 173 | `GET :5000/products?sortColumn=name` | t_manager | Trước logout | 200 | 200 | ✅ |
| 174 | `GET :5001/banks` | t_manager | Trước logout (CommonServices) | 200 | 200 | ✅ |
| 175 | `POST :5000/auth/logout` | (anonymous) | Logout không có token | 401 | 401 | ✅ |
| 176 | `POST :5000/auth/logout` | t_manager | Logout | 200 | 200 | ✅ |
| 177 | `GET :5000/products?sortColumn=name` | t_manager | Token đã logout gọi Aeration | 401 | 401 | ✅ |
| 178 | `GET :5001/banks` | t_manager | Token đã logout gọi CommonServices (danh sách thu hồi dùng chung) | 401 | 401 | ✅ |
| 179 | `POST :5000/auth/refresh-token` | t_manager | Refresh bằng cookie đã logout | 4xx | 400 | ✅ |
