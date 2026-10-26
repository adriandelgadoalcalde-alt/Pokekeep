// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen6y7;

/// <summary>
/// PK-SAV-106 · <c>SAV_HallOfFame</c>, el Hall de la Fama de X/Y y de Rubí Omega/Zafiro Alfa: 16 entradas (la primera vez que se vence a la Liga y las 15 más recientes), cada una con su número de victorias, su fecha y hasta
/// 6 Pokémon (especie, objeto, 4 movimientos, constante de cifrado, ID/IDS, forma, género, nivel, variocolor, mote y entrenador original con su género). Como en el original, una entrada SIN registro no se puede editar y solo
/// se enseñan los Pokémon que tiene (en el formulario el selector de «N.º equipo» llega hasta los que hay). Cada cambio en el formulario reescribe el Pokémon entero (<c>Write_Entry</c>): aqui los campos de un Pokémon forman
/// un grupo de escritura por Pokémon y los de la entrada (victorias, fecha) se escriben aparte. Cambiar la especie lleva la forma a la primera y, si el Pokémon no tiene mote, su mote al nombre de la especie; el género
/// solo admite los de la especie (los dos si tiene dos). «Borrar» quita la entrada y las siguientes suben (la primera no se puede quitar): accion inmediata. «Copiar» (el resumen en texto al portapapeles) no se porta:
/// es un texto informativo sin efecto sobre la partida.
/// </summary>
internal static class SalonFama6
{
    public const string Form = "SAV_HallOfFame";
    public static readonly string[] Censo = ["PK-SAV-106"];
    private const int Entradas = HallOfFame6.Entries, Miembros = HallOfFame6.PokeCount;

    private static string L(string control) => EtiquetasLang.Requerir(Form, control);
    private static HallOfFame6 Fama(SaveFile sav) => ((ISaveBlock6Main)sav).HallOfFame;
    private static HallFame6Index Indice(SaveFile sav, int i) => new(Fama(sav).GetEntry(i)[^4..]);
    private static HallFame6Entity Ent(SaveFile sav, int i, int k) => new(Fama(sav).GetEntity(i, k), sav.Language);
    private static bool TieneDatos(SaveFile sav, int i) => Indice(sav, i).HasData;

    private static int Existentes(SaveFile sav, int i)
    {
        var n = 0;
        for (var k = 0; k < Miembros; k++) if (Ent(sav, i, k).Species != 0) n++;
        return n;
    }

    private static string? SinDatos(ContextoP c, int i) => TieneDatos(c.Sav, i) ? null : IdiomaInterfaz.T("SalonFama6.EntradaTieneRegistroAun");

    private static string NombreEspecie(SaveFile sav, long especie)
        => especie is 0 or > (int)Species.Volcanion ? string.Empty : SpeciesName.GetSpeciesNameGeneration((ushort)especie, sav.Language, 6);

    /// <summary>Predicado barato de <see cref="Crear"/>: el editor existe para este guardado.</summary>
    public static bool Aplica(SaveFile sav) => sav is ISaveBlock6Main;

