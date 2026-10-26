// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Partida.Catalogos;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen8y9;

/// <summary>
/// PK-SAV-150, 151 y 152 · <c>SAV_BlockDump8</c>, «Volcado de bloque de datos de guardado» de Espada y Escudo, Leyendas Arceus, Escarlata y Púrpura y Leyendas Z-A (<c>ISCBlockArray</c>: los guardados de Switch son una lista de bloques
/// con clave de 32 bits, cifrados con SwishCrypto). Es una herramienta AVANZADA («Más > Herramientas > Bloques del guardado», no un botón de «Específicos del juego»). Pestaña «Respaldar» (<c>Tab_Dump</c>): elegir un bloque por su
/// clave o su nombre (<c>SCBlockMetadata</c>), ver su resumen (clave, tipo, longitud, valor) y sus bytes, cambiar el tipo de un bloque booleano (<c>Bool1</c>/<c>Bool2</c>), importar y exportar el bloque actual (el archivo tiene que medir
/// lo mismo que el bloque), exportar todos los bloques a una carpeta (un archivo <c>CLAVE [valor].bin</c> por bloque con datos), exportar todo en un solo archivo (con las opciones solo datos, clave, tipo y marca de inicio) e importar
/// bloques desde una carpeta (el nombre del archivo da la clave; los que no cuadran se anotan como fallidos). Pestaña «Comparar» (<c>Tab_Compare</c>): dos guardados y la lista de bloques añadidos, quitados, de tipo cambiado y de valor cambiado
/// (<c>SCBlockCompare</c>). Adaptación a móvil: una CARPETA no se puede elegir con el selector de archivos del sistema, así que «a una carpeta» y «desde una carpeta» son un archivo .zip con los mismos archivos <c>.bin</c> dentro; el cuadro
/// de propiedades (<c>PG_BlockView</c>) de PKHeX nunca llega a recibir su objeto (el formulario no asigna <c>SelectedObject</c>), así que la edición de propiedades es la que el cuadro DEBERÍA dar: por reflexión sobre el objeto del bloque
/// (<c>ReflectUtil.GetPropertiesCanWritePublicDeclared</c>) y, para los bloques de un valor suelto, la propiedad «Valor» (<c>GetEditableBlockObject</c>). Todo se escribe al aplicar, con Deshacer exacto (la foto del guardado incluye el tipo y los
/// bytes de cada bloque).
/// </summary>
internal static class BloquesSC
{
    public const string Form = "SAV_BlockDump8";
    public static readonly string[] Censo = ["PK-SAV-150", "PK-SAV-151", "PK-SAV-152"];
    /// <summary>Bytes de un bloque que se enseñan en hexadecimal (el resto se exporta, no se dibuja).</summary>
    private const int BytesVisibles = 256;

    private static string L(string control) => EtiquetasLang.Requerir(Form, control);

    /// <summary>Predicado barato de <see cref="Crear"/>: el editor existe para este guardado.</summary>
    public static bool Aplica(SaveFile sav) => sav is ISCBlockArray;

    // ---------------- estado por editor abierto ----------------
    private sealed class Estado
    {
        public required SCBlockMetadata Meta { get; init; }
        public required ComboItem[] Claves { get; init; }
        public required ISCBlockArray Sav { get; init; }
        /// <summary>Los archivos del ultimo zip aplicado que no cuadraban (clave desconocida o tamaño distinto): se avisan una vez tras aplicar.</summary>
        public List<string>? FallidosDelZip { get; set; }
    }

