// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Globalization;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;
using SesionM = Pokekeep.Mobile.Ui.Modelo.Sesion;

namespace Pokekeep.Mobile.Ui.Partida;

/// <summary>Como se pinta y se edita un campo de un editor de partida.</summary>
public enum TipoP
{
    /// <summary>Texto libre (nombre del entrenador, frases).</summary>
    Texto,
    /// <summary>Numero entero con limites.</summary>
    Entero,
    /// <summary>Numero con decimales (coordenadas).</summary>
    Decimal,
    /// <summary>Se elige de una lista con buscador.</summary>
    Opcion,
    /// <summary>Interruptor.</summary>
    Marca,
    /// <summary>Fecha y hora dd/mm/aaaa hh:mm:ss.</summary>
    FechaHora,
    /// <summary>Solo lectura.</summary>
    Info,
    /// <summary>
    /// Una ACCION de un solo sentido que el formulario de PKHeX ejecuta con un boton («Reaparecer», «Dar todos», «Activar evento»): en el movil queda PENDIENTE en el borrador (marcada «Se hara») y se ejecuta, dentro de la transaccion
    /// y en su orden, al aplicar; se puede desmarcar antes de aplicar y Deshacer la revierte entera. Su valor es un <c>bool</c>.
    /// </summary>
    Accion,
    /// <summary>
    /// Un archivo que se importa (o se exporta) por el selector del sistema: «Importar Pokémon», «Importar skin C-Gear». Su valor en el borrador es el contenido elegido (<c>byte[]</c>) y se escribe al aplicar; null = nada elegido.
    /// </summary>
    Archivo,
}

/// <summary>Una opcion de una lista de un campo (valor del Core y texto en castellano).</summary>
public sealed record OpcionP(int Id, string Texto);

/// <summary>
/// Lo que un campo ve del editor: la partida, los textos del Core y el BORRADOR de los demas campos (un campo puede depender de otro: el pais fija la lista de regiones).
/// El borrador nunca se escribe en la partida hasta «Aplicar».
/// </summary>
public sealed class ContextoP
{
    internal readonly Dictionary<string, object?> Borrador = new();
    /// <summary>Nombre del archivo que el usuario eligio para cada campo <see cref="TipoP.Archivo"/> (por id del campo): el contenido va en el borrador, pero a veces el nombre cuenta (el musical toma su nombre del archivo, como <c>SAV_DLC5.GetImportedMusicalName</c>). Lo rellena la pantalla al importar; vacio si no se conoce.</summary>
    public Dictionary<string, string> NombresDeArchivo { get; } = new();

    public ContextoP(SesionM sesion) { Sesion = sesion; }
    public SesionM Sesion { get; }
    public SaveFile Sav => Sesion.Guardado;
    public GameStrings Textos => Sesion.Textos;
    private GameDataSource? _datos;
    /// <summary>Listas del Core en el idioma actual. Renovar la presentación no toca el borrador.</summary>
    public GameDataSource Datos
    {
        get
        {
            var textos = Textos;
            if (!ReferenceEquals(_datos?.Strings, textos)) _datos = new GameDataSource(textos);
            return _datos;
        }
    }

    public bool Tiene(string id) => Borrador.ContainsKey(id);
    public object? Valor(string id) => Borrador.TryGetValue(id, out var v) ? v : null;
    public long Entero(string id) => Borrador.TryGetValue(id, out var v) && v is not null ? Convert.ToInt64(v, CultureInfo.InvariantCulture) : 0;
    public double Decimal(string id) => Borrador.TryGetValue(id, out var v) && v is not null ? Convert.ToDouble(v, CultureInfo.InvariantCulture) : 0;
    public string Texto(string id) => Borrador.TryGetValue(id, out var v) ? v as string ?? string.Empty : string.Empty;
    public bool Marca(string id) => Borrador.TryGetValue(id, out var v) && v is true;
    public DateTime Fecha(string id) => Borrador.TryGetValue(id, out var v) && v is DateTime d ? d : DateTime.MinValue;
}

