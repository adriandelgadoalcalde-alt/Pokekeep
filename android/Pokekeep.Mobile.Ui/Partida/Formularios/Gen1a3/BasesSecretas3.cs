// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;
using Pokekeep.Mobile.Ui.Ajustes.Pkhex;
using Pokekeep.Mobile.Ui.Modelo;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen1a3;

/// <summary>
/// PK-SAV-018: <c>SAV_SecretBase3</c>, las bases secretas de otros entrenadores guardadas en Rubí, Zafiro y Esmeralda (<see cref="SecretBaseManager3"/>: hasta 20, solo las ocupadas). Para cada base WinForms tiene dos botones:
/// «Update Trainer» (nombre, género, ID, IDS, veces que se ha entrado, «combatido hoy» y «registrada», en ese orden) y «Update PKM» (especie, PID, objeto, cuatro movimientos, nivel y EVs del miembro abierto; con la especie
/// en «---» todo lo demás se queda a cero y desactivado, y con una especie el nivel mínimo es 2). Aquí cada grupo es un GRUPO de escritura: si cambia un campo se escriben los del grupo, en el orden del formulario. Al guardar el
/// formulario llama a <see cref="SecretBaseManager3.Save"/>, que además rellena con 0xFF el nombre de los huecos libres; se hace igual después de aplicar cualquier cambio.
/// Diferencias declaradas: el formulario de PKHeX no está traducido (<c>TranslateInterface</c> está comentado), así que las etiquetas son del respaldo <see cref="RespaldoGen1a3"/>; el visor de caracteres especiales del nombre
/// (<c>TrashEditor</c>) no tiene equivalente; la «forma» de Unown se enseña como letra calculada del PID (el formulario la enseña desplazada una posición por <c>pk.Form - 1</c>).
/// </summary>
internal static class BasesSecretas3
{
    public const string Form = "SAV_SecretBase3";
    public static readonly string[] Censo = ["PK-SAV-018"];
    private const int Miembros = 6;
    private static readonly string[] LetrasUnown = [.. "ABCDEFGHIJKLMNOPQRSTUVWXYZ".Select(c => c.ToString()), "!", "?"];

    // El Core proporciona las clases en inglés con GetTrainerClass; no trae una lista española.
    // Se conserva el respaldo español documentado en ADR 0063. El texto informativo guarda solo su ID.
    private static readonly string[] Clases = [.. Enumerable.Range(0, 5).Select(i => ServicioIdioma.Recursos("es-ES")["Form.Bases3.Clase" + i])];

    private static string Clase(int id) => ProgramaPkhex.Idioma == "en" ? SecretBase3.GetTrainerClass(id) : Clases[id];

    private static string L(string control) => EtiquetasLang.Requerir(Form, control);
    private static SecretBaseManager3 Gestor(SaveFile sav) => ((ISaveBlock3LargeHoenn)((SAV3)sav).LargeBlock).SecretBases;
    private static SecretBase3 Base(SaveFile sav, int i) => Gestor(sav).Bases[i];

    /// <summary>Predicado barato de <see cref="Crear"/>: el editor existe para este guardado (<c>SAV3 { LargeBlock: ISaveBlock3LargeHoenn }</c>).</summary>
    public static bool Aplica(SaveFile sav) => sav is SAV3 { LargeBlock: ISaveBlock3LargeHoenn };

