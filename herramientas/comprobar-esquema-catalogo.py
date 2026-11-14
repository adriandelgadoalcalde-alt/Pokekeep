# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Pokekeep contributors
"""Comprueba que un catálogo esté en la última migración del código antes de empaquetarlo.

R-014 (14-nov-2026): la 1.1.0 salió con el APK y el ZIP llevando catálogos del 23-oct (versión 7 del esquema, sin event_offer)
y «Añadir evento» quedó vacío sin avisar. compilar-apk.ps1 y construir-portable.ps1 lo llaman con el catálogo que empaquetan.
Uso: python comprobar-esquema-catalogo.py <catalogo.sqlite> [--migraciones <carpeta con NNNN_*.up.sql>]
Sale con 0 si el catálogo está en la última versión; con 1 (y el motivo) si no.
"""
import argparse
import re
import sqlite3
import sys
from pathlib import Path


def ultima_migracion(carpeta: Path) -> int:
    versiones = [int(m.group(1)) for f in carpeta.glob('*.up.sql') if (m := re.match(r'^(\d{4})_', f.name))]
    if not versiones:
        raise SystemExit(f'No hay migraciones en {carpeta}.')
    return max(versiones)


def version_catalogo(ruta: Path) -> int:
    if not ruta.is_file():
        raise SystemExit(f'No existe el catálogo {ruta}.')
    c = sqlite3.connect(f'file:{ruta}?mode=ro', uri=True)
    try:
        if not c.execute("SELECT count(*) FROM sqlite_master WHERE type='table' AND name='schema_version'").fetchone()[0]:
            return 0
        return c.execute('SELECT COALESCE(MAX(version), 0) FROM schema_version').fetchone()[0]
    finally:
        c.close()


def comprobar(catalogo: Path, migraciones: Path) -> str | None:
    """None si está al día; si no, el motivo."""
    esperada = ultima_migracion(migraciones)
    actual = version_catalogo(catalogo)
    if actual < esperada:
        return (f'El catálogo {catalogo.name} está en la versión {actual} del esquema y el código va por la {esperada}: '
                f'hay que reconstruirlo antes de empaquetar (construir-bd-android.ps1 o construir-portable.ps1 -ReconstruirBD).')
    if actual > esperada:
        return f'El catálogo {catalogo.name} está en la versión {actual}, más nueva que el código ({esperada}).'
    return None


def main() -> int:
    p = argparse.ArgumentParser()
    p.add_argument('catalogo')
    p.add_argument('--migraciones', default=str(Path(__file__).resolve().parent.parent / 'src' / 'AutoLegalidad.Catalog' / 'Migrations'))
    a = p.parse_args()
    motivo = comprobar(Path(a.catalogo), Path(a.migraciones))
    if motivo:
        print(motivo, file=sys.stderr)
        return 1
    print(f'Catálogo al día: versión {ultima_migracion(Path(a.migraciones))} del esquema.')
    return 0


if __name__ == '__main__':
    sys.exit(main())