    private static IEnumerable<CampoP> Pokemon(ColeccionDefP col, int i, int k)
    {
        string Id(string campo) => col.IdCampo(i, $"m{k}.{campo}");
        var idEspecie = Id("especie"); var idForma = Id("forma"); var idMote = Id("mote"); var idPonerMote = Id("apodado"); var idGenero = Id("genero");
        var grupo = $"hof6:{i}:{k}";
        string? Motivo(ContextoP c) => SinDatos(c, i);
        IReadOnlyList<OpcionP> Formas(ContextoP c) => P.Lista(FormConverter.GetFormList((ushort)c.Entero(idEspecie), c.Textos.types, c.Textos.forms, GameInfo.GenderSymbolUnicode, c.Sav.Context));
        bool TieneFormas(ContextoP c) => FormInfo.HasFormSelection(PersonalTable.AO[(ushort)c.Entero(idEspecie)], (ushort)c.Entero(idEspecie), 6);
        IReadOnlyList<OpcionP> Generos(ContextoP c)
        {
            var pi = c.Sav.Personal[(ushort)c.Entero(idEspecie)];
            return pi.IsDualGender ? [new(0, IdiomaInterfaz.T("Form.Trainer8.Male")), new(1, IdiomaInterfaz.T("Form.Trainer8.Female"))] : [new(pi.FixedGender(), pi.FixedGender() switch { 0 => IdiomaInterfaz.T("Form.Trainer8.Male"), 1 => IdiomaInterfaz.T("Form.Trainer8.Female"), _ => IdiomaInterfaz.T("GoPark.Genero") })];
        }

        yield return P.Opcion(idEspecie, L("Label_Species").TrimEnd(':'), c => Ent(c.Sav, i, k).Species, (c, v) => { var e = Ent(c.Sav, i, k); e.Species = (ushort)v; },
            c => P.Lista(FuentesGen6y7.Filtro(c).Species), titulo: L("Label_Species").TrimEnd(':'), motivo: Motivo, prioridad: 0, grupo: grupo,
            deriva: c =>
            {
                // UpdateSpecies: SetForms (la forma vuelve a la primera) y UpdateNickname (sin mote, el nombre de la especie).
                var d = new Dictionary<string, object?> { [idForma] = 0L };
                if (!c.Marca(idPonerMote)) d[idMote] = NombreEspecie(c.Sav, c.Entero(idEspecie));
                return d;
            }, especie: true);
        yield return P.Opcion(Id("objeto"), L("Label_HeldItem").TrimEnd(':'), c => Ent(c.Sav, i, k).HeldItem, (c, v) => { var e = Ent(c.Sav, i, k); e.HeldItem = (ushort)v; },
            c => P.Lista(FuentesGen6y7.Filtro(c).Items), titulo: L("Label_HeldItem").TrimEnd(':'), motivo: Motivo, grupo: grupo);
        for (var m = 1; m <= 4; m++)
        {
            var n = m;
            yield return P.Opcion(Id($"mov{n}"), $"{L("GB_CurrentMoves")} {n}",
                c => { var e = Ent(c.Sav, i, k); return n switch { 1 => e.Move1, 2 => e.Move2, 3 => e.Move3, _ => e.Move4 }; },
                (c, v) => { var e = Ent(c.Sav, i, k); switch (n) { case 1: e.Move1 = (ushort)v; break; case 2: e.Move2 = (ushort)v; break; case 3: e.Move3 = (ushort)v; break; default: e.Move4 = (ushort)v; break; } },
                c => P.Lista(FuentesGen6y7.Filtro(c).Moves), titulo: IdiomaInterfaz.F("Catalogo.Movimiento", n), motivo: Motivo, grupo: grupo);
        }
        yield return P.Entero(Id("ec"), L("Label_EncryptionConstant").TrimEnd(':'), c => Ent(c.Sav, i, k).EncryptionConstant, (c, v) => { var e = Ent(c.Sav, i, k); e.EncryptionConstant = (uint)v; }, 0, uint.MaxValue, hex: true, motivo: Motivo, grupo: grupo);
        yield return P.Entero(Id("tid"), L("Label_TID").TrimEnd(':'), c => Ent(c.Sav, i, k).TID16, (c, v) => { var e = Ent(c.Sav, i, k); e.TID16 = (ushort)v; }, 0, ushort.MaxValue, motivo: Motivo, grupo: grupo);
        yield return P.Entero(Id("sid"), L("Label_SID").TrimEnd(':'), c => Ent(c.Sav, i, k).SID16, (c, v) => { var e = Ent(c.Sav, i, k); e.SID16 = (ushort)v; }, 0, ushort.MaxValue, motivo: Motivo, grupo: grupo);
        yield return P.Opcion(idForma, L("Label_Form").TrimEnd(':'), c => Ent(c.Sav, i, k).Form, (c, v) => { var e = Ent(c.Sav, i, k); e.Form = (byte)v; }, Formas,
            visible: TieneFormas, titulo: L("Label_Form").TrimEnd(':'), motivo: Motivo, grupo: grupo);
        yield return P.Opcion(idGenero, IdiomaInterfaz.T("Form.Genero"), c => Ent(c.Sav, i, k).Gender, (c, v) => { var e = Ent(c.Sav, i, k); e.Gender = (uint)v & 3; }, Generos, titulo: IdiomaInterfaz.T("Form.Genero"), motivo: Motivo, grupo: grupo,
            deriva: c =>
            {
                // UpdateGender: una forma de un solo género sigue al género elegido y los Pyroar toman la forma de su género.
                var especie = (ushort)c.Entero(idEspecie);
                if (!c.Sav.Personal[especie].IsDualGender) return new Dictionary<string, object?>();
                var g = (int)c.Entero(idGenero);
                var formas = FormConverter.GetFormList(especie, c.Textos.types, c.Textos.forms, GameInfo.GenderSymbolUnicode, c.Sav.Context);
                var f = (int)c.Entero(idForma);
                var d = new Dictionary<string, object?>();
                var porForma = f >= 0 && f < formas.Length ? EntityGender.GetFromString(formas[f]) : 2;
                if (porForma == 0 && g != 0) d[idForma] = 1L;
                else if (porForma == 1 && g != 1) d[idForma] = 0L;
                if (especie == (int)Species.Pyroar) d[idForma] = (long)g;
                return d;
            });
        yield return P.Entero(Id("nivel"), L("L_Level").TrimEnd(':'), c => Ent(c.Sav, i, k).Level, (c, v) => { var e = Ent(c.Sav, i, k); e.Level = (uint)v; }, 0, 100, motivo: Motivo, grupo: grupo);
        yield return P.Marca(Id("brillante"), IdiomaInterfaz.T("Catalogo.Variocolor"), c => Ent(c.Sav, i, k).IsShiny, (c, v) => { var e = Ent(c.Sav, i, k); e.IsShiny = v; }, motivo: Motivo, grupo: grupo);
        // El mote solo se edita con «Mote» marcado; sin marcar sigue a la especie (UpdateNickname).
        yield return P.Marca(idPonerMote, L("CHK_Nicknamed").TrimEnd(':'), c => Ent(c.Sav, i, k).IsNicknamed, (c, v) => { var e = Ent(c.Sav, i, k); e.IsNicknamed = v; }, motivo: Motivo, grupo: grupo,
            ayuda: IdiomaInterfaz.T("Form.Fama1.MoteAyuda"),
            deriva: c => c.Marca(idPonerMote) ? new Dictionary<string, object?>() : new Dictionary<string, object?> { [idMote] = NombreEspecie(c.Sav, c.Entero(idEspecie)) });
        yield return P.Texto(idMote, IdiomaInterfaz.T("Form.Respaldo.SAV_HallOfFame1.TB_Nickname"), c => Ent(c.Sav, i, k).Nickname,
            // Write_Entry de WinForms escribe SIEMPRE el texto del mote, y sin «Mote» marcado ese texto es el nombre de la especie (UpdateNickname): al escribir la entrada, un mote que no sigue a la especie se normaliza igual.
            (c, v) => { var e = Ent(c.Sav, i, k); e.Nickname = c.Marca(idPonerMote) ? v : NombreEspecie(c.Sav, c.Entero(idEspecie)); }, 12, grupo: grupo,
            motivo: c => SinDatos(c, i) ?? (c.Marca(idPonerMote) ? null : IdiomaInterfaz.T("Form.Fama1.MoteMotivo")));
        yield return P.Texto(Id("eo"), L("Label_OT").TrimEnd(':'), c => Ent(c.Sav, i, k).OriginalTrainerName, (c, v) => { var e = Ent(c.Sav, i, k); e.OriginalTrainerName = v; }, 12, motivo: Motivo, grupo: grupo);
        yield return P.Opcion(Id("genero-eo"), IdiomaInterfaz.F("Form.PuntoGenero", L("Label_OT").TrimEnd(':')), c => Ent(c.Sav, i, k).OriginalTrainerGender, (c, v) => { var e = Ent(c.Sav, i, k); e.OriginalTrainerGender = (uint)v & 1; },
            _ => P.Lista(IdiomaInterfaz.T("Form.Trainer8.Male"), IdiomaInterfaz.T("Form.Trainer8.Female")), titulo: IdiomaInterfaz.T("SalonFama6.GeneroEntrenador"), motivo: Motivo, grupo: grupo);
    }

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (sav is not ISaveBlock6Main) return null;
        ColeccionDefP? col = null;
        col = new ColeccionDefP
        {
            Id = "entradas", Elemento = L("groupBox1"), Cantidad = _ => Entradas, MostrarPosicion = false,
            Titulo = (_, i) => i == 0 ? IdiomaInterfaz.T("SalonFama6.Primera") : $"{i:00}",
            Resumen = (c, i) => TieneDatos(c.Sav, i) ? IdiomaInterfaz.F("SalonFama6.PokemonVictorias", Existentes(c.Sav, i), Indice(c.Sav, i).ClearIndex) : IdiomaInterfaz.T("SalonFama6.Registro"),
            Secciones = (_, i) =>
            {
                var secciones = new List<SeccionP>
                {
                    new(string.Empty,
                    [
                        P.Entero(col!.IdCampo(i, "victorias"), L("L_Victory").TrimEnd(':'), c => Indice(c.Sav, i).ClearIndex, (c, v) => { var x = Indice(c.Sav, i); x.ClearIndex = (uint)v; }, 0, byte.MaxValue,
                            maxFn: c => Math.Max(byte.MaxValue, Indice(c.Sav, i).ClearIndex), motivo: c => SinDatos(c, i), ayuda: IdiomaInterfaz.T("SalonFama6.CuantasVecesSuperadoLiga")),
                        new CampoP
                        {
                            Id = col.IdCampo(i, "fecha"), Etiqueta = L("Label_MetDate").TrimEnd(':'), Tipo = TipoP.FechaHora, SoloFecha = true, Motivo = c => SinDatos(c, i),
                            Leer = c => Fecha(c.Sav, i),
                            Escribir = (c, v) => { var d = (DateTime)v!; var x = Indice(c.Sav, i); x.Year = (uint)(d.Year - 2000); x.Month = (uint)d.Month; x.Day = (uint)d.Day; },
                        },
                    ]),
                };
                for (var k = 0; k < Miembros; k++)
                {
                    var n = k;
                    secciones.Add(new SeccionP($"{L("L_PartyNum").TrimEnd(':')} {n + 1}", [.. Pokemon(col!, i, n)], c => n < Math.Max(1, Existentes(c.Sav, i))));
                }
                // «Borrar» (B_Delete): quita la entrada y las siguientes suben; la primera no se puede quitar.
                secciones.Add(new SeccionP(string.Empty, [P.Accion(col!.IdCampo(i, "quitar"), L("B_Delete"), c => Fama(c.Sav).ClearEntry(i), inmediata: true, textoBoton: IdiomaInterfaz.T("Seleccion.Borrar"),
                    motivo: c => i == 0 ? IdiomaInterfaz.T("SalonFama6.PrimeraEntradaHallFama") : TieneDatos(c.Sav, i) ? null : IdiomaInterfaz.T("SalonFama6.EntradaTieneRegistro"),
                    confirmacion: IdiomaInterfaz.T("SalonFama6.BorraEntradaHallFama"), ayuda: IdiomaInterfaz.T("SalonFama6.BorraEntradaSiguientesSuben"))]));
                return secciones;
            },
        };
        return new EditorDefP("hof", IdiomaInterfaz.T("SalonFama6.HallFama"), [new PestanaP("entradas", EtiquetasLang.RequerirFormulario(Form), [], Coleccion: col)], Censo, IdiomaInterfaz.T("SalonFama6.EntradasHallFamaGeneracion"));
    }

    private static DateTime Fecha(SaveFile sav, int i)
    {
        var x = Indice(sav, i);
        return x.Month is >= 1 and <= 12 && x.Day is >= 1 and <= 31 && DateTime.DaysInMonth((int)x.Year + 2000, (int)x.Month) >= x.Day ? new DateTime((int)x.Year + 2000, (int)x.Month, (int)x.Day) : new DateTime(2000, 1, 1);
    }
}
