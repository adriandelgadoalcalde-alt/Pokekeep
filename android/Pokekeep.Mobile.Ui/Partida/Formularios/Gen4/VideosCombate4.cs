// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen4;

/// <summary>
/// PK-SAV-213: <c>SAV_DLC4</c>, los cuatro vídeos de batalla de Platino y HeartGold/SoulSilver (nuevo en PKHeX 26.10.18; el botón «DLC I/O» de <c>SAVEditor</c> abre este formulario en Gen. 4 y <c>SAV_DLC5</c> en Gen. 5).
/// El original es una lista de cuatro ranuras con el nombre de los jugadores («Ash vs Gary»), una vista previa de los equipos de cada jugador (especie, mote y nivel), «Importar», «Exportar» y «Forzar exportación descifrada».
/// Aquí cada ranura es un elemento de una colección. Lo que cambia respecto a una ventana de escritorio: importar deja el archivo en el borrador y se escribe al pulsar «Aplicar» (como todos los campos de archivo), y la vista previa
/// enseña el archivo elegido mientras está pendiente, no solo lo que ya hay en la partida. Exportar saca siempre lo que está en la PARTIDA.
/// Reglas calcadas de <c>B_Import_Click</c>: el archivo ha de medir exactamente <see cref="BattleVideo4.SIZE_USED"/> bytes con el pie válido (<see cref="BattleVideo4.IsValid"/>) y, o la suma de comprobación del pie cuadra
/// (cifrado) o la semilla cuadra con el CRC del bloque descifrado; se conservan el número mágico, la revisión, el tamaño y el ID de bloque de la ranura de destino, se recalculan las sumas y se cifra. Divergencia: el original da el mismo
/// aviso («Se esperaban exactamente N bytes») también cuando el tamaño es correcto y falla el pie; aquí se distingue (los dos casos se rechazan igual).
/// </summary>
internal static class VideosCombate4
{
    public const string Form = "SAV_DLC4";
    public static readonly string[] Censo = ["PK-SAV-213"];
    private const int Cantidad = 4;

    private static string L(string control) => EtiquetasLang.Requerir(Form, control);
    private static SAV4 S(ContextoP c) => (SAV4)c.Sav;

    /// <summary>Predicado barato de <see cref="Crear"/>: el botón de DLC del SAVEditor existe en Platino, HeartGold y SoulSilver (no en Diamante y Perla, que no tienen vídeos).</summary>
    public static bool Aplica(SaveFile sav) => sav is SAV4Pt or SAV4HGSS;

    /// <summary>El vídeo de la ranura <paramref name="i"/> tal como está en la partida (una vista de sus bytes), o null si la ranura no existe en este guardado.</summary>
    private static BattleVideo4? Ranura(ContextoP c, int i) => S(c).GetBattleVideo(i);

    // ---------------- lo que se enseña de un vídeo ----------------
    private sealed record Jugador(string Nombre, IReadOnlyList<(ushort Especie, string Mote, int Nivel)> Equipo);
    private sealed record Resumen(bool Cifrado, bool SumaValida, ushort Bloque, IReadOnlyList<Jugador> Jugadores, int Pokemon, int ConNombre);

