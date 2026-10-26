// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace AutoLegalidad.Ui.AcercaDe;

/// <summary>Una versión del historial de novedades de Pokekeep (resumido de la bitácora del proyecto).</summary>
public sealed record NovedadVersion(string Version, string Fecha, string Resumen, IReadOnlyList<string> Novedades);

/// <summary>Un componente de terceros con su licencia y cómo se usa (los mismos que <c>NOTICE.md</c>).</summary>
public sealed record Credito(string Componente, string Licencia, string Uso);

/// <summary>
/// Contenido de la ventana «Acerca de Pokekeep» (familia Keep): nombre, versión real, autoría, créditos y licencias y novedades por versión. Sin dependencias de WinForms para poder
/// probarlo; <see cref="VentanaAcercaDe"/> solo lo pinta. Todo en castellano de España.
/// </summary>
public static class AcercaDeModelo
{
    public const string Nombre = "Pokekeep";
    public static string Lema => AppIdioma.T("PKHeX portable en castellano con «Legalizar automáticamente».", "Portable PKHeX with “Legalize automatically”.");
    /// <summary>Lema de la app de Android (1.0.0): la de Windows sigue siendo el PKHeX portable.</summary>
    public static string LemaAndroid => AppIdioma.T("El editor de partidas de PKHeX en tu móvil, con «Legalizar automáticamente».", "PKHeX’s save editor on your phone, with “Legalize automatically”.");
    public static string Autoria => AppIdioma.T("Pokekeep está diseñado y desarrollado por IncrediBad. Es una herramienta de la familia Keep (Terrakeep, Starvekeep, ServidorKeep...), con el mismo estándar de calidad que el resto.", "Pokekeep is designed and developed by IncrediBad. It is a tool of the Keep family (Terrakeep, Starvekeep, ServidorKeep...), with the same quality standard as the rest.");
    public static string Licencia => AppIdioma.T("Pokekeep se publica bajo la licencia GNU GPL v3.0 o posterior, la misma que PKHeX, de la que es una obra derivada. Todo el código propio lleva esa cabecera.", "Pokekeep is published under the GNU GPL v3.0 or later license, the same as PKHeX, of which it is a derivative work. All of its own code carries that header.");
    public static string AvisoEventos => AppIdioma.T("Datos de eventos: este paquete incluye una base de datos derivada de EventsGallery (Project Pokémon), que no declara licencia. El repositorio no contiene ninguno de sus ficheros y Pokekeep no muestra ni publica títulos de eventos. Si eres autor de EventsGallery y no estás de acuerdo con este uso, abre una incidencia en el repositorio y se retiran.", "Event data: this package includes a database derived from EventsGallery (Project Pokémon), which declares no license. The repository contains none of its files and Pokekeep does not show or publish event titles. If you are an EventsGallery author and do not agree with this use, open an issue in the repository and they will be removed.");
    public static string AvisoMarcas => AppIdioma.T("Pokémon y sus nombres, formas y datos son marcas y contenido de Nintendo, Creatures Inc. y GAME FREAK inc. Pokekeep es una herramienta no oficial y no está afiliada ni respaldada por ellos. No se distribuyen ROMs, partidas guardadas ni claves.", "Pokémon and its names, forms and data are trademarks and content of Nintendo, Creatures Inc. and GAME FREAK inc. Pokekeep is an unofficial tool and is not affiliated with or endorsed by them. No ROMs, saves or keys are distributed.");
    public static string AvisoPrivacidad => AppIdioma.T("Tus guardados y los datos del entrenador se quedan en tu equipo: Pokekeep no usa la red y nunca los envía a ningún servicio externo.", "Your saves and trainer data stay on your device: Pokekeep does not use the network and never sends them to any external service.");