    private static readonly ConditionalWeakTable<ContextoP, Estado> Estados = new();

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "La reflexion recorre las propiedades publicas de los bloques de PKHeX, que se conservan.")]
    private static Estado E(ContextoP c) => Estados.GetValue(c, x =>
    {
        var sav = (ISCBlockArray)x.Sav;
        var meta = new SCBlockMetadata(sav.Accessor, Ajustes.Pkhex.AvanzadoPkhex.NombresExtraDeBloques(sav), Ajustes.Pkhex.AvanzadoPkhex.ExclusionesDeBloques());   // Advanced.PathBlockKeyList y HideEvent8Contains, como SAV_BlockDump8 (bloque 10-1)
        return new Estado { Meta = meta, Claves = Ordenar(meta, sav.Accessor.BlockInfo), Sav = sav };
    });

    /// <summary>
    /// <c>SCBlockMetadata.GetSortedBlockKeyList</c> calcado (los bloques con nombre primero y despues por el texto «clave - posicion tipo ...»), pero sin lanzar con un bloque sin tipo (el original pide el valor de
    /// todos los que no son booleanos y falla en una partida en blanco). Con un guardado real da el MISMO orden que el original (lo comprueba una prueba).
    /// </summary>
    internal static ComboItem[] Ordenar(SCBlockMetadata meta, IReadOnlyList<SCBlock> bloques)
    {
        string Pista(SCBlock z, int indice)
        {
            var nombre = meta.GetBlockName(z, out _);
            var esBool = z.Type.IsBoolean();
            var tipo = esBool ? "Bool" : z.Type.ToString();
            if (nombre is not null) return $"*{tipo} {nombre}";
            var r = $"{z.Key:X8} - {indice:0000} {tipo}";
            if (z.Type is SCTypeCode.Object or SCTypeCode.Array) r += $" 0x{z.Data.Length:X3}";
            else if (!esBool && z.HasValue()) r += $" {z.GetValue()}";
            return r;
        }
        static string ClaveOrden(string texto) => texto.StartsWith('*') ? texto : texto[(8 + 3 + 4 + 1)..];
        return [.. bloques.Select((z, i) => new ComboItem(Pista(z, i), (int)z.Key)).OrderBy(z => !z.Text.StartsWith('*')).ThenBy(z => ClaveOrden(z.Text))];
    }

    private static SCBlock Bloque(ContextoP c, int i) => E(c).Sav.Accessor.GetBlock((uint)E(c).Claves[i].Value);

    // ---------------- textos ----------------
    private static string NombreTipo(SCTypeCode t) => t switch
    {
        SCTypeCode.None => IdiomaInterfaz.T("BloquesSC.Tipo"), SCTypeCode.Bool1 => IdiomaInterfaz.T("BloquesSC.BooleanoBool"), SCTypeCode.Bool2 => IdiomaInterfaz.T("BloquesSC.BooleanoBool2"), SCTypeCode.Bool3 => IdiomaInterfaz.T("BloquesSC.BooleanoBool3"), SCTypeCode.Object => IdiomaInterfaz.T("VariosGen4Frente.Objeto"), SCTypeCode.Array => IdiomaInterfaz.T("BloquesSC.Lista"),
        SCTypeCode.Byte => IdiomaInterfaz.T("BloquesSC.EnteroBitsSigno"), SCTypeCode.UInt16 => IdiomaInterfaz.T("BloquesSC.EnteroBitsSigno2"), SCTypeCode.UInt32 => IdiomaInterfaz.T("BloquesSC.EnteroBitsSigno3"), SCTypeCode.UInt64 => IdiomaInterfaz.T("BloquesSC.EnteroBitsSigno4"),
        SCTypeCode.SByte => IdiomaInterfaz.T("BloquesSC.EnteroBits"), SCTypeCode.Int16 => IdiomaInterfaz.T("BloquesSC.EnteroBits2"), SCTypeCode.Int32 => IdiomaInterfaz.T("BloquesSC.EnteroBits3"), SCTypeCode.Int64 => IdiomaInterfaz.T("BloquesSC.EnteroBits4"),
        SCTypeCode.Single => IdiomaInterfaz.T("BloquesSC.DecimalDeBits"), SCTypeCode.Double => IdiomaInterfaz.T("BloquesSC.DecimalDeBits2"),
        _ => t.ToString(),
    };

    private static string Hex(ReadOnlySpan<byte> datos)
    {
        var n = Math.Min(datos.Length, BytesVisibles);
        var sb = new StringBuilder(n * 3);
        for (var i = 0; i < n; i++) sb.Append(datos[i].ToString("X2", CultureInfo.InvariantCulture)).Append(i + 1 < n ? " " : string.Empty);
        if (datos.Length > n) sb.Append(IdiomaInterfaz.F("BloquesSC.Bytes", datos.Length - n));
        return sb.ToString();
    }

    private static string? NombreTecnico(ContextoP c, SCBlock b) => E(c).Meta.GetBlockName(b, out _);

    private static string Titulo(ContextoP c, int i)
    {
        var b = Bloque(c, i);
        return NombreTecnico(c, b) is { } n ? $"{n} · {b.Key:X8}" : $"{b.Key:X8}";
    }

    private static string Resumen(ContextoP c, int i)
    {
        var b = Bloque(c, i);
        var detalle = b.Data.Length == 0 ? string.Empty : b.Type is SCTypeCode.Object or SCTypeCode.Array ? $" · {b.Data.Length} bytes" : b.HasValue() ? $" · {Convert.ToString(b.GetValue(), CultureInfo.InvariantCulture)}" : string.Empty;
        return $"{NombreTipo(b.Type)}{detalle}";
    }

    // ---------------- campos de un bloque ----------------
    private static Func<string, string> Rotulo => n => n;

    /// <summary>Contadores que el Core guarda en un byte sin validar (<c>Party.PartyCount = (byte)value</c>, <c>BoxLayout.CurrentBox = (byte)value</c>): se acotan por nombre a lo que el juego espera.</summary>
    internal static Func<string, (long Min, long Max)?> Limites(ContextoP c)
    {
        var cajas = (E(c).Sav as SaveFile)?.BoxCount ?? 0;
        return nombre => nombre switch
        {
            "PartyCount" => (0, 6),
            "CurrentBox" when cajas > 0 => (0, cajas - 1),
            _ => null,
        };
    }

    private static IReadOnlyList<CampoP> CamposDe(ContextoP c, ColeccionDefP col, int i)
    {
        var b = Bloque(c, i);
        string Id(string campo) => col.IdCampo(i, campo);
        var campos = new List<CampoP>();
        // El resumen del formulario (clave, tipo, longitud, valor).
        campos.Add(P.Info(Id("clave"), L("L_Key").TrimEnd(':'), _ => $"{b.Key:X8}"));
        campos.Add(P.Info(Id("tipo"), IdiomaInterfaz.T("MenuCaja.Tipo2"), _ => NombreTipo(Bloque(c, i).Type)));
        if (b.Data.Length != 0) campos.Add(P.Info(Id("longitud"), IdiomaInterfaz.T("BloquesSC.Longitud"), _ => $"0x{Bloque(c, i).Data.Length:X8} ({Bloque(c, i).Data.Length} bytes)"));
        if (NombreTecnico(c, b) is { } nombre) campos.Add(P.Info(Id("nombre"), IdiomaInterfaz.T("BloquesSC.NombreTecnico"), _ => nombre));
        if (b.SubType != 0) campos.Add(P.Info(Id("subtipo"), IdiomaInterfaz.T("BloquesSC.Subtipo"), _ => NombreTipo(Bloque(c, i).SubType)));

        // Tipo de un bloque booleano (CB_TypeToggle: solo Bool1 y Bool2).
        if (b.Type is SCTypeCode.Bool1 or SCTypeCode.Bool2)
            campos.Add(P.Opcion(Id("tipo-booleano"), IdiomaInterfaz.T("BloquesSC.TipoBooleano"), _ => (int)Bloque(c, i).Type, (_, v) => Bloque(c, i).ChangeBooleanType((SCTypeCode)(int)v), _ => [new OpcionP((int)SCTypeCode.Bool1, "Bool1"), new OpcionP((int)SCTypeCode.Bool2, "Bool2")], titulo: IdiomaInterfaz.T("BloquesSC.TipoBooleano"), ayuda: IdiomaInterfaz.T("BloquesSC.CambiaTipoAlmacenadoBloque")));

        // Valor suelto (los bloques de un entero o un decimal): la propiedad «Value» del envoltorio que da GetEditableBlockObject.
        if (SCBlockMetadata.GetEditableBlockObject(b) is { } envoltorio)
        {
            var pi = envoltorio.GetType().GetProperty("Value")!;
            var hermanos = new List<CampoP>();
            if (pi.PropertyType == typeof(ulong))
                campos.Add(Numero64(Id("valor"), IdiomaInterfaz.T("EdicionLotes.Valor2"), () => (ulong)pi.GetValue(SCBlockMetadata.GetEditableBlockObject(Bloque(c, i)))!, v => pi.SetValue(SCBlockMetadata.GetEditableBlockObject(Bloque(c, i)), v)));
            else if (BloquesP.Campo(Id("valor"), () => SCBlockMetadata.GetEditableBlockObject(Bloque(c, i))!, null, pi, () => hermanos, _ => IdiomaInterfaz.T("EdicionLotes.Valor2")) is { } valor) { hermanos.Add(valor); campos.Add(valor); }
        }

        // Propiedades del objeto del bloque (las que PKHeX conoce por nombre): solo las simples y escribibles; las compuestas se pueden ver en el volcado y exportar.
        if (E(c).Meta.GetBlockName(b, out var obj) is not null && obj is not null)
        {
            var tipo = obj.GetType();
            var hermanos = new List<CampoP>();
            foreach (var nombrePropiedad in ReflectUtil.GetPropertiesCanWritePublicDeclared(tipo).Order(StringComparer.Ordinal))
            {
                if (tipo.GetProperty(nombrePropiedad, BindingFlags.Public | BindingFlags.Instance) is not { } p || p.GetIndexParameters().Length != 0) continue;
                if (p.PropertyType == typeof(ulong))
                {
                    var objeto = obj;
                    campos.Add(Numero64(Id($"prop.{p.Name}"), p.Name, () => (ulong)p.GetValue(objeto)!, v => { p.SetValue(objeto, v); if ((ulong)p.GetValue(objeto)! != v) throw new InvalidOperationException(IdiomaInterfaz.F("BloquesSC.ElJuegoNoAdmite", p.Name, p.GetValue(objeto), v)); }));
                }
                else if (BloquesP.Simple(p.PropertyType) && BloquesP.Campo(Id("prop"), () => obj, null, p, () => hermanos, Rotulo, Limites(c)) is { } campo) { hermanos.Add(campo); campos.Add(campo); }
            }
        }

        // Los bytes y la importación/exportación del bloque actual.
        if (b.Data.Length != 0)
        {
            campos.Add(P.Info(Id("bytes"), "Bytes (hexadecimal)", _ => Hex(Bloque(c, i).Data)));
            campos.Add(P.Archivo(Id("archivo"), $"{L("B_ImportCurrent")} / {L("B_ExportCurrent")}",
                (cx, datos) => Bloque(cx, i).ChangeData(datos),
                (cx, datos) => datos.Length == Bloque(cx, i).Data.Length ? null : IdiomaInterfaz.F("BloquesSC.ArchivoMideBytesBloque", datos.Length, Bloque(cx, i).Data.Length),
                cx => ($"{SCBlockUtil.GetBlockFileNameWithoutExtension(Bloque(cx, i))}.bin", Bloque(cx, i).Data.ToArray()),
                extension: "bin", textoBoton: L("B_ImportCurrent"), prioridad: -1, confirmacion: IdiomaInterfaz.T("BloquesSC.BytesBloqueSustituyenArchivo"), ayuda: IdiomaInterfaz.T("BloquesSC.ArchivoMismoTamanoSustituye")));
        }
        return campos;
    }

    /// <summary>Un entero de 64 bits sin signo como texto decimal (el rango no cabe en un entero con signo); lo que no es un número se rechaza al escribir.</summary>
    private static CampoP Numero64(string id, string etiqueta, Func<ulong> leer, Action<ulong> escribir) => new()
    {
        Id = id, Etiqueta = etiqueta, Tipo = TipoP.Texto, MaxLongitud = 20, Ayuda = IdiomaInterfaz.T("BloquesSC.NumeroEnteroSignoBits"),
        Leer = _ => leer().ToString(CultureInfo.InvariantCulture), Escribir = (_, v) => escribir(ulong.Parse((string)v!, CultureInfo.InvariantCulture)),
        Validar = (cx, t) => ulong.TryParse(t.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out _) ? null : IdiomaInterfaz.T("BloquesSC.EscribeNumeroEnteroSigno"),
        Normaliza = t => ulong.TryParse(t.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n.ToString(CultureInfo.InvariantCulture) : "0",
    };

    // ---------------- volcado, zip y comparacion ----------------
    private static SCBlockExportOption Opciones(ContextoP c)
    {
        var o = SCBlockExportOption.None;
        if (c.Marca("solo-datos")) o |= SCBlockExportOption.DataOnly;
        if (c.Marca("clave")) o |= SCBlockExportOption.Key;
        if (c.Marca("tipo")) o |= SCBlockExportOption.TypeInfo;
        if (c.Marca("cabecera")) o |= SCBlockExportOption.FakeHeader;
        return o;
    }

    /// <summary><c>ExportAllBlocks(blocks, path)</c> a un zip: un archivo <c>{clave} [valor].bin</c> por cada bloque con datos.</summary>
    internal static byte[] ExportarZip(IEnumerable<SCBlock> bloques)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var b in bloques.Where(z => z.Data.Length != 0))
            {
                var entrada = zip.CreateEntry($"{SCBlockUtil.GetBlockFileNameWithoutExtension(b)}.bin", CompressionLevel.Optimal);
                using var s = entrada.Open();
                s.Write(b.Data);
            }
        return ms.ToArray();
    }

    /// <summary>
    /// <c>ImportBlocksFromFolder</c> sobre un zip: el nombre de cada archivo (sin extension y hasta el primer espacio) es la clave del bloque; solo se importan los que existen y miden lo mismo. Devuelve las claves que
    /// fallaron. Con <paramref name="aplicar"/> a false solo cuenta (para validar antes de aplicar).
    /// </summary>
    internal static (int Importados, List<string> Fallidos) ImportarZip(ISCBlockArray sav, byte[] datos, bool aplicar)
    {
        var fallidos = new List<string>(); var importados = 0;
        using var zip = new ZipArchive(new MemoryStream(datos), ZipArchiveMode.Read);
        foreach (var e in zip.Entries.Where(x => x.Name.Length != 0))
        {
            var fn = Path.GetFileNameWithoutExtension(e.Name);
            var espacio = fn.IndexOf(' ');
            if (espacio != -1) fn = fn[..espacio];
            var clave = Util.GetHexValue(fn);
            try
            {
                var bloque = sav.Accessor.GetBlock(clave);
                if (e.Length != bloque.Data.Length) { fallidos.Add(fn); continue; }
                var bytes = new byte[e.Length];
                using (var s = e.Open()) s.ReadExactly(bytes);
                if (aplicar) bloque.ChangeData(bytes);
                importados++;
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException or IndexOutOfRangeException or InvalidDataException or IOException) { fallidos.Add(fn); }
        }
        return (importados, fallidos);
    }

    private static string TextoImportacion(ContextoP c, byte[]? zip)
    {
        if (zip is null) return IdiomaInterfaz.T("BloquesSC.EligeArchivoZipBloques");
        try
        {
            var (ok, fallidos) = ImportarZip(E(c).Sav, zip, aplicar: false);
            return IdiomaInterfaz.F("BloquesSC.BloquesImportaran", ok) + (fallidos.Count == 0 ? "." : IdiomaInterfaz.F("BloquesSC.NoSeImportaran", fallidos.Count, string.Join(", ", fallidos.Take(20)), fallidos.Count > 20 ? IdiomaInterfaz.F("Form.Mas", fallidos.Count - 20) : string.Empty));
        }
        catch (InvalidDataException) { return IdiomaInterfaz.T("BloquesSC.ArchivoZipValido"); }
    }

    private static (string Origen, string Destino)[] Traducciones =>
    [
        ("Blocks Added:", IdiomaInterfaz.T("BloquesSC.BloquesAnadidos")), ("Blocks Removed:", IdiomaInterfaz.T("BloquesSC.BloquesQuitados")), ("BlockType Changed:", IdiomaInterfaz.T("BloquesSC.TipoBloqueCambiado")), ("Value Changed:", IdiomaInterfaz.T("BloquesSC.ValorCambiado")),
        ("Bytes Changed: Length:", IdiomaInterfaz.T("BloquesSC.BytesCambiadosLongitud")), ("- Length:", "- longitud:"),
    ];

    private static string? Resumen2(ContextoP c)
    {
        var viejo = InvestigarGuardados.Leer(c.Valor("viejo") as byte[], InvestigarGuardados.MaximoBytesSwitch) as ISCBlockArray;
        var nuevo = InvestigarGuardados.Leer(c.Valor("nuevo") as byte[], InvestigarGuardados.MaximoBytesSwitch) as ISCBlockArray;
        if (viejo is null || nuevo is null) return IdiomaInterfaz.T("BloquesSC.EligeGuardadoViejoNuevo");
        var lineas = new SCBlockCompare(viejo.Accessor, nuevo.Accessor, []).Summary();
        if (lineas.Count == 0) return IdiomaInterfaz.T("BloquesSC.DosGuardadosTienenMismos");
        var traducidas = lineas.Select(l => Traducciones.Aggregate(l, (t, x) => t.Replace(x.Origen, x.Destino, StringComparison.Ordinal))).ToList();
        var texto = traducidas.Take(InvestigarGuardados.MaximoLineas).ToList();
        if (traducidas.Count > InvestigarGuardados.MaximoLineas) texto.Add(IdiomaInterfaz.F("BloquesSC.Lineas", traducidas.Count - InvestigarGuardados.MaximoLineas));
        return string.Join("\n", texto);
    }

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (sav is not ISCBlockArray) return null;
        ColeccionDefP? col = null;
        col = new ColeccionDefP
        {
            Id = "bloques", Elemento = IdiomaInterfaz.T("BloquesSC.Bloque"), Cantidad = c => E(c).Claves.Length,
            Titulo = Titulo, Resumen = Resumen,
            Secciones = (c, i) => [new SeccionP(string.Empty, CamposDe(c, col!, i))],
        };
        var volcado = new SeccionP(string.Empty,
        [
            new CampoP { Id = "solo-datos", Etiqueta = L("CHK_DataOnly"), Tipo = TipoP.Marca, Leer = _ => true, Escribir = null, SinEscritura = true, Ayuda = IdiomaInterfaz.T("BloquesSC.SoloBloquesDatosBooleanos") },
            new CampoP { Id = "clave", Etiqueta = L("CHK_Key"), Tipo = TipoP.Marca, Leer = _ => false, Escribir = null, SinEscritura = true, Ayuda = IdiomaInterfaz.T("BloquesSC.PoneClaveBitsDelante") },
            new CampoP { Id = "tipo", Etiqueta = L("CHK_Type"), Tipo = TipoP.Marca, Leer = _ => false, Escribir = null, SinEscritura = true, Ayuda = IdiomaInterfaz.T("BloquesSC.PoneTipoSubtipoDelante") },
            new CampoP { Id = "cabecera", Etiqueta = L("CHK_FakeHeader"), Tipo = TipoP.Marca, Leer = _ => true, Escribir = null, SinEscritura = true, Ayuda = IdiomaInterfaz.T("BloquesSC.PoneCabeceraTextoNumero") },
            new CampoP
            {
                Id = "exportar-uno", Etiqueta = L("B_ExportAllSingle"), Tipo = TipoP.Archivo, Leer = _ => null, Escribir = null, SoloExporta = true, Extension = "bin", Ayuda = IdiomaInterfaz.T("BloquesSC.TodosBloquesSoloArchivo"),
                Exporta = c => ("raw.bin", SCBlockUtil.ExportAllBlocks(E(c).Sav.Accessor.BlockInfo, Opciones(c))),
            },
            new CampoP
            {
                Id = "exportar-zip", Etiqueta = $"{L("B_ExportAll")} (.zip)", Tipo = TipoP.Archivo, Leer = _ => null, Escribir = null, SoloExporta = true, Extension = "zip", Ayuda = IdiomaInterfaz.T("BloquesSC.ArchivoBinBloqueDatos"),
                Exporta = c => ("bloques.zip", ExportarZip(E(c).Sav.AllBlocks)),
            },
            new CampoP
            {
                Id = "importar-zip", Etiqueta = $"{L("B_ImportFolder")} (.zip)", Tipo = TipoP.Archivo, Leer = _ => null, Extension = "zip", TextoBoton = $"{L("B_ImportFolder")} (.zip)", Prioridad = 0,
                Confirmacion = IdiomaInterfaz.T("BloquesSC.BloquesArchivoSustituyenGuardado"), Ayuda = IdiomaInterfaz.T("BloquesSC.ZipArchivoBinBloque"),
                ValidarArchivo = (c, datos) =>
                {
                    try { var (ok, _) = ImportarZip(E(c).Sav, datos, aplicar: false); return ok == 0 ? IdiomaInterfaz.T("BloquesSC.NingunArchivoZipCorresponde") : null; }
                    catch (InvalidDataException) { return IdiomaInterfaz.T("BloquesSC.ArchivoZipValido"); }
                },
                Escribir = (c, v) => { if (v is byte[] datos) E(c).FallidosDelZip = ImportarZip(E(c).Sav, datos, aplicar: true).Fallidos; },
                Detalle = c => TextoImportacion(c, c.Valor("importar-zip") as byte[]),
            },
        ]);
        var comparar = new SeccionP(L("GB_Researcher"),
        [
            InvestigarGuardados.Campo("viejo", L("B_LoadOld"), g => g is ISCBlockArray, IdiomaInterfaz.T("BloquesSC.ArchivoGuardadoSwitchBloques"), IdiomaInterfaz.T("BloquesSC.GuardadoAntes"), maximo: InvestigarGuardados.MaximoBytesSwitch),
            InvestigarGuardados.Campo("nuevo", L("B_LoadNew"), g => g is ISCBlockArray, IdiomaInterfaz.T("BloquesSC.ArchivoGuardadoSwitchBloques"), IdiomaInterfaz.T("BloquesSC.GuardadoDespues"), Resumen2, InvestigarGuardados.MaximoBytesSwitch),
        ]);
        return new EditorDefP("bloques8", IdiomaInterfaz.T("BloquesSC.BloquesGuardadoAvanzado"),
        [
            new PestanaP("bloques", L("Tab_Dump"), [], Coleccion: col),
            new PestanaP("volcado", IdiomaInterfaz.T("BloquesSC.ExportarEImportar"), [volcado]),
            new PestanaP("comparar", L("Tab_Compare"), [comparar]),
        ], Censo, IdiomaInterfaz.T("Partida.Especifico.bloques8.Descripcion"))
        {
            AvisoTrasAplicar = c =>
            {
                var e = E(c); var fallidos = e.FallidosDelZip; e.FallidosDelZip = null;
                return fallidos is { Count: > 0 } ? IdiomaInterfaz.F("BloquesSC.ArchivosNoImportados", fallidos.Count == 1 ? IdiomaInterfaz.T("BloquesSC.ArchivoZipImportado") : IdiomaInterfaz.F("BloquesSC.ArchivosZipImportado", fallidos.Count), string.Join(", ", fallidos.Take(20)), fallidos.Count > 20 ? IdiomaInterfaz.F("Form.Mas", fallidos.Count - 20) : string.Empty) : null;
            },
        };
    }
}
