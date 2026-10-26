// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Mobile.Ui.Ajustes.Pkhex;

public static partial class RespaldoAjustesPkhex
{
    static partial void AnadirEtiquetasB10_1(Action<string, string> a)
    {
        // lang_es.txt deja «PropertyGrid.DefaultSaveVersion» sin traducir («DefaultSaveVersion»); PKHeX en castellano lo rotula con «SettingsEditor.L_Blank» («Versión partida vacía:»), que es como se llama aquí sin los dos puntos.
        a("DefaultSaveVersion", "Versión de la partida en blanco");
        // En el móvil son carpetas que se ELIGEN con el selector del sistema, no rutas que se escriben: «Ruta de…» de lang_es.txt describiría un campo de texto que no existe.
        a("LocalResources.MGDatabasePath", "Carpeta de la base de datos de regalos misteriosos");
        a("LocalResources.DatabasePath", "Carpeta de la base de datos PKM");
        a("LocalResources.BackupPath", "Carpeta de copias de seguridad");
        a("LocalResources.SoundPath", "Carpeta de sonidos");
        a("LocalResources.TemplatePath", "Carpeta de plantillas");
        a("LocalResources.TrainerPath", "Carpeta de entrenadores");
        a("Advanced.PathBlockKeyList", "Carpeta de nombres de bloques");
    }

    static partial void AnadirDescripcionesB10_1(Action<string, string> a)
    {
        // lang_es.txt dice «al iniciar» (PKHeX solo cambia el modo de color al arrancar); en el móvil el tema cambia al momento. «Utilice» es un usted que el resto de la app no usa.
        a("DarkMode", "Usa el modo oscuro en toda la aplicación. El cambio se ve al momento.");
        // lang_es.txt habla de «movimiento del ratón»; en táctil es el dedo, y el único arrastre que empieza en las casillas es el que cambia de caja.
        a("DragStartThreshold", "Distancia mínima que tiene que recorrer el dedo sobre las casillas para que el deslizamiento cambie de caja (0 = la de siempre).");
        // lang_es.txt: «los nombre», «no exite» y «Ruta de la carpeta» (aquí se elige la carpeta). Mismo sentido, sin erratas.
        a("PathBlockKeyList", "Carpeta con volcados de nombres de bloques (un archivo «<tipo de guardado>.txt» por juego). Si no hay archivo para el juego, solo salen los nombres que conoce el programa.");
        // lang_es.txt: «Filtra entradas irrelevantes de la interfaz» no dice dónde; en PKHeX solo lo lee el volcado de bloques de Gen. 8 y posteriores (SAV_BlockDump8).
        a("HideEvent8Contains", "En el editor de bloques (Gen. 8 y 9), los bloques cuyo nombre contenga alguna de estas palabras (separadas por comas) salen sin nombre, solo con su clave.");
        // lang_es.txt solo dice «Forzar el modo HaX al inicio de programa»: sin decir qué hace, nadie sabe si activarlo.
        a("ForceHaXOnLaunch", "Arranca en modo HaX: el editor abre en modo avanzado (listas completas, valores fuera de rango) y la Mochila no limita las cantidades. Puede dejar datos ilegales. Vale desde el siguiente arranque.");
        // lang_es.txt: «cuando corre por primera vez» es un calco del inglés («when run»).
        a("ShowChangelogOnUpdate", "Enseña las novedades la primera vez que se abre una versión nueva de la aplicación.");
        // Las carpetas: lang_es.txt describe rutas que se escriben y, la de regalos, sin decir para qué sirven las plantillas en la app.
        a("MGDatabasePath", "Carpeta con regalos misteriosos (wc*, pgf, pcd…) que el programa aún no conoce: se usan al comprobar la legalidad y al generar. Se elige con el selector del sistema; la app guarda una copia.");
        a("DatabasePath", "Carpeta de la base de datos PKM (archivos .pk*). Se elige con el selector del sistema; la app guarda una copia.");
        a("BackupPath", "Carpeta de copias de seguridad de las partidas. Se elige con el selector del sistema.");
        a("SoundPath", "Carpeta con tus propios gritos (nombres como en PKHeX: 25.wav, 6-1.wav…). Suenan antes que los que trae la app, que ya incluye los de todas las especies. Se elige con el selector del sistema; la app guarda una copia.");
        a("TemplatePath", "Carpeta de plantillas (*.pk) que inicializan el editor al cargar una partida. Se elige con el selector del sistema; la app guarda una copia.");
        a("TrainerPath", "Carpeta de entrenadores (*.pk) para generar encuentros con datos de entrenador conocidos. Se elige con el selector del sistema; la app guarda una copia.");
        // Ocultos en PKHeX (Browsable(false)): lang_es.txt no les da descripción porque su ventana no los enseña; aquí sí salen (PKHeX los cambia con el menú Idioma y el selector de la ventana de Ajustes).
        a("Language", "Idioma de los nombres del juego: especies, movimientos, objetos, habilidades y lugares. La interfaz de Pokekeep sigue en castellano de España.");
        a("DefaultSaveVersion", "Juego de la partida en blanco con la que se puede empezar sin abrir un guardado.");
    }
}
