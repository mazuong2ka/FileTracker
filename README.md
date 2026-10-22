FileTracker 1.0 — Lịch sử file trên Windows
=========================================

BẮT ĐẦU
1. Giải nén toàn bộ gói ZIP vào một thư mục cố định.
2. Mở FileTracker.exe. Giữ FileTracker.exe.config cùng thư mục.
3. Chờ quét lần đầu; kết quả tự cập nhật trong khi quét.

Yêu cầu: Windows 10/11 x64 và .NET Framework 4.8. Không cần Python,
Node.js, kết nối mạng hay quyền Administrator. App chưa ký số thương mại.

PHẠM VI MẶC ĐỊNH
- Program Files (bản 64-bit)
- Program Files (x86)
- Downloads của tài khoản Windows hiện tại, kể cả vị trí đã chuyển
- D:\ và các thư mục con

Trong “Thư mục & Cài đặt” bạn có thể thêm/bỏ phạm vi. Sau khi lưu,
chọn Thoát ở khay hệ thống rồi mở lại app để áp dụng phạm vi mới.
Không có thao tác sửa, di chuyển hoặc xóa file gốc trong app.

CHỨC NĂNG HẰNG NGÀY
- File gần đây: nhóm theo ngày, sắp theo ngày tạo, ngày sửa hoặc dung lượng.
- Lịch sử thay đổi: thêm, sửa, xóa và đổi tên/di chuyển trong phạm vi theo dõi.
- Tìm tên/đường dẫn; lọc thư mục, loại file và thời gian.
- File đã đánh dấu: lưu dấu sao để mở lại nhanh.
- File lớn: xem các file từ 100 MB, sắp lớn nhất trước.
- Mở file, mở thư mục chứa, sao chép đường dẫn. File thực thi cần xác nhận.
- Xuất CSV theo bộ lọc (tối đa 50.000 dòng/lần, có thông báo nếu chạm giới hạn).
- Tạm dừng/tiếp tục; quét lại thủ công và tự đồng bộ mỗi 30 phút.
- Chạy dưới khay hệ thống. Đóng cửa sổ mặc định chưa thoát app.
- Tùy chọn khởi động cùng Windows, mặc định TẮT.
- Ctrl+F: tìm; Ctrl+C trên dòng được chọn: copy đường dẫn; F5: quét lại.

CÁCH HIỂU THỜI GIAN
“Ngày tạo” và “Ngày sửa” là metadata Windows; không nhất thiết là lúc bạn
tải/lưu file, vì sao chép, giải nén và ứng dụng có thể ảnh hưởng các mốc này.
Lần quét đầu lập danh sách file có sẵn, không dựng lại lịch sử thao tác cũ.

Sự kiện không có chữ “Phát hiện” được bộ theo dõi nhận khi app đang chạy.
“Phát hiện thêm/sửa/mất” là kết quả đối chiếu khi quét lại; thời gian hiển thị
là lúc phát hiện, không phải thời điểm chính xác đã thao tác khi app tắt.
Theo dõi dựa vào thông báo filesystem và đối chiếu metadata, không phải
nhật ký kiểm toán đầy đủ. Một số sự kiện quá nhanh, trùng lặp, trong lúc
tạm dừng/khởi động lại hoặc tràn bộ đệm có thể không được ghi đầy đủ.
File được tạo rồi xóa trong thời gian app tắt không thể được khôi phục lịch sử.
App không lưu nội dung hoặc phiên bản cũ và không khôi phục file đã xóa.
Đổi tên thư mục được đối soát thành file thêm/mất; chuyển giữa hai phạm vi
có thể hiển thị là Xóa và Thêm, thay vì một sự kiện Di chuyển.

HIỆU NĂNG VÀ PHẠM VI
Mỗi trang hiển thị 200 mục. Lịch sử được giới hạn về 100.000 sự kiện gần
nhất sau mỗi lần đồng bộ. Chỉ mục file có sẵn được lưu lâu dài.
“Ẩn file kỹ thuật” chỉ lọc danh sách; có thể bỏ chọn để thấy DLL, log, v.v.
Mặc định bỏ qua .git, node_modules, __pycache__, .venv, venv; có thể đổi
trong cài đặt. Thùng rác, System Volume Information, junction/symlink thư mục
và thư mục không đủ quyền đọc được bỏ qua. Không vượt quyền truy cập.
Ổ đĩa mất kết nối hoặc thư mục không đọc được không bị coi là đã xóa hết file.
Quét ổ D lớn có thể mất nhiều phút và tăng hoạt động đọc đĩa lúc đầu.

DỮ LIỆU VÀ QUYỀN RIÊNG TƯ
App không có chức năng gửi dữ liệu qua mạng. Chỉ lưu tên, đường dẫn,
metadata, dấu sao và sự kiện trong:
  %LOCALAPPDATA%\FileTracker
Database chứa đường dẫn cá nhân, nên chỉ chia sẻ CSV/database khi bạn muốn.
Gói phát hành không chứa chỉ mục, cấu hình cá nhân hoặc dữ liệu thử nghiệm.

Chế độ portable (tùy chọn): tạo file trống tên FileTracker.portable cùng
thư mục FileTracker.exe trước khi mở app. Dữ liệu sẽ nằm trong thư mục Data
bên cạnh app. Cần quyền ghi vào thư mục đó. Không đặt Data trong Program Files.
Cũng có thể chỉ định: FileTracker.exe --data-dir "C:\duong-dan\Data"
Tham số --tray mở app ẩn dưới khay hệ thống.

Để sao lưu database, Thoát app hẳn rồi sao chép toàn bộ thư mục dữ liệu.
Để gỡ app: tắt “Khởi động cùng Windows” trong app, chọn Thoát ở khay,
rồi xóa thư mục đã giải nén. Xóa thư mục dữ liệu là tùy chọn và sẽ mất lịch sử.
Nếu đã bật tự khởi động, không di chuyển file EXE mà chưa cập nhật cài đặt.

MÃ NGUỒN
Core.cs: chỉ mục SQLite (winsqlite3 có sẵn trong Windows), bộ theo dõi,
quét đối chiếu, lưu cấu hình.
App.cs: giao diện tiếng Việt, tìm/lọc, dấu sao, CSV, khay và cài đặt.
build.ps1: biên dịch bằng C# compiler đi kèm .NET Framework.
tests/IntegrationTests.cs: kiểm tra chức năng trong thư mục tạm riêng.

KIỂM TRA ĐÃ THỰC HIỆN
Lập chỉ mục ban đầu không tạo lịch sử giả; thêm/sửa file trực tiếp; tên file
Unicode; đổi tên giữ đường dẫn cũ và dấu sao; xóa; bỏ qua thư mục lập trình;
đối chiếu sau tạm dừng/mở lại; ổ đĩa không sẵn sàng; toàn vẹn SQLite;
lưu/đọc lại cấu hình. Kiểm tra không sửa file cá nhân của bạn.
