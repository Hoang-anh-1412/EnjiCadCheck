# Kế hoạch chi tiết — Hệ thống tạo bản vẽ bồn bể parametric (enjiCAD + C#)

| Mục | Nội dung |
|-----|----------|
| Dự án | EnjiCadCheck |
| Mục tiêu | Từ bản vẽ gốc chuẩn, nhập tham số → sinh bản vẽ mới tự động, giảm sửa tay |
| Nền tảng | enjiCAD (.NET API `Gssoft.Gscad` / `GcMgd`, `GcDbMgd`, `GcCoreMgd`) |
| Ngôn ngữ | C# — Class Library (.NET Framework 4.8), non-SDK csproj + `.sln` |
| Bản vẽ gốc | Bồn nằm ngang: plan, section A-A, B-B/C-C, detail, BOM, specs, title block |
| Phiên bản kế hoạch | 1.0 — 2026-07-13 |

---

## 1. Mục tiêu & phạm vi

### 1.1 Mục tiêu

- Người dùng gõ lệnh CAD → điền form tham số → nhận file DWG mới đã cập nhật geometry, dimension, text, BOM/specs.
- Bản gốc (template) không bị ghi đè.
- Có thể tái chỉnh bản đã sinh (nếu đã gắn metadata parametric).

### 1.2 Trong phạm vi (MVP → đầy đủ)

| Giai đoạn | Phạm vi |
|-----------|---------|
| MVP | Clone template; đổi L thân, D; move 1–3 nozzle; cập nhật vài dim + 1–2 ô text (dung tích / mã BV) |
| Mở rộng | Đồng bộ plan + A-A + B-B/C-C; BOM; bảng specs; title block; preset |
| Nâng cao | Thêm/bớt nozzle theo số lượng; công thức dung tích/trọng lượng; validate nâng cao; panel live Apply |

### 1.3 Ngoài phạm vi (giai đoạn đầu)

- Vẽ lại toàn bộ bồn từ zero bằng Line/Arc (không dùng template).
- Tính toán chịu áp / FEA / compliance code đầy đủ.
- Web/cloud — chỉ plugin CAD local.
- Tự OCR đọc kích thước từ ảnh/PDF.

---

## 2. Kiến trúc tổng thể

```
[Lệnh CAD] → [UI Form / CLI]
                 ↓
           [ParamModel + Validate]
                 ↓
           [TemplateLoader: clone DWG]
                 ↓
           [ApplyEngine]
              ├─ Geometry (shell, heads)
              ├─ Blocks (nozzles…)
              ├─ Dimensions
              └─ Text / Table / Attributes
                 ↓
           [Export: SaveAs + metadata]
```

### 2.1 Module code đề xuất

| Module | Trách nhiệm |
|--------|-------------|
| `Commands` | Đăng ký lệnh `TANKNEW`, `TANKAPPLY`, … |
| `UI` | Form nhập liệu, load/save preset |
| `Models` | `TankParameters`, kết quả tính toán phụ |
| `Validation` | Kiểm tra range, ràng buộc hình học |
| `Template` | Đường dẫn template, clone database |
| `Tagging` | Đọc/ghi XData hoặc Named Dictionary |
| `Apply` | Engine áp tham số lên entity |
| `Export` | SaveAs, ghi metadata |
| `Config` | JSON/XML: mapping tên entity ↔ param |

---

## 3. Hạng mục công việc chi tiết

### Giai đoạn 0 — Chuẩn bị & khảo sát (nền tảng)