/// <summary>
/// Un campo de un editor de partida. Cada uno nace de una fila del censo de paridad (<see cref="Id"/>) y de una linea del formulario de PKHeX: sabe leer su valor de la partida y como escribirlo
/// con la API del Core (la misma llamada del formulario). <see cref="Escribir"/> solo se invoca si el valor cambio: lo que el usuario no toca no se reescribe.
/// </summary>
public sealed class CampoP
{
    public required string Id { get; init; }
    public required string Etiqueta { get; init; }
    public required TipoP Tipo { get; init; }
    public string? Ayuda { get; init; }
    public Func<ContextoP, string>? EtiquetaFn { get; init; }
    public string TextoEtiqueta(ContextoP c) => EtiquetaFn?.Invoke(c) ?? Etiqueta;
    public Func<ContextoP, bool>? Visible { get; init; }
    public bool EsVisible(ContextoP c) => Visible?.Invoke(c) ?? true;

    /// <summary>Valor actual en la partida: string, long, double, bool o DateTime segun el tipo (el valor de una opcion es su <see cref="OpcionP.Id"/> como long).</summary>
    public required Func<ContextoP, object?> Leer { get; init; }
    /// <summary>Presentación de un ID informativo estable; traducirlo no vuelve a leer la partida.</summary>
    public Func<ContextoP, long, string>? PresentarInfo { get; init; }
    /// <summary>Escribe el valor ya validado en la partida. Null = solo lectura.</summary>
    public Action<ContextoP, object?>? Escribir { get; init; }
    /// <summary>Si dice un texto, el campo es de solo lectura y ese es el motivo.</summary>
    public Func<ContextoP, string?>? Motivo { get; init; }
    /// <summary>Texto secundario derivado de lo que se escribe («TSV: 1234»).</summary>
    public Func<ContextoP, string?>? Detalle { get; init; }

