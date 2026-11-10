import importlib.util
import json
from pathlib import Path
import sys
import tempfile
import unittest


MODULE_PATH = Path(__file__).parents[2] / "tools" / "dxf-converter" / "geomap_dxf.py"
SPEC = importlib.util.spec_from_file_location("geomap_dxf", MODULE_PATH)
assert SPEC and SPEC.loader
geomap_dxf = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = geomap_dxf
SPEC.loader.exec_module(geomap_dxf)


def dxf(entities: str) -> str:
    return "0\nSECTION\n2\nENTITIES\n" + entities + "0\nENDSEC\n0\nEOF\n"


def dxf_with_layers(layers: str, entities: str) -> str:
    return (
        "0\nSECTION\n2\nTABLES\n0\nTABLE\n2\nLAYER\n70\n1\n" + layers
        + "0\nENDTAB\n0\nENDSEC\n"
        + "0\nSECTION\n2\nENTITIES\n" + entities + "0\nENDSEC\n0\nEOF\n"
    )


def layer_record(name: str, aci: int, true_color: int | None = None) -> str:
    record = "0\nLAYER\n2\n" + name + "\n70\n0\n62\n" + str(aci) + "\n6\nContinuous\n"
    if true_color is not None:
        record += "420\n" + str(true_color) + "\n"
    return record


def insert(handle: str, layer: str, block: str, attributes: dict[str, str] | None = None,
           x: float = 700100, y: float = 7100100, rotation: float = 0, paper: bool = False,
           scale: float = 1) -> str:
    text = (
        "0\nINSERT\n5\n" + handle + "\n8\n" + layer + "\n"
        + ("67\n1\n" if paper else "")
        + ("66\n1\n" if attributes else "")
        + "2\n" + block + "\n10\n" + str(x) + "\n20\n" + str(y) + "\n30\n0\n"
        "41\n" + str(scale) + "\n42\n" + str(scale) + "\n50\n" + str(rotation) + "\n"
    )
    for index, (tag, value) in enumerate((attributes or {}).items()):
        text += "0\nATTRIB\n5\n" + handle + "A" + str(index) + "\n8\n0\n1\n" + value + "\n2\n" + tag + "\n"
    if attributes:
        text += "0\nSEQEND\n5\n" + handle + "E\n8\n" + layer + "\n"
    return text


def block_definition(name: str, entities: str, base: tuple[float, float] = (0, 0)) -> str:
    return (
        "0\nBLOCK\n8\n0\n2\n" + name + "\n70\n0\n10\n" + str(base[0]) + "\n20\n"
        + str(base[1]) + "\n30\n0\n" + entities + "0\nENDBLK\n8\n0\n"
    )


def dxf_with_blocks(blocks: str, entities: str) -> str:
    return (
        "0\nSECTION\n2\nBLOCKS\n" + blocks + "0\nENDSEC\n"
        + "0\nSECTION\n2\nENTITIES\n" + entities + "0\nENDSEC\n0\nEOF\n"
    )


def block_square(x0: float, y0: float, size: float) -> str:
    return (
        "0\nLWPOLYLINE\n8\n0\n90\n4\n70\n1\n"
        f"10\n{x0}\n20\n{y0}\n10\n{x0 + size}\n20\n{y0}\n"
        f"10\n{x0 + size}\n20\n{y0 + size}\n10\n{x0}\n20\n{y0 + size}\n"
    )


def lwpolyline(handle: str, layer: str, elevation: float = 0, bulge: float = 0) -> str:
    return (
        "0\nLWPOLYLINE\n5\n" + handle + "\n8\n" + layer + "\n90\n2\n70\n0\n"
        "38\n" + str(elevation) + "\n10\n700000\n20\n7100000\n42\n" + str(bulge)
        + "\n10\n700010\n20\n7100010\n"
    )