| ID | Hạng mục | Mô tả | Đầu ra | Ưu tiên |
|----|----------|-------|--------|---------|
| 0.1 | Xác nhận API enjiCAD | Build/load plugin hiện có (`CHECKENJI`), xác nhận đọc/ghi entity, block, text, dim, table | Checklist API OK/NOT OK | P0 |
| 0.2 | Inventory bản vẽ gốc | Liệt kê layer, block, dim, text, table trên DWG gốc; ghi chú text `????` (font/encoding) | File inventory (Excel/Markdown) | P0 |
| 0.3 | Chốt bộ tham số MVP | Danh sách input tối thiểu (vd: D, ShellLength, khoảng nozzle, DrawingNo, Capacity) | Spec v0.1 — `spec/param-mvp-v0.1.md` ✅ | P0 |
| 0.4 | Chốt quy ước đặt tên | Quy tắc đặt tên block/layer/text hoặc key XData (`TANK_PARAM=SHELL_L`) | Tài liệu naming convention | P0 |
| 0.5 | Sao lưu template | Copy DWG gốc → `templates/tank_master.dwg`; làm việc trên bản copy | File template ổn định | P0 |
| 0.6 | Cấu trúc solution | Tách folder: Commands, UI, Models, Engine, Config; Class Library .NET Framework 4.8, Platform x64 | Solution sạch, build OK | P1 |

**Tiêu chí hoàn thành G0:** Plugin load được; đã có inventory + danh sách param MVP + file template tách biệt.

---

### Giai đoạn 1 — Chuẩn hóa template (làm trên CAD, ít code)

| ID | Hạng mục | Mô tả | Đầu ra | Ưu tiên |
|----|----------|-------|--------|---------|
| 1.1 | Đặt tên / gắn tag geometry | Đánh dấu thân, đầu bồn, path stretch (XData hoặc tên) | Template có tag geometry | P0 |
| 1.2 | Chuẩn hóa nozzle thành block | Đảm bảo N1/N2/N3 là block insert; đặt tên `NOZZLE_N1`… | Block nozzle ổn định | P0 |
| 1.3 | Gắn tag dimension | Dim chiều dài, đường kính, khoảng nozzle gắn key param | Dim có thể update theo code | P0 |
| 1.4 | Gắn tag text / attribute | Ô dung tích, trọng lượng, số bản vẽ, title block | Text/Att có key | P0 |
| 1.5 | Gắn tag BOM / specs table | Xác định cột/ô cần update; gắn mapping | Mapping bảng | P1 |
| 1.6 | Baseline “identity check” | Script/lệnh liệt kê mọi entity đã tag → in ra Editor | Lệnh `TANKTAGS` | P1 |
| 1.7 | Tài liệu map param ↔ entity | Bảng: ParamKey → Handle/Name → Loại thao tác (move/stretch/settext) | `config/param-map.json` (hoặc tương đương) | P0 |

**Tiêu chí hoàn thành G1:** Trên template, mọi thứ MVP cần điều khiển đều có tag; `TANKTAGS` liệt kê đủ.

---

### Giai đoạn 2 — Model tham số & validate (logic thuần C#)

| ID | Hạng mục | Mô tả | Đầu ra | Ưu tiên |
|----|----------|-------|--------|---------|
| 2.1 | Class `TankParameters` | Thuộc tính input + giá trị derived | Model | P0 |
| 2.2 | Default từ template | Load giá trị mặc định (hardcode hoặc đọc từ DWG/config) | Defaults | P0 |
| 2.3 | Validate cơ bản | D>0, L>0, tổng span nozzle ≤ shell, số nozzle hợp lệ | `ValidationResult` | P0 |
| 2.4 | Công thức derived (MVP) | Capacity (ước tính), có thể weight placeholder | Hàm tính | P1 |
| 2.5 | Serialize preset | Save/Load JSON preset bộ thông số | File `.json` preset | P2 |
| 2.6 | Unit test logic (nếu có thể) | Test validate + công thức không cần CAD | Test project (tuỳ chọn) | P2 |

**Tiêu chí hoàn thành G2:** Nhập số → model hợp lệ hoặc báo lỗi rõ; preset lưu/đọc được (nếu làm P2).

---

### Giai đoạn 3 — UI trong CAD