    public long Min { get; init; }
    public long Max { get; init; } = long.MaxValue;
    public Func<ContextoP, long>? MaxFn { get; init; }
    public long MaximoEn(ContextoP c) => MaxFn?.Invoke(c) ?? Max;
    public int MaxLongitud { get; init; }
    public Func<ContextoP, int>? MaxLongitudFn { get; init; }
    public int LongitudEn(ContextoP c) => MaxLongitudFn?.Invoke(c) ?? MaxLongitud;
    public Func<ContextoP, IReadOnlyList<OpcionP>>? Opciones { get; init; }
    public string? Sufijo { get; init; }
    /// <summary>Decimales que se muestran de un campo Decimal.</summary>
    public int Decimales { get; init; } = 2;
    /// <summary>Campos cuyo borrador vuelve a su valor por defecto cuando este cambia (el pais reinicia la region).</summary>
    public IReadOnlyList<string> Reinicia { get; init; } = [];
    /// <summary>Valores que este campo fija en otros del borrador al cambiar (el titulo del Bastion fija sus puntos): id del otro campo y su valor nuevo.</summary>
    public Func<ContextoP, IReadOnlyDictionary<string, object?>>? Deriva { get; init; }
    /// <summary>Orden de escritura (menor primero) cuando el orden del formulario difiere del de la pantalla.</summary>
    public int Prioridad { get; init; }
    /// <summary>Solo se escribe si el campo cambio Y la condicion se cumple (los datos del mapa solo se escriben si el usuario los toco, como <c>MapUpdated</c>).</summary>
    public bool SoloSiCambia { get; init; } = true;
    /// <summary>Texto del selector (titulo de la hoja) para un campo de tipo Opcion.</summary>
    public string? TituloSelector { get; init; }
    /// <summary>La fila y su selector muestran sprites de especie; independiente del idioma del rotulo.</summary>
    public bool EsEspecie { get; init; }
    /// <summary>Campos que el Core escribe como UN solo valor (horas, minutos y segundos del tiempo de juego de XD): si cambia uno se escriben todos los del grupo, en su orden, con sus valores del borrador, como el formulario.</summary>
    public string? GrupoEscritura { get; init; }
    /// <summary>La fecha y hora se guardan con resolucion de minuto (el borrador descarta los segundos).</summary>
    public bool SinSegundos { get; init; }
    /// <summary>La resolucion depende del dato de la partida (algunas marcas de tiempo del Core guardan segundos y otras solo minutos).</summary>
    public Func<ContextoP, bool>? SinSegundosFn { get; init; }
    public bool SinSegundosEn(ContextoP c) => SinSegundosFn?.Invoke(c) ?? SinSegundos;
    /// <summary>Solo se guarda el dia (inicio de la aventura de Espada/Escudo): sin hora.</summary>
    public bool SoloFecha { get; init; }
    /// <summary>Rango de fechas que admite el campo (el <c>DateTimePicker</c> del formulario original lo limita con <c>MinDate</c>/<c>MaxDate</c>; fuera de el la fecha se rechaza con su motivo). Null = sin limite.</summary>
    public DateTime? FechaMin { get; init; }
    public DateTime? FechaMax { get; init; }
    /// <summary>Es una lista cuya eleccion EJECUTA algo (cargar toda la ropa): al aplicar, el valor vuelve a «sin cambios» y no se puede releer de la partida.</summary>
    public bool SoloAccion { get; init; }
    /// <summary>El Core escribe este dato en una copia distinta de la que lee (el pais de Battle Revolution): tras aplicar puede seguir leyendose el valor anterior.</summary>
    public bool LecturaAsimetrica { get; init; }
    /// <summary>Normaliza el texto escrito antes de guardarlo en el borrador (el ID de jugador de Battle Revolution se vuelve a 16 cifras hexadecimales).</summary>
    public Func<string, string>? Normaliza { get; init; }
    /// <summary>Texto de varias lineas (la presentacion de Battle Revolution).</summary>
    public bool Multilinea { get; init; }
    /// <summary>
    /// El valor de texto (una línea por fila, un carácter por casilla) se edita como una REJILLA TÁCTIL de casillas de un tono (el «Artista puntos» del Poké-reloj; el C-Gear de Gen. 5 NO la usa: en WinForms solo importa y exporta, ADR 0067 decisión 5). El valor sigue siendo el texto:
    /// borrador, Deshacer y pruebas no cambian.
    /// </summary>
    public RejillaP? Rejilla { get; init; }
    /// <summary>Regla extra de un texto (longitud especial): devuelve el motivo del rechazo o null.</summary>
    public Func<ContextoP, string, string?>? Validar { get; init; }
    /// <summary>Regla del valor fuera de rango que depende de OTROS campos (TID y SID de 6+4 cifras): se comprueba en el borrador antes de guardar y devuelve el motivo del rechazo.</summary>
    public Func<ContextoP, long, string?>? ValidarValor { get; init; }
    /// <summary>Otros campos que cambian al escribir este (al aplicar se releen de la partida): vaciar una carta deja sus demas campos vacios. Las pruebas no exigen que se queden como estaban.</summary>
    public Func<ContextoP, IEnumerable<string>>? AfectaA { get; init; }
    /// <summary>Muestra el valor en hexadecimal y lo edita asi.</summary>
    public bool Hex { get; init; }
    /// <summary>El campo NO se escribe nunca y no cuenta como cambio: solo ayuda a editar otro (el interruptor «Mote» del Salon de la Fama decide si el apodo sigue a la especie).</summary>
    public bool SinEscritura { get; init; }
    /// <summary>
    /// Una <see cref="TipoP.Accion"/> que NO queda pendiente: se ejecuta al pulsarla sobre la partida real, como UNA entrada del historial (reordena, borra o cambia la estructura de una lista: «Quitar equipo», «Añadir equipo actual»).
    /// Solo se puede pulsar sin cambios pendientes en el editor (despues los campos se vuelven a leer).
    /// </summary>
    public bool Inmediata { get; init; }
    /// <summary>Un campo <see cref="TipoP.Archivo"/> tambien exporta: nombre sugerido y contenido (null = nada que exportar). Exportar NO cambia la partida.</summary>
    public Func<ContextoP, (string Nombre, byte[] Datos)?>? Exporta { get; init; }
    /// <summary>Un campo <see cref="TipoP.Archivo"/> acepta importar: valida el contenido y devuelve el motivo del rechazo (o null si sirve).</summary>
    public Func<ContextoP, byte[], string?>? ValidarArchivo { get; init; }
    /// <summary>El campo de archivo solo EXPORTA (no tiene importar): «Exportar todos los bloques», «Exportar todo en un archivo».</summary>
    public bool SoloExporta { get; init; }
    /// <summary>Extension sugerida para importar/exportar (sin punto), por ejemplo «cgb».</summary>
    public string? Extension { get; init; }
    /// <summary>Texto del boton de una <see cref="TipoP.Accion"/> o de importar de un <see cref="TipoP.Archivo"/> (por defecto «Hacer» / «Importar…»).</summary>
    public string? TextoBoton { get; init; }
    /// <summary>La accion pide confirmacion antes de quedar pendiente (borra o sobrescribe mucho): el texto de la advertencia.</summary>
    public string? Confirmacion { get; init; }
    /// <summary>
    /// Vista previa de una imagen que el campo maneja (la skin del C-Gear): pixeles BGRA de lo que hay ahora en el borrador o, si no hay nada elegido, en la partida; null = sin imagen. La pantalla la enseña bajo los botones y la
    /// vuelve a pedir en cada cambio. Nunca escribe nada.
    /// </summary>
    public Func<ContextoP, ImagenJugador?>? Vista { get; init; }
}

