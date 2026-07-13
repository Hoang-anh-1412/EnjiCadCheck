# Drawing inventory (G0.2)

- Generated: 2026-07-13 11:28:37
- DWG: `C:\Users\HP\Desktop\file\FAF08S06.dwg`
- Format note: AutoCAD 2007(LT2007)
- Source: `TANKINV` log pasted by user

## Layers

| Name | On | Frozen | Locked | Color |
|------|----|--------|--------|-------|
| `0` | on | no | no | White |
| `001` | on | no | no | Red |
| `002` | on | no | no | Red |
| `003` | on | no | no | Red |
| `004` | on | no | no | Red |
| `005` | on | no | no | Red |
| `006` | on | no | no | Red |
| `007` | on | no | no | Red |
| `008` | on | no | no | Red |
| `009` | on | no | no | Red |
| `010` | on | no | no | Red |
| `040` | on | no | no | Red |
| `041` | on | no | no | Red |
| `043` | on | no | no | Red |
| `044` | on | no | no | Red |
| `051` | on | no | no | Red |
| `057` | on | no | no | Red |
| `058` | on | no | no | Red |
| `200` | on | no | no | Red |
| `201` | on | no | no | White |
| `203` | on | no | no | White |

- Layer count: 21

### Layer roles (from content)

| Layer | Observed role |
|-------|----------------|
| `001` | Notes (注記) |
| `006` | Specs, capacity calc, nozzle schedule text |
| `009` | Detail callouts (a/b部詳細参照) |
| `010` | Labels on details (pipes, manhole) |
| `040`/`041` | Detail notes / plate callouts |
| `043` | Dimension-related labels (内径, 外径, 全長, 胴長…) |
| `044` | View titles (平面図, A-A/B-B/C-C…) |
| `051` | Material note |
| `203` | Title block |

## Block definitions

- `_NONE` only
- Named block definition count: 1 (placeholder)
- **Block references in ModelSpace: none** — geometry is exploded (Line/Circle/Arc), not inserts

## ModelSpace entity counts

| Kind | Count |
|------|------:|
| Line | 3018 |
| Circle | 104 |
| DBText | 203 |
| Dimension | 79 |
| Arc | 81 |
| BlockReference | 0 |
| Table | 0 |
| MText | 0 |

## Dimensions

- All 79 are `AlignedDimension`, almost all on layer `043`
- API `Measurement` = **-1.000** for every dim; `DimensionText` empty
- On-screen values likely live in dim geometry; API read needs follow-up (recompute / alternate property) before parametric dim update

## Text highlights (parametric-relevant)

| Handle | Content | Notes |
|--------|---------|--------|
| `6F7` | `SF二重殻地下タンク 8ＫＬ（ストレート） 缶体図` | Drawing title |
| `8D9` | `FAF08S06` | Drawing number |
| `6FF` | `約 1,900` | Weight (~1900 kg) |
| `6F5` | `kg` | Weight unit |
| `56D` | `8,000L` | Real capacity |
| `589` | `8,510L` | Gross capacity |
| `587` | formula with `0.750`, `4.440`, `1.500` | R=0.75 → D=1500; L=4440 |
| `6F2` | `(胴板長　4,400）` | Shell length label ~4400 |
| `75A` / `75B` | `タンク全長` / `タンク胴長` | Overall / shell length labels |
| `64F`–`651` | `鏡板内径` / `タンク内径` / `タンク外径` | Diameter labels |
| `613`+ | `ノズル明細表` + NO.1–8 | Nozzle schedule as **DBText**, not Table |

## Tables

_None in ModelSpace._ Nozzle BOM / specs are composed of individual `DBText` entities.

## Encoding (`????`)

_None detected._ Japanese text (title block, notes, schedule) reads correctly.

## Implications for later phases

1. **G1 block nozzles:** currently no inserts — need to create/replace nozzle geometry as named blocks (`NOZZLE_N1`…) or drive exploded geometry another way.
2. **G1 tables:** treat nozzle schedule + capacity block as tagged **texts**, not `Table` API.
3. **Dims:** inventory OK for location/layer; value read via `Measurement` is broken (-1) — must fix before dim apply engine.
4. **MVP params visible in drawing:** D≈1500, shell L≈4400–4440, capacity 8000L / 8510L, weight ~1900, drawing no `FAF08S06`.
