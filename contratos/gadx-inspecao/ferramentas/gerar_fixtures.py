"""Gera as fixtures de referência do GADX 1.1 (M3.2).

Uso, a partir da raiz do monorepo:

    python contratos/gadx-inspecao/ferramentas/gerar_fixtures.py

Escreve em contratos/gadx-inspecao/fixtures/:
- json/<nome>.json: payload de cada mensagem (UTF-8 sem BOM, LF);
- frames/<nome>.bin: o frame completo (cabeçalho de 16 bytes + o mesmo payload, byte a byte);
- manifest.json: lista de fixtures com tipo e requestId, lida pelos testes das duas aplicações.

Todos os dados são fictícios. O script é determinístico: rodar de novo gera os mesmos bytes.
"""

from __future__ import annotations

import json
import struct
from pathlib import Path

FIXTURES = Path(__file__).resolve().parents[1] / "fixtures"
MAGIC = b"GADX"
WIRE_VERSION = 1

HELLO, HELLO_ACK, INSPECTION_PUSH, INSPECTION_ACK, ERROR, CORRECTION_PUSH, CORRECTION_ACK = range(1, 8)

PROJECT_ID = "0e0c0000-0000-4000-8000-000000000001"
REPORT_ID = "0e0c0000-0000-4000-8000-000000000002"
LOCATION_ID = "0e0c0000-0000-4000-8000-000000000003"
SECTION_ID = "0e0c0000-0000-4000-8000-000000000004"
VIDEO_ID = "0e0c0000-0000-4000-8000-000000000005"
OCC_POINT = "0e0c0000-0000-4000-8000-000000000011"
OCC_RANGE = "0e0c0000-0000-4000-8000-000000000012"
OCC_MULTI = "0e0c0000-0000-4000-8000-000000000013"
OCC_ENTIRE = "0e0c0000-0000-4000-8000-000000000014"
ANOMALY_TYPE_ID = "0e0c0000-0000-4000-8000-000000000021"
EVIDENCE_ID = "0e0c0000-0000-4000-8000-000000000031"
CAPTURE_ID = "0e0c0000-0000-4000-8000-000000000032"


def hello(capabilities: list[str] | None) -> dict:
    payload = {
        "application": "GeoAutoDocs",
        "applicationVersion": "1.2.0",
        "protocolVersion": 1,
        "inspectionSchemaVersion": 1,
    }
    if capabilities is not None:
        payload["capabilities"] = capabilities
    return payload


def hello_ack(capabilities: list[str] | None) -> dict:
    payload = {
        "application": "GeoAutoEngine",
        "applicationVersion": "3.3.0",
        "protocolVersion": 1,
        "inspectionSchemaVersion": 1,
        "cadProduct": "Civil 3D",
        "cadVersion": "2026",
    }
    if capabilities is not None:
        payload["capabilities"] = capabilities
    return payload


def occurrence(occurrence_id: str, order: int, mode: str, meterage, end_meterage, points: list,
               description: str, anomalies: list | None = None, evidence: list | None = None) -> dict:
    return {
        "occurrenceId": occurrence_id,
        "order": order,
        "locationMode": mode,
        "meterage": meterage,
        "endMeterage": end_meterage,
        "meteragePoints": points,
        "referencePoint": None,
        "position": None,
        "notes": None,
        "generatedDescription": description,
        "manualDescriptionOverride": None,
        "effectiveDescription": description,
        "interruptedRecording": False,
        "isSevere": False,
        "videoReferenceId": VIDEO_ID,
        "videoTimestamp": "00:01:15",
        "anomalies": anomalies or [],
        "evidence": evidence or [],
    }


def inspection_document() -> dict:
    anomaly = {
        "anomalyTypeId": ANOMALY_TYPE_ID,
        "name": "Trinca",
        "position": "UpperGenerator",
        "orientation": "Longitudinal",
        "approximateExtension": 0.5,
        "dimension": None,
        "condition": "Moderate",
        "infiltrationCondition": "NotReported",
        "estimatedPercentage": None,
        "notes": None,
        "customText": None,
        "order": 0,
    }
    evidence = {
        "evidenceId": EVIDENCE_ID,
        "type": "VideoCapture",
        "captureId": CAPTURE_ID,
        "videoReferenceId": VIDEO_ID,
        "videoTimestamp": "00:01:15",
        "caption": "Trinca a 12,5 m",
        "isPrimary": True,
        "order": 0,
    }
    return {
        "schema": "GeoAutoDocs.Inspection",
        "schemaVersion": 1,
        "generatedAtUtc": "2026-11-16T12:00:00Z",
        "project": {"projectId": PROJECT_ID, "name": "Projeto fictício de contrato"},
        "report": {
            "reportId": REPORT_ID,
            "reportCode": "FX-001",
            "name": "Relatório de referência GADX 1.1",
            "client": "Cliente fictício",
            "area": "Área de teste",
            "contract": "CT-FX",
            "reportDate": "2026-11-16T00:00:00",
            "totalInvestigatedMeters": 40,
        },
        "locations": [
            {"locationId": LOCATION_ID, "type": "Street", "name": "Rua Fictícia", "order": 0},
        ],
        "sections": [
            {
                "sectionId": SECTION_ID,
                "locationId": LOCATION_ID,
                "name": "Trecho 01",
                "description": "PV-01 até PV-02",
                "diameter": 300,
                "startPoint": "PV-01",
                "endPoint": "PV-02",
                "reference": None,
                "investigatedMeters": 40,
                "order": 0,
                "videoReferences": [
                    {
                        "videoReferenceId": VIDEO_ID,
                        "displayName": "Filmagem 01",
                        "fileName": "trecho-01.mp4",
                        "order": 0,
                        "description": None,
                        "createdAt": "2026-11-16T09:00:00",
                    }
                ],
                "occurrences": [
                    occurrence(OCC_POINT, 0, "Point", 12.5, None, [], "Trinca longitudinal a 12,5 m",
                               [anomaly], [evidence]),
                    occurrence(OCC_RANGE, 1, "Range", 20, 25.5, [], "Raízes de 20,0 m a 25,5 m"),
                    occurrence(OCC_MULTI, 2, "MultiPoint", None, None, [3, 6, 9], "Juntas abertas"),
                    occurrence(OCC_ENTIRE, 3, "EntireSection", None, None, [], "Sedimentos em todo o trecho"),
                ],
            }
        ],
    }


