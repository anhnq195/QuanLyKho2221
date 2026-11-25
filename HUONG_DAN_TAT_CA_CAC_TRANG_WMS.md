# TÀI LIỆU GIẢI THÍCH CHI TIẾT TẤT CẢ CÁC TRANG & PHÂN HỆ TRONG HỆ THỐNG QUẢN LÝ KHO (WMS - QuanLyKho2221)

> **Dự án:** Hệ Thống Quản Lý Kho (Warehouse Management System - WMS)  
> **Sinh viên thực hiện:** Nguyễn Quốc Anh  
> **Nền tảng công nghệ:** ASP.NET Core 10.0 MVC & RESTful API, Entity Framework Core 10, SQL Server, ASP.NET Core Identity, ClosedXML, Chart.js, Html5-QRCode, JsBarcode.

Tài liệu này mô tả chi tiết **toàn bộ 48 trang giao diện (Razor Views)**, **26 Controllers**, **các thuật toán cốt lõi** và **cách vận hành từng màn hình** trong hệ thống để phục vụ viết báo cáo tốt nghiệp (`T75`, `T88`, `T89`, `T128`) cũng như thuyết trình Demo bảo vệ đồ án (`T73`, `T95`).

---
ng 
## MỤC LỤC NHANH THEO 10 NHÓM CHỨC NĂNG

