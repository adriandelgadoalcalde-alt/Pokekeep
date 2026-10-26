// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Globalization;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen8y9;

/// <summary>
/// PK-SAV-183, 184, 186 y 187 · <c>SAV_Donut9a</c>, <c>DonutEditor9a</c>, <c>DonutFlavorProfile9a</c> y <c>SAV_DonutGenerator9a</c>, los dónuts de Leyendas Pokémon Z-A (revisión 1 del guardado o posterior): 999 huecos de 0x48 bytes
/// (<c>DonutPocket9a</c>). El editor de un dónut (<c>DonutEditor9a</c>) enseña el tipo de dónut, estrellas (0 a 6), calorías (0 a 65.535), nivel extra (0 a 255), la baya del nombre y las ocho bayas, los tres sabores, la
/// fecha de creación y los milisegundos desde 1970 (cambiar uno recalcula el otro, conservando los milisegundos sueltos de la fecha), y el perfil de sabor (<c>DonutFlavorProfile9a</c>: la suma de los cinco sabores de las ocho
/// bayas, <c>RecalculateDonutFlavors</c>). La lista (<c>SAV_Donut9a</c>) elige el dónut y trae «Reiniciar» (<c>Clear</c>), «Importar»/«Exportar» (archivo <c>.donut</c> de 72 bytes; con Ctrl, el portapapeles en hexadecimal) y
/// «Modificar todo»: aleatorizar los 999 con sabores de nivel 3, clonar el actual a todos, surtido variocolor y el generador (<c>SAV_DonutGenerator9a</c>: un rango de huecos y los sabores permitidos). Los efectos de
/// «Modificar todo» son aleatorios y llevan la hora actual (<c>Util.Rand</c>, <c>DateTime.Now</c>): se comparan por su estructura. Divergencias declaradas en el ADR 0066: el formulario reescribe desde los controles el dónut
/// que se visita (también sin tocarlo, y con la fecha 1970 si no tenía), la app solo lo que se cambia; la imagen del dónut y los iconos de sabores no se portan (la lista de tipos ya los nombra).
/// </summary>
internal static class Donuts9a
{
    public const string Form = "SAV_Donut9a";
    public static readonly string[] Censo = ["PK-SAV-183", "PK-SAV-184", "PK-SAV-186", "PK-SAV-187"];
    private const int Maximo = DonutPocket9a.MaxCount;

    private static string L(string control) => EtiquetasLang.Requerir(Form, control);
    private static string LG(string control) => EtiquetasLang.Requerir("SAV_DonutGenerator9a", control);
    private static SAV9ZA Sav(ContextoP c) => (SAV9ZA)c.Sav;
    private static Donut9a Donut(ContextoP c, int i) => Sav(c).Donuts.GetDonut(i);
    private static DateTime Epoca => Donut9a.Epoch;

    /// <summary>Predicado barato de <see cref="Crear"/>: el editor existe para este guardado.</summary>
    public static bool Aplica(SaveFile sav) => sav is SAV9ZA { SaveRevision: >= 1 };

    // ---------------- listas ----------------
    private static IReadOnlyList<OpcionP> Tipos(ContextoP c) => [.. c.Textos.donutName.Select((t, i) => new OpcionP(i, t))];

    /// <summary>Las bayas del dónut: «ninguna» (0) y las del almacén de objetos de Z-A, con el nombre del objeto.</summary>
    private static IReadOnlyList<OpcionP> Bayas(ContextoP c) => [new OpcionP(0, c.Textos.itemlist[0].Length == 0 ? IdiomaInterfaz.T("Catalogo.Ninguna") : c.Textos.itemlist[0]), .. ItemStorage9ZA.Berry.ToArray().Select(b => new OpcionP(b, c.Textos.itemlist[b]))];

    /// <summary>Los sabores: «ninguno» (0) y cada sabor de <c>DonutInfo.Flavors</c> (valor = posición + 1) con su texto en castellano.</summary>
    private static IReadOnlyList<OpcionP> Sabores(ContextoP c) => [new OpcionP(0, IdiomaInterfaz.T("Form.Ninguno")), .. DonutInfo.Flavors.Select((f, i) => new OpcionP(i + 1, i < c.Textos.donutFlavor.Length ? c.Textos.donutFlavor[i] : f.Name))];

    private static long SaborLeido(ulong hash)
    {
        if (hash == 0) return 0;
        for (var i = 0; i < DonutInfo.Flavors.Count; i++) if (DonutInfo.Flavors[i].Hash == hash) return i + 1;
        return 0;   // un sabor desconocido sale como «ninguno», igual que LoadDonutFlavorHash
    }