    public static IReadOnlyList<Credito> Creditos => [
        new(AppIdioma.T("PKHeX, de Kurt «kwsch» y colaboradores (github.com/kwsch/PKHeX)", "PKHeX, by Kurt “kwsch” and contributors (github.com/kwsch/PKHeX)"), "GPL-3.0-or-later", AppIdioma.T("Base del producto: el editor y el motor de legalidad (PKHeX.Core) en su versión 26.10.18, sin cambios de legalidad.", "Foundation of the product: the editor and the legality engine (PKHeX.Core) in its version 26.10.18, with no legality changes.")),
        new(AppIdioma.T("Auto Legality Mod, de PKHeX-Plugins (santacrab2, architdate)", "Auto Legality Mod, from PKHeX-Plugins (santacrab2, architdate)"), "MIT", AppIdioma.T("Motor de generación, encapsulado tras un adaptador propio; cada resultado se vuelve a comprobar con el Core oficial.", "Generation engine, encapsulated behind its own adapter; every result is checked again with the official Core.")),
        new(AppIdioma.T("Project Pokémon: EventsGallery", "Project Pokémon: EventsGallery"), AppIdioma.T("Sin licencia declarada", "No license declared"), AppIdioma.T("Fuente de los datos de eventos; ver el aviso de arriba.", "Source of the event data; see the notice above.")),
        new("Project Pokémon: PoGoEncTool", "AGPL-3.0", AppIdioma.T("Solo sus datos (ventanas de Pokémon GO); no se copia ni se enlaza su código.", "Only its data (Pokémon GO windows); its code is neither copied nor linked.")),
        new("Microsoft.Data.Sqlite y SQLite", AppIdioma.T("MIT / Apache-2.0 / dominio público", "MIT / Apache-2.0 / public domain"), AppIdioma.T("Acceso a la base de datos del catálogo.", "Access to the catalog database.")),
        new(AppIdioma.T("Avalonia UI, SkiaSharp, CommunityToolkit.Mvvm y .NET para Android", "Avalonia UI, SkiaSharp, CommunityToolkit.Mvvm and .NET for Android"), "MIT", AppIdioma.T("Interfaz y plataforma de la app de Android.", "Interface and platform of the Android app.")),
        new("QRCoder / ZXing.Net", "MIT / Apache-2.0", AppIdioma.T("Crear y leer los códigos QR de los Pokémon (Android).", "Creating and reading Pokémon QR codes (Android).")),
        new(AppIdioma.T("Roboto y Noto Symbols", "Roboto and Noto Symbols"), "Apache-2.0 / SIL OFL 1.1", AppIdioma.T("Tipografía e iconos de la app de Android.", "Typeface and icons of the Android app.")),
        new(AppIdioma.T("Contenido Pokémon: imágenes, sonidos y datos", "Pokémon content: images, sounds and data"), "© Nintendo, Creatures Inc., GAME FREAK inc. / The Pokémon Company", AppIdioma.T("Vistas previas, iconos y logos sacados de los propios juegos; carátulas de WikiDex; gritos de PokeAPI. Pokekeep es una herramienta no oficial.", "Previews, icons and logos taken from the games themselves; box art from WikiDex; cries from PokeAPI. Pokekeep is an unofficial tool.")),
    ];

