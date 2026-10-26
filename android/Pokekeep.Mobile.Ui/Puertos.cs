// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Pokekeep.Mobile.Ui.Localizacion;
namespace Pokekeep.Mobile.Ui;

/// <summary>
/// Una entrada de la Librería (catálogo) lista para pintar: lo mismo que una fila de la lista del escritorio. Los textos (origen, estado, procedencia, región) los pone el núcleo compartido
/// (<c>LibraryNames</c>); la interfaz no inventa ninguno. Sin ningún título de eventos de terceros que no sea el que el núcleo ya muestra.
/// </summary>
public sealed record EntradaLibreria(
    string Clave, int Especie, int Forma, string Nombre, string? NombreForma, string Juego, string Origen,
    bool Disponible, string? MotivoNoDisponible, bool Anadible, bool RequiereHome, int Genero = 0, bool Brillante = false)
{
    /// <summary>Clase de origen del catálogo (<c>Wild</c>, <c>Event</c>, <c>Egg</c>...).</summary>
    public string OrigenCodigo { get; init; } = string.Empty;
    /// <summary>Texto de la columna «Estado»: el motivo si no existe o no se puede generar, «Disponible», «Pendiente de verificar»...</summary>
    public string Estado { get; init; } = string.Empty;
    public string Procedencia { get; init; } = string.Empty;
    public string Region { get; init; } = string.Empty;
    /// <summary>Por qué lo que solo existe pasando por HOME no se ofrece (null si se ofrece o no es de HOME).</summary>
    public string? BloqueoHome { get; init; }
    /// <summary>Distintivo Gigamax de las tarjetas que lo reparten.</summary>
    public bool Gigamax { get; init; }
    /// <summary>Id del juego de la entrada (para sprites con el contexto del juego).</summary>
    public int JuegoId { get; init; }
    /// <summary>Parámetro especial de forma cuando el catálogo lo identifica; cero para las recetas sin ese dato.</summary>
    public uint ArgumentoForma { get; init; }
    public int NumeroPokedex { get; init; }

    /// <summary>Atenuada: no existe en la partida, no se puede generar todavía o está bloqueada por HOME. Va con su motivo.</summary>
    public bool Atenuada => !Disponible || !Anadible || BloqueoHome is not null;
    /// <summary>«Añadir a la caja» posible sobre el catálogo (sin mirar la partida): disponible, añadible y sin HOME bloqueado.</summary>
    public bool Ofrecible => Disponible && Anadible && BloqueoHome is null;
}

/// <summary>Filtros de la Librería (se combinan con AND), los mismos que el escritorio: texto, origen, generación, especie, forma, idioma y región de evento, solo disponibles, juego y destino GameCube.</summary>
public sealed record FiltroLibreria
{
    public string? Texto { get; init; }
    /// <summary>Juego de DESTINO (la partida abierta); 0 = sin destino.</summary>
    public int Juego { get; init; }
    /// <summary><c>GameCubeIds.Colosseum</c> o <c>GameCubeIds.Xd</c> cuando el destino es uno de los dos; 0 en cualquier otro juego.</summary>
    public int DestinoGc { get; init; }
    public string? Origen { get; init; }
    public int? Generacion { get; init; }
    public int? Especie { get; init; }
    public int? Forma { get; init; }
    /// <summary>Nombre del idioma de evento en inglés (<c>Spanish</c>...), como lo guarda el catálogo.</summary>
    public string? Idioma { get; init; }
    /// <summary>Código de región (<c>US</c>, <c>EU</c>, <c>JP</c>, <c>KO</c>, <c>TW</c>, <c>INT</c>).</summary>
    public string? Region { get; init; }
    public bool SoloDisponibles { get; init; }
    /// <summary>Ajuste «Sin HOME (con riesgo)» activo: lo que solo existe pasando por HOME se ofrece.</summary>
    public bool PermitirHome { get; init; }
    public int Limite { get; init; } = 50;
    public int Desplazamiento { get; init; }
}

public sealed record LineaFicha(string Texto, bool Negrita = false);

/// <summary>La ficha de una entrada: nombre, disponibilidad, origen, entrenador original, evento, niveles, fuente, rasgos fijos y variables.</summary>
public sealed record FichaLibreria(string Titulo, IReadOnlyList<LineaFicha> Lineas)
{
    public string TextoPlano => string.Join("\n", Lineas.Select(l => l.Texto));
}