| ID | Hạng mục | Mô tả | Đầu ra | Ưu tiên |
|----|----------|-------|--------|---------|
| 3.1 | Form nhập MVP | WinForms/WPF: D, L, vị trí nozzle, DrawingNo, Capacity | Dialog | P0 |
| 3.2 | Nút Tạo / Hủy | Validate trước khi chạy engine | UX cơ bản | P0 |
| 3.3 | Chọn đường dẫn SaveAs | Dialog chọn tên/file output | Path output | P0 |
| 3.4 | Hiển thị cảnh báo | List lỗi validate / entity thiếu tag | Message rõ | P1 |
| 3.5 | Load preset | Dropdown hoặc Open file preset | UX preset | P2 |
| 3.6 | (Sau) Palette Apply | Side panel chỉnh live — giai đoạn sau MVP | Palette | P3 |

**Tiêu chí hoàn thành G3:** Gõ lệnh → hiện form → nhập → OK trả về `TankParameters` hợp lệ.

---

### Giai đoạn 4 — Template load & clone

| ID | Hạng mục | Mô tả | Đầu ra | Ưu tiên |
|----|----------|-------|--------|---------|
| 4.1 | Config đường dẫn template | App config / JSON: `TemplatePath` | Config | P0 |
| 4.2 | Mở template read-only / clone | DeepClone hoặc copy file rồi mở | DB làm việc riêng | P0 |
| 4.3 | Bảo vệ file gốc | Không bao giờ `Save` vào path template | Rule trong code | P0 |
| 4.4 | Ghi metadata sinh bản | Dictionary: template version, params snapshot, thời gian | Metadata trên DWG mới | P1 |

**Tiêu chí hoàn thành G4:** Mỗi lần chạy tạo DB/file mới; file `tank_master.dwg` không đổi.

---

### Giai đoạn 5 — Apply Engine (trọng tâm)

Thực hiện theo thứ tự cố định: **Geometry → Blocks → Dimensions → Text/Table**.

| ID | Hạng mục | Mô tả | Đầu ra | Ưu tiên |
|----|----------|-------|--------|---------|
| 5.1 | Resolver entity theo tag | Tìm entity bằng XData/name từ `param-map` | `IEntityResolver` | P0 |
| 5.2 | Stretch / scale thân theo L, D | Đổi geometry chính section A-A (và plan nếu cùng hệ) | Geometry updater | P0 |
| 5.3 | Move nozzle blocks | Đặt lại insert point theo tọa độ tính từ param | Block mover | P0 |
| 5.4 | Update dimension values | Set giá trị dim + recompute/refresh | Dim updater | P0 |
| 5.5 | Update text / attributes | Dung tích, mã BV, title block | Text updater | P0 |
| 5.6 | Đồng bộ view phụ B-B / C-C | Scale/đường kính/hướng nozzle cho mặt cắt | Multi-view sync | P1 |
| 5.7 | Update BOM / specs table | Ghi ô theo mapping | Table updater | P1 |
| 5.8 | Thêm/bớt nozzle | Insert/delete block theo số lượng (vượt MVP) | Nozzle count logic | P2 |
| 5.9 | Báo cáo apply | Log: OK / skipped / missing tag | Report trên Editor | P1 |
| 5.10 | Transaction an toàn | Mọi sửa trong `Transaction`; lỗi → abort, không file dở | Robustness | P0 |

**Tiêu chí hoàn thành G5 (MVP):** Đổi L + D + vị trí 3 nozzle + vài text/dim → bản vẽ nhìn đúng trên A-A và plan cơ bản.

---

### Giai đoạn 6 — Lệnh CAD & xuất file

| ID | Hạng mục | Mô tả | Đầu ra | Ưu tiên |
|----|----------|-------|--------|---------|
| 6.1 | Lệnh `TANKNEW` | Form → clone → apply → SaveAs → mở file mới (tuỳ chọn) | Command | P0 |
| 6.2 | Lệnh `TANKTAGS` | Liệt kê tag trên bản đang mở | Debug command | P1 |
| 6.3 | Lệnh `TANKAPPLY` | Đọc metadata + form → apply lên bản hiện tại | Update mode | P2 |
| 6.4 | Auto-load plugin | Bundle / registry / `.bundle` nếu enjiCAD hỗ trợ | Deploy dễ hơn | P2 |
| 6.5 | Hướng dẫn NETLOAD | README bước build, path DLL, lệnh | README | P0 |

