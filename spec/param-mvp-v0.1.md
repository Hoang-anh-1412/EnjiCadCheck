# Spec tham số MVP v0.1

| Mục | Nội dung |
|-----|----------|
| Nguồn chốt | Phiếu `地下タンク缶体図 作図依頼書` (2026-07-01) + inventory `FAF08S06.dwg` |
| Ngày chốt | 2026-07-13 |
| Đơn vị dài | mm |
| Đơn vị dung tích | L (1 KL = 1000 L) |
| `DIAMETER` | **内径** (đường kính trong) |

---

## 1. Input bắt buộc (form UI / JSON)

| ParamKey | Mô tả | Đơn vị | Ví dụ phiếu | Bắt buộc |
|----------|-------|--------|-------------|----------|
| `CAPACITY_L` | Dung tích danh định (実容量 / 容量) | L | `8000` | Có |
| `INNER_DIAMETER` | Đường kính trong (内径) | mm | `1500` | Có |
| `SHELL_LENGTH` | Chiều dài thân (胴長) | mm | `4440` | Có |
| `SHELL_THICKNESS` | Dày thân (胴板厚) | mm | `6` | Có |
| `HEAD_THICKNESS` | Dày gương / vách (鏡・中仕切り板厚) | mm | `6` | Có |
| `TANK_TYPE` | Loại bồn | enum | `SF` | Có |
| `MANHOLE_500` | Có manhole φ500 | bool | `true` | Có |
| `MANHOLE_600` | Có manhole φ600 | bool | `true` | Có |
| `PROTECTOR` | Protector | enum | `NONE` / `SITE_WELD` | Có |
| `DRAWING_NO` | Số bản vẽ | text | `""` (新規) hoặc `FAF08S06` | Có (cho phép rỗng) |
| `DRAWING_DATE` | Ngày trên title block | text | `2026-07-01` | Không |
| `SITE_NAME` | Tên công trình (現場名) | text | `福井春山合同庁舎` | Không |
| `MATERIAL` | Vật liệu | text | `SS400` | Không (default `SS400`) |

### Nozzle — input dạng danh sách (`NOZZLES[]`)

Mỗi phần tử:

| Field | Mô tả | Ví dụ |
|-------|--------|--------|
| `NO` | Số thứ tự trên phiếu | `1`…`9` |
| `NAME` | Tên chức năng | `注油口` |
| `SIZE_A` | Cỡ ống (A) | `65` |
| `CONN` | Kết nối | `FLANGE_10K` / `SOCKET` |
| `PIPE` | Loại ống (nếu có) | `""` / `Sch80` |
| `DOWN_PIPE` | 管下有無 | `true` / `false` / `null` |
| `HEIGHT_MM` | Chiều cao nozzle | `null` (phiếu để trống → phase sau) |
| `SPAN_MM` | Khoảng từ mốc dọc thân | `null` (chưa chốt mốc → G1) |

**Mẫu từ phiếu (9 miệng):**

| NO | NAME | SIZE_A | CONN | PIPE | DOWN_PIPE |
|----|------|-------:|------|------|-----------|
| 1 | 注油口 | 65 | FLANGE_10K | | true |
| 2 | 送油口 | 125 | FLANGE_10K | | false |
| 3 | 通気口 | 50 | FLANGE_10K | | true |
| 4 | 液面計口 | 50 | FLANGE_10K | | true |
| 5 | 漏洩検知管 | 100 | FLANGE_10K | Sch80 | null |
| 6 | 返油口 | 65 | FLANGE_10K | | true |
| 7 | 除水口 | 40 | SOCKET | | true |
| 8 | 計量口 | 32 | SOCKET | | true |
| 9 | フロートスイッチ | 50 | SOCKET | | true |

`NOZZLE_COUNT` = `NOZZLES.Length` (derived, không nhập riêng).

---

## 2. Derived (không nhập tay trên form MVP)