def correction(occurrence_id: str, mode: str, meterage, end_meterage, points: list, description: str) -> dict:
    # Estado completo (SPEC §9.2): todos os campos, inclusive nulos e listas vazias.
    return {
        "sectionId": SECTION_ID,
        "occurrenceId": occurrence_id,
        "locationMode": mode,
        "meterage": meterage,
        "endMeterage": end_meterage,
        "meteragePoints": points,
        "description": description,
    }


def correction_push() -> dict:
    return {
        "reportId": REPORT_ID,
        "generatedAtUtc": "2026-11-16T12:30:00Z",
        "corrections": [
            correction(OCC_POINT, "Point", 13, None, [], "Trinca longitudinal a 13,0 m"),
            correction(OCC_RANGE, "Range", 20, 26, [], "Raízes de 20,0 m a 26,0 m"),
            correction(OCC_MULTI, "MultiPoint", None, None, [3, 6], "Juntas abertas"),
        ],
    }


def correction_ack_partial() -> dict:
    return {
        "accepted": True,
        "status": "Partial",
        "message": "2 correção(ões) aplicada(s); 1 item(ns) não foi(ram) localizado(s) ou validado(s).",
        "requestedCount": 3,
        "appliedCount": 2,
        "rejectedCount": 1,
        "results": [
            {"sectionId": SECTION_ID, "occurrenceId": OCC_POINT, "applied": True, "status": "Applied",
             "message": "Pronta para aplicar."},
            {"sectionId": SECTION_ID, "occurrenceId": OCC_RANGE, "applied": True, "status": "Applied",
             "message": "Pronta para aplicar."},
            {"sectionId": SECTION_ID, "occurrenceId": OCC_MULTI, "applied": False, "status": "Invalid",
             "message": "A ocorrência multiponto deve manter 3 ponto(s)."},
        ],
    }


# (nome, tipo, requestId, payload, descrição)
FIXTURES_SPEC = [
    ("hello-1.0", HELLO, 1001, hello(None), "Hello de cliente 1.0 (GAD 1.1.0), sem capabilities"),
    ("hello-1.1", HELLO, 1002, hello(["corrections"]), "Hello de cliente 1.1 que aceita correções"),
    ("hello-1.1-sem-correcoes", HELLO, 1003, hello([]), "Hello de cliente 1.1 sem nenhuma capacidade (regra S1)"),
    ("hello-ack-1.0", HELLO_ACK, 1001, hello_ack(None), "HelloAck de servidor 1.0 (GAE 3.2.0)"),
    ("hello-ack-1.1", HELLO_ACK, 1002, hello_ack(["corrections"]), "HelloAck de servidor 1.1 que oferece correções"),
    ("inspection-push", INSPECTION_PUSH, 1004, inspection_document(),
     "Documento GeoAutoDocs.Inspection v1 com Point, Range, MultiPoint e EntireSection"),
    ("inspection-ack-success", INSPECTION_ACK, 1004,
     {"accepted": True, "status": "Success", "message": "Inspeção recebida e validada pelo GeoAutoEngine."},
     "InspectionAck de sucesso"),
    ("error-incompatible-version", ERROR, 1005,
     {"status": "IncompatibleVersion", "message": "Compatibilidade requerida: protocolo 1, schema 1."},
     "Error de versão incompatível no handshake"),
    ("correction-push", CORRECTION_PUSH, 100001, correction_push(),
     "CorrectionPush com estado completo: Point, Range e MultiPoint (o MultiPoint traz 2 pontos para uma ocorrência com 3, inválido de propósito)"),
    ("correction-ack-partial", CORRECTION_ACK, 100001, correction_ack_partial(),
     "CorrectionAck Partial: 2 aplicadas, 1 inválida"),
]


def encode(payload: dict) -> bytes:
    return (json.dumps(payload, ensure_ascii=False, indent=2) + "\n").encode("utf-8")


def frame(message_type: int, request_id: int, payload: bytes) -> bytes:
    return MAGIC + struct.pack("<HHii", WIRE_VERSION, message_type, len(payload), request_id) + payload


def main() -> None:
    (FIXTURES / "json").mkdir(parents=True, exist_ok=True)
    (FIXTURES / "frames").mkdir(parents=True, exist_ok=True)
    manifest = []
    for name, message_type, request_id, payload, description in FIXTURES_SPEC:
        body = encode(payload)
        (FIXTURES / "json" / f"{name}.json").write_bytes(body)
        (FIXTURES / "frames" / f"{name}.bin").write_bytes(frame(message_type, request_id, body))
        manifest.append({
            "name": name,
            "messageType": message_type,
            "requestId": request_id,
            "description": description,
        })
    (FIXTURES / "manifest.json").write_bytes(encode({
        "contract": "GADX",
        "specRevision": "1.1",
        "wireVersion": WIRE_VERSION,
        "fixtures": manifest,
    }))
    print(f"{len(manifest)} fixtures em {FIXTURES}")


if __name__ == "__main__":
    main()