**Tiêu chí hoàn thành G6:** User chạy `TANKNEW` end-to-end ra file DWG mới.

---

### Giai đoạn 7 — Kiểm thử & nghiệm thu

| ID | Hạng mục | Mô tả | Đầu ra | Ưu tiên |
|----|----------|-------|--------|---------|
| 7.1 | Test case baseline | Input = đúng số trên bản gốc → output gần như identical | TC-01 | P0 |
| 7.2 | Test đổi L | Chỉ đổi chiều dài thân; kiểm tra dim + nozzle relative | TC-02 | P0 |
| 7.3 | Test đổi D | Đổi đường kính; kiểm tra A-A và B-B | TC-03 | P0 |
| 7.4 | Test dời nozzle | Đổi khoảng cách; không overlap; dim cập nhật | TC-04 | P0 |
| 7.5 | Test text/BOM | Ô dung tích / mã BV đúng | TC-05 | P1 |
| 7.6 | Test validate | Input sai bị chặn, không sinh file hỏng | TC-06 | P0 |
| 7.7 | Test font/encoding | Text JP/VN không thành `????` sau SaveAs | TC-07 | P1 |
| 7.8 | Checklist nghiệm thu MVP | Bảng pass/fail ký xác nhận | Biên bản | P0 |

**Tiêu chí hoàn thành G7:** Bộ TC MVP pass; có biên bản nghiệm thu.

---

### Giai đoạn 8 — Tài liệu & bàn giao

| ID | Hạng mục | Mô tả | Đầu ra | Ưu tiên |
|----|----------|-------|--------|---------|
| 8.1 | README vận hành | Cài plugin, chạy lệnh, ý nghĩa từng ô form | README | P0 |
| 8.2 | Hướng dẫn sửa template | Cách thêm tag param mới | Doc maintainer | P1 |
| 8.3 | Changelog param map | Version template ↔ version plugin | Version note | P1 |
| 8.4 | Danh sách hạn chế đã biết | Việc vẫn phải sửa tay | Known limitations | P1 |

---

### Giai đoạn 9 — Mở rộng sau MVP (backlog)

| ID | Hạng mục | Ghi chú |
|----|----------|---------|
| 9.1 | Palette chỉnh live | Apply không SaveAs |
| 9.2 | Đọc input từ Excel | Batch nhiều bồn |
| 9.3 | Xuất PDF kèm theo | Plot/publish sau SaveAs |
| 9.4 | Nhiều loại bồn | Nhiều template (đứng / nằm / dung tích khác family) |
| 9.5 | Constraint nâng cao | Không giao nozzle–seam, min spacing |
| 9.6 | UI tiếng Việt đầy đủ | Label/tooltip |

---

## 4. Bộ tham số MVP

Đã tách file riêng (không nhân đôi bảng trong kế hoạch):

- Spec: [`spec/param-mvp-v0.1.md`](spec/param-mvp-v0.1.md)

---

## 5. Lệnh CAD dự kiến

| Lệnh | Chức năng | Giai đoạn |
|------|-----------|-----------|
| `CHECKENJI` | Smoke test API (đã có) | Có sẵn |
| `TANKTAGS` | Liệt kê entity đã tag | G1 / G6 |
| `TANKNEW` | Tạo bản vẽ mới từ template + form | G6 |
| `TANKAPPLY` | Áp param lên bản đang mở | G6 (sau MVP) |

---

## 6. Thứ tự triển khai đề xuất (timeline logic)