    /// <summary>De la más reciente a la más antigua. Resumido de <c>bitacora.md</c>; cada versión con su fecha real de publicación.</summary>
    public static IReadOnlyList<NovedadVersion> Novedades => [
        new("1.0.1", FechaVisible("26-oct-2026"), AppIdioma.T("Android: ninguna palabra partida ni texto recortado con letra grande.", "Android: no split words or clipped text with large text sizes."),
        [
            AppIdioma.T("Los textos ya no se parten a mitad de palabra en móviles de 360 dp ni con letra grande: «Pokémon» en la barra inferior, la marca de la cabecera y las etiquetas del editor, los ajustes, el banco y las hojas de confirmación. La etiqueta baja su valor a la línea siguiente, las filas de botones se apilan y, como último recurso, el texto se reduce un poco (nunca por debajo de 10 dp).", "Text no longer breaks in the middle of a word on 360 dp phones or with large text: “Pokémon” in the bottom bar, the header logo and the labels in the editor, settings, bank and confirmation sheets. Labels move their value to the next line, button rows stack and, as a last resort, the text shrinks slightly (never below 10 dp)."),
            AppIdioma.T("Tampoco se recorta ningún texto: el botón «Volver a Herramientas», los textos de orden, el selector de caja y los botones segmentados bajan de línea; los valores de las estadísticas base no parten sus cifras.", "No text is clipped either: the “Back to Tools” button, sort labels, the box picker and segmented buttons wrap; base stat values no longer split their digits."),
            AppIdioma.T("En horizontal con el teclado abierto, el aviso de solo lectura del editor ya no saca de la pantalla la barra de acciones. Chips y filtros de al menos 48 dp de ancho, casillas de caja de 48 dp a 360 dp y el icono del objeto equipado en su tamaño.", "In landscape with the keyboard open, the editor’s read-only notice no longer pushes the action bar off the screen. Chips and filters at least 48 dp wide, 48 dp box slots at 360 dp and the held item icon at its normal size."),
            AppIdioma.T("Ajustes de Showdown en castellano: «del» y «al» en vez de «de el» y «a el». En Windows solo cambia el número de versión.", "Showdown settings in Spanish say “del” and “al” instead of “de el” and “a el”. On Windows only the version number changes."),
        ]),
        new("1.0.0", FechaVisible("21-oct-2026"), AppIdioma.T("Pokekeep para Android: la primera versión estable.", "Pokekeep for Android: the first stable version."),
        [
            AppIdioma.T("PKHeX pasa de la 26.08.26 a la 26.10.18, con ALM 26.10.18 y los datos de Pokémon GO más recientes. Reglas nuevas del Core: Rojo Fuego y Verde Hoja de la Consola Virtual de Switch al pasar a HOME, experiencia mínima para los EV de Gen. 3 y 4, cría encadenada de Gen. 1 y de Oro HeartGold/Plata SoulSilver, y el Greninja de la WCS 2026.", "PKHeX moves from 26.08.26 to 26.10.18, with ALM 26.10.18 and the latest Pokémon GO data. New Core rules: FireRed and LeafGreen from Switch Virtual Console when moving to HOME, minimum experience for the EVs of Gen. 3 and 4, chained breeding in Gen. 1 and in HeartGold/SoulSilver, and the 2026 WCS Greninja."),
            AppIdioma.T("Con esas reglas, algunos Pokémon cambian de veredicto: en los guardados de prueba, 5 de 32.599 (cuatro Smeargle de nivel 5 de Esmeralda pasan a ilegales por EV sin la experiencia necesaria y un Dratini de Negro 2 pasa a legal por la cría encadenada).", "With those rules, some Pokémon change verdict: in the test saves, 5 out of 32,599 (four level 5 Smeargle in Emerald become illegal because their EVs lack the required experience, and a Black 2 Dratini becomes legal through chained breeding)."),
            AppIdioma.T("PKHeX ya no importa el Pokémon a tu partida al escribirlo en una casilla (entrenador actual, Pokédex y récords): Pokekeep escribe el resultado tal cual, como antes, y Deshacer sigue dejando la partida byte a byte como estaba.", "PKHeX no longer imports the Pokémon into your save when it is written to a slot (current trainer, Pokédex and records): Pokekeep writes the result as is, as before, and Undo still leaves the save byte for byte as it was."),
            AppIdioma.T("Las tarjetas de Flabébé del Centro Pokémon con escala fija conservan su escala cuando Pokekeep elige la fecha, y ya no se confunden con la tarjeta gemela de escala aleatoria.", "The Pokémon Center Flabébé cards with a fixed scale keep their scale when Pokekeep picks the date, and are no longer mixed up with the twin card with a random scale."),
            AppIdioma.T("Vídeos de batalla de Gen. 4 en Platino y HeartGold/SoulSilver: se ven los equipos de los cuatro vídeos y se importan o exportan, cifrados o sin cifrar. Cintas de regalo de Gen. 3 y 4 con «Todas legales» y «Restablecer».", "Gen 4 Battle Videos in Platinum and HeartGold/SoulSilver: you can see the teams of the four videos and import or export them, encrypted or not. Gen 3 and 4 gift ribbons with “All legal” and “Reset”."),
            AppIdioma.T("Fondos de caja como en PKHeX 26.10.18: los nombres propios de cada juego, un fondo por caja en Colosseum, XD y Battle Revolution, y «Mi fondo» de Pokémon Box Rubí y Zafiro. Las cartas de Gen. 3 permiten elegir la letra de Unown.", "Box wallpapers as in PKHeX 26.10.18: each game’s own names, one wallpaper per box in Colosseum, XD and Battle Revolution, and “My Wallpaper” in Pokémon Box Ruby & Sapphire. Gen 3 mail lets you pick Unown’s letter."),
            AppIdioma.T("Dos ajustes nuevos: «Severidad si el entrenador actual no es el esperado» (Legalidad) y «Pasar de Gen. 3 a HOME directamente» (Conversión), que deja al Pokémon con el origen de Rojo Fuego o Verde Hoja de Switch Online. La ficha enseña la marca de origen de Game Boy Advance.", "Two new settings, shown with PKHeX’s own names: CurrentHandlerMismatch (Legality), the severity when the current trainer is not the expected one, and IsPreferredTransferVirtualHOME (Conversion), which goes from Gen 3 to HOME directly and leaves the Pokémon with the FireRed or LeafGreen Switch Online origin. The sheet shows the Game Boy Advance origin mark."),
            AppIdioma.T("Catálogo, caché de soluciones y bases de datos de Android reconstruidos con el Core nuevo.", "Catalog, solution cache and Android databases rebuilt with the new Core."),
            AppIdioma.T("Primera versión estable de Pokekeep para Android: el editor completo de PKHeX con «Legalizar automáticamente» en el móvil, con la misma comprobación oficial del Core que en Windows.", "First stable version of Pokekeep for Android: PKHeX’s full editor with “Legalize automatically” on your phone, with the same official Core check as on Windows."),
            AppIdioma.T("Cajas, equipo y «Otros» en una sola vista, con modo arrastre (la caja arriba y el equipo debajo), búsqueda que te lleva al Pokémon, selección de varios y menú de caja.", "Boxes, party and “Other” in a single view, with drag mode (the box on top and the party below), a search that takes you to the Pokémon, multi-selection and a box menu."),
            AppIdioma.T("Vistas previas e iconos sacados de cada juego, de Gen. 1 a Leyendas Z-A (también huevos), etiquetas de tipo con el color de Escarlata/Púrpura, carátulas y logos de cada juego, y gritos.", "Previews and icons taken from each game, from Gen 1 to Legends: Z-A (eggs too), type badges with Scarlet/Violet colors, box art and logos of each game, and cries."),
            AppIdioma.T("Edición masiva («Solo los ilegales» legaliza y borra los bytes de basura), «Completar el juego», Librería, Mochila, Partida, banco de Pokémon, cobertura y plantillas de equipo.", "Bulk edit (“Illegal only” legalizes and clears trash bytes), “Complete the game”, Library, Bag, Save, Pokémon bank, team coverage and templates."),
            AppIdioma.T("Todo en español de España y en inglés, con cambio en vivo; tema claro y oscuro, letra grande, TalkBack y el gesto de atrás de Android.", "Everything in Spanish (Spain) and English, switchable live; light and dark themes, large text, TalkBack and the Android back gesture."),
            AppIdioma.T("«Legalizar» de un Pokémon suelto borra también los bytes de basura sin tocar nada más, y el APK va compactado sin perder calidad.", "“Legalize” on a single Pokémon also clears trash bytes without touching anything else, and the APK is compacted without losing quality."),
        ]),
        new("0.7.0", FechaVisible("8-oct-2026"), AppIdioma.T("Cimientos para la versión de Android: nada cambia en Windows.", "Foundations for the Android version: nothing changes on Windows."),
        [
            AppIdioma.T("El núcleo de Pokekeep (coordinadores, operaciones masivas, datos de la Librería, ajustes y textos) se separa de la interfaz de PKHeX para poder compartirlo con la futura versión de Android. En Windows todo funciona igual.", "Pokekeep’s core (coordinators, bulk operations, Library data, settings and texts) is separated from PKHeX’s interface so that it can be shared with the future Android version. On Windows everything works the same."),
            AppIdioma.T("El motor ya puede ejecutarse dentro del propio programa, la identidad de los binarios y las rutas del registro de fallos se reciben desde fuera y los hilos de la búsqueda de semillas son configurables (por defecto, los de siempre).", "The engine can now run inside the program itself, the identity of the binaries and the failure log paths are received from outside, and the seed-search threads are configurable (by default, the usual ones)."),
            AppIdioma.T("El catálogo se divide en lo que usa el producto y lo que solo usa el PC al construir la base de datos; el contrato con el motor se serializa sin reflexión y con el mismo JSON de antes.", "The catalog is split into what the product uses and what only the PC uses when building the database; the contract with the engine is serialized without reflection and with the same JSON as before."),
            AppIdioma.T("La caché de soluciones se ha recalculado con el motor de esta versión.", "The solution cache has been recalculated with this version’s engine."),
        ]),
        new("0.6.6", FechaVisible("8-oct-2026"), AppIdioma.T("Auditoría de Fable: más rigor con HOME, con el nombre del entrenador y con el origen de GO.", "Fable audit: more rigor with HOME, the trainer name and the GO origin."),
        [
            AppIdioma.T("Con el ajuste «Sin HOME (con riesgo)», lo único que se perdona es la matrícula de HOME: un movimiento, una evolución o una cinta que solo valdrían tras pasar por HOME ya no se aceptan, tampoco en un Pokémon nativo.", "With the “Without HOME (risky)” setting, the only thing forgiven is the HOME tracker: a move, an evolution or a ribbon that would only be valid after passing through HOME is no longer accepted, not even on a native Pokémon."),
            AppIdioma.T("Un Pokémon tuyo de Pokémon GO (o de otra generación) sin matrícula conserva su origen con ese ajuste y sale como «Necesita pasar por HOME», nunca como legal; si ya estaba bien, no se reescribe nada.", "A Pokémon of yours from Pokémon GO (or from another generation) without a tracker keeps its origin with that setting and comes out as “Needs to pass through HOME”, never as legal; if it was already fine, nothing is rewritten."),
            AppIdioma.T("El nombre de tu entrenador nunca sale recortado: si no cabe en la generación de un origen, se descarta ese origen y se explica en «Ver cambios».", "Your trainer’s name is never trimmed: if it doesn’t fit in an origin’s generation, that origin is discarded and it is explained in “View changes”."),
            AppIdioma.T("La segunda opinión de la interfaz usa siempre la severidad por defecto de PKHeX, sin tocar sus ajustes desde hilos en segundo plano.", "The interface’s second opinion always uses PKHeX’s default severity, without touching its settings from background threads."),
            AppIdioma.T("Un Pokémon de Consola Virtual con tu nombre y tu TID cuenta como entrenador original tuyo; sin nombre de entrenador en la partida, Pokekeep avisa en vez de inventarse uno.", "A Virtual Console Pokémon with your name and your TID counts as yours as original trainer; with no trainer name in the save, Pokekeep warns instead of making one up."),
            AppIdioma.T("Mensajes más honestos (una imposibilidad de un origen concreto ya no dice que la especie no exista), «Añadir a la caja» respeta las casillas protegidas, la edición masiva relee el ajuste de HOME al aplicar y la ficha exportada de la Librería no lleva títulos de eventos.", "More honest messages (an impossibility of a specific origin no longer says that the species doesn’t exist), “Add to box” respects protected slots, bulk editing rereads the HOME setting when applying and the exported Library summary carries no event titles."),
        ]),
        new("0.6.5", FechaVisible("8-oct-2026"), AppIdioma.T("Origen propio primero: tus Pokémon, con tu entrenador.", "Your own origin first: your Pokémon, with your trainer."),
        [
            AppIdioma.T("Al legalizar sin pista, completar el juego y crear desde la Librería, Pokekeep prefiere un origen capturado o criado en tu juego con el entrenador de tu partida; el entrenador original ajeno solo queda cuando el Core no admite otra ruta (intercambios con personajes, eventos con entrenador propio).", "When legalizing without a hint, completing the game and creating from the Library, Pokekeep prefers an origin caught or bred in your game with your save’s trainer; another original trainer is only kept when the Core allows no other route (trades with characters, events with their own trainer)."),
            AppIdioma.T("Si hace falta otro juego o generación, el entrenador original sigue siendo el de tu partida, con su nombre real; si el nombre no cabe en esa generación (longitud o caracteres), se explica y no se inventa otro.", "If another game or generation is needed, the original trainer is still your save’s, with its real name; if the name doesn’t fit in that generation (length or characters), it is explained and no other is made up."),
            AppIdioma.T("En las evoluciones por intercambio el entrenador anterior eres tú, no un «amigo» inventado.", "In trade evolutions the previous trainer is you, not a made-up “friend”."),
            AppIdioma.T("«Ver cambios» explica el origen: propio, con otro entrenador, transferido desde otro juego o entrenador anterior. La ficha de la Librería dice quién figura como entrenador original.", "“View changes” explains the origin: your own, with another trainer, transferred from another game, or previous trainer. The Library summary says who appears as the original trainer."),
            AppIdioma.T("Lo que ya es tuyo se conserva: un Pokémon con entrenador original ajeno no se pasa a tu entrenador.", "What is already yours is kept: a Pokémon with another original trainer isn’t moved to your trainer."),
        ]),
        new("0.6.4", FechaVisible("7-oct-2026"), AppIdioma.T("Ventana «Acerca de Pokekeep» propia, con créditos, licencias y estas novedades.", "Its own “About Pokekeep” window, with credits, licenses and these release notes."),
        [
            AppIdioma.T("Nueva entrada «Acerca de Pokekeep» en el menú Opciones, con la versión real, la autoría, los créditos y licencias y las novedades por versión.", "New “About Pokekeep” entry in the Options menu, with the real version, the authorship, the credits and licenses and the release notes per version."),
            AppIdioma.T("Paquete reconstruido con el mismo proceso de verificación (auditoría de privacidad, manifiesto SHA-256 y humo con interfaz real sobre una copia de un guardado de prueba).", "Package rebuilt with the same verification process (privacy audit, SHA-256 manifest and smoke test with the real interface on a copy of a test save)."),
        ]),
        new("0.6.3", FechaVisible("7-oct-2026"), AppIdioma.T("Eventos, informe de cobertura y más casos de cría.", "Events, coverage report and more breeding cases."),
        [
            AppIdioma.T("«Preferir la versión de evento» en la edición masiva y en «Completar el juego»; región de los eventos en la Librería y «Exportar ficha…».", "“Prefer the event version” in bulk editing and in “Complete the game”; event region in the Library and “Export summary…”."),
            AppIdioma.T("Informe de cobertura publicable con la matriz por generación y juego, y aviso de eventos nuevos al actualizar el catálogo.", "Publishable coverage report with the matrix by generation and game, and a notice of new events when the catalog is updated."),
            AppIdioma.T("Dos casos de cría resueltos con el Core (Vivillon en Escarlata, crías de Brillante/Reluciente); caché de Espada diagnosticada.", "Two breeding cases solved with the Core (Vivillon in Scarlet, hatchlings of Brilliant/Shining); Sword cache diagnosed."),
        ]),
        new("0.6.2", FechaVisible("7-oct-2026"), AppIdioma.T("Idioma, recuerdos y caché de soluciones ampliada.", "Language, memories and expanded solution cache."),
        [
            AppIdioma.T("El idioma pedido (el tuyo, o español al generar) pesa más que cualquier otro rasgo al elegir el candidato.", "The requested language (yours, or Spanish when generating) weighs more than any other trait when choosing the candidate."),
            AppIdioma.T("Recuerdos de entrenador y de entrenador anterior coherentes con el recorrido del Pokémon, validados por el Core.", "Trainer and previous-trainer memories consistent with the Pokémon’s journey, validated by the Core."),
            AppIdioma.T("Caché de soluciones precalculada con las plantillas de Teratipo (6.375 entradas).", "Solution cache precalculated with the Tera Type templates (6,375 entries)."),
            AppIdioma.T("Corregido: la copia de PKHeX del paquete no llevaba el parche del registro de fallos.", "Fixed: the package’s copy of PKHeX did not carry the failure log patch."),
        ]),
        new("0.6.1", FechaVisible("5-oct-2026"), AppIdioma.T("Registro de fallos y traslado del proyecto.", "Failure log and project move."),
        [
            AppIdioma.T("Registro de fallos: cada error de Pokekeep o de PKHeX queda anotado en la carpeta «registros», sin datos del entrenador (Opciones > Abrir carpeta de registros).", "Failure log: every Pokekeep or PKHeX error is recorded in the “registros” folder, without trainer data (Options > Open logs folder)."),
            AppIdioma.T("Librería honesta con la cría en todos los juegos y pruebas de interfaz estables.", "Library honest about breeding in all games and stable interface tests."),
        ]),
        new("0.6.0", FechaVisible("5-oct-2026"), AppIdioma.T("Primer paquete portable.", "First portable package."),
        [
            AppIdioma.T("Paquete portable autocontenido: sin instalación, sin administrador, sin red y con un único PKHeX.Core comprobado al arrancar.", "Self-contained portable package: no installation, no administrator, no network, and a single PKHeX.Core checked at startup."),
            AppIdioma.T("Caché de soluciones precalculada: «Completar el juego» de Espada pasa de minutos a segundos.", "Precalculated solution cache: “Complete the game” for Sword goes from minutes to seconds."),
            AppIdioma.T("Colosseum como destino completo y registro de la Pokédex al añadir o completar.", "Colosseum as a full target and Pokédex registration when adding or completing."),
            AppIdioma.T("Historia coherente (cintas, fechas, entrenador anterior y memorias que el origen otorga), «Completar el juego» y «Edición masiva».", "Coherent history (ribbons, dates, previous trainer and memories that the origin grants), “Complete the game” and “Bulk edit”."),
            AppIdioma.T("Botones «Legalizar automáticamente» y «Optimizar stats» en el editor y en el menú de cada casilla, y pestaña «Librería».", "“Legalize automatically” and “Optimize stats” buttons in the editor and in each slot’s menu, and a “Library” tab."),
        ]),
    ];