public enum FormatoFicha { Json, Texto }

/// <summary>Una ficha exportada: nombre sugerido (sin extensión), extensión, contenido y tipo MIME. Solo datos del catálogo y versiones del motor; nunca rutas ni datos del usuario.</summary>
public sealed record FicheroFicha(string NombreSugerido, string Extension, string Contenido, string TipoMime)
{
    public string NombreCompleto => NombreSugerido + Extension;
}

/// <summary>Puerto del catálogo de la Librería: lo implementa <c>Pokekeep.Mobile.Operaciones</c> (<c>LibraryData</c> del núcleo, solo lectura, sobre el catálogo de FilesDir) y las pruebas (sobre la BD del PC).</summary>
public interface ICatalogoLibreria
{
    bool Disponible { get; }

    /// <summary>Id de juego de la Librería correspondiente a la partida (como <c>LibraryData.GameIdOf</c>).</summary>
    int JuegoDe(PKHeX.Core.SaveFile sav);

    /// <summary>Colosseum/XD comparten juego en la Librería pero no encuentros: <c>GameCubeIds.Colosseum</c> o <c>GameCubeIds.Xd</c>; 0 en el resto.</summary>
    int DestinoGc(PKHeX.Core.SaveFile sav) => 0;

    IReadOnlyList<EntradaLibreria> Consultar(FiltroLibreria filtro);
    long Contar(FiltroLibreria filtro);

    /// <summary>Compatibilidad con la consulta simple de F2-F5.</summary>
    IReadOnlyList<EntradaLibreria> Buscar(string? texto, int juegoDestino, bool soloDisponibles, int limite, int desplazamiento)
        => Consultar(new FiltroLibreria { Texto = texto, Juego = juegoDestino, SoloDisponibles = soloDisponibles, Limite = limite, Desplazamiento = desplazamiento });

    long Contar(string? texto, int juegoDestino, bool soloDisponibles)
        => Contar(new FiltroLibreria { Texto = texto, Juego = juegoDestino, SoloDisponibles = soloDisponibles, Limite = 1 });

    /// <summary>Clases de origen en el orden del árbol del escritorio (código del catálogo y etiqueta en castellano).</summary>
    IReadOnlyList<(string Codigo, string Etiqueta)> Origenes() => [];

    /// <summary>Idiomas de evento presentes en el catálogo (nombre del enum en inglés y etiqueta en castellano).</summary>
    IReadOnlyList<(string Codigo, string Etiqueta)> IdiomasEvento() => [];

    /// <summary>Regiones del filtro (código y etiqueta). Solo casan los eventos cuya región aportan el Core o la documentación de la tarjeta.</summary>
    IReadOnlyList<(string Codigo, string Etiqueta)> Regiones() => [];

    /// <summary>Rangos de la Pokédex del árbol por especie (Kanto, Johto...).</summary>
    IReadOnlyList<(int Primera, int Ultima, string Etiqueta)> RangosPokedex() => [];

    /// <summary>Nombre de la especie en castellano (el del Core).</summary>
    string NombreEspecie(int especie) => IdiomaInterfaz.F("Puertos.PokemonN", especie);

    /// <summary>Formas distintas de una especie en un juego (para el filtro «Forma»).</summary>
    IReadOnlyList<(int Forma, string Nombre)> Formas(int juego, int especie) => [];

    /// <summary>La entrada con esa clave en ese juego (para soltarla en una casilla o abrir su ficha).</summary>
    EntradaLibreria? Entrada(int juego, int destinoGc, string clave, bool permitirHome) => null;

    FichaLibreria? Ficha(EntradaLibreria entrada, bool permitirHome) => null;

    FicheroFicha? ExportarFicha(EntradaLibreria entrada, FormatoFicha formato, VersionesMotor? motor) => null;
}

/// <summary>Valores por defecto de los puertos cuando no hay catálogo (la Librería dice que no está disponible, nunca inventa filas).</summary>
public sealed class CatalogoNulo : ICatalogoLibreria
{
    public bool Disponible => false;
    public int JuegoDe(PKHeX.Core.SaveFile sav) => 0;
    public IReadOnlyList<EntradaLibreria> Consultar(FiltroLibreria filtro) => [];
    public long Contar(FiltroLibreria filtro) => 0;
}

