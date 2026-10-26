# X28: arte de objetos de Gen. 4 (DS) desde las decompilaciones públicas

Fecha: 14-oct-2026. Base `fb5dd1f`. Continúa el ADR 0088, X18 y X27.

| Juego | Fuente | Revisión fijada | Entradas |
|---|---|---|---:|
| Platino | [pret/pokeplatinum](https://github.com/pret/pokeplatinum/tree/fe1ea418a7364c93daf0d07ae55e195452d8803c/res/items) | `fe1ea418a7364c93daf0d07ae55e195452d8803c` | 444 |
| HeartGold/SoulSilver | [pret/pokeheartgold](https://github.com/pret/pokeheartgold/tree/6cedc4f57c9ce18821adbfff25c6aefbd0dc1176) | `6cedc4f57c9ce18821adbfff25c6aefbd0dc1176` | 451 |
| Diamante/Perla | [pret/pokediamond](https://github.com/pret/pokediamond/tree/b623abef1b4dfe204ca50aab42447ac00c134290) | `b623abef1b4dfe204ca50aab42447ac00c134290` | 440 |

Identidad demostrada: id nativo del Core (`Universo.Objetos`) → tabla del propio juego (`res/items/data/*.json` en Platino, `src/item.c` en HGSS, `sItemIndexMappings` de `arm9/src/itemtool.c` en DP) → par gráfico/paleta. El índice del registro de datos NO es el id nativo en los ids altos (la prueba lo comprueba con DP/Pt 442 y HGSS 532). Los iconos son 32×32; el mapa por fila (`sprites-objetos-ds-x28.tsv`) guarda SHA-256 de gráfico, paleta, PNG y píxeles BGRA. Herramienta: `herramientas/preparar-objetos-ds.py` (+ `objetos_formatos.py` para NCGR/NCLR). Sin ROMs.

Clave de recurso `xorig_<juego>_<id>`; gana a las demás fuentes del mismo juego. Procedencia legible por fila en `sprites-objetos-procedencia.tsv`.

Licencias: igual que X27. Los repositorios pret no declaran licencia general del arte, que sigue siendo © The Pokémon Company / Nintendo / Game Freak; almacenamiento privado y procedencia explícita, sin inferir permiso de redistribución.

Resultado: 1.335 entradas pasan de «arte de otra versión» a «arte exacto» (DP 440, Pt 444, HGSS 451). Los 827 sin arte no cambian.