    private static IEnumerable<CampoP> CamposDelEntrenador(ColeccionDefP col, int i)
    {
        var g = $"base3:{i}:entrenador";
        yield return P.Texto(col.IdCampo(i, "nombre"), IdiomaInterfaz.T("Form.NombreEntrenador"), c => Base(c.Sav, i).OriginalTrainerName, (c, v) => Base(c.Sav, i).OriginalTrainerName = v, maxLongitud: 7, prioridad: 0, grupo: g);
        yield return P.Opcion(col.IdCampo(i, "genero"), IdiomaInterfaz.T("Form.Genero"), c => Base(c.Sav, i).OriginalTrainerGender, (c, v) => Base(c.Sav, i).OriginalTrainerGender = (byte)v, _ => AyudasGen3.Generos, titulo: IdiomaInterfaz.T("Form.Genero"), prioridad: 1, grupo: g);
        yield return P.Entero(col.IdCampo(i, "tid"), L("L_TID"), c => Base(c.Sav, i).TID16, (c, v) => Base(c.Sav, i).TID16 = (ushort)v, 0, 65535, prioridad: 2, grupo: g);
        yield return P.Entero(col.IdCampo(i, "sid"), L("L_SID"), c => Base(c.Sav, i).SID16, (c, v) => Base(c.Sav, i).SID16 = (ushort)v, 0, 65535, prioridad: 3, grupo: g);
        yield return P.Entero(col.IdCampo(i, "entradas"), L("L_Entered"), c => Base(c.Sav, i).TimesEntered, (c, v) => Base(c.Sav, i).TimesEntered = (byte)v, 0, 255, prioridad: 4, grupo: g);
        yield return P.Marca(col.IdCampo(i, "combatido"), L("CHK_Battled"), c => Base(c.Sav, i).BattledToday, (c, v) => Base(c.Sav, i).BattledToday = v, prioridad: 5, grupo: g);
        yield return P.Marca(col.IdCampo(i, "registrada"), L("CHK_Registered"), c => Base(c.Sav, i).RegistryStatus == 1, (c, v) => Base(c.Sav, i).RegistryStatus = v ? 1 : 0, prioridad: 6, grupo: g);
        yield return P.Info(col.IdCampo(i, "clase"), L("L_Class"), c => Base(c.Sav, i).OriginalTrainerClass, (_, id) => Clase((int)id));
    }