/// <summary>Portapapeles del sistema (exportar el set de Showdown, copiar un informe): lo implementa la vista raíz con el <c>IClipboard</c> de Avalonia; las pruebas, una cadena en memoria.</summary>
public interface IPortapapeles
{
    Task<bool> CopiarAsync(string texto);
    Task<string?> LeerAsync();
    /// <summary>Copia una imagen PNG al portapapeles (el QR de un Pokémon, <c>PB_QR_Click</c> de PKHeX). False si este sistema no admite imágenes en el portapapeles: entonces se ofrece «Compartir».</summary>
    Task<bool> CopiarImagenAsync(byte[] png) => Task.FromResult(false);
}

/// <summary>
/// La hoja de compartir del sistema (Android: <c>ACTION_SEND</c>), el equivalente móvil de arrastrar el sprite del editor a otra carpeta (<c>Dragout_MouseDown</c> de PKHeX). SOLO la dispara una acción explícita del
/// usuario (regla 13) y no sale nada por su cuenta (regla 15): el sistema le enseña las apps y es él quien elige. Devuelve null si se ha abierto la hoja y, si no, el motivo en castellano.
/// </summary>
public interface ICompartirSistema
{
    /// <summary>Comparte un texto (el set de Showdown).</summary>
    string? CompartirTexto(string titulo, string texto);
    /// <summary>Comparte un archivo con ese nombre y contenido (el <c>.pk*</c> descifrado o el <c>.ek*</c> cifrado, o la imagen del QR). El archivo temporal es privado de la app y se borra solo.</summary>
    string? CompartirArchivo(string titulo, string nombre, byte[] datos, string tipoMime);
}

/// <summary>Portapapeles en memoria (pruebas y capturas sin ventana).</summary>
public sealed class PortapapelesMemoria : IPortapapeles
{
    public string? Texto { get; private set; }
    /// <summary>La última imagen copiada (PNG) o null; <see cref="AdmiteImagenes"/> false imita un sistema que no la admite.</summary>
    public byte[]? Imagen { get; private set; }
    public bool AdmiteImagenes { get; set; } = true;
    public Task<bool> CopiarAsync(string texto) { Texto = texto; return Task.FromResult(true); }
    public Task<string?> LeerAsync() => Task.FromResult(Texto);
    public Task<bool> CopiarImagenAsync(byte[] png) { if (!AdmiteImagenes) return Task.FromResult(false); Imagen = png; return Task.FromResult(true); }
}

/// <summary>Brillo de la pantalla al máximo mientras se enseña un código QR que otro aparato tiene que leer (la cámara de la 3DS escanea el QR de Sol, Luna, Ultrasol y Ultraluna desde cualquier pantalla). Android: el brillo de la ventana, solo mientras dura la hoja.</summary>
public interface IBrilloPantalla
{
    /// <summary>Con <c>true</c> pone el brillo al máximo; con <c>false</c> devuelve el que eligió el usuario.</summary>
    void Maximo(bool activo);
}

public interface IReproductorGritos
{
    Task<bool> ReproducirAsync(string ruta, CancellationToken cancelar);
    void Parar();
}

/// <summary>Brillo en memoria (pruebas): anota cada cambio.</summary>
public sealed class BrilloMemoria : IBrilloPantalla
{
    public List<bool> Cambios { get; } = [];
    public bool EstaAlMaximo { get; private set; }
    public void Maximo(bool activo) { EstaAlMaximo = activo; Cambios.Add(activo); }
}

/// <summary>Compartir en memoria (pruebas y capturas sin sistema): guarda lo último que se habría mandado a la hoja de compartir.</summary>
public sealed class CompartirMemoria : ICompartirSistema
{
    public List<(string Titulo, string Nombre, byte[]? Datos, string? Texto, string TipoMime)> Envios { get; } = [];
    public string? Motivo { get; set; }
    public string? CompartirTexto(string titulo, string texto) { if (Motivo is not null) return Motivo; Envios.Add((titulo, string.Empty, null, texto, "text/plain")); return null; }
    public string? CompartirArchivo(string titulo, string nombre, byte[] datos, string tipoMime) { if (Motivo is not null) return Motivo; Envios.Add((titulo, nombre, datos, null, tipoMime)); return null; }
}