/// <summary>Forma de una rejilla táctil de tonos: casillas de ancho × alto y un color (0xRRGGBB) por tono. <c>RejillaTonos.Grises</c> hace una paleta de grises.</summary>
public sealed record RejillaP(int Ancho, int Alto, IReadOnlyList<uint> Paleta);

/// <summary>Una fila de una tabla de numeros o de marcas (record de entrenador, constante de evento, bandera): nombre, valor y como se escribe.</summary>
public sealed class FilaTablaP
{
    public required int Id { get; init; }
    public required string Nombre { get; init; }
    public string? Detalle { get; init; }
    public bool EsMarca { get; init; }
    /// <summary>La fila se ve pero no se cambia (siempre marcada por el juego); el motivo va en <see cref="Detalle"/>.</summary>
    public bool SoloLectura { get; init; }
    /// <summary>La fila es un PARAMETRO de una accion de la tabla (la cantidad que pone «Marcar todos»): se ve y se edita, pero no se escribe en la partida y por eso no cuenta como cambio ni sale en «Solo cambiadas».</summary>
    public bool SinEscritura { get; init; }
    public long Min { get; init; }
    public long Max { get; init; } = long.MaxValue;
    /// <summary>Valor en la partida: long o bool.</summary>
    public required Func<ContextoP, object> Leer { get; init; }
    public required Action<ContextoP, object> Escribir { get; init; }
    public string? Grupo { get; init; }
    public string Clave(string tabla) => $"{tabla}:{Id}";
}

/// <summary>Una tabla larga con buscador (records, constantes, banderas, estadisticas).</summary>
public sealed class TablaDefP
{
    public required string Id { get; init; }
    public required Func<ContextoP, IReadOnlyList<FilaTablaP>> Filas { get; init; }
    public string Marcador { get; init; } = IdiomaInterfaz.T("CamposP.Buscar");
    public string? Ayuda { get; init; }
    /// <summary>Los numeros de fila son internos (especie × 16 + dato...): no se ensenan.</summary>
    public bool SinNumero { get; init; }
    /// <summary>Tras cambiar una fila, otras filas de esta misma tabla o de otra deben cambiar con ella (marcar «capturado» marca tambien «visto»): devuelve (clave de fila, valor) a poner en el borrador.</summary>
    public Func<ContextoP, FilaTablaP, object, IReadOnlyList<(string Clave, object Valor)>>? Coherencia { get; init; }
    /// <summary>Acciones masivas sobre las filas («Obtener todos», «Quitar todos», «Activar todos»): cada una cambia el BORRADOR de varias filas a la vez (se aplica y se deshace como cualquier cambio).</summary>
    public IReadOnlyList<AccionTablaP> Acciones { get; init; } = [];
}