    private static ulong HashDe(long opcion) => opcion <= 0 || opcion > DonutInfo.Flavors.Count ? 0 : DonutInfo.Flavors[(int)opcion - 1].Hash;

    // ---------------- nombre y perfil ----------------
    private static string Nombre(Donut9a d, int i) => $"#{i + 1:000} {d.Stars}★ · {d.Calories:0000} cal {new string('*', d.FlavorCount)}";

    private static readonly string[] IdsBayas = ["baya1", "baya2", "baya3", "baya4", "baya5", "baya6", "baya7", "baya8"];

    /// <summary>El perfil de sabor del borrador: lo mismo que <c>DonutFlavorProfile9a.LoadFromDonut</c> (suma de las ocho bayas; la baya del nombre no cuenta).</summary>
    private static string Perfil(ContextoP c, ColeccionDefP col, int i)
    {
        var s = new int[5];
        foreach (var id in IdsBayas)
            if (DonutInfo.TryGetBerry((ushort)c.Entero(col.IdCampo(i, id)), out var b)) { s[0] += b.Spicy; s[1] += b.Fresh; s[2] += b.Sweet; s[3] += b.Bitter; s[4] += b.Sour; }
        return IdiomaInterfaz.F("Donuts9a.PerfilSabor", L("L_NameSpicy"), s[0], L("L_NameFresh"), s[1], L("L_NameSweet"), s[2], L("L_NameBitter"), s[3], L("L_NameSour"), s[4]);
    }

    // ---------------- fecha y milisegundos ----------------
    private static bool FechaEquivalente(DateTime a, DateTime b) => a.Year == b.Year && a.Month == b.Month && a.Day == b.Day && a.Hour == b.Hour && a.Minute == b.Minute && a.Second == b.Second;

    private static DateTime FechaLeida(Donut9a d) => !d.HasDateTime() ? Epoca : d.DateTime1900.Timestamp;

    /// <summary>Lo que <c>SaveDonut</c> hace con la fecha: la fecha cero (1900-01-01 00:00:00) borra el valor; cualquier otra se guarda (y si el Core no la admite se borra).</summary>
    private static void FechaEscrita(Donut9a d, DateTime fecha)
    {
        var dt = new DateTime(fecha.Year, fecha.Month, fecha.Day, fecha.Hour, fecha.Minute, fecha.Second);
        if (dt is { Year: 1900, Month: <= 1, Day: <= 1 } and { Day: 1, Hour: 0, Minute: 0, Second: 0 }) { d.ClearDateTime(); return; }
        try { d.DateTime1900.Timestamp = dt; }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { d.ClearDateTime(); }
    }