class GeoMapDxfTests(unittest.TestCase):
    def options(self, root: Path, *, project_to_2d: bool = True):
        return geomap_dxf.ConversionOptions(
            input_path=root / "input.dxf",
            output_path=root / "output.geomap.json",
            layer="TEST-NETWORK",
            source_crs="EPSG:31982",
            units="metre",
            project_external_id="fixture-project",
            project_name="Fixture project",
            network_external_id="fixture-network",
            network_name="Fixture network",
            network_type="stormwater",
            exported_at="2026-09-09T09:00:00Z",
            project_to_2d=project_to_2d,
        )

    def test_converts_selected_layer_and_preserves_basic_attributes(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            root.joinpath("input.dxf").write_text(
                dxf(
                    lwpolyline("A1", "TEST-NETWORK", 12.5)
                    + lwpolyline("B1", "OTHER-LAYER")
                    + lwpolyline("A3", "TEST-NETWORK").replace(
                        "8\nTEST-NETWORK\n", "8\nTEST-NETWORK\n67\n1\n"
                    )
                    + "0\nTEXT\n5\nA2\n8\nTEST-NETWORK\n1\nannotation\n"
                ),
                encoding="utf-8",
            )

            document, summary = geomap_dxf.convert(self.options(root))

            self.assertEqual("0.1", document["contractVersion"])
            self.assertEqual("EPSG:31982", document["sourceCrs"])
            self.assertEqual(1, len(document["features"]))
            feature = document["features"][0]
            self.assertEqual("A1", feature["sourceId"])
            self.assertEqual("Polyline", feature["sourceType"])
            self.assertEqual("TEST-NETWORK", feature["sourceLayer"])
            self.assertEqual(
                [[700000.0, 7100000.0], [700010.0, 7100010.0]],
                feature["geometry"]["coordinates"],
            )
            self.assertEqual(12.5, feature["properties"]["cadElevation"])
            self.assertTrue(feature["properties"]["projectedTo2D"])
            self.assertEqual({"TEXT": 1}, summary["ignoredEntities"])
            self.assertEqual(1, summary["skippedPaperSpaceEntities"])

    def test_requires_explicit_projection_for_non_zero_elevation(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            root.joinpath("input.dxf").write_text(
                dxf(lwpolyline("A1", "TEST-NETWORK", 5)), encoding="utf-8"
            )
            with self.assertRaisesRegex(geomap_dxf.DxfConversionError, "project-to-2d"):
                geomap_dxf.convert(self.options(root, project_to_2d=False))

    def test_aci_palette_matches_autocad_reference_values(self):
        reference = {
            1: "#FF0000", 2: "#FFFF00", 5: "#0000FF", 7: "#000000", 8: "#808080",
            11: "#FF7F7F", 13: "#CC6666", 20: "#FF3F00", 30: "#FF7F00",
            52: "#CCCC00", 140: "#00BFFF", 170: "#0000FF", 250: "#333333",
        }
        for index, expected in reference.items():
            self.assertEqual(expected, geomap_dxf.aci_to_hex(index), index)
        self.assertIsNone(geomap_dxf.aci_to_hex(0))
        self.assertIsNone(geomap_dxf.aci_to_hex(256))

    def test_exports_the_layer_aci_color_as_contract_02(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            layers = layer_record("OTHER", 1) + layer_record("TEST-NETWORK", 30)
            root.joinpath("input.dxf").write_text(
                dxf_with_layers(layers, lwpolyline("A1", "TEST-NETWORK")), encoding="utf-8"
            )
            document, summary = geomap_dxf.convert(self.options(root))
            self.assertEqual("0.2", document["contractVersion"])
            self.assertEqual("#FF7F00", document["network"]["color"])
            self.assertEqual("#FF7F00", summary["networkColor"])

    def test_prefers_true_color_and_ignores_the_layer_off_sign(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            root.joinpath("input.dxf").write_text(
                dxf_with_layers(
                    layer_record("TEST-NETWORK", 30, true_color=0x1A2B3C),
                    lwpolyline("A1", "TEST-NETWORK"),
                ),
                encoding="utf-8",
            )
            document, _ = geomap_dxf.convert(self.options(root))
            self.assertEqual("#1A2B3C", document["network"]["color"])

            root.joinpath("input.dxf").write_text(
                dxf_with_layers(layer_record("TEST-NETWORK", -5), lwpolyline("A1", "TEST-NETWORK")),
                encoding="utf-8",
            )
            document, _ = geomap_dxf.convert(self.options(root))
            self.assertEqual("#0000FF", document["network"]["color"])

    def test_keeps_contract_01_without_a_layer_table(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            root.joinpath("input.dxf").write_text(
                dxf(lwpolyline("A1", "TEST-NETWORK")), encoding="utf-8"
            )
            document, summary = geomap_dxf.convert(self.options(root))
            self.assertEqual("0.1", document["contractVersion"])
            self.assertNotIn("color", document["network"])
            self.assertIsNone(summary["networkColor"])

    def block_options(self, root: Path):
        return geomap_dxf.ConversionOptions(**{**self.options(root).__dict__, "blocks": True})

    def test_converts_block_references_to_points_with_name_and_attributes(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            entities = (
                insert("B1", "TEST-NETWORK", "TE", {"PV": "PV-12", "COTA": "512.30", "bloco": "x"},
                       rotation=90)
                + insert("B2", "TEST-NETWORK", "CAP")
                + lwpolyline("L1", "TEST-NETWORK")
                + insert("B3", "OTHER", "CRUZETA", {"PV": "PV-99"})
                + insert("B4", "TEST-NETWORK", "TE", paper=True)
            )
            root.joinpath("input.dxf").write_text(
                dxf_with_layers(layer_record("TEST-NETWORK", 3), entities), encoding="utf-8"
            )
            document, summary = geomap_dxf.convert(self.block_options(root))

            self.assertEqual("0.3", document["contractVersion"])
            self.assertEqual("#00FF00", document["network"]["color"])
            self.assertEqual(["B1", "B2"], [f["sourceId"] for f in document["features"]])
            first, second = document["features"]
            self.assertEqual({"type": "Point", "coordinates": [700100.0, 7100100.0]}, first["geometry"])
            self.assertEqual("BlockReference", first["sourceType"])
            self.assertEqual("TE", first["properties"]["bloco"])
            self.assertEqual(90.0, first["properties"]["rotacao"])
            self.assertEqual("PV-12", first["properties"]["PV"])
            self.assertEqual("512.30", first["properties"]["COTA"])
            self.assertEqual("x", first["properties"]["atributo_bloco"])
            self.assertEqual("CAP", second["properties"]["bloco"])
            self.assertNotIn("PV", second["properties"])
            self.assertEqual({"LWPOLYLINE": 1}, summary["ignoredEntities"])
            self.assertEqual(1, summary["skippedPaperSpaceEntities"])

    def test_block_drawn_far_from_its_base_point_uses_the_symbol_position(self):
        # Bloco desenhado já nas coordenadas reais com ponto base em 0,0: o
        # AutoCAD mostra o símbolo em inserção + escala x rotação x desenho.
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            blocks = (
                block_definition(
                    "TE",
                    block_square(700500, 7100500, 6)
                    + "0\nARC\n8\n0\n10\n700503\n20\n7100503\n40\n1\n50\n0\n51\n180\n"
                    + "0\nTEXT\n8\n0\n10\n0\n20\n0\n40\n1\n1\nT\n",
                )
                + block_definition("CAP", block_square(-1, -1, 2))
                + block_definition("PV", block_square(0, 0, 2))
            )
            entities = (
                insert("B1", "TEST-NETWORK", "TE", x=-1000, y=50, rotation=90, scale=0.5)
                + insert("B2", "TEST-NETWORK", "CAP", x=700100, y=7100100, rotation=30, scale=3)
                + insert("B3", "TEST-NETWORK", "PV", x=700200, y=7100200, rotation=45)
            )
            root.joinpath("input.dxf").write_text(dxf_with_blocks(blocks, entities), encoding="utf-8")
            document, summary = geomap_dxf.convert(self.block_options(root))

            te, cap, pv = document["features"]
            # centro do desenho (700503, 7100503) x 0.5, girado 90 graus
            expected = (-1000 - 7100503 * 0.5, 50 + 700503 * 0.5)
            for actual, wanted in zip(te["geometry"]["coordinates"], expected):
                self.assertAlmostEqual(wanted, actual, places=6)
            self.assertTrue(te["properties"]["posicaoPeloSimbolo"])
            self.assertEqual([700100.0, 7100100.0], cap["geometry"]["coordinates"])
            self.assertFalse(cap["properties"]["posicaoPeloSimbolo"])
            self.assertEqual([700200.0, 7100200.0], pv["geometry"]["coordinates"])
            self.assertFalse(pv["properties"]["posicaoPeloSimbolo"])
            self.assertEqual(1, summary["symbolPositionedBlocks"])

    def test_survey_points_are_always_excluded(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            entities = (
                insert("B1", "TEST-NETWORK", "TE")
                + insert("B2", "TEST-NETWORK", "SIMB_PONTO", {"PONTO": "12", "DESC": "PT"})
                + insert("B3", "_TG_06-03", "SIMB_PONTO")
            )
            root.joinpath("input.dxf").write_text(dxf(entities), encoding="utf-8")
            document, summary = geomap_dxf.convert(self.block_options(root))
            self.assertEqual(["B1"], [f["sourceId"] for f in document["features"]])
            self.assertNotIn("PONTO", document["features"][0]["properties"])
            self.assertEqual({"INSERT survey point": 1}, summary["ignoredEntities"])

            for blocks in (True, False):
                survey = geomap_dxf.ConversionOptions(
                    **{**self.options(root).__dict__, "layer": "_TG_06-03", "blocks": blocks}
                )
                with self.assertRaisesRegex(geomap_dxf.DxfConversionError, "survey"):
                    geomap_dxf.convert(survey)

    def test_blocks_mode_requires_at_least_one_block(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            root.joinpath("input.dxf").write_text(
                dxf(lwpolyline("L1", "TEST-NETWORK")), encoding="utf-8"
            )
            with self.assertRaisesRegex(geomap_dxf.DxfConversionError, "no block references"):
                geomap_dxf.convert(self.block_options(root))

    def test_rejects_curved_bulge_instead_of_flattening_it(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            root.joinpath("input.dxf").write_text(
                dxf(lwpolyline("A1", "TEST-NETWORK", bulge=0.5)), encoding="utf-8"
            )
            with self.assertRaisesRegex(geomap_dxf.DxfConversionError, "curved bulge"):
                geomap_dxf.convert(self.options(root))

    def test_converts_line_and_closes_closed_lwpolyline(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            entities = (
                "0\nLINE\n5\n10\n8\nTEST-NETWORK\n10\n1\n20\n2\n30\n3\n"
                "11\n4\n21\n5\n31\n6\n"
                "0\nLWPOLYLINE\n5\n11\n8\nTEST-NETWORK\n90\n2\n70\n1\n"
                "10\n0\n20\n0\n10\n1\n20\n1\n"
            )
            root.joinpath("input.dxf").write_text(dxf(entities), encoding="utf-8")
            document, _ = geomap_dxf.convert(self.options(root))
            self.assertEqual(2, len(document["features"]))
            self.assertEqual("Line", document["features"][0]["sourceType"])
            self.assertEqual(
                [[0.0, 0.0], [1.0, 1.0], [0.0, 0.0]],
                document["features"][1]["geometry"]["coordinates"],
            )

    def test_cli_writes_utf8_json_and_aggregate_summary(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            root.joinpath("input.dxf").write_text(
                dxf(lwpolyline("A1", "TEST-NETWORK")), encoding="utf-8"
            )
            options = self.options(root)
            document, summary = geomap_dxf.convert(options)
            options.output_path.write_text(
                json.dumps(document, ensure_ascii=False), encoding="utf-8"
            )
            parsed = json.loads(options.output_path.read_text(encoding="utf-8"))
            self.assertEqual(1, summary["features"])
            self.assertEqual("Fixture network", parsed["network"]["name"])


if __name__ == "__main__":
    unittest.main()