/// <summary>Una accion masiva de una tabla: devuelve (clave de fila, valor nuevo) de las filas que cambia en el borrador. <see cref="Confirmacion"/> si cambia muchas cosas de golpe.</summary>
public sealed record AccionTablaP(string Id, string Texto, Func<ContextoP, IReadOnlyList<FilaTablaP>, IReadOnlyList<(FilaTablaP Fila, object Valor)>> Cambios, string? Ayuda = null);

/// <summary>
/// Una lista de elementos con el mismo editor (entradas del Salon de la Fama, visitantes de la Avenida Union, bases secretas, donuts, guaridas): se elige el elemento (anterior, siguiente o una lista con buscador) y se editan
/// SUS campos. Los campos de un elemento se crean y se leen cuando se abre por primera vez (no antes): una coleccion de cientos de elementos no cuesta nada hasta que se toca. Los ids de campo llevan el elemento
/// (<see cref="IdCampo"/>), asi que el borrador conserva lo editado de cada uno al cambiar de elemento.
/// </summary>
public sealed class ColeccionDefP
{
    public required string Id { get; init; }
    /// <summary>Nombre del elemento en singular para el selector («Equipo», «Visitante»).</summary>
    public required string Elemento { get; init; }
    public required Func<ContextoP, int> Cantidad { get; init; }
    /// <summary>Titulo del elemento i (base 0): «Equipo 03».</summary>
    public required Func<ContextoP, int, string> Titulo { get; init; }
    /// <summary>Segunda linea del elemento en la lista (resumen del estado en la PARTIDA, no del borrador): «Pikachu · Nv. 50» o «Vacio».</summary>
    public Func<ContextoP, int, string?>? Resumen { get; init; }
    /// <summary>Secciones de campos del elemento i. Los ids se forman con <see cref="IdCampo"/>.</summary>
    public required Func<ContextoP, int, IReadOnlyList<SeccionP>> Secciones { get; init; }
    /// <summary>Se enseña el numero del elemento en el selector («Equipo 3 de 50»). Desactivalo si el titulo ya lo lleva.</summary>
    public bool MostrarPosicion { get; init; } = true;
    public string IdCampo(int i, string campo) => $"{Id}[{i}].{campo}";
}

public sealed record SeccionP(string Titulo, IReadOnlyList<CampoP> Campos, Func<ContextoP, bool>? Visible = null)
{
    public bool EsVisible(ContextoP c) => Visible?.Invoke(c) ?? true;
}

/// <summary>Una pestana del editor: secciones de campos, una tabla o una coleccion (lista de elementos con un editor por elemento).</summary>
public sealed record PestanaP(string Id, string Titulo, IReadOnlyList<SeccionP> Secciones, Func<ContextoP, bool>? Visible = null, TablaDefP? Tabla = null, ColeccionDefP? Coleccion = null)
{
    public bool EsVisible(ContextoP c) => Visible?.Invoke(c) ?? true;
}

/// <summary>Un editor de partida completo: titulo, pestanas y las filas del censo que cubre.</summary>
public sealed record EditorDefP(string Id, string Titulo, IReadOnlyList<PestanaP> Pestanas, IReadOnlyList<string> Censo, string Descripcion = "")
{
    public IEnumerable<CampoP> Campos => Pestanas.SelectMany(p => p.Secciones).SelectMany(s => s.Campos);
    /// <summary>Se llama dentro de la transaccion ANTES de escribir los campos y devuelve lo que hay que hacer DESPUES (BDSP rehace el nombre del entrenador en el bloque de records ampliado y evita un ID todo a cero).</summary>
    public Func<ContextoP, Action<ContextoP>?>? PrepararAplicar { get; init; }
    /// <summary>Se llama DESPUES de aplicar con exito y puede dar un aviso para el usuario (el zip de bloques importa los que cuadran y anota los que no); null si no hay nada que avisar.</summary>
    public Func<ContextoP, string?>? AvisoTrasAplicar { get; init; }
    /// <summary>Solo para el gate visual: las pestanas que se dibujan (null = todas). Las pestanas que comparten plantilla (las 25 categorias de Moda de Z-A son tres clases de columnas) se dibujan una vez por clase; que de verdad sean iguales lo comprueba una prueba.</summary>
    public IReadOnlyCollection<string>? PestanasDeGate { get; init; }
}
