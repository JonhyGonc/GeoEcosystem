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