| ParamKey | Cách lấy | Ghi chú |
|----------|----------|---------|
| `OUTER_DIAMETER` | `INNER_DIAMETER + 2 * SHELL_THICKNESS` (+ lớp SF nếu sau này có) | MVP đơn giản: + 2× dày thân |
| `OVERALL_LENGTH` | Từ template / công thức head + `SHELL_LENGTH` | Chốt công thức ở G2 |
| `GROSS_CAPACITY_L` | Công thức trên BV (πR²L + head) | Template FAF08S06 ≈ 8510 L |
| `WEIGHT_KG` | Placeholder hoặc tra bảng | Phiếu không có; BV mẫu ≈ 1900 |
| `NOZZLE_COUNT` | `len(NOZZLES)` | |

---

## 3. Phạm vi MVP code (ưu tiên dùng input)

**Phase apply đầu:** chỉ bắt buộc dùng

1. `INNER_DIAMETER`
2. `SHELL_LENGTH`
3. `CAPACITY_L`
4. `DRAWING_NO` (+ `DRAWING_DATE`, `SITE_NAME` nếu có ô title)
5. `SHELL_THICKNESS` / `HEAD_THICKNESS` (update text liên quan)

**Hoãn vị trí nozzle** (`HEIGHT_MM`, `SPAN_MM`) đến khi G1 gắn tag + đọc được dim (API `Measurement` hiện = -1).

---

## 4. JSON ví dụ

```json
{
  "CAPACITY_L": 8000,
  "INNER_DIAMETER": 1500,
  "SHELL_LENGTH": 4440,
  "SHELL_THICKNESS": 6,
  "HEAD_THICKNESS": 6,
  "TANK_TYPE": "SF",
  "MANHOLE_500": true,
  "MANHOLE_600": true,
  "PROTECTOR": "SITE_WELD",
  "DRAWING_NO": "",
  "DRAWING_DATE": "2026-07-01",
  "SITE_NAME": "福井春山合同庁舎",
  "MATERIAL": "SS400",
  "NOZZLES": [
    { "NO": 1, "NAME": "注油口", "SIZE_A": 65, "CONN": "FLANGE_10K", "PIPE": "", "DOWN_PIPE": true, "HEIGHT_MM": null, "SPAN_MM": null },
    { "NO": 2, "NAME": "送油口", "SIZE_A": 125, "CONN": "FLANGE_10K", "PIPE": "", "DOWN_PIPE": false, "HEIGHT_MM": null, "SPAN_MM": null },
    { "NO": 3, "NAME": "通気口", "SIZE_A": 50, "CONN": "FLANGE_10K", "PIPE": "", "DOWN_PIPE": true, "HEIGHT_MM": null, "SPAN_MM": null },
    { "NO": 4, "NAME": "液面計口", "SIZE_A": 50, "CONN": "FLANGE_10K", "PIPE": "", "DOWN_PIPE": true, "HEIGHT_MM": null, "SPAN_MM": null },
    { "NO": 5, "NAME": "漏洩検知管", "SIZE_A": 100, "CONN": "FLANGE_10K", "PIPE": "Sch80", "DOWN_PIPE": null, "HEIGHT_MM": null, "SPAN_MM": null },
    { "NO": 6, "NAME": "返油口", "SIZE_A": 65, "CONN": "FLANGE_10K", "PIPE": "", "DOWN_PIPE": true, "HEIGHT_MM": null, "SPAN_MM": null },
    { "NO": 7, "NAME": "除水口", "SIZE_A": 40, "CONN": "SOCKET", "PIPE": "", "DOWN_PIPE": true, "HEIGHT_MM": null, "SPAN_MM": null },
    { "NO": 8, "NAME": "計量口", "SIZE_A": 32, "CONN": "SOCKET", "PIPE": "", "DOWN_PIPE": true, "HEIGHT_MM": null, "SPAN_MM": null },
    { "NO": 9, "NAME": "フロートスイッチ", "SIZE_A": 50, "CONN": "SOCKET", "PIPE": "", "DOWN_PIPE": true, "HEIGHT_MM": null, "SPAN_MM": null }
  ]
}
```

---

## 5. Quyết định đã khóa