    private static IEnumerable<CampoP> CamposDelMiembro(ColeccionDefP col, int i, int m)
    {
        var g = $"base3:{i}:m{m}";
        string Id(string campo) => col.IdCampo(i, $"m{m}.{campo}");
        var idEsp = Id("especie"); var idNivel = Id("nivel");
        string[] ceros = [Id("pid"), Id("objeto"), Id("mov1"), Id("mov2"), Id("mov3"), Id("mov4"), idNivel, Id("evs")];
        SecretBase3PKM Leer(ContextoP c) => Base(c.Sav, i).Team.Team[m];
        void Cambiar(ContextoP c, Action<SecretBase3PKM> cambia) { var b = Base(c.Sav, i); var equipo = b.Team; cambia(equipo.Team[m]); b.Team = equipo; }
        string? SinEspecie(ContextoP c) => c.Entero(idEsp) == 0 ? IdiomaInterfaz.T("Form.ElegirEspecie") : null;

        // Elegir «---» deja el resto a cero y desactivado; con una especie el nivel minimo es 2 (NumericUpDown.Minimum), asi que un 0 sube a 2.
        IReadOnlyDictionary<string, object?> Deriva(ContextoP c)
        {
            if (c.Entero(idEsp) == 0) return ceros.ToDictionary(x => x, _ => (object?)0L);
            return c.Entero(idNivel) < 2 ? new Dictionary<string, object?> { [idNivel] = 2L } : new Dictionary<string, object?>();
        }

        yield return P.Opcion(idEsp, EtiquetasLang.Requerir("SAV_HallOfFame1", "Label_Species"), c => Leer(c).Species, (c, v) => Cambiar(c, pk => pk.Species = (ushort)v), AyudasGen3.Especies, titulo: EtiquetasLang.Requerir("SAV_HallOfFame1", "Label_Species"), prioridad: 0, grupo: g, deriva: Deriva, especie: true);
        yield return P.Info(Id("forma"), IdiomaInterfaz.T("Form.FormaUnown"), c => LetrasUnown[Math.Min(Leer(c).Form, LetrasUnown.Length - 1)], visible: c => c.Entero(idEsp) == (int)Species.Unown);
        yield return P.Entero(Id("pid"), L("L_PID"), c => Leer(c).PID, (c, v) => Cambiar(c, pk => pk.PID = (uint)v), 0, uint.MaxValue, hex: true, motivo: SinEspecie, prioridad: 1, grupo: g, ayuda: IdiomaInterfaz.T("Form.AyudaHex8"));
        yield return P.Opcion(Id("objeto"), L("L_Item"), c => Leer(c).HeldItem, (c, v) => Cambiar(c, pk => pk.HeldItem = (ushort)v), AyudasGen3.Objetos, titulo: L("L_Item"), motivo: SinEspecie, prioridad: 2, grupo: g);
        for (var k = 1; k <= 4; k++)
        {
            var mov = k;
            yield return P.Opcion(Id($"mov{k}"), L($"L_Move{k}"), c => mov switch { 1 => Leer(c).Move1, 2 => Leer(c).Move2, 3 => Leer(c).Move3, _ => Leer(c).Move4 },
                (c, v) => Cambiar(c, pk => { switch (mov) { case 1: pk.Move1 = (ushort)v; break; case 2: pk.Move2 = (ushort)v; break; case 3: pk.Move3 = (ushort)v; break; default: pk.Move4 = (ushort)v; break; } }),
                AyudasGen3.Movimientos, titulo: L($"L_Move{k}"), motivo: SinEspecie, prioridad: 2 + k, grupo: g);
        }
        yield return P.Entero(idNivel, L("L_Level"), c => Leer(c).Level, (c, v) => Cambiar(c, pk => pk.Level = (byte)v), 0, 100, motivo: SinEspecie, prioridad: 7, grupo: g,
            validarValor: (c, v) => c.Entero(idEsp) != 0 && v < 2 ? IdiomaInterfaz.T("Form.Bases3.NivelMin") : null);
        yield return P.Entero(Id("evs"), L("L_EVs"), c => Leer(c).EVAll, (c, v) => Cambiar(c, pk => pk.EVAll = (byte)v), 0, 85, motivo: SinEspecie, prioridad: 8, grupo: g, ayuda: IdiomaInterfaz.T("Form.Bases3.Esfuerzo"));
    }

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (!Aplica(sav)) return null;
        ColeccionDefP? col = null;
        col = new ColeccionDefP
        {
            Id = "bases", Elemento = IdiomaInterfaz.T("Form.Bases3.Base"), Cantidad = c => Gestor(c.Sav).Count,
            Titulo = (c, i) => $"{i + 1:00} · {Base(c.Sav, i).OriginalTrainerName}",
            Resumen = (c, i) => $"{Clase(Base(c.Sav, i).OriginalTrainerClass)} · {Enumerable.Range(0, Miembros).Count(m => Base(c.Sav, i).Team.Team[m].Species != 0)} Pokémon",
            Secciones = (c, i) =>
            {
                var secciones = new List<SeccionP> { new(IdiomaInterfaz.T("Form.Entrenador"), [.. CamposDelEntrenador(col!, i)]) };
                for (var m = 0; m < Miembros; m++) secciones.Add(new SeccionP($"{L("L_Member")} {m + 1}", [.. CamposDelMiembro(col!, i, m)]));
                return secciones;
            },
        };
        return new EditorDefP("basessecretas", IdiomaInterfaz.T("Form.Bases3.Titulo"), [new PestanaP("bases", L("L_Trainers"), [], Coleccion: col)], Censo,
            IdiomaInterfaz.F("Form.Bases3.Descripcion", NombresJuego.Corto(GameVersion.R), NombresJuego.Corto(GameVersion.S), NombresJuego.Corto(GameVersion.E)))
        {
            // B_Save_Click: Manager.Save() tambien rellena con 0xFF el nombre de los huecos libres; se repite tras aplicar para que el guardado quede igual que el del formulario.
            PrepararAplicar = c => _ => Gestor(c.Sav).Save(),
        };
    }
}