```
G0 Chuẩn bị
 → G1 Chuẩn hóa template + param-map
 → G2 Model + Validate
 → G3 UI Form
 → G4 Clone template
 → G5 Apply Engine (MVP: geometry + nozzle + dim + text)
 → G6 TANKNEW end-to-end
 → G7 Kiểm thử nghiệm thu
 → G8 Tài liệu
 → G9 Backlog
```

G2 và một phần G3 có thể song song với G1 (sau khi chốt ParamKey).

---

## 7. Rủi ro & cách giảm thiểu

| Rủi ro | Ảnh hưởng | Giảm thiểu |
|--------|-----------|------------|
| Entity không ổn định (handle đổi) | Engine không tìm thấy đối tượng | Dùng XData/tên chuẩn, không dựa handle cứng |
| Stretch làm vỡ view phụ / detail | Bản vẽ lệch | MVP chỉ vài dim; đồng bộ B-B sau; test TC-03 |
| Font/encoding `????` | Text sai sau save | Inventory font; test TC-07; set style đúng |
| API enjiCAD khác AutoCAD một phần | Thiếu method | G0.1 checklist; fallback từng loại entity |
| Template bị sửa tay lệch tag | Engine fail | `TANKTAGS` + version template + không Save vào master |
| Phạm vi phình to | Chậm MVP | Khóa MVP: L, D, 3 nozzle, vài text |

---

## 8. Định nghĩa xong MVP (Definition of Done)

MVP được coi là xong khi:

1. Có `templates/tank_master.dwg` tách biệt, có tag đủ cho param MVP.
2. Có `param-map` (JSON hoặc tương đương) khớp template.
3. Lệnh `TANKNEW` tạo được DWG mới từ form.
4. Đổi `SHELL_LENGTH` và `DIAMETER` phản ánh đúng trên section A-A (và plan cơ bản).
5. Ba nozzle move đúng khoảng; dim liên quan cập nhật.
6. Ít nhất một ô dung tích / số bản vẽ cập nhật đúng.
7. File gốc không bị ghi đè.
8. Bộ test TC-01 … TC-04, TC-06 pass.
9. README hướng dẫn chạy được trên máy user.

---

## 9. Tài liệu / artifact cần có trong repo

| Artifact | Mô tả |
|----------|-------|
| `KE-HOACH-PARAMETRIC-BON-BE.md` | File kế hoạch này |
| `README.md` | Build, NETLOAD, lệnh (G8) |
| `templates/tank_master.dwg` | Template chuẩn (binary, không commit nếu policy cấm — ghi rõ path) |
| `config/param-map.json` | Mapping param ↔ entity/tag |
| `docs/inventory-ban-ve-goc.md` | Kết quả G0.2 |
| `docs/naming-convention.md` | Kết quả G0.4 |
| `presets/*.json` | Bộ thông số mẫu (tuỳ chọn) |

---

## 10. Ghi chú triển khai với codebase hiện tại

Hiện repo đã có:

- `EnjiCadCheck.csproj` / `EnjiCadCheck.sln` — Class Library .NET Framework 4.8 (x64), reference `GcMgd` / `GcDbMgd` / `GcCoreMgd`
- `Commands.cs` — lệnh `CHECKENJI`
- `Find-EnjiDlls.ps1`

Hướng mở rộng: giữ `CHECKENJI`, thêm module/engine/UI theo G2–G6; không phá smoke test hiện có.

---

## 11. Bước tiếp theo ngay (action items)

1. **G0.2** — Inventory bản vẽ gốc trên enjiCAD (layer, block, text, dim).
2. **G0.3** — Chốt danh sách param MVP với người thiết kế/bạn.
3. **G0.5 + G1** — Copy template, bắt đầu gắn tag theo naming convention.
4. **G0.1** — Xác nhận API đủ thao tác: BlockReference move, Dimension update, DBText/MText, Table (nếu dùng).

Khi G0–G1 xong, mới nên code sâu Apply Engine (G5) — tránh viết engine khi template chưa có điểm neo ổn định.