1. Đường kính input = **内径** (`INNER_DIAMETER`), không dùng ngoại kính làm input.
2. Dung tích input = **CAPACITY_L** (L), không nhập KL trên model (UI có thể hiện KL).
3. Nozzle = **danh sách 9** theo phiếu; không còn MVP cố định 3 span.
4. Vị trí dọc thân / cao nozzle = **chưa phải input bắt buộc** MVP geometry.

---

## 6. Mapping từ plan view (平面図) — FAF08S06

Nguồn: ảnh plan trên BV (dim 400 / 3600 / 400 + khoảng nozzle).

### Đã có trong spec

| Trên hình | → ParamKey | Giá trị / cách map |
|-----------|------------|-------------------|
| 400 + 3600 + 400 | `SHELL_LENGTH` | `4440` |
| Nhãn `(1)`…`(8)` | `NOZZLES[].NO` | Khớp NO 1–8 |
| (Tâm cụm trái từ đầu thân) | `NOZZLES[NO∈{1,7,8}].SPAN_MM` | `400` (mốc = đầu thân trái) |
| (Tâm cụm phải từ đầu thân) | `NOZZLES[NO=5].SPAN_MM` | `400 + 3600` = `4000` (= `SHELL_LENGTH - 400`) |

Ghi chú: `(6)(2)(3)(4)` nằm quanh cụm giữa/trái trên plan — **offset cục bộ** (xem bảng dưới), không phải `SPAN_MM` dọc thân riêng trừ khi G1 chốt lại mốc.

### Có trên hình nhưng chưa có field trong spec

| Trên hình | Ý nghĩa | Đề xuất field (v0.2) |
|-----------|---------|----------------------|
| 130, 150 (cụm 8–1–7) | Offset ngang trên plan so với tâm cụm | `NOZZLES[].OFFSET_X` / `OFFSET_Y` |
| 150 / 150 (cụm 6–2–3–4) | Offset dọc/ngang trên plan | cùng `OFFSET_X` / `OFFSET_Y` |
| 300 / 300 quanh (5) | Extent theo phương ngang thân (plan) | `OFFSET_Y` hoặc extent riêng |
| `24×φ18` · `M16(SUS)` | Bu-lông nắp manhole | `MANHOLE_BOLT_COUNT`, `MANHOLE_BOLT_HOLE_D`, `MANHOLE_BOLT_SPEC` |
| `4×19t 吊り金具` | Móc treo | `LIFTING_LUG_COUNT`, `LIFTING_LUG_THICKNESS` |
| A-A / B-B / C-C | Mặt cắt | Không phải param input |

### Có trong spec nhưng không có trên hình plan này

| ParamKey | Lấy từ đâu |
|----------|------------|
| `CAPACITY_L` | Phiếu / text dung tích |
| `INNER_DIAMETER` | Phiếu (内径) |
| `SHELL_THICKNESS` / `HEAD_THICKNESS` | Phiếu |
| `TANK_TYPE`, `MANHOLE_500/600`, `PROTECTOR` | Phiếu |
| `DRAWING_NO`, `DRAWING_DATE`, `SITE_NAME`, `MATERIAL` | Title / phiếu |
| `NOZZLES[].NAME`, `SIZE_A`, `CONN`, `PIPE`, `DOWN_PIPE` | Phiếu / bảng ノズル明細表 |
| `NOZZLES[NO=9]` | Phiếu có フロートスイッチ — **không thấy** trên plan này |
| `HEIGHT_MM` | Không có trên plan (phiếu cũng trống) |
| Derived: `OUTER_DIAMETER`, `OVERALL_LENGTH`, `GROSS_CAPACITY_L`, `WEIGHT_KG` | Tính / chỗ khác BV |

### Tóm tắt map nhanh

```
Hình plan          Spec v0.1
─────────────      ─────────────────────────
400+3600+400   →   SHELL_LENGTH = 4440
(1)…(8)        →   NOZZLES[].NO
vị trí dọc cụm →   NOZZLES[].SPAN_MM  (400 / 4000)
130,150,300…   →   (chưa có) OFFSET_X/Y
24×φ18 M16     →   (chưa có) manhole bolt
4×19t treo     →   (chưa có) lifting lug
```
