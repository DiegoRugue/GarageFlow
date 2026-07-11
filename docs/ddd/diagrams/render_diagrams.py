from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from textwrap import wrap

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parent
IMAGE_DIR = ROOT / "images"


@dataclass(frozen=True)
class Style:
    fill: str
    outline: str
    text: str


STYLES = {
    "actor": Style("#f4e8ff", "#7e22ce", "#3b0764"),
    "command": Style("#dbeafe", "#2563eb", "#172554"),
    "event": Style("#fed7aa", "#ea580c", "#7c2d12"),
    "aggregate": Style("#fef3c7", "#d97706", "#78350f"),
    "policy": Style("#dcfce7", "#16a34a", "#14532d"),
    "module": Style("#fef3c7", "#d97706", "#78350f"),
    "support": Style("#dcfce7", "#16a34a", "#14532d"),
    "api": Style("#e0f2fe", "#0284c7", "#0c4a6e"),
    "infra": Style("#f1f5f9", "#475569", "#0f172a"),
    "state": Style("#e0f2fe", "#0284c7", "#0c4a6e"),
    "work_object": Style("#fef3c7", "#d97706", "#78350f"),
    "system": Style("#e0f2fe", "#0284c7", "#0c4a6e"),
    "activity": Style("#eef2ff", "#4f46e5", "#312e81"),
}


def font(size: int, bold: bool = False) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    candidates = [
        Path("C:/Windows/Fonts/segoeuib.ttf" if bold else "C:/Windows/Fonts/segoeui.ttf"),
        Path("C:/Windows/Fonts/arialbd.ttf" if bold else "C:/Windows/Fonts/arial.ttf"),
        Path("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf" if bold else "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"),
    ]

    for candidate in candidates:
        if candidate.exists():
            return ImageFont.truetype(str(candidate), size)

    return ImageFont.load_default()


TITLE_FONT = font(42, bold=True)
SUBTITLE_FONT = font(22)
BOX_FONT = font(18, bold=True)
FIELD_FONT = font(16, bold=True)
SMALL_FONT = font(17)
TINY_FONT = font(15)
BADGE_FONT = font(15, bold=True)

HEADER_HEIGHT = 128
FOOTER_HEIGHT = 92


def canvas(width: int, height: int, title: str, subtitle: str) -> tuple[Image.Image, ImageDraw.ImageDraw]:
    image = Image.new("RGB", (width, height), "#ffffff")
    draw = ImageDraw.Draw(image)
    draw.rectangle((0, 0, width, HEADER_HEIGHT), fill="#0f172a")

    title_width, title_height = text_size(draw, title, TITLE_FONT)
    subtitle_width, subtitle_height = text_size(draw, subtitle, SUBTITLE_FONT)
    total_height = title_height + subtitle_height + 10
    y = (HEADER_HEIGHT - total_height) / 2

    draw.text(((width - title_width) / 2, y), title, font=TITLE_FONT, fill="#ffffff")
    draw.text(((width - subtitle_width) / 2, y + title_height + 10), subtitle, font=SUBTITLE_FONT, fill="#cbd5e1")
    return image, draw