1. [Nhóm 1: Xác thực & Kiểm soát Truy cập (`Account`)](#1-nhóm-1-xác-thực--kiểm-soát-truy-cập-account)
2. [Nhóm 2: Bảng Điều khiển, Tìm kiếm Toàn cục & Phân tích Hiệu suất (`Dashboard`, `GlobalSearch`, `Analytics`)](#2-nhóm-2-bảng-điều-khiển-tìm-kiếm-toàn-cục--phân-tích-hiệu-suất)
3. [Nhóm 3: Danh mục Hàng hóa, Trung tâm Cảnh báo & Nhà cung cấp (`Products`, `Categories`, `Suppliers`)](#3-nhóm-3-danh-mục-hàng-hóa-trung-tâm-cảnh-báo--nhà-cung-cấp)
4. [Nhóm 4: Cấu trúc Kho 4 Tầng & Quản lý Lô hàng (`Warehouses` $\rightarrow$ `Areas` $\rightarrow$ `Shelves` $\rightarrow$ `Bins` $\rightarrow$ `ProductBatches`)](#4-nhóm-4-cấu-trúc-kho-4-tầng--quản-lý-lô-hàng)
5. [Nhóm 5: Quét Mã Kho Mobile & In Tem Barcode/QR (`Scanner`)](#5-nhóm-5-quét-mã-kho-mobile--in-tem-barcodeqr-scanner)
6. [Nhóm 6: Nghiệp vụ Nhập Kho & Gợi ý Vị trí Tự động (`Imports`, `ReceiptPrint`)](#6-nhóm-6-nghiệp-vụ-nhập-kho--gợi-ý-vị-trí-tự-động-imports)
7. [Nhóm 7: Nghiệp vụ Xuất Kho Thông minh FEFO/FIFO/LIFO & Lộ trình Lấy hàng (`Exports`)](#7-nhóm-7-nghiệp-vụ-xuất-kho-thông-minh-fefofifolifo--lộ-trình-lấy-hàng-exports)
8. [Nhóm 8: Điều chuyển, Kiểm kê, Báo cáo Tồn kho & Thẻ kho Kế toán (`WarehouseTransfers`, `InventoryChecks`, `Inventory`, `StockCards`)](#8-nhóm-8-điều-chuyển-kiểm-kê-báo-cáo-tồn-kho--thẻ-kho-kế-toán)
9. [Nhóm 9: Quản trị Hệ thống, Công cụ Excel/Sao lưu & Kiểm thử Tự động (`Users`, `WarehousePermissions`, `ActivityLogs`, `DataTools`, `SystemTests`)](#9-nhóm-9-quản-trị-hệ-thống-công-cụ-excelsao-lưu--kiểm-thử-tự-động)
10. [Nhóm 10: Hệ thống RESTful Web API (`ApiControllers`)](#10-nhóm-10-hệ-thống-restful-web-api)

---

## 1. NHÓM 1: XÁC THỰC & KIỂM SOÁT TRUY CẬP (`Account`)

**Controller xử lý:** `Controllers/AccountController.cs`  
**Task liên quan:** `T08` (`RQ01` / `PB01`)

| Đường dẫn (URL) | File View | Quyền truy cập | Giải thích chức năng chi tiết |
|---|---|---|---|
| `/Account/Login` | `Views/Account/Login.cshtml` | Công khai | Trang đăng nhập vào hệ thống WMS. Xác thực thông qua `SignInManager<IdentityUser>`, kiểm tra tài khoản bị khóa (`Lockout`) và tự động ghi vết thời điểm đăng nhập vào bảng `ActivityLogs`.<br>• Tài khoản mẫu Admin: `admin` / `Admin@123`<br>• Tài khoản mẫu Nhân viên: `employee` / `Employee@123` |
| `/Account/Logout` | *(Action POST)* | Đã đăng nhập | Đăng xuất phiên làm việc an toàn (có chống CSRF bằng `ValidateAntiForgeryToken`) và ghi nhận lịch sử đăng xuất. |
| `/Account/AccessDenied` | `Views/Account/AccessDenied.cshtml` | Đã đăng nhập | Trang thông báo từ chối truy cập khi nhân viên (`Employee`) cố tình truy cập vào các đường dẫn chỉ dành cho Quản lý (`Admin`) như duyệt phiếu, quản lý người dùng hoặc công cụ sao lưu. |

---

## 2. NHÓM 2: BẢNG ĐIỀU KHIỂN, TÌM KIẾM TOÀN CỤC & PHÂN TÍCH HIỆU SUẤT

### 2.1. Trang Tổng quan Kho hàng (`Dashboard`)
- **Controller:** `Controllers/DashboardController.cs`
- **File View:** `Views/Dashboard/Index.cshtml`
- **Đường dẫn:** `/` hoặc `/Dashboard/Index`
- **Task liên quan:** `T14`, `T50` (`PB02`, `PB08`)
- **Giải thích chi tiết:**
  - Là màn hình trung tâm hiển thị ngay sau khi đăng nhập.
  - **Thẻ thống kê KPI:** Tổng số mã sản phẩm (`SKU`), tổng số danh mục, tổng số nhà cung cấp, tổng số lượng hàng tồn kho và tổng giá trị tồn kho ước tính ($\sum \text{Quantity} \times \text{Price}$).
  - **Cảnh báo tức thời:** Đếm số mặt hàng sắp hết (`1–5`) và đã hết hàng (`<= 0`).
  - **Biểu đồ xu hướng 6 tháng (`Chart.js`):** So sánh trực quan tổng lượng hàng Nhập kho (đã duyệt) và Xuất kho (đã duyệt) theo từng tháng trong 6 tháng gần nhất.
  - **Bảng hoạt động gần đây:** 5 phiếu nhập và 5 phiếu xuất mới được phê duyệt gần nhất.

### 2.2. Tìm kiếm Toàn cục (`Global Search`)
- **Controller:** `Controllers/GlobalSearchController.cs`
- **File View:** `Views/GlobalSearch/Index.cshtml` & Thanh Header trên `Views/Shared/_Layout.cshtml`
- **Đường dẫn:** `/GlobalSearch?q=...` và API gợi ý nhanh `/GlobalSearch/Quick?q=...`
- **Task liên quan:** `T22` (`RQ22` / `PB04`)
- **Giải thích chi tiết:**
  - **Trên thanh Topbar ở mọi trang:** Nhấn phím tắt **`Ctrl + K`** (hoặc click vào ô tìm kiếm) và gõ từ 2 ký tự trở lên, hệ thống sẽ xổ xuống danh sách gợi ý trực tiếp (Live Autocomplete) từ 5 bảng dữ liệu khác nhau.
  - **Trang kết quả đầy đủ (`/GlobalSearch`):** Hiển thị đồng thời kết quả khớp theo 5 khối: (1) Sản phẩm SKU, (2) Lô hàng, (3) Phiếu nhập kho, (4) Phiếu xuất kho, và (5) Ô chứa/Kệ. Tự động phân quyền để nhân viên chỉ nhìn thấy phiếu do mình tạo.

### 2.3. Phân tích Hiệu suất Lấp đầy & Vòng quay Tồn kho (`Analytics`)
- **Controller:** `Controllers/AnalyticsController.cs`
- **File View:** `Views/Analytics/Index.cshtml`
- **Đường dẫn:** `/Analytics` (hoặc `/Analytics?days=30|90|180|365`)
- **Task liên quan:** `T55`, `T97`, `T98`, `T108` (`PB08`, `PB10`)
- **Giải thích chi tiết:**
  - **Đo lường Vòng quay Tồn kho (`Inventory Turnover Ratio - T55`):**
    $$\text{Turnover Ratio} = \frac{\text{Tổng lượng xuất trong kỳ}}{\text{Tồn kho bình quân}}, \quad \text{DSI (Số ngày lưu kho)} = \frac{\text{Số ngày trong kỳ}}{\text{Turnover Ratio}}$$
  - **Phân tích FSN (`Fast / Slow / Non-moving Dead Stock - T98`):** Tự động gắn nhãn mặt hàng nào đang **Luân chuyển nhanh (`Fast-Moving`)**, **Luân chuyển chậm (`Slow-Moving`)**, hoặc **Hàng tồn đọng / Chết (`Dead Stock` — có tồn kho nhưng 0 xuất trong suốt kỳ)** để doanh nghiệp có kế hoạch xả kho.
  - **Phân tích Tỷ lệ Lấp đầy từng Kho (`Occupancy Rate - T108`):** Thống kê từng kho đã dùng bao nhiêu phần trăm sức chứa tối đa.
  - **Xuất Biểu đồ ra Ảnh PNG (`T97`) & Xuất Excel 2 Sheets:** Nút tải biểu đồ dưới dạng ảnh `.png` sắc nét và nút xuất file Excel 2 Sheets (`LuanChuyenTonKho` & `HieuSuatKho`).

---

## 3. NHÓM 3: DANH MỤC HÀNG HÓA, TRUNG TÂM CẢNH BÁO & NHÀ CUNG CẤP

### 3.1. Quản lý Sản phẩm / Mã SKU (`Products`)
- **Controller:** `Controllers/ProductsController.cs`
- **Task liên quan:** `T11`, `T12`, `T13`, `T16`, `T36`, `T62`, `T102`, `T103`

| Đường dẫn (URL) | File View | Giải thích chức năng chi tiết |
|---|---|---|
| `/Products/Index` | `Views/Products/Index.cshtml` | Danh sách toàn bộ hàng hóa kèm bộ lọc đa chiều: Tìm theo tên/mã SKU, lọc theo danh mục, lọc theo trạng thái tồn (`Còn hàng > 5`, `Sắp hết 1–5`, `Hết hàng 0`) và **lọc sâu theo vị trí không gian 5 cấp**: Kho $\rightarrow$ Khu vực $\rightarrow$ Mã kệ $\rightarrow$ Tầng $\rightarrow$ Mã ô chứa. |
| `/Products/Create` | `Views/Products/Create.cshtml` | Thêm sản phẩm mới. Hệ thống tự động sinh mã SKU ngẫu nhiên không trùng dạng `SP-XXXXXX` bằng thuật toán mật mã `RandomNumberGenerator`, hỗ trợ cờ **Có hạn sử dụng (`HasExpiryDate`)** để bắt buộc nhập HSD khi nhập kho. |
| `/Products/Edit/{id}` | `Views/Products/Edit.cshtml` | Chỉnh sửa thông tin sản phẩm, đơn giá, danh mục và ghi vết thay đổi giá cũ $\rightarrow$ giá mới vào `ActivityLogs`. |
| `/Products/Delete/{id}` | `Views/Products/Delete.cshtml` | Xóa sản phẩm. **Cơ chế bảo vệ toàn vẹn dữ liệu:** Tự động kiểm tra nếu sản phẩm đã từng phát sinh Phiếu nhập, Phiếu xuất hoặc Phiếu kiểm kê thì khóa nút xóa và cảnh báo người dùng. |
| `/Products/StockAlerts` | `Views/Products/StockAlerts.cshtml` | **Trung tâm Cảnh báo Đặt hàng Bổ sung (`Auto Reorder - T62`) & Cảnh báo Hạn dùng (`FEFO - T103`):**<br>• **Tab 1 (Auto Reorder):** Liệt kê các mặt hàng hết hàng hoặc chạm ngưỡng tối thiểu (`<= 5`), tự động tính số lượng đề xuất đặt bù về định mức an toàn (`20`) kèm nút lập phiếu nhập ngay.<br>• **Tab 2 (FEFO Reminder):** Liệt kê các lô hàng đã quá hạn hoặc còn hạn dưới 30 ngày kèm nút **"Ưu tiên xuất FEFO"**. |

### 3.2. Quản lý Nhóm / Danh mục Hàng hóa (`Categories`)
- **Controller:** `Controllers/CategoriesController.cs`
- **Các trang:** `/Categories/Index`, `/Categories/Create`, `/Categories/Edit/{id}`, `/Categories/Delete/{id}` (`Views/Categories/*.cshtml`)
- **Task liên quan:** `T37` (`RQ37` / `PB06`)
- **Giải thích:** Phân nhóm hàng hóa (Thực phẩm, Đồ uống, Thiết bị điện tử, Hàng lạnh, Hàng dễ vỡ...), thống kê số sản phẩm thuộc từng nhóm.

### 3.3. Quản lý Nhà cung cấp & Đối tác (`Suppliers`)
- **Controller:** `Controllers/SuppliersController.cs`
- **Các trang:** `/Suppliers/Index`, `/Suppliers/Create`, `/Suppliers/Edit/{id}`, `/Suppliers/Delete/{id}` (`Views/Suppliers/*.cshtml`) và `/Suppliers/ExportExcel`
- **Task liên quan:** `T42`, `T43` (`PB06`)
- **Giải thích:** Quản lý thông tin liên hệ đối tác cung ứng hàng hóa. Trang `Index` tích hợp nút **"Xuất Excel & Lịch sử giao hàng" (`T43`)** tổng hợp tổng số phiếu nhập, tổng sản lượng đã cung cấp và ngày giao hàng gần nhất của từng nhà cung cấp.

---

## 4. NHÓM 4: CẤU TRÚC KHO 4 TẦNG & QUẢN LÝ LÔ HÀNG

Hệ thống mô hình hóa không gian kho vật lý theo cấu trúc cây 4 cấp chuẩn WMS (`T38`, `T39`, `T100`, `T101`):
$$\text{Warehouse (Kho)} \longrightarrow \text{WarehouseArea (Khu vực)} \longrightarrow \text{WarehouseShelf (Dãy kệ)} \longrightarrow \text{WarehouseBin (Tầng / Ô chứa)}$$

| Phân cấp | Controller | Các trang View (`Views/...`) | Giải thích chức năng chi tiết |
|---|---|---|---|
| **Cấp 1: Kho (`Warehouses`)** | `WarehousesController.cs` | `Index`, `Details`, `Create`, `Edit`, `Delete` | Quản lý danh sách các kho vật lý (Mã kho duy nhất, tên kho, địa chỉ, người quản lý, trạng thái hoạt động). Trang `Details` hiển thị sơ đồ phân cấp toàn bộ Khu vực $\rightarrow$ Kệ $\rightarrow$ Ô chứa bên trong kho. |
| **Cấp 2: Khu vực (`WarehouseAreas`)** | `WarehouseAreasController.cs` | `Index`, `Create`, `Edit` | Quản lý các phân khu chức năng bên trong một kho (Khu hàng khô, Khu hàng mát, Khu chờ xuất Picking Zone...). |
| **Cấp 3: Dãy kệ (`WarehouseShelves`)** | `WarehouseShelvesController.cs` | `Index`, `Create`, `Edit` | Quản lý các dãy kệ chứa hàng nằm trong từng khu vực. |
| **Cấp 4: Ô chứa (`WarehouseBins`)** | `WarehouseBinsController.cs` | `Index`, `Create`, `Edit` | Quản lý từng ô chứa gắn với số **Tầng (`FloorNumber`)** và **Sức chứa tối đa (`MaxCapacity` - `T39`)**. Trang `Index` hiển thị thanh tiến trình màu sắc (`Xanh` $\rightarrow$ `Vàng` $\rightarrow$ `Đỏ`) báo hiệu tỷ lệ lấp đầy của từng ô và có nút **"In tem ô chứa kệ này"**. |
| **Lô hàng (`ProductBatches`)** | `ProductBatchesController.cs` | `Index`, `Create`, `Edit`, `History` | Quản lý từng lô hàng cụ thể (`BatchCode`, `ManufactureDate`, `ExpiryDate`, `Quantity`) nằm tại từng ô chứa.<br>• Trang `Index`: Thống kê lô hết hạn/cận date, chọn nhiều lô để **In tem QR/Barcode hàng loạt**.<br>• Trang `History` (`/ProductBatches/History/{id}` - **`T67`**): **Truy xuất nguồn gốc & vòng đời lô hàng (Batch Genealogy)** qua 4 chiều: Nguồn phiếu nhập từ NCC $\rightarrow$ Phiếu xuất cho khách $\rightarrow$ Lịch sử điều chuyển ô kệ $\rightarrow$ Lịch sử kiểm kê. |

---

## 5. NHÓM 5: QUÉT MÃ KHO MOBILE & IN TEM BARCODE/QR (`Scanner`)

**Controller xử lý:** `Controllers/ScannerController.cs`  
**Task liên quan:** `T23` (`RQ23` / `PB04`), `T111` (`RQ111` / `PB10`)

| Đường dẫn (URL) | File View | Giải thích chức năng chi tiết |
|---|---|---|
| `/Scanner/Index` | `Views/Scanner/Index.cshtml` | **Giao diện Quét Mã Kho trên Điện thoại & Súng bắn mã vạch USB (`T23`):**<br>• Mở trực tiếp camera sau của điện thoại bằng `html5-qrcode` để quét QR Code hoặc Barcode 1D (`CODE128`, `EAN13`, `CODE39`).<br>• Nhận diện thông minh 5 loại mã: Tem lô hàng (`WMS-B{id}`), Tem ô chứa (`WMS-L{id}`), Mã lô nhà cung cấp (`LOT-...`), Mã SKU (`SP-...`), và Mã ô kệ.<br>• Phát âm thanh **Beep** và **Rung** khi quét trúng; hiển thị ngay số lượng tồn, chờ xuất, khả dụng, hạn dùng và 4 nút thao tác nhanh (*Xem Thẻ kho, Xuất kho, Điều chuyển, In tem*). |
| `/Scanner/Labels` | `Views/Scanner/Labels.cshtml` | **Trang In Tem Nhiệt / Decal Barcode & QR Code (`T111`):**<br>• Dàn trang tự động theo kích thước tem chuẩn **70 × 42 mm** (in trên giấy decal hoặc khổ A4).<br>• Mỗi tem tích hợp cả **QR Code** lẫn **Barcode `CODE128`** kèm thông tin SKU, Lô, HSD và đường dẫn vị trí kệ. |

---

## 6. NHÓM 6: NGHIỆP VỤ NHẬP KHO & GỢI Ý VỊ TRÍ TỰ ĐỘNG (`Imports`)

**Controller xử lý:** `Controllers/ImportsController.cs` & `Controllers/ReceiptPrintController.cs`  
**Task liên quan:** `T15`, `T25`, `T28`, `T39`, `T40`, `T41`, `T61`, `T66`

| Đường dẫn (URL) | File View | Giải thích chức năng chi tiết |
|---|---|---|
| `/Imports/Index` | `Views/Imports/Index.cshtml` | Danh sách phiếu nhập kho, bộ lọc theo từ khóa, khoảng ngày và trạng thái (`Pending`, `Approved`, `Rejected`, `Cancelled`). Nhân viên xem phiếu của mình; Admin xem và quản lý toàn bộ. |
| `/Imports/Create` | `Views/Imports/Create.cshtml` | **Lập phiếu nhập kho mới (`T28`) tích hợp Gợi ý Vị trí Kệ trống Tối ưu (`T66`):**<br>• Tự động sinh mã lô hàng `LOT-yyyyMMdd-XXXXXX`.<br>• Nút **"Gợi ý vị trí trống tối ưu" (`T66`):** Tự động quét các kho đang hoạt động và đề xuất Top 4 ô chứa tốt nhất theo thứ tự: (1) Ô đang chứa sẵn cùng sản phẩm đó để gom chung khu vực, (2) Ô đang trống hoàn toàn, (3) Ô còn dư nhiều sức chứa nhất.<br>• Kiểm tra quyền nhập kho (`CanImport - T61`), sức chứa tối đa của ô (`MaxCapacity - T39`) và kiểm tra hạn sử dụng hợp lệ. |
| `/Imports/Details/{id}` | `Views/Imports/Details.cshtml` | **Chi tiết & Phê duyệt phiếu nhập (`T40`, `T41`, `T15`):**<br>• Khi phiếu ở trạng thái `Pending`, tồn kho chưa thay đổi.<br>• Khi Admin bấm **Duyệt (`Approve`)**: Chạy trong Transaction `IsolationLevel.Serializable`, kiểm tra lại tải trọng ô kệ, tạo hoặc cộng dồn `ProductBatch`, cập nhật `Product.Quantity` và **tự động ghi dòng Nhập (+) vào sổ Thẻ kho (`StockCard`)**.<br>• Hỗ trợ **Từ chối (`Reject`)** kèm lý do bắt buộc hoặc **Hủy phiếu (`Cancel`)**. |
| `/ReceiptPrint/Import/{id}` | `Views/ReceiptPrint/Import.cshtml` | Mẫu in Phiếu Nhập Kho chuẩn biểu mẫu kế toán khổ A4 (chỉ cho phép in khi phiếu đã được duyệt). |

---

## 7. NHÓM 7: NGHIỆP VỤ XUẤT KHO THÔNG MINH FEFO/FIFO/LIFO & LỘ TRÌNH LẤY HÀNG (`Exports`)

**Controller xử lý:** `Controllers/ExportsController.cs` & `Controllers/ReceiptPrintController.cs`  
**Service cốt lõi:** `Services/WarehouseRuleService.cs`  
**Task liên quan:** `T15`, `T20`, `T25`, `T40`, `T41`, `T45`, `T60`, `T106`, `T113`

| Đường dẫn (URL) | File View | Giải thích chức năng chi tiết |
|---|---|---|
| `/Exports/Index` | `Views/Exports/Index.cshtml` | Danh sách phiếu xuất kho kèm bộ lọc tìm kiếm theo mã phiếu, sản phẩm, tên người nhận và khoảng thời gian. |
| `/Exports/Create` | `Views/Exports/Create.cshtml` | **Lập phiếu xuất kho thông minh 2 chế độ (`T60`):**<br>• **Tab 1 — Xuất tự động theo Quy tắc (`FEFO / FIFO / LIFO`):** Người dùng chỉ cần chọn Sản phẩm, chọn Quy tắc (**FEFO**: Ưu tiên lô sắp hết hạn trước & loại bỏ lô hết hạn; **FIFO**: Nhập trước xuất trước; **LIFO**: Nhập sau xuất trước) và nhập Số lượng cần xuất. Bấm **"Xem trước phân bổ lô"**, hệ thống hiển thị ngay bảng kế hoạch lấy bao nhiêu chiếc từ những lô nào, ở ô kệ nào, và tự động tạo phiếu xuất giữ chỗ.<br>• **Tab 2 — Chọn lô thủ công:** Chọn trực tiếp 1 lô cụ thể để xuất. |
| `/Exports/Details/{id}` | `Views/Exports/Details.cshtml` | **Chi tiết & Phê duyệt phiếu xuất + Cơ chế Giữ chỗ (`Pending Reservation - T113`):**<br>• Khi phiếu mới tạo (`Pending`), số lượng xuất đã được giữ chỗ để tránh nhiều nhân viên xuất trùng cùng 1 lô ($\text{Khả dụng} = \text{Tồn lô} - \text{Đang chờ duyệt}$).<br>• Khi Admin bấm **Duyệt (`Approve`)**: Trừ tồn thực tế của lô, cập nhật tổng tồn sản phẩm và **tự động ghi dòng Xuất (-) vào sổ Thẻ kho (`StockCard`)**.<br>• Khi Từ chối hoặc Hủy: Giải phóng số lượng giữ chỗ ngay lập tức. |
| `/Exports/PickingList` | `Views/Exports/PickingList.cshtml` | **Phiếu Soạn Hàng Tối Ưu Lộ Trình Kho (`Auto Picking List - T106`):**<br>• Tập hợp các phiếu xuất đang chờ soạn và tự động sắp xếp thứ tự điểm dừng một chiều: **Kho $\rightarrow$ Khu vực $\rightarrow$ Dãy kệ $\rightarrow$ Tầng (từ thấp lên cao) $\rightarrow$ Ô chứa $\rightarrow$ FEFO** để thủ kho đi một vòng duy nhất nhặt đủ hàng.<br>• Hỗ trợ in phiếu khổ A4 có ô checkbox `[ ] Đã nhặt`. |
| `/ReceiptPrint/Export/{id}` | `Views/ReceiptPrint/Export.cshtml` | Mẫu in Phiếu Xuất Kho chuẩn biểu mẫu kế toán khổ A4. |

---

## 8. NHÓM 8: ĐIỀU CHUYỂN, KIỂM KÊ, BÁO CÁO TỒN KHO & THẺ KHO KẾ TOÁN

### 8.1. Điều chuyển Kho (`WarehouseTransfers`)
- **Controller:** `Controllers/WarehouseTransfersController.cs`
- **Các trang:** `/WarehouseTransfers/Index`, `/WarehouseTransfers/Create`, `/WarehouseTransfers/Details/{id}` (`Views/WarehouseTransfers/*.cshtml`)
- **Task liên quan:** `T100`, `T39`, `T107`
- **Giải thích:** Di chuyển một phần hoặc toàn bộ số lượng của một lô hàng từ Ô nguồn (`SourceBin`) sang Ô đích (`DestinationBin`). Kiểm tra sức chứa tối đa của ô đích (`MaxCapacity`), tự động tách lô hoặc cộng dồn nếu ô đích đã có lô trùng thông tin, đồng thời ghi 2 dòng đối ứng (**Điều chuyển đi -** và **Điều chuyển đến +**) vào sổ Thẻ kho.

### 8.2. Kiểm kê Thực tế & Đối soát Chênh lệch (`InventoryChecks`)
- **Controller:** `Controllers/InventoryChecksController.cs`
- **Các trang:** `/InventoryChecks/Index`, `/InventoryChecks/Create`, `/InventoryChecks/Details/{id}` (`Views/InventoryChecks/*.cshtml`) và `/InventoryChecks/ExportExcel/{id}`
- **Task liên quan:** `T26`, `T31`, `T32`, `T105`
- **Giải thích:**
  - Trang `Create`: Liệt kê tồn sổ sách của từng lô hàng tại từng vị trí ô kệ để nhân viên nhập số lượng kiểm đếm thực tế (`ActualQuantity`).
  - Trang `Details`: Đối soát chênh lệch ($\text{Difference} = \text{Thực tế} - \text{Sổ sách}$), thống kê số lô thừa / thiếu / khớp.
  - Khi Admin bấm **Xác nhận kiểm kê (`Confirm - T105`)**: Tự động cân bằng lại tồn kho từng lô, đồng bộ tổng tồn sản phẩm và ghi dòng **Cân bằng kiểm kê (+/-)** vào Thẻ kho.
  - Hỗ trợ **Xuất Biên bản Kiểm kê ra Excel (`T32`)** và **In Biên bản A4**.

### 8.3. Báo cáo Tồn kho (`Inventory`)
- **Controller:** `Controllers/InventoryController.cs`
- **Các trang:** `/Inventory/Index`, `/Inventory/History/{id}` (`Views/Inventory/*.cshtml`) và `/Inventory/ExportExcel`
- **Task liên quan:** `T20`, `T51`, `T52`, `T53`
- **Giải thích:** Báo cáo tổng hợp số lượng và giá trị tồn kho của toàn bộ mặt hàng, sắp xếp theo tên/số lượng/giá trị, xem lịch sử nhập xuất của từng mặt hàng và xuất báo cáo ra file Excel (`ClosedXML`).

### 8.4. Thẻ Kho — Sổ Chi tiết Vật tư, Hàng hóa (`StockCards`)
- **Controller:** `Controllers/StockCardsController.cs`
- **File View:** `Views/StockCards/Index.cshtml`
- **Đường dẫn:** `/StockCards` và `/StockCards/ExportExcel`
- **Task liên quan:** `T107`, `T54` (`PB10`, `PB08`)
- **Giải thích:**
  - Là sổ kế toán vật tư quan trọng nhất của kho.
  - Tra cứu theo Sản phẩm, Kho và Kỳ báo cáo (Từ ngày $\rightarrow$ Đến ngày).
  - Tự động tính **Số dư đầu kỳ**, liệt kê từng chứng từ phát sinh (**Nhập +**, **Xuất -**, **Điều chuyển**, **Kiểm kê**) kèm **Tồn lũy kế sau mỗi giao dịch**, và chốt **Số dư cuối kỳ**.
  - Hỗ trợ **Xuất Thẻ kho ra Excel** và **In Thẻ kho khổ A4** (có chữ ký Người lập, Thủ kho, Kế toán trưởng).

---

## 9. NHÓM 9: QUẢN TRỊ HỆ THỐNG, CÔNG CỤ EXCEL/SAO LƯU & KIỂM THỬ TỰ ĐỘNG

| Phân hệ | Controller | Các trang View | Quyền | Giải thích chức năng chi tiết |
|---|---|---|---|---|
| **Quản lý Tài khoản (`Users`)** | `UsersController.cs` | `Index`, `Create`, `Edit`, `ResetPassword` | Admin | Quản lý tài khoản nhân sự kho (`Admin`, `Employee`). Hỗ trợ tạo mới, đổi vai trò, đặt lại mật khẩu, khóa/mở khóa tài khoản (`ToggleLock`) và chặn tự khóa/tự xóa tài khoản đang đăng nhập (`T09`, `T10`, `T34`, `T35`). |
| **Phân quyền theo Kho (`WarehousePermissions`)** | `WarehousePermissionsController.cs` | `Index`, `Create`, `Edit` | Admin | Cấp quyền chi tiết cho từng nhân viên trên từng kho cụ thể với 4 cờ: `CanView` (Xem), `CanImport` (Nhập), `CanExport` (Xuất), `CanManage` (Quản lý) (`T61`). |
| **Nhật ký Hoạt động (`ActivityLogs`)** | `ActivityLogsController.cs` | `Views/ActivityLogs/Index.cshtml` | Admin | Lưu vết kiểm toán (`Audit Log - T63`) toàn bộ thao tác trong hệ thống, thống kê số lượt đăng nhập, nhập/xuất, điều chuyển, thêm/sửa/xóa sản phẩm và lọc theo người dùng/thời gian. |
| **Import Excel & Sao lưu (`DataTools`)** | `DataToolsController.cs` | `Views/DataTools/Index.cshtml` | Admin | • **Tải file Excel mẫu & Import hàng loạt (`T44`, `T56`):** Nhập nhanh danh mục sản phẩm & số dư lô đầu kỳ từ file `.xlsx`.<br>• **Sao lưu (`Backup - T57`) & Khôi phục (`Restore - T58`):** Xuất toàn bộ dữ liệu kho ra gói `.json` kèm mã kiểm tra toàn vẹn `SHA-256` và khôi phục an toàn. |
| **Kiểm thử Tự động (`SystemTests`)** | `SystemTestsController.cs` | `Views/SystemTests/Index.cshtml` | Admin | **Trình chạy 8 Kịch bản Kiểm thử Tự động (`T78`–`T81`, `T120`, `T121`):** Thực thi kiểm thử đơn vị (Unit Test) và bảo mật (`SQL Injection`, `XSS`) cho thuật toán `FEFO`, `FIFO`, `LIFO`, `Pending Reservation`, `StockCard`, `MaxCapacity` bên trong Transaction Rollback cô lập. Hiển thị kết quả `PASS/FAIL` theo mili-giây và hỗ trợ in biên bản nghiệm thu kiểm thử A4. |

---

## 10. NHÓM 10: HỆ THỐNG RESTFUL WEB API (`ApiControllers`)

**File mã nguồn:** `ApiControllers/WarehouseApiControllers.cs`  
**Task liên quan:** `T89` (Tài liệu kỹ thuật & API)

Hệ thống cung cấp sẵn các RESTful API chuẩn JSON (yêu cầu xác thực `[Authorize]`) để tích hợp với ứng dụng di động hoặc hệ thống ERP bên ngoài:
- **Products API (`/api/products`):**
  - `GET /api/products`: Lấy danh sách sản phẩm.
  - `GET /api/products/{id}`: Lấy chi tiết sản phẩm và các lô hàng.
  - `POST /api/products`: Thêm sản phẩm mới qua JSON DTO.
  - `PUT /api/products/{id}`: Cập nhật sản phẩm.
  - `DELETE /api/products/{id}`: Xóa sản phẩm (có kiểm tra ràng buộc lịch sử).
- **Imports API (`/api/imports`):**
  - `GET /api/imports`: Lấy danh sách phiếu nhập.
  - `POST /api/imports`: Tạo phiếu nhập kho chờ duyệt.
  - `POST /api/imports/{id}/approve`: Admin duyệt phiếu nhập qua API.
- **Exports API (`/api/exports`):**
  - `GET /api/exports`: Lấy danh sách phiếu xuất.
  - `POST /api/exports`: Tạo phiếu xuất kho giữ chỗ.
  - `POST /api/exports/{id}/approve`: Admin duyệt phiếu xuất qua API.

---

## GỢI Ý KỊCH BẢN DEMO 5 PHÚT ẤN TƯỢNG NHẤT TRƯỚC HỘI ĐỒNG BẢO VỆ (`T73`, `T95`)

1. **Phút 1 — Tổng quan & Tìm kiếm tức thì (`Ctrl+K`):**
   - Đăng nhập tài khoản `admin` / `Admin@123`, giới thiệu `Dashboard` và nhấn **`Ctrl + K`** gõ nhanh mã sản phẩm/lô hàng để trình diễn **Global Search (`T22`)**.
2. **Phút 2 — Nhập kho thông minh & Kiểm soát sức chứa (`T66`, `T39`):**
   - Vào **Nhập kho $\rightarrow$ Tạo phiếu nhập**, bấm nút **"Gợi ý vị trí trống tối ưu"** để hệ thống tự chọn ô kệ trống phù hợp nhất, sau đó bấm **Duyệt phiếu nhập**.
3. **Phút 3 — In tem QR/Barcode & Quét mã bằng Camera (`T111`, `T23`):**
   - Vào **Quản lý lô hàng**, tích chọn các lô vừa nhập và bấm **"In tem đã chọn"** để mở trang tem nhiệt QR + Barcode.
   - Mở trang **Quét mã kho (`/Scanner`)**, quét mã `WMS-B...` để hội đồng nghe tiếng **Beep** và xem thông tin tồn khả dụng hiển thị tức thì.
4. **Phút 4 — Xuất kho tự động FEFO & Phiếu soạn hàng tối ưu lộ trình (`T60`, `T106`):**
   - Vào **Xuất kho $\rightarrow$ Lập phiếu xuất**, chọn Tab **Xuất tự động theo Quy tắc (`FEFO`)**, nhập số lượng để bảng Preview tự động chọn lô cận date nhất.
   - Mở **Phiếu soạn hàng tối ưu lộ trình (`Picking List`)** và mở **Thẻ kho (`StockCards`)** để chứng minh số dư đầu kỳ $\rightarrow$ nhập $\rightarrow$ xuất $\rightarrow$ cuối kỳ khớp tuyệt đối.
5. **Phút 5 — Phân tích Vòng quay & Chạy Kiểm thử Tự động 100% PASS (`T55`, `T78`–`T81`):**
   - Mở trang **Phân tích & Vòng quay (`/Analytics`)** trình diễn biểu đồ lấp đầy kho & phân nhóm hàng tồn đọng `Dead Stock`.
   - Kết thúc bằng trang **Kiểm thử tự động (`/SystemTests`)**, bấm chạy trực tiếp **8/8 Test Cases (FEFO, FIFO, LIFO, Reservation, StockCard, MaxCapacity, SQLi, XSS) đạt `100% PASSED`** ngay trước mắt Hội đồng!