    private static bool MsValido(string t) => t.Length is >= 1 and <= 20 && t.All(char.IsAsciiDigit) && ulong.TryParse(t, NumberStyles.None, CultureInfo.InvariantCulture, out var ms) && ms <= (ulong)(DateTime.MaxValue - Epoca).TotalMilliseconds;

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (sav is not SAV9ZA { SaveRevision: >= 1 }) return null;
        ColeccionDefP? col = null;
        col = new ColeccionDefP
        {
            Id = "donuts", Elemento = IdiomaInterfaz.T("Donuts9a.Donut"), Cantidad = _ => Maximo,
            Titulo = (c, i) => Nombre(Donut(c, i), i),
            Resumen = (c, i) => Donut(c, i).Donut < c.Textos.donutName.Length ? c.Textos.donutName[Donut(c, i).Donut] : null,
            Secciones = (_, i) =>
            {
                string Id(string campo) => col!.IdCampo(i, campo);
                CampoP Baya(string campo, string etiqueta, Func<Donut9a, ushort> leer, Action<Donut9a, ushort> escribir, bool conPerfil = false)
                {
                    var f = P.Opcion(Id(campo), etiqueta, c => leer(Donut(c, i)), (c, v) => escribir(Donut(c, i), (ushort)v), Bayas, titulo: etiqueta, detalle: conPerfil ? c => Perfil(c, col!, i) : null);
                    return f;
                }
                CampoP Sabor(string campo, string etiqueta, Func<Donut9a, ulong> leer, Action<Donut9a, ulong> escribir)
                    => P.Opcion(Id(campo), etiqueta, c => SaborLeido(leer(Donut(c, i))), (c, v) => escribir(Donut(c, i), HashDe(v)), Sabores, titulo: etiqueta);

                var fecha = new CampoP
                {
                    Id = Id("fecha"), Etiqueta = IdiomaInterfaz.T("Donuts9a.FechaCreacion"), Tipo = TipoP.FechaHora, Ayuda = IdiomaInterfaz.T("Donuts9a.FechaHoraCreacionCambiarla"),
                    Leer = c => FechaLeida(Donut(c, i)), Escribir = (c, v) => FechaEscrita(Donut(c, i), (DateTime)v!),
                    Deriva = c =>
                    {
                        // ChangeDateTime: si los milisegundos ya dan esa fecha no se toca nada; si no, delta desde la epoca, conservando los milisegundos sueltos (ms % 1000).
                        if (!ulong.TryParse(c.Texto(Id("milisegundos")), out var ms)) return new Dictionary<string, object?>();
                        try
                        {
                            var fechaNueva = c.Fecha(Id("fecha"));
                            if (FechaEquivalente(fechaNueva, Epoca.AddMilliseconds(ms))) return new Dictionary<string, object?>();
                            var delta = unchecked((ulong)(fechaNueva - Epoca).TotalMilliseconds);
                            var existente = ms % 1000;
                            delta -= delta % 1000; delta += existente;
                            return new Dictionary<string, object?> { [Id("milisegundos")] = delta.ToString(CultureInfo.InvariantCulture) };
                        }
                        catch (ArgumentOutOfRangeException) { return new Dictionary<string, object?>(); }
                    },
                };
                var milisegundos = new CampoP
                {
                    Id = Id("milisegundos"), Etiqueta = L("L_Milliseconds").TrimEnd(':'), Tipo = TipoP.Texto, MaxLongitud = 20, Ayuda = IdiomaInterfaz.T("Donuts9a.MilisegundosDesdeCambiarlosRecalcula"),
                    Leer = c => Donut(c, i).MillisecondsSince1970.ToString(CultureInfo.InvariantCulture), Escribir = (c, v) => Donut(c, i).MillisecondsSince1970 = ulong.TryParse((string)v!, out var n) ? n : 0,
                    Validar = (_, t) => MsValido(t.Trim()) ? null : IdiomaInterfaz.T("Donuts9a.EscribeSoloCifrasInstante"),
                    Normaliza = t => ulong.TryParse(t.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var n) && MsValido(n.ToString(CultureInfo.InvariantCulture)) ? n.ToString(CultureInfo.InvariantCulture) : "0",
                    Deriva = c =>
                    {
                        // ChangeMilliseconds: la fecha pasa a ser epoca + ms, salvo que ya sea esa.
                        if (!ulong.TryParse(c.Texto(Id("milisegundos")), out var ms)) return new Dictionary<string, object?>();
                        try
                        {
                            var ticks = Epoca.AddMilliseconds(ms);
                            return FechaEquivalente(c.Fecha(Id("fecha")), ticks) ? new Dictionary<string, object?>() : new Dictionary<string, object?> { [Id("fecha")] = ticks };
                        }
                        catch (ArgumentOutOfRangeException) { return new Dictionary<string, object?>(); }
                    },
                };
                var archivo = P.Archivo(Id("archivo"), $"{L("B_Import")} / {L("B_Export")}", (c, datos) => datos.CopyTo(Donut(c, i).Data),
                    (_, datos) => datos.Length == Donut9a.Size ? null : IdiomaInterfaz.F("Donuts9a.TamanoDonutValidoEsperaban", Donut9a.Size, datos.Length),
                    c => ($"{i + 1:000}_{c.Textos.donutName[Math.Min(Donut(c, i).Donut, c.Textos.donutName.Length - 1)]}.donut", Donut(c, i).Data.ToArray()),
                    extension: "donut", textoBoton: L("B_Import"), prioridad: -1, confirmacion: IdiomaInterfaz.T("Donuts9a.DonutSustituyeContenidoArchivo"), ayuda: IdiomaInterfaz.T("Donuts9a.ArchivoDonutBytesSustituye"));
                // «Reiniciar»: DonutEditor.Reset() hace Clear() y recarga los controles; al guardar, SaveDonut reescribe desde los controles: el dónut queda a cero salvo la fecha, que sale como 1970-01-01 (la que enseña el
                // control cuando el dónut no tiene fecha).
                var reiniciar = P.Accion(Id("reiniciar"), L("B_Reset"), c => { var d = Donut(c, i); d.Clear(); FechaEscrita(d, Epoca); }, inmediata: true, textoBoton: IdiomaInterfaz.T("Form.Rtc.Reiniciar"), confirmacion: IdiomaInterfaz.T("Donuts9a.BorraDonutQuedaVacio"), ayuda: IdiomaInterfaz.T("Donuts9a.DejaDonutVacio"));
                return
                [
                    new SeccionP(string.Empty,
                    [
                        P.Opcion(Id("tipo"), L("L_Donut").TrimEnd(':'), c => Donut(c, i).Donut, (c, v) => Donut(c, i).Donut = (ushort)v, Tipos, titulo: L("L_Donut").TrimEnd(':')),
                        P.Entero(Id("estrellas"), L("L_Stars").TrimEnd(':'), c => Donut(c, i).Stars, (c, v) => Donut(c, i).Stars = (byte)v, 0, 6),
                        P.Entero(Id("calorias"), L("L_Calories").TrimEnd(':'), c => Donut(c, i).Calories, (c, v) => Donut(c, i).Calories = (ushort)v, 0, 65535),
                        P.Entero(Id("nivel-extra"), L("L_LevelBoost").TrimEnd(':'), c => Donut(c, i).LevelBoost, (c, v) => Donut(c, i).LevelBoost = (byte)v, 0, 255),
                    ]),
                    new SeccionP(IdiomaInterfaz.T("Mochila.Bolsillo.Berries"),
                    [
                        Baya("baya-nombre", L("L_Berry0"), d => d.BerryName, (d, v) => d.BerryName = v),
                        Baya("baya1", L("L_Berry1"), d => d.Berry1, (d, v) => d.Berry1 = v),
                        Baya("baya2", L("L_Berry2"), d => d.Berry2, (d, v) => d.Berry2 = v),
                        Baya("baya3", L("L_Berry3"), d => d.Berry3, (d, v) => d.Berry3 = v),
                        Baya("baya4", L("L_Berry4"), d => d.Berry4, (d, v) => d.Berry4 = v),
                        Baya("baya5", L("L_Berry5"), d => d.Berry5, (d, v) => d.Berry5 = v),
                        Baya("baya6", L("L_Berry6"), d => d.Berry6, (d, v) => d.Berry6 = v),
                        Baya("baya7", L("L_Berry7"), d => d.Berry7, (d, v) => d.Berry7 = v),
                        Baya("baya8", L("L_Berry8"), d => d.Berry8, (d, v) => d.Berry8 = v, conPerfil: true),
                    ]),
                    new SeccionP(IdiomaInterfaz.T("Donuts9a.Sabores"),
                    [
                        Sabor("sabor1", L("L_Flavor0").TrimEnd(':'), d => d.Flavor0, (d, v) => d.Flavor0 = v),
                        Sabor("sabor2", L("L_Flavor1").TrimEnd(':'), d => d.Flavor1, (d, v) => d.Flavor1 = v),
                        Sabor("sabor3", L("L_Flavor2").TrimEnd(':'), d => d.Flavor2, (d, v) => d.Flavor2 = v),
                    ]),
                    new SeccionP(IdiomaInterfaz.T("Donuts9a.Creacion"), [fecha, milisegundos]),
                    new SeccionP(IdiomaInterfaz.T("Donuts9a.ArchivoReinicio"), [archivo, reiniciar]),
                ];
            },
        };

