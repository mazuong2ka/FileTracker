<div align="center">

# 📁 FileTracker

**Xem PC Windows của bạn đã thay đổi gì: file gần đây, lịch sử thay đổi và file lớn trong một nơi.**

[English](README.md) | **Tiếng Việt**

![Platform](https://img.shields.io/badge/n%E1%BB%81n%20t%E1%BA%A3ng-Windows%2010%2F11%20x64-0078D6?logo=windows&logoColor=white)
![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.8-512BD4?logo=dotnet&logoColor=white)
![Admin](https://img.shields.io/badge/quy%E1%BB%81n%20Administrator-kh%C3%B4ng%20c%E1%BA%A7n-2ea44f)
![Data](https://img.shields.io/badge/d%E1%BB%AF%20li%E1%BB%87u-n%E1%BA%B1m%20tr%C3%AAn%20m%C3%A1y%20b%E1%BA%A1n-2ea44f)
[![Release](https://img.shields.io/github/v/release/mazuong2ka/FileTracker?label=release)](https://github.com/mazuong2ka/FileTracker/releases/latest)

[⬇️ **Tải bản phát hành mới nhất**](https://github.com/mazuong2ka/FileTracker/releases/latest)

</div>

<!-- Biểu tượng ứng dụng (tùy chọn): thêm docs/icon.png rồi bỏ comment của ảnh bên dưới.
<p align="center"><img src="docs/icon.png" width="96" alt="Biểu tượng thư mục màu xanh dương của FileTracker"></p>
-->

---

## ✨ Tính năng

| | |
|---|---|
| 🕘 **File gần đây** | Nhóm theo ngày; sắp theo ngày tạo, ngày sửa hoặc dung lượng. |
| 🔄 **Lịch sử thay đổi** | Thêm, sửa, xóa và đổi tên/di chuyển trong phạm vi theo dõi. |
| 🔎 **Tìm kiếm & bộ lọc** | Tìm theo tên/đường dẫn; lọc theo thư mục, loại file và thời gian. |
| ⭐ **File đã đánh dấu** | Lưu dấu sao để mở lại nhanh. |
| 📦 **File lớn** | Xem các file từ 100 MB, lớn nhất trước. |
| 📂 **Thao tác nhanh** | Mở file, mở thư mục chứa, sao chép đường dẫn. File thực thi cần xác nhận. |
| 📤 **Xuất CSV** | Xuất theo bộ lọc (tối đa 50.000 dòng/lần, có thông báo nếu chạm giới hạn). |
| ⏯️ **Tạm dừng / tiếp tục** | Quét lại thủ công và tự đồng bộ mỗi 30 phút. |
| 🔔 **Khay hệ thống** | Chạy dưới khay hệ thống. Đóng cửa sổ mặc định chưa thoát app. |
| 🚀 **Khởi động cùng Windows** | Tùy chọn, mặc định **TẮT**. |
| 🌐 **English / Tiếng Việt** | Chuyển giao diện ứng dụng giữa tiếng Anh và tiếng Việt. |
| 🎨 **Biểu tượng ứng dụng riêng** | Biểu tượng thư mục màu xanh dương được thiết kế riêng cho FileTracker. |

FileTracker **không có thao tác sửa, di chuyển hoặc xóa file gốc** của bạn.

## 🆕 Có gì mới trong v1.1.0

- Chuyển giao diện ứng dụng giữa **tiếng Anh và tiếng Việt**.
- Thêm **biểu tượng ứng dụng riêng hình thư mục màu xanh dương**.

Xem ghi chú phát hành và tải xuống tại [Releases](https://github.com/mazuong2ka/FileTracker/releases).

## 📸 Ảnh chụp màn hình

Để hiển thị giao diện của cả hai ngôn ngữ, thêm `docs/screenshot-en.png` và `docs/screenshot-vi.png` vào repository. Chỉ bỏ comment của từng ảnh bên dưới khi file tương ứng đã có.

<!-- Giao diện tiếng Anh — bỏ comment sau khi thêm docs/screenshot-en.png:
![FileTracker — giao diện tiếng Anh](docs/screenshot-en.png)
-->

<!-- Giao diện tiếng Việt — bỏ comment sau khi thêm docs/screenshot-vi.png:
![FileTracker — giao diện tiếng Việt](docs/screenshot-vi.png)
-->

## 🚀 Bắt đầu nhanh

1. Giải nén toàn bộ gói ZIP vào một thư mục cố định.
2. Mở `FileTracker.exe`. Giữ `FileTracker.exe.config` cùng thư mục.
3. Chờ quét lần đầu; kết quả tự cập nhật trong khi quét.

**Yêu cầu:** Windows 10/11 x64 và .NET Framework 4.8. Không cần Python, Node.js, kết nối mạng hay quyền Administrator. App chưa ký số thương mại nên Windows có thể hiện cảnh báo bảo mật ở lần chạy đầu.

## 🎯 Phạm vi mặc định

- Program Files (bản 64-bit)
- Program Files (x86)
- Downloads của tài khoản Windows hiện tại, kể cả vị trí đã chuyển
- `D:\` và các thư mục con

Trong **Thư mục & Cài đặt** bạn có thể thêm/bỏ phạm vi. Sau khi lưu, chọn **Thoát** ở khay hệ thống rồi mở lại app để áp dụng phạm vi mới.

## ⌨️ Phím tắt

| Phím tắt | Chức năng |
|---|---|
| `Ctrl` + `F` | Tìm kiếm |
| `Ctrl` + `C` | Copy đường dẫn của dòng đang chọn |
| `F5` | Quét lại |

## 🕒 Cách hiểu thời gian

- **Ngày tạo** và **Ngày sửa** là metadata của Windows; không nhất thiết là lúc bạn tải/lưu file, vì sao chép, giải nén và ứng dụng có thể ảnh hưởng các mốc này.
- Lần quét đầu chỉ lập danh sách file có sẵn, **không** dựng lại lịch sử thao tác cũ.
- Sự kiện **không có** chữ “Phát hiện” được bộ theo dõi nhận khi app đang chạy.
- “**Phát hiện** thêm/sửa/mất” là kết quả đối chiếu khi quét lại; thời gian hiển thị là lúc phát hiện, không phải thời điểm chính xác đã thao tác khi app tắt.

<details>
<summary><b>⚠️ Giới hạn (vui lòng đọc)</b></summary>

<br>

- Theo dõi dựa vào thông báo filesystem và đối chiếu metadata, **không phải nhật ký kiểm toán đầy đủ**. Một số sự kiện quá nhanh, trùng lặp, trong lúc tạm dừng/khởi động lại hoặc tràn bộ đệm có thể không được ghi đầy đủ.
- File được tạo rồi xóa trong thời gian app tắt không thể được khôi phục lịch sử.
- App không lưu nội dung hoặc phiên bản cũ và không khôi phục file đã xóa.
- Đổi tên thư mục được đối soát thành file thêm/mất; chuyển giữa hai phạm vi có thể hiển thị là *Xóa* và *Thêm*, thay vì một sự kiện *Di chuyển*.
- Mỗi trang hiển thị 200 mục. Lịch sử được giới hạn về 100.000 sự kiện gần nhất sau mỗi lần đồng bộ. Chỉ mục file có sẵn được lưu lâu dài.
- “Ẩn file kỹ thuật” chỉ lọc danh sách; có thể bỏ chọn để thấy DLL, log, v.v.
- Mặc định bỏ qua `.git`, `node_modules`, `__pycache__`, `.venv`, `venv` (có thể đổi trong cài đặt). Thùng rác, System Volume Information, junction/symlink thư mục và thư mục không đủ quyền đọc cũng được bỏ qua. App không vượt quyền truy cập.
- Ổ đĩa mất kết nối hoặc thư mục không đọc được **không** bị coi là đã xóa hết file.
- Quét ổ D lớn có thể mất nhiều phút và tăng hoạt động đọc đĩa lúc đầu.

</details>

## 🔒 Dữ liệu và quyền riêng tư

App không có chức năng gửi dữ liệu qua mạng. Chỉ lưu tên, đường dẫn, metadata, dấu sao và sự kiện trong:

```text
%LOCALAPPDATA%\FileTracker
```

Database chứa đường dẫn cá nhân, nên chỉ chia sẻ CSV/database khi bạn muốn.
Gói phát hành không chứa chỉ mục, cấu hình cá nhân hoặc dữ liệu thử nghiệm.

### Chế độ portable (tùy chọn)

Tạo file trống tên `FileTracker.portable` cùng thư mục với `FileTracker.exe` **trước khi** mở app. Dữ liệu sẽ nằm trong thư mục `Data` bên cạnh app (cần quyền ghi; không đặt `Data` trong Program Files).

Cũng có thể chỉ định thư mục dữ liệu:

```powershell
FileTracker.exe --data-dir "C:\duong-dan\Data"
```

Tham số `--tray` mở app ẩn dưới khay hệ thống.

### Sao lưu

**Thoát** app hẳn rồi sao chép toàn bộ thư mục dữ liệu.

### Gỡ cài đặt

1. Tắt “Khởi động cùng Windows” trong app.
2. Chọn **Thoát** ở khay hệ thống.
3. Xóa thư mục đã giải nén.
4. *(Tùy chọn)* Xóa thư mục dữ liệu. Việc này sẽ mất lịch sử.

> Nếu đã bật tự khởi động, không di chuyển file EXE mà chưa cập nhật cài đặt.

## 🧱 Mã nguồn

| File | Chức năng |
|---|---|
| `Core.cs` | Chỉ mục SQLite (`winsqlite3` có sẵn trong Windows), bộ theo dõi, quét đối chiếu, lưu cấu hình |
| `App.cs` | Giao diện song ngữ Anh/Việt, tìm/lọc, dấu sao, CSV, khay và cài đặt |
| `build.ps1` | Biên dịch bằng C# compiler đi kèm .NET Framework |
| `tests/IntegrationTests.cs` | Kiểm tra chức năng trong thư mục tạm riêng |

## ✅ Kiểm tra đã thực hiện

Lập chỉ mục ban đầu không tạo lịch sử giả; thêm/sửa file trực tiếp; tên file Unicode; đổi tên giữ đường dẫn cũ và dấu sao; xóa; bỏ qua thư mục lập trình; đối chiếu sau tạm dừng/mở lại; ổ đĩa không sẵn sàng; toàn vẹn SQLite; lưu/đọc lại cấu hình. Kiểm tra không sửa file cá nhân của bạn.