    /// <summary>Versión REAL del ensamblado de la interfaz (<c>InformationalVersion</c> sin el sufijo «+commit»; si no la lleva, la de ensamblado).</summary>
    public static string VersionEnsamblado()
    {
        var asm = typeof(AcercaDeModelo).Assembly;
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(info)) return info.Split('+')[0];
        var v = asm.GetName().Version;
        return v is null ? "desconocida" : $"{v.Major}.{v.Minor}.{v.Build}";
    }

    /// <summary>Versión escrita en el manifiesto del paquete portable (<c>Pokekeep\manifiesto.json</c>), o null en desarrollo.</summary>
    public static string? VersionManifiesto(string? carpetaBase = null)
    {
        try
        {
            var p = Path.Combine(carpetaBase ?? Portable.PortableMode.Root, "Pokekeep", "manifiesto.json");
            if (!File.Exists(p)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllBytes(p));
            return doc.RootElement.TryGetProperty("version", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { return null; }
    }

    private static readonly string[] MesesEs = ["ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sep", "oct", "nov", "dic"];
    private static readonly string[] MesesEn = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    /// <summary>«8-oct-2026» tal cual en castellano; en inglés «Oct 8, 2026».</summary>
    private static string FechaVisible(string fecha)
    {
        if (!AppIdioma.Ingles) return fecha;
        var p = fecha.Split('-');
        var mes = p.Length == 3 ? Array.IndexOf(MesesEs, p[1]) : -1;
        return mes >= 0 && int.TryParse(p[0], out var dia) ? $"{MesesEn[mes]} {dia}, {p[2]}" : fecha;
    }

    /// <summary>«Versión 0.6.4 (portable)»; si el manifiesto del paquete no coincide con el ensamblado lo dice (no debería pasar).</summary>
    public static string VersionCompleta(string? carpetaBase = null)
    {
        var real = VersionEnsamblado();
        var man = VersionManifiesto(carpetaBase);
        return man is null ? AppIdioma.F("Versión {0} (desarrollo)", "Version {0} (development)", real) : man == real ? AppIdioma.F("Versión {0} (portable)", "Version {0} (portable)", real) : AppIdioma.F("Versión {0} (el manifiesto del paquete dice {1})", "Version {0} (the package manifest says {1})", real, man);
    }

    /// <summary>Todo el texto de la ventana en orden de lectura (para las pruebas).</summary>
    public static string TextoCompleto(string? carpetaBase = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Nombre).AppendLine(Lema).AppendLine(VersionCompleta(carpetaBase)).AppendLine();
        sb.AppendLine(AppIdioma.T("Autoría", "Authorship")).AppendLine(Autoria).AppendLine();
        sb.AppendLine(AppIdioma.T("Créditos y licencias", "Credits and licenses"));
        foreach (var c in Creditos) sb.Append("• ").Append(c.Componente).Append(" — ").Append(c.Licencia).Append(". ").AppendLine(c.Uso);
        sb.AppendLine().AppendLine(Licencia).AppendLine().AppendLine(AvisoEventos).AppendLine().AppendLine(AvisoMarcas).AppendLine().AppendLine(AvisoPrivacidad).AppendLine();
        sb.AppendLine(AppIdioma.T("Novedades", "Release notes"));
        foreach (var n in Novedades)
        {
            sb.Append(AppIdioma.T("Versión ", "Version ")).Append(n.Version).Append("  ").AppendLine(n.Fecha).AppendLine(n.Resumen);
            foreach (var l in n.Novedades) sb.Append("+ ").AppendLine(l);
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