def draw_footer(
    draw: ImageDraw.ImageDraw,
    width: int,
    height: int,
    text: str,
    *,
    legend_items: list[tuple[str, str]] | None = None,
) -> None:
    footer_height = 116 if legend_items else FOOTER_HEIGHT
    y1 = height - footer_height
    draw.rectangle((0, y1, width, height), fill="#f8fafc")
    draw.line((0, y1, width, y1), fill="#e2e8f0", width=2)

    lines = wrap_label(text, max(60, width // 18))
    line_height = text_size(draw, "Ag", SMALL_FONT)[1] + 7
    total_height = len(lines) * line_height
    y = y1 + (footer_height - total_height) / 2

    if legend_items:
        draw_horizontal_legend(draw, width, y1 + 16, legend_items)
        y = y1 + 66

    for line in lines:
        line_width, _ = text_size(draw, line, SMALL_FONT)
        draw.text(((width - line_width) / 2, y), line, font=SMALL_FONT, fill="#475569")
        y += line_height


def draw_horizontal_legend(
    draw: ImageDraw.ImageDraw,
    width: int,
    y: int,
    items: list[tuple[str, str]],
) -> None:
    item_widths = []
    for label, _ in items:
        label_width, _ = text_size(draw, label, TINY_FONT)
        item_widths.append(28 + 9 + label_width)

    gap = 28
    total_width = sum(item_widths) + gap * (len(items) - 1)
    x = (width - total_width) / 2

    for (label, kind), item_width in zip(items, item_widths):
        style = STYLES[kind]
        draw.rounded_rectangle((x, y, x + 28, y + 20), radius=5, fill=style.fill, outline=style.outline, width=2)
        draw.text((x + 37, y - 1), label, font=TINY_FONT, fill="#334155")
        x += item_width + gap


def text_size(draw: ImageDraw.ImageDraw, text: str, selected_font: ImageFont.ImageFont) -> tuple[int, int]:
    left, top, right, bottom = draw.textbbox((0, 0), text, font=selected_font)
    return right - left, bottom - top


def draw_centered_text(
    draw: ImageDraw.ImageDraw,
    center_xy: tuple[float, float],
    text: str,
    selected_font: ImageFont.ImageFont,
    fill: str,
) -> None:
    left, top, right, bottom = draw.textbbox((0, 0), text, font=selected_font)
    width = right - left
    height = bottom - top
    x = center_xy[0] - width / 2 - left
    y = center_xy[1] - height / 2 - top
    draw.text((x, y), text, font=selected_font, fill=fill)


def wrap_label(text: str, max_chars: int) -> list[str]:
    lines: list[str] = []
    for raw_line in text.split("\n"):
        lines.extend(wrap(raw_line, width=max_chars, break_long_words=False) or [""])
    return lines


def draw_box(
    draw: ImageDraw.ImageDraw,
    xy: tuple[int, int, int, int],
    label: str,
    kind: str,
    *,
    radius: int = 18,
    max_chars: int = 18,
    box_font: ImageFont.ImageFont = BOX_FONT,
) -> None:
    style = STYLES[kind]
    draw.rounded_rectangle(xy, radius=radius, fill=style.fill, outline=style.outline, width=3)

    x1, y1, x2, y2 = xy
    lines = wrap_label(label, max_chars)
    line_height = text_size(draw, "Ag", box_font)[1] + 7
    total_height = len(lines) * line_height
    y = y1 + ((y2 - y1) - total_height) / 2

    for line in lines:
        draw_centered_text(draw, ((x1 + x2) / 2, y + line_height / 2), line, box_font, style.text)
        y += line_height


def center(xy: tuple[int, int, int, int]) -> tuple[int, int]:
    x1, y1, x2, y2 = xy
    return ((x1 + x2) // 2, (y1 + y2) // 2)


def edge_points(
    start: tuple[int, int, int, int],
    end: tuple[int, int, int, int],
) -> tuple[tuple[int, int], tuple[int, int]]:
    sx, sy = center(start)
    ex, ey = center(end)

    if abs(ex - sx) >= abs(ey - sy):
        if ex >= sx:
            return (start[2], sy), (end[0], ey)
        return (start[0], sy), (end[2], ey)

    if ey >= sy:
        return (sx, start[3]), (ex, end[1])
    return (sx, start[1]), (ex, end[3])


def draw_arrow(
    draw: ImageDraw.ImageDraw,
    start: tuple[int, int, int, int],
    end: tuple[int, int, int, int],
    *,
    label: str | None = None,
    color: str = "#334155",
) -> None:
    p1, p2 = edge_points(start, end)
    draw.line([p1, p2], fill=color, width=3)
    draw_arrow_head(draw, p1, p2, color)
    if label:
        mx = (p1[0] + p2[0]) // 2
        my = (p1[1] + p2[1]) // 2
        draw_label_badge(draw, (mx, my), label)


def draw_label_badge(
    draw: ImageDraw.ImageDraw,
    center_xy: tuple[int, int],
    label: str,
) -> None:
    tw, th = text_size(draw, label, BADGE_FONT)
    width = tw + 22
    height = th + 12
    mx, my = center_xy
    draw.rounded_rectangle(
        (mx - width / 2, my - height / 2, mx + width / 2, my + height / 2),
        radius=8,
        fill="#ffffff",
        outline="#cbd5e1",
    )
    draw_centered_text(draw, center_xy, label, BADGE_FONT, "#334155")


def draw_polyline_arrow(
    draw: ImageDraw.ImageDraw,
    points: list[tuple[int, int]],
    *,
    label: str | None = None,
    label_at: tuple[int, int] | None = None,
    color: str = "#334155",
) -> None:
    draw.line(points, fill=color, width=3, joint="curve")
    draw_arrow_head(draw, points[-2], points[-1], color)

    if label:
        if label_at is None:
            longest_segment = max(
                zip(points, points[1:]),
                key=lambda pair: abs(pair[1][0] - pair[0][0]) + abs(pair[1][1] - pair[0][1]),
            )
            label_at = (
                (longest_segment[0][0] + longest_segment[1][0]) // 2,
                (longest_segment[0][1] + longest_segment[1][1]) // 2,
        )

        draw_label_badge(draw, label_at, label)


def draw_arrow_head(draw: ImageDraw.ImageDraw, p1: tuple[int, int], p2: tuple[int, int], color: str) -> None:
    x1, y1 = p1
    x2, y2 = p2
    if abs(x2 - x1) >= abs(y2 - y1):
        direction = 1 if x2 >= x1 else -1
        points = [(x2, y2), (x2 - 14 * direction, y2 - 8), (x2 - 14 * direction, y2 + 8)]
    else:
        direction = 1 if y2 >= y1 else -1
        points = [(x2, y2), (x2 - 8, y2 - 14 * direction), (x2 + 8, y2 - 14 * direction)]
    draw.polygon(points, fill=color)


def draw_section(draw: ImageDraw.ImageDraw, label: str, xy: tuple[int, int]) -> None:
    draw.text(xy, label, font=SUBTITLE_FONT, fill="#0f172a")


def draw_legend(draw: ImageDraw.ImageDraw, x: int, y: int, items: list[tuple[str, str]]) -> None:
    draw.text((x, y), "Legenda", font=SMALL_FONT, fill="#334155")
    y += 28
    for label, kind in items:
        style = STYLES[kind]
        draw.rounded_rectangle((x, y, x + 28, y + 20), radius=5, fill=style.fill, outline=style.outline, width=2)
        draw.text((x + 38, y - 1), label, font=TINY_FONT, fill="#334155")
        y += 28


def draw_chain(draw: ImageDraw.ImageDraw, boxes: list[tuple[int, int, int, int]]) -> None:
    for current, next_box in zip(boxes, boxes[1:]):
        draw_arrow(draw, current, next_box)


def draw_story_step(
    draw: ImageDraw.ImageDraw,
    number: int,
    label: str,
    xy: tuple[int, int, int, int],
) -> None:
    style = STYLES["activity"]
    x1, y1, x2, y2 = xy
    draw.rounded_rectangle(xy, radius=18, fill=style.fill, outline=style.outline, width=3)
    circle = (x1 + 16, y1 + 16, x1 + 52, y1 + 52)
    draw.ellipse(circle, fill="#ffffff", outline=style.outline, width=3)
    draw_centered_text(draw, ((circle[0] + circle[2]) / 2, (circle[1] + circle[3]) / 2), str(number), BADGE_FONT, style.text)

    lines = wrap_label(label, 22)
    line_height = text_size(draw, "Ag", BOX_FONT)[1] + 7
    total_height = len(lines) * line_height
    y = y1 + ((y2 - y1) - total_height) / 2
    for line in lines:
        draw_centered_text(draw, ((x1 + x2) / 2 + 18, y + line_height / 2), line, BOX_FONT, style.text)
        y += line_height


def draw_story_flow(
    draw: ImageDraw.ImageDraw,
    steps: list[tuple[int, str, tuple[int, int, int, int]]],
) -> None:
    for number, label, xy in steps:
        draw_story_step(draw, number, label, xy)
    for (_, _, current), (_, _, next_step) in zip(steps, steps[1:]):
        draw_arrow(draw, current, next_step, color="#475569")


def event_storming_work_orders() -> Image.Image:
    image, draw = canvas(
        1900,
        1220,
        "Event Storming - Criação e Acompanhamento da OS",
        "Fluxo principal da ordem de serviço, do atendimento à entrega do veículo.",
    )

    row1 = [
        ("Consultor da oficina", "actor"),
        ("Identificar cliente por CPF/CNPJ", "command"),
        ("Cliente identificado", "event"),
        ("Cadastrar ou selecionar veículo", "command"),
        ("Veículo vinculado à OS", "event"),
        ("Criar ordem de serviço", "command"),
        ("OS criada / recebida", "event"),
    ]
    row2 = [
        ("WorkOrder", "aggregate"),
        ("Iniciar diagnóstico", "command"),
        ("Diagnóstico iniciado", "event"),
        ("Criar orçamento", "command"),
        ("Orçamento criado", "event"),
        ("Estimate", "aggregate"),
        ("Adicionar serviços, peças e insumos", "command"),
    ]
    row3 = [
        ("Linhas adicionadas", "event"),
        ("Calcular total", "policy"),
        ("Submeter orçamento", "command"),
        ("Orçamento submetido ao cliente", "event"),
        ("Cliente", "actor"),
        ("Aprovar ou rejeitar orçamento", "command"),
        ("Orçamento aprovado ou rejeitado", "event"),
    ]
    row4 = [
        ("Iniciar execução", "command"),
        ("Serviço iniciado", "event"),
        ("Concluir serviço", "command"),
        ("Serviço concluído", "event"),
        ("Finalizar OS", "command"),
        ("OS finalizada", "event"),
        ("Entregar veículo", "command"),
        ("Veículo entregue", "event"),
    ]

    rows = [row1, row2, row3]
    y_positions = [178, 430, 682]
    all_boxes: list[list[tuple[int, int, int, int]]] = []
    for label, y, row in zip(["1. Recepção", "2. Diagnóstico e orçamento", "3. Aprovação"], y_positions, rows):
        draw_section(draw, label, (48, y - 44))
        boxes: list[tuple[int, int, int, int]] = []
        for index, (text, kind) in enumerate(row):
            x = 48 + index * 254
            box = (x, y, x + 210, y + 96)
            draw_box(draw, box, text, kind, max_chars=19)
            boxes.append(box)
        draw_chain(draw, boxes)
        all_boxes.append(boxes)

    draw.text((48, 306), "continua no diagnóstico e orçamento", font=TINY_FONT, fill="#64748b")
    draw.text((48, 558), "continua na aprovação do cliente", font=TINY_FONT, fill="#64748b")

    draw_section(draw, "4. Execução e entrega", (48, 934 - 44))
    row4_boxes: list[tuple[int, int, int, int]] = []
    for index, (text, kind) in enumerate(row4):
        x = 48 + index * 225
        box = (x, 934, x + 182, 1030)
        draw_box(draw, box, text, kind, max_chars=16)
        row4_boxes.append(box)
    draw_chain(draw, row4_boxes)
    draw.text((48, 810), "se aprovado, a OS segue para execução", font=TINY_FONT, fill="#64748b")

    draw_footer(
        draw,
        1900,
        1220,
        "No código, 'OS recebida' é representada por WorkOrderStatus.Created.",
        legend_items=[
            ("Ator", "actor"),
            ("Comando", "command"),
            ("Evento", "event"),
            ("Agregado / entidade", "aggregate"),
            ("Política", "policy"),
        ],
    )
    return image


def event_storming_inventory() -> Image.Image:
    image, draw = canvas(
        1680,
        900,
        "Event Storming - Gestão de Peças e Insumos",
        "Fluxo administrativo de estoque e uso de itens no orçamento da OS.",
    )

    draw_section(draw, "Administração do estoque", (48, 138))
    admin = [
        ("Administrador / atendente", "actor"),
        ("Cadastrar peça ou insumo", "command"),
        ("Item de estoque cadastrado", "event"),
        ("InventoryItem", "aggregate"),
        ("Ajustar estoque", "command"),
        ("Estoque atualizado", "event"),
    ]
    admin_boxes: list[tuple[int, int, int, int]] = []
    for index, (text, kind) in enumerate(admin):
        x = 48 + index * 248
        box = (x, 182, x + 205, 278)
        draw_box(draw, box, text, kind, max_chars=18)
        admin_boxes.append(box)
    draw_chain(draw, admin_boxes[:4])
    draw_arrow(draw, admin_boxes[3], admin_boxes[4])
    draw_arrow(draw, admin_boxes[4], admin_boxes[5])

    draw_section(draw, "Uso do estoque no orçamento", (48, 408))
    estimate = [
        ("Técnico / consultor", "actor"),
        ("Consultar item para orçamento", "command"),
        ("Validar quantidade e disponibilidade", "policy"),
        ("Adicionar item ao orçamento", "command"),
        ("Item adicionado ao orçamento", "event"),
        ("Estimate", "aggregate"),
        ("Recalcular total", "policy"),
        ("Orçamento atualizado", "event"),
    ]
    estimate_boxes: list[tuple[int, int, int, int]] = []
    for index, (text, kind) in enumerate(estimate):
        x = 48 + index * 198
        box = (x, 452, x + 162, 548)
        draw_box(draw, box, text, kind, max_chars=15)
        estimate_boxes.append(box)
    draw_chain(draw, estimate_boxes)
    draw.text((790, 352), "InventoryItem fornece preço, custo e estoque para validação do orçamento.", font=SMALL_FONT, fill="#475569")

    draw_footer(
        draw,
        1680,
        900,
        "A inclusão no orçamento congela descrição, custo, preço e quantidade. O estoque é controlado pelo InventoryItem.",
        legend_items=[
            ("Ator", "actor"),
            ("Comando", "command"),
            ("Evento", "event"),
            ("Agregado", "aggregate"),
            ("Política", "policy"),
        ],
    )
    return image


def domain_storytelling_work_orders() -> Image.Image:
    image, draw = canvas(
        1800,
        1060,
        "Domain Storytelling - Ordem de Serviço",
        "Narrativa do atendimento, do pedido do cliente à entrega do veículo.",
    )

    steps = [
        (1, "Cliente\nsolicita atendimento\nConsultor", (70, 185, 340, 305)),
        (2, "Consultor\nidentifica cliente\nGarageFlow", (425, 185, 695, 305)),
        (3, "Consultor\nvincula veículo\nOS", (780, 185, 1050, 305)),
        (4, "GarageFlow\ncria ordem de serviço\nOS", (1135, 185, 1405, 305)),
        (5, "Técnico\ninicia diagnóstico\nOS", (1490, 185, 1760, 305)),
        (6, "GarageFlow\ngera orçamento\nOrçamento", (1490, 590, 1760, 710)),
        (7, "Cliente\naprova ou rejeita\nOrçamento", (1135, 590, 1405, 710)),
        (8, "Técnico\nexecuta serviço\nOS", (780, 590, 1050, 710)),
        (9, "Consultor\nentrega veículo\nCliente", (425, 590, 695, 710)),
    ]
    draw_story_flow(draw, steps)

    draw_footer(
        draw,
        1800,
        1060,
        "Domain Storytelling explicita atores, objetos de trabalho e atividades principais do fluxo da OS.",
        legend_items=[
            ("Atividade narrada", "activity"),
        ],
    )
    return image


def domain_storytelling_inventory() -> Image.Image:
    image, draw = canvas(
        1700,
        960,
        "Domain Storytelling - Peças e Insumos",
        "Narrativa do estoque e do uso de itens no orçamento da ordem de serviço.",
    )

    steps = [
        (1, "Administrador\ncadastra peça ou insumo\nGarageFlow", (70, 185, 380, 305)),
        (2, "GarageFlow\nregistra quantidade\nEstoque", (455, 185, 765, 305)),
        (3, "Consultor / Técnico\nconsulta disponibilidade\nEstoque", (840, 185, 1150, 305)),
        (4, "Consultor / Técnico\nadiciona item\nOrçamento", (1225, 185, 1535, 305)),
        (5, "GarageFlow\nrecalcula total\nOrçamento", (1225, 590, 1535, 710)),
        (6, "Orçamento\ncompõe\nOrdem de Serviço", (840, 590, 1150, 710)),
        (7, "GarageFlow\natualiza quantidade\nEstoque", (455, 590, 765, 710)),
    ]
    draw_story_flow(draw, steps)

    draw_footer(
        draw,
        1700,
        960,
        "O estoque fornece disponibilidade e preço para o orçamento; a OS consome os snapshots dos itens selecionados.",
        legend_items=[
            ("Atividade narrada", "activity"),
        ],
    )
    return image


def bounded_contexts() -> Image.Image:
    image, draw = canvas(
        1600,
        980,
        "Mapa de Contextos e Módulos",
        "Monolito modular com camadas e módulos de negócio do GarageFlow.",
    )

    api = (595, 140, 1005, 235)
    draw_box(draw, api, "GarageFlow.Adapters.Api\nMinimal APIs + Scalar", "api", max_chars=28)
    draw_centered_text(draw, (800, 270), "A API expõe os módulos abaixo por endpoints REST.", SMALL_FONT, "#475569")

    modules = [
        ("Users / Auth\nusuários, papéis e JWT", "support", (80, 342, 460, 457)),
        ("Customers\nclientes e CPF/CNPJ", "module", (610, 342, 990, 457)),
        ("Vehicles\nveículos e placa", "module", (1140, 342, 1520, 457)),
        ("Services\ncatálogo de serviços", "module", (80, 552, 460, 667)),
        ("WorkOrders\nOS, orçamento e execução", "module", (610, 552, 990, 667)),
        ("InventoryItems\npeças, insumos e estoque", "module", (1140, 552, 1520, 667)),
    ]

    module_boxes: dict[str, tuple[int, int, int, int]] = {}
    for text, kind, box in modules:
        draw_box(draw, box, text, kind, max_chars=26)
        module_name = text.split("\n", maxsplit=1)[0]
        module_boxes[module_name] = box

    database = (595, 760, 1005, 850)
    draw_box(draw, database, "PostgreSQL\npersistência transacional", "infra", max_chars=28)
    draw_centered_text(draw, (800, 730), "Todos os módulos persistem dados via Infrastructure + EF Core.", SMALL_FONT, "#475569")

    work_orders = module_boxes["WorkOrders"]
    draw_polyline_arrow(draw, [(800, 552), (800, 457)], label="cliente", label_at=(800, 505), color="#64748b")
    draw_polyline_arrow(draw, [(990, 590), (1065, 590), (1065, 400), (1140, 400)], label="veículo", label_at=(1065, 495), color="#64748b")
    draw_polyline_arrow(draw, [(610, 610), (460, 610)], label="serviços", label_at=(535, 610), color="#64748b")
    draw_polyline_arrow(draw, [(990, 610), (1140, 610)], label="itens", label_at=(1065, 610), color="#64748b")
    draw_footer(draw, 1600, 980, "WorkOrders coordena o fluxo de atendimento e referencia clientes, veículos, serviços e itens de estoque por identificadores e snapshots.")
    return image


def aggregates() -> Image.Image:
    image, draw = canvas(
        1800,
        1040,
        "Modelo de Agregados",
        "Principais agregados, entidades internas e referências entre módulos.",
    )

    groups = {
        "Customer": (55, 150, 390, 315, "aggregate"),
        "Vehicle": (55, 405, 390, 570, "aggregate"),
        "Service": (55, 665, 390, 830, "aggregate"),
        "InventoryItem": (1410, 665, 1745, 830, "aggregate"),
        "WorkOrder": (610, 145, 1280, 900, "aggregate"),
    }

    for title, xy_kind in groups.items():
        x1, y1, x2, y2, kind = xy_kind
        style = STYLES[kind]
        draw.rounded_rectangle((x1, y1, x2, y2), radius=22, fill=style.fill, outline=style.outline, width=4)
        draw.text((x1 + 20, y1 + 18), title, font=BOX_FONT, fill=style.text)

    def item(x: int, y: int, text: str) -> None:
        draw.rounded_rectangle((x, y, x + 245, y + 48), radius=10, fill="#ffffff", outline="#cbd5e1", width=2)
        draw_centered_text(draw, (x + 122.5, y + 24), text, FIELD_FONT, "#334155")

    item(85, 220, "TaxDocument")
    item(85, 274, "FullName / Email / Phone")
    item(85, 475, "LicensePlate")
    item(85, 529, "Brand / Model / Year")
    item(85, 735, "Description")
    item(85, 789, "ServicePrice")
    item(1440, 735, "Cost / Price")
    item(1440, 789, "StockQuantity")

    item(650, 220, "CustomerId")
    item(650, 274, "VehicleId")
    item(650, 328, "WorkOrderStatus")
    estimate = (805, 420, 1085, 530)
    draw_box(draw, estimate, "Estimate\nstatus e total", "module", max_chars=20)
    service_line = (690, 640, 955, 745)
    inventory_line = (995, 640, 1260, 745)
    draw_box(draw, service_line, "EstimateServiceLine\nserviço e preço", "api", max_chars=20)
    draw_box(draw, inventory_line, "EstimateInventoryLine\nitem, quantidade e preço", "support", max_chars=20)

    junction = (945, 590)
    draw.line([(945, 530), junction], fill="#64748b", width=3)
    draw_polyline_arrow(draw, [junction, (822, 590), (822, 640)], color="#64748b")
    draw_polyline_arrow(draw, [junction, (1128, 590), (1128, 640)], label="contém", label_at=(1028, 578), color="#64748b")

    draw_polyline_arrow(draw, [(390, 232), (500, 232), (500, 245), (610, 245)], label="cliente", label_at=(500, 219), color="#64748b")
    draw_polyline_arrow(draw, [(390, 488), (515, 488), (515, 299), (610, 299)], label="veículo", label_at=(515, 392), color="#64748b")
    draw_polyline_arrow(draw, [(690, 692), (530, 692), (530, 748), (390, 748)], label="snapshot", label_at=(530, 720), color="#64748b")
    draw_polyline_arrow(draw, [(1260, 692), (1340, 692), (1340, 748), (1410, 748)], label="snapshot", label_at=(1340, 720), color="#64748b")

    draw_footer(draw, 1800, 1040, "WorkOrder é o agregado central da execução; Estimate e suas linhas vivem dentro dele. As linhas guardam snapshots de Service e InventoryItem.")
    return image


def work_order_state_machine() -> Image.Image:
    image, draw = canvas(
        1600,
        760,
        "Máquina de Estados da Ordem de Serviço",
        "Transições permitidas pelo agregado WorkOrder.",
    )

    states = [
        ("Recebida\nCreated", "state"),
        ("Em diagnóstico\nDiagnosing", "state"),
        ("Aguardando aprovação\nWaitingApproval", "state"),
        ("Orçamento aprovado\nApproved", "state"),
        ("Em execução\nInProgress", "state"),
        ("Finalizada\nCompleted", "state"),
        ("Entregue\nDelivered", "state"),
    ]

    boxes: list[tuple[int, int, int, int]] = []
    for index, (text, kind) in enumerate(states):
        x = 45 + index * 218
        box = (x, 250, x + 178, 355)
        draw_box(draw, box, text, kind, max_chars=17)
        boxes.append(box)
    draw_chain(draw, boxes)

    labels = [
        "iniciar diagnóstico",
        "submeter orçamento",
        "aprovar orçamento",
        "iniciar execução",
        "concluir serviços",
        "entregar veículo",
    ]
    for index, label in enumerate(labels):
        p1, p2 = edge_points(boxes[index], boxes[index + 1])
        mx = (p1[0] + p2[0]) // 2
        draw_centered_text(draw, (mx, 226), label, TINY_FONT, "#475569")

    cancelled = (615, 545, 985, 650)
    draw_box(draw, cancelled, "Cancelada\nCancelled", "event", max_chars=20)
    cancel_note = "Cancelamento permitido a partir de: Created, Diagnosing, WaitingApproval e Approved."
    draw.text((555, 500), cancel_note, font=TINY_FONT, fill="#7f1d1d")

    draw_footer(draw, 1600, 760, "A OS só pode ser entregue após finalizada. O cancelamento é permitido antes da execução avançar para finalização.")
    return image


def save(name: str, image: Image.Image) -> None:
    IMAGE_DIR.mkdir(parents=True, exist_ok=True)
    path = IMAGE_DIR / f"{name}.png"
    image.save(path, "PNG", optimize=True)
    print(path)


def main() -> None:
    save("event-storming-work-orders", event_storming_work_orders())
    save("event-storming-inventory", event_storming_inventory())
    save("domain-storytelling-work-orders", domain_storytelling_work_orders())
    save("domain-storytelling-inventory", domain_storytelling_inventory())
    save("bounded-contexts", bounded_contexts())
    save("aggregates", aggregates())
    save("work-order-state-machine", work_order_state_machine())


if __name__ == "__main__":
    main()