    /// <summary>Lee un vídeo SIN tocarlo (trabaja sobre una copia: descifrar es una operación sobre los propios bytes).</summary>
    private static Resumen? Leer(ReadOnlySpan<byte> bytes, bool? descifrado = null)
    {
        if (bytes.Length != BattleVideo4.SIZE_USED) return null;
        var copia = bytes.ToArray();
        var estado = descifrado is { } d ? (d ? BattleVideo4DecryptionState.Decrypted : BattleVideo4DecryptionState.Encrypted) : BattleVideo4.DetectEncryption(copia);
        var video = new BattleVideo4(copia) { IsDecrypted = estado == BattleVideo4DecryptionState.Decrypted };
        var equipos = video.GetTeams();
        var nombres = video.GetTrainerNames();
        var jugadores = new List<Jugador>(BattleVideo4.TrainerCount);
        for (var t = 0; t < BattleVideo4.TrainerCount; t++)
        {
            var nombre = nombres[t];
            if (string.IsNullOrWhiteSpace(nombre)) nombre = equipos[t].FirstOrDefault()?.OriginalTrainerName ?? string.Empty;
            jugadores.Add(new Jugador(nombre, [.. equipos[t].Select(p => (p.Species, p.Nickname, (int)p.CurrentLevel))]));
        }
        var suma = estado == BattleVideo4DecryptionState.Encrypted && video.ChecksumValid;
        return new Resumen(estado != BattleVideo4DecryptionState.Decrypted, estado != BattleVideo4DecryptionState.Invalid && (estado == BattleVideo4DecryptionState.Decrypted || suma), video.BlockID, jugadores, equipos.Sum(e => e.Length), nombres.Count(n => !string.IsNullOrWhiteSpace(n)));
    }

    // Una sola entrada de memoria por ranura: la pantalla pide la vista en cada relectura y cada campo; si no cambia ni el borrador ni la partida se devuelve lo mismo sin descifrar otra vez.
    private static readonly object Cerrojo = new();
    private static readonly Dictionary<int, (object? Borrador, byte[] Partida, Resumen? Resumen)> _vistas = [];

    /// <summary>El resumen del archivo elegido (pendiente de «Aplicar») o, si no hay, el de la ranura de la partida; null si la ranura no existe o el archivo no sirve.</summary>
    private static Resumen? Vista(ContextoP c, int i, out bool esBorrador)
    {
        var borrador = c.Valor(Id(i, "archivo")) as byte[];
        esBorrador = borrador is not null && Validar(c, i, borrador) is null;
        var partida = Ranura(c, i)?.Data.ToArray() ?? [];
        lock (Cerrojo)
            if (_vistas.TryGetValue(i, out var u) && ReferenceEquals(u.Borrador, esBorrador ? borrador : null) && u.Partida.AsSpan().SequenceEqual(partida)) return u.Resumen;
        var r = esBorrador ? Leer(borrador!) : partida.Length == 0 ? null : Leer(partida, descifrado: false);
        lock (Cerrojo) _vistas[i] = (esBorrador ? borrador : null, partida, r);
        return r;
    }

    private static string Id(int i, string campo) => $"videos[{i}].{campo}";

    private static string Nombre(Resumen? r)
    {
        if (r is null) return IdiomaInterfaz.T("VideosCombate4.NoDisponible");
        var nombres = r.Jugadores.Select(j => j.Nombre).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
        return nombres.Count == 0 ? IdiomaInterfaz.T("VideosCombate4.Vacio") : string.Join(" vs ", nombres);
    }

    private static string Estado(Resumen r)
        => IdiomaInterfaz.F("VideosCombate4.Estado", IdiomaInterfaz.T(r.Cifrado ? "VideosCombate4.Cifrado" : "VideosCombate4.Descifrado"), r.ConNombre, r.Pokemon,
            IdiomaInterfaz.T(r.SumaValida ? "VideosCombate4.SumaValida" : "VideosCombate4.SumaInvalida"), r.Bloque.ToString("X4"));

    // ---------------- validación e importación ----------------
    /// <summary>El motivo de rechazo de un archivo para la ranura <paramref name="i"/> (null = sirve), con las mismas condiciones que <c>B_Import_Click</c>.</summary>
    private static string? Validar(ContextoP c, int i, byte[] datos)
    {
        if (Ranura(c, i) is null) return IdiomaInterfaz.T("VideosCombate4.RanuraNoDisponible");
        if (datos.Length != BattleVideo4.SIZE_USED) return IdiomaInterfaz.F("VideosCombate4.TamanoNoValido", BattleVideo4.SIZE_USED, datos.Length);
        if (!BattleVideo4.IsValid(datos)) return IdiomaInterfaz.T("VideosCombate4.PieNoValido");
        return BattleVideo4.DetectEncryption(datos) == BattleVideo4DecryptionState.Invalid ? IdiomaInterfaz.T("VideosCombate4.NoValido") : null;
    }