        // ---- Modificar todo y generador ----
        var tablaGenerador = new TablaDefP
        {
            Id = "sabores-generador", Marcador = IdiomaInterfaz.T("Donuts9a.BuscarSabor"), SinNumero = true, Ayuda = LG("L_FlavorPool"),
            Filas = c => [.. DonutInfo.Flavors.Select((f, i) => (f, i)).Where(x => TipoDeSabor(x.f.Name) >= 0).Select(x => new FilaTablaP
            {
                Id = x.i, Nombre = x.i < c.Textos.donutFlavor.Length ? c.Textos.donutFlavor[x.i] : x.f.Name, EsMarca = true, SinEscritura = true, Leer = _ => true, Escribir = (_, _) => { },
            })],
            Acciones =
            [
                new AccionTablaP("todos", IdiomaInterfaz.T("Subeditores.MarcarTodos"), (_, filas) => [.. filas.Select(f => (f, (object)true))], IdiomaInterfaz.T("Donuts9a.PermiteTodosSaboresGenerador")),
                new AccionTablaP("ninguno", IdiomaInterfaz.T("Donuts9a.DesmarcarTodos"), (_, filas) => [.. filas.Select(f => (f, (object)false))], IdiomaInterfaz.T("Donuts9a.PermiteNingunSaborGenerador")),
            ],
        };
        ulong[] Elegidos(ContextoP c) => [.. DonutInfo.Flavors.Select((f, i) => (f, i)).Where(x => TipoDeSabor(x.f.Name) >= 0 && c.Marca($"sabores-generador:{x.i}")).Select(x => x.f.Hash)];
        string? SinRango(ContextoP c) => c.Entero("rango-inicio") >= c.Entero("rango-fin") ? IdiomaInterfaz.T("Donuts9a.EligeRangoDonutsValido") : Elegidos(c).Length == 0 ? IdiomaInterfaz.T("Donuts9a.EligeMenosTipoSabor") : null;
        var acciones = new SeccionP(IdiomaInterfaz.T("Donuts9a.ModificarTodo"),
        [
            P.Accion("aleatorizar", IdiomaInterfaz.T("Donuts9a.AleatorizarNivelMaximo"), c => Sav(c).Donuts.SetAllRandomLv3(), inmediata: true, textoBoton: IdiomaInterfaz.T("PokecubosORAS.Aleatorizar"), confirmacion: IdiomaInterfaz.T("Donuts9a.DonutsSustituyenDonutsNivel"), ayuda: IdiomaInterfaz.T("Donuts9a.SustituyeDonutsDonutsNivel")),
            new CampoP { Id = "clonar-origen", Etiqueta = IdiomaInterfaz.T("Donuts9a.DonutClona"), Tipo = TipoP.Entero, Min = 1, Max = Maximo, Leer = _ => 1L, Escribir = null, SinEscritura = true, Ayuda = IdiomaInterfaz.T("Donuts9a.NumeroDonutCopiaTodos") },
            P.Accion("clonar", IdiomaInterfaz.T("Donuts9a.ClonarDonutElegidoTodos"), c => Sav(c).Donuts.CloneAllFromIndex((int)c.Entero("clonar-origen") - 1), inmediata: true, textoBoton: IdiomaInterfaz.T("Cajas.Clonar"), confirmacion: IdiomaInterfaz.T("Donuts9a.DonutElegidoCopiaSobre"), ayuda: IdiomaInterfaz.T("Donuts9a.CopiaDonutElegidoSobre")),
            P.Accion("variocolor", IdiomaInterfaz.T("Donuts9a.SurtidoVariocolor"), c => Sav(c).Donuts.SetAllAsShinyTemplate(), inmediata: true, textoBoton: IdiomaInterfaz.T("Editor.Aplicar"), confirmacion: IdiomaInterfaz.T("Donuts9a.DonutsSustituyenDonutsPoder"), ayuda: IdiomaInterfaz.T("Donuts9a.SustituyeDonutsPlantillaPoder")),
        ]);
        var generador = new SeccionP(IdiomaInterfaz.T("Donuts9a.Generador"),
        [
            new CampoP { Id = "rango-inicio", Etiqueta = IdiomaInterfaz.F("Donuts9a.PuntoInicio", LG("L_Range")), Tipo = TipoP.Entero, Min = 0, Max = Maximo - 1, Leer = _ => 0L, Escribir = null, SinEscritura = true, Ayuda = IdiomaInterfaz.T("Donuts9a.PrimerHuecoDesdeSustituye") },
            new CampoP { Id = "rango-fin", Etiqueta = $"{LG("L_Range")} · fin", Tipo = TipoP.Entero, Min = 0, Max = Maximo, Leer = _ => (long)Maximo, Escribir = null, SinEscritura = true, Ayuda = IdiomaInterfaz.T("Donuts9a.HuecoIncluye") },
            P.Accion("generar", LG("B_Generate"), c => Sav(c).Donuts.SetRandomShinyTemplateRange(Elegidos(c), (int)c.Entero("rango-inicio"), (int)c.Entero("rango-fin")), SinRango, inmediata: true, textoBoton: LG("B_Generate"), confirmacion: IdiomaInterfaz.T("Donuts9a.DonutsRangoSustituyenDonuts"), ayuda: IdiomaInterfaz.T("Donuts9a.GeneraDonutsAzarRango")),
        ]);
        return new EditorDefP("donuts", IdiomaInterfaz.T("Partida.Especifico.donuts.Titulo"),
        [
            new PestanaP("donuts", EtiquetasLang.RequerirFormulario(Form), [], Coleccion: col),
            new PestanaP("acciones", L("B_ModifyAll"), [acciones, generador]),
            new PestanaP("sabores", LG("L_FlavorPool"), [], Tabla: tablaGenerador),
        ], Censo, IdiomaInterfaz.T("Partida.Especifico.donuts.Descripcion"));
    }

    /// <summary>El tipo de un sabor para el generador (<c>TryGetTypeIndex</c>): los dos dígitos de la posición 6 del nombre interno, entre 3 y 21; -1 si no lo tiene (Alfa, Cría/Gigante, especiales).</summary>
    private static int TipoDeSabor(string nombre) => nombre.Length >= 8 && int.TryParse(nombre.AsSpan(6, 2), out var v) && v is >= 3 and <= 21 ? v - 3 : -1;
}
