# X27: procedencia y límites de los sprites de objetos

Fecha: 14-oct-2026. Base `4d92e1c`. Continúa el ADR 0088 y X18.

## Fuentes de Gen 3 aceptadas

| Juego | Fuente | Revisión fijada | Entradas objeto-juego |
|---|---|---|---:|
| Esmeralda | [pret/pokeemerald](https://github.com/pret/pokeemerald/tree/731ad5bfd6e6f265508d0efcca0ba42f9dcf5881/graphics/items) | `731ad5bfd6e6f265508d0efcca0ba42f9dcf5881` | 284 |
| Rojo Fuego / Verde Hoja | [pret/pokefirered](https://github.com/pret/pokefirered/tree/037335f4c725d7c9aecdac87066f2002b4bd7e14/graphics/items) | `037335f4c725d7c9aecdac87066f2002b4bd7e14` | 284 |

Identidad demostrada: `InventoryPouch.GetAllItems` del Core → id nativo → `include/constants/items.h` de pret → `src/data/item_icon_table.h` → par gráfico/paleta de `src/data/graphics/items.h`. No se deduce del nombre ni del id moderno. Incluye MT/MO, con la paleta de tipo que asigna el juego. Los PNG son paletizados 24×24, con transparencia en el índice 0. Se conserva IDAT y tRNS; solo se sustituye PLTE y su CRC. Sin escalado ni filtros. Los valores RGB son los de los assets JASC-PAL originales de pret, no una simulación de la pantalla física de GBA.

La referencia independiente usa Pillow para leer los índices y aplicar cada paleta. Los hashes BGRA de esa referencia se comparan con los píxeles que devuelve `SpriteComposer.ResolverObjeto` a la app, en 568 pruebas por fila. Para una misma forma (Potion/Hyper Potion, status heals, gemas, MT/MO) la paleta cambia el objeto: no basta con copiar el PNG base. Cada fuente PNG, paleta, PNG resultante y referencia de píxeles tiene SHA-256 en `pret-gen3.tsv`; procedencia legible por fila en `sprites-objetos-procedencia.tsv`.

### Rubí/Zafiro

Se revisó [pret/pokeruby@5784633c](https://github.com/pret/pokeruby/tree/5784633ce4ef7ade1a7f2d2d0c288e3d5e6cdd7f). No hay `graphics/items` ni tabla de iconos por objeto. `src/item_menu.c` dibuja bolsa/texto y su `DrawSelectIcon` es el cursor de selección, no arte distinto por objeto. Por tanto no se reutiliza Esmeralda como arte exacto de RS. Se conservan sus iconos anteriores; las cuatro filas que X18 llamaba exactas por la carpeta gen3 de PokeAPI pasan a «arte de otra versión». No se borra contenido ni se cambia la mochila de RS.

### Licencias

Las raíces de ambos repositorios pret no contienen una licencia general para los gráficos. Las licencias en `tools/` pertenecen a las herramientas (p. ej., `gbagfx`), no conceden derechos sobre el arte del juego. El arte sigue siendo © The Pokémon Company / Nintendo / Game Freak. No se han copiado ROMs ni código de conversión de pret. Se mantiene el criterio documentado en X18/NOTICE: almacenamiento privado y procedencia explícita; el acceso público no se presenta como autorización de redistribución.

## Otras fuentes comprobadas

| Fuente | Evidencia | Resultado X27 |
|---|---|---|
| [PokeAPI/sprites](https://github.com/PokeAPI/sprites/tree/0dc105d5b12416932f29b594078282ab4be045f5/sprites/items) | La cabeza pública sigue en `0dc105d5...`, la revisión de X18. Árbol de items examinado. | No aporta una revisión nueva ni arte para los huecos restantes. Se conserva su procedencia/licencia de X18. |
| [msikma/pokesprite](https://github.com/msikma/pokesprite/tree/c5aaa610ff2acdf7fd8e2dccd181bca8be9fcb3e/items) | 904 enlaces del mapa, hasta item_1785; sin items de PLA/BDSP en el catálogo examinado. | Mismo conjunto de X18; no se inventan equivalencias de los caramelos ni las cartas de Let's Go. MIT para el código, arte © The Pokémon Company, como X18. |
| [PKForge ItemArt](https://github.com/sofianeelhor/PKForge/blob/94fff38ea7817a594db58f38722d6f2f008d3b9f/src/PKForge.App/Services/ItemArt.cs) | Usa PNG de PokeAPI por nombre y un marcador generado cuando falta arte. | No es una fuente adicional de arte real; no se copia código ni el marcador. |
| [Anuncio de iconos PLA en PokéSprite #116](https://github.com/msikma/pokesprite/issues/116) | Enlaza `mega.nz/file/dA0l1AhZ`. Consulta pública normal `g.api.mega.co.nz`, petición `g`, devuelve **-9** (archivo no disponible). Respuesta local `compilacion/x27/mega-la.json`. | **LÍMITE REAL: -9**. No se fuerza ni se declara cubierto PLA. |
| The Spriters Resource, Bulbapedia, Serebii | Desafíos Cloudflare documentados en X18 (assets 123706 y 187089). | Se conserva ese límite previo; no se afirma una comprobación nueva ni se intenta eludirlo. |

El primer `git clone` con Schannel falló por el entorno: `AcquireCredentialsHandle failed: SEC_E_NO_CREDENTIALS (0x8009030e)`. La descarga HTTPS normal con el backend OpenSSL de Git funcionó; pret no estaba bloqueado. No se modificó la configuración global de Git ni se desactivó la verificación TLS.

## Reproducción

1. Clonar las fuentes bajo `compilacion/x27/fuentes/` y fijarlas a las revisiones indicadas (sin modificar los árboles).
2. `python -I -B herramientas/generar-mapa-pret.py --fuentes compilacion/x27/fuentes` genera el mapa con Pillow y exige las revisiones fijadas.
3. `herramientas/generar-objetos-android.ps1` descarga/cacha gráfico y paleta fijados, valida sus hashes y genera el paquete. `-SinDescargas` permite repetir con la caché completa, sin red. `-SinCopiaCompartida` permite validar antes de copiar.
4. Ejecutar `ObjetosX27Tests`, las pruebas afectadas de X18/matriz/objetos y las dos clases `ObjetosMochila*Tests` con `-p:SourceRevisionId=74b88906e935 -p:UsedAvaloniaProducts=`.

Paquete verificado en ambos `datos/android/objetos`: 1.223.713 bytes, 1.469 claves, 771 PNG distintos por SHA-256; deduplicado. Los 901 recursos anteriores se conservan. **Los 827 sin arte no se han reducido:** los nuevos gráficos acreditan arte exacto donde antes había arte de otra versión, no cubren objetos sin fuente.