    private static void Importar(ContextoP c, int i, byte[] b)
    {
        var destino = Ranura(c, i) ?? throw new InvalidOperationException(IdiomaInterfaz.T("VideosCombate4.RanuraNoDisponible"));
        var datos = (byte[])b.Clone();
        var estado = BattleVideo4.DetectEncryption(datos);
        var importado = new BattleVideo4(datos)
        {
            IsDecrypted = estado == BattleVideo4DecryptionState.Decrypted,
            // La ranura es dueña de los metadatos del bloque: se conservan los de destino y solo se reemplaza el vídeo.
            Magic = destino.Magic,
            Revision = destino.Revision,
            BlockSize = destino.BlockSize,
            BlockID = destino.BlockID,
        };
        importado.RefreshChecksums();
        importado.Encrypt();
        importado.Data.CopyTo(destino.Data);
        // Igual que SAV_DLC4: la clave del bloque (sus 4 primeros bytes) es la del archivo, y el Core solo da la ranura por disponible si coincide con la que la partida anota en su bloque general.
        // Un vídeo exportado de OTRA partida trae otra clave y la ranura deja de leerse; PKHeX de escritorio la deja igual (enseña «N/A» al releer). Se avisa al aplicar.
        if (S(c).GetBattleVideo(i) is null) _perdidas.GetOrCreateValue(c).Add(i);
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ContextoP, HashSet<int>> _perdidas = new();

    private static string? AvisoTrasAplicar(ContextoP c)
    {
        if (!_perdidas.TryGetValue(c, out var perdidas) || perdidas.Count == 0) return null;
        var texto = IdiomaInterfaz.F("VideosCombate4.AvisoClave", string.Join(", ", perdidas.Order().Select(i => i + 1)));
        perdidas.Clear();
        return texto;
    }

    private static string NombreArchivo(ContextoP c, int i, bool descifrado)
    {
        var nombre = Ranura(c, i)?.GetName() is { } n && !string.IsNullOrWhiteSpace(n) ? n : IdiomaInterfaz.F("VideosCombate4.Video", i + 1);
        foreach (var malo in Path.GetInvalidFileNameChars()) nombre = nombre.Replace(malo, '_');
        return $"{nombre}{(descifrado ? " (" + L("CHK_ExportDecrypted") + ")" : string.Empty)}.{BattleVideo4.Extension}";
    }

    // ---------------- campos de un jugador ----------------
    /// <summary>El original enseña los jugadores que tienen equipo y, como mínimo, los dos primeros.</summary>
    private static bool JugadorVisible(Resumen? r, int jugador)
        => r is not null && jugador < r.Jugadores.Count && (r.Jugadores[jugador].Equipo.Count != 0 || jugador < Math.Max(2, r.Jugadores.Count(j => j.Equipo.Count != 0)));

    private static IEnumerable<CampoP> CamposDeJugador(int i, int jugador)
    {
        Jugador? De(ContextoP c) => Vista(c, i, out _) is { } r && jugador < r.Jugadores.Count ? r.Jugadores[jugador] : null;
        yield return P.Info(Id(i, $"j{jugador}"), IdiomaInterfaz.T("VideosCombate4.Entrenador"),
            c => De(c) is { } j ? (string.IsNullOrWhiteSpace(j.Nombre) ? IdiomaInterfaz.T("VideosCombate4.SinNombre") : j.Nombre) : string.Empty);
        for (var k = 0; k < 6; k++)
        {
            var pos = k;
            yield return P.Info(Id(i, $"j{jugador}p{pos}"), IdiomaInterfaz.F("VideosCombate4.Pokemon", pos + 1),
                c =>
                {
                    if (De(c) is not { } j || pos >= j.Equipo.Count) return string.Empty;
                    var (especie, mote, nivel) = j.Equipo[pos];
                    var nombre = especie < c.Textos.specieslist.Length ? c.Textos.specieslist[especie] : $"{especie:000}";
                    return IdiomaInterfaz.F("VideosCombate4.FilaPokemon", nombre, string.IsNullOrWhiteSpace(mote) ? "-" : mote, nivel);
                },
                c => De(c) is { } j && pos < j.Equipo.Count);
        }
        yield return P.Info(Id(i, $"j{jugador}vacio"), IdiomaInterfaz.T("VideosCombate4.Equipo"), _ => "-", c => De(c) is { Equipo.Count: 0 });
    }

    private static ColeccionDefP Videos()
    {
        ColeccionDefP? col = null;
        col = new ColeccionDefP
        {
            Id = "videos", Elemento = IdiomaInterfaz.T("VideosCombate4.VideoSingular"), Cantidad = _ => Cantidad, Titulo = (_, i) => IdiomaInterfaz.F("VideosCombate4.Video", i + 1),
            Resumen = (c, i) => Ranura(c, i) is { } v ? Nombre(Leer(v.Data, descifrado: false)) : IdiomaInterfaz.T("VideosCombate4.NoDisponible"),
            Secciones = (_ctx, i) =>
            [
                new SeccionP(string.Empty,
                [
                    P.Info(Id(i, "nombre"), L("L_VideoTitle"), c => Nombre(Vista(c, i, out _))),
                    P.Info(Id(i, "estado"), IdiomaInterfaz.T("VideosCombate4.EtiquetaEstado"),   // L_VideoStatus del original es el texto inicial «Seleccione un vídeo», no una etiqueta
                        c => Vista(c, i, out var borrador) is { } r ? (borrador ? IdiomaInterfaz.F("VideosCombate4.Pendiente", Estado(r)) : Estado(r)) : IdiomaInterfaz.T("VideosCombate4.RanuraNoDisponible")),
                    P.Archivo(Id(i, "archivo"), IdiomaInterfaz.T("VideosCombate4.ArchivoVideo"), (c, b) => Importar(c, i, b), validar: (c, b) => Validar(c, i, b),
                        exporta: c => Ranura(c, i) is { } v ? (NombreArchivo(c, i, descifrado: false), v.Data.ToArray()) : null,
                        extension: BattleVideo4.Extension, textoBoton: L("B_Import"), ayuda: IdiomaInterfaz.F("VideosCombate4.AyudaArchivo", BattleVideo4.Extension, BattleVideo4.SIZE_USED),
                        motivo: c => Ranura(c, i) is null ? IdiomaInterfaz.T("VideosCombate4.RanuraNoDisponible") : null),
                    P.Archivo(Id(i, "descifrado"), L("CHK_ExportDecrypted"), null,
                        exporta: c =>
                        {
                            if (Ranura(c, i) is not { } v) return null;
                            var copia = v.Data.ToArray();
                            new BattleVideo4(copia) { IsDecrypted = false }.Decrypt();   // se descifra la COPIA, nunca la partida
                            return (NombreArchivo(c, i, descifrado: true), copia);
                        },
                        extension: BattleVideo4.Extension, ayuda: IdiomaInterfaz.T("VideosCombate4.AyudaDescifrado"),
                        motivo: c => Ranura(c, i) is null ? IdiomaInterfaz.T("VideosCombate4.RanuraNoDisponible") : null),
                ]),
                .. Enumerable.Range(0, BattleVideo4.TrainerCount).Select(j => new SeccionP(IdiomaInterfaz.F("VideosCombate4.Jugador", j + 1), [.. CamposDeJugador(i, j)],
                    c => JugadorVisible(Vista(c, i, out _), j))),
            ],
        };
        return col;
    }

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (!Aplica(sav)) return null;
        return new EditorDefP("dlc4", TituloGen4.De("dlc4"), [new PestanaP("videos", L("L_Slots"), [], Coleccion: Videos())], Censo, IdiomaInterfaz.T("Partida.Especifico.dlc4.Descripcion")) { AvisoTrasAplicar = AvisoTrasAplicar };
    }
}
