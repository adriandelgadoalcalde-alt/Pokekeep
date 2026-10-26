// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Modelo;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Cajas;

/// <summary>Como se coloca un Pokemon sobre otra casilla (los tres modificadores de arrastrar y soltar de PKHeX: nada, Mayus, Alt).</summary>
public enum ModoMover
{
    /// <summary>Sin modificador: si el destino esta vacio se mueve; si no, los dos Pokemon se INTERCAMBIAN.</summary>
    MoverOIntercambiar,
    /// <summary>Alt: el destino se sobrescribe y el origen queda vacio (el Pokemon del destino se pierde; Deshacer lo devuelve).</summary>
    Sobrescribir,
    /// <summary>Mayus: copia; el origen se conserva y el destino se sobrescribe.</summary>
    Copiar,
}

/// <summary>Que paso con una operacion sobre casillas. Los textos son los de PKHeX en castellano (<c>MessageStrings</c> del Core) o propios cuando PKHeX no tiene mensaje.</summary>
public enum TipoResultado { Correcto, SinCambios, Bloqueada, EquipoInvalido, NoAdmitido, FormatoIncompatible, Cancelada, Fallo }

/// <summary>Resultado de una operacion: siempre dice QUE paso y cuantas casillas se tocaron/omitieron; nunca un fallo silencioso.</summary>
public sealed record InformeOperacion(TipoResultado Tipo, string Mensaje, int Tocadas = 0, int Omitidas = 0, IReadOnlyList<string>? Avisos = null)
{
    public bool Correcto => Tipo == TipoResultado.Correcto;
    public static InformeOperacion Ok(string mensaje, int tocadas = 1, int omitidas = 0) => new(TipoResultado.Correcto, mensaje, tocadas, omitidas);
    public static InformeOperacion Falla(TipoResultado tipo, string mensaje) => new(tipo, mensaje);
}

/// <summary>Una casilla identificada: de donde es (caja, equipo, zona) y que contiene ahora. Las dos acciones Keep y el resto del menu trabajan sobre esto.</summary>
public sealed record CasillaRef(ISlotInfo Info, int Caja = -1)
{
    public StorageSlotType Tipo => Info.Type;
    public bool EsCaja => Info is SlotInfoBox;
    public bool EsEquipo => Info is SlotInfoParty;
    public int Indice => Info.Slot;

    public static CasillaRef Caja_(SaveFile sav, int caja, int indice) => new(new SlotInfoBox(caja, indice, sav), caja);
    public static CasillaRef Equipo(int indice) => new(new SlotInfoParty(indice));

    public string Descripcion => Info switch
    {
        SlotInfoBox b => IdiomaInterfaz.F("EditorCasillas.CajaCasilla", b.Box + 1, b.Slot + 1),
        SlotInfoParty p => IdiomaInterfaz.F("EditorCasillas.EquipoCasilla", p.Slot + 1),
        _ => IdiomaInterfaz.F("EditorCasillas.Casilla", ZonasGuardado.Titulo(Info.Type), Info.Slot + 1),
    };

    public override string ToString() => Descripcion;
}

/// <summary>
/// Operaciones sobre las casillas de UNA partida (ver, colocar, borrar, mover, copiar, clonar a la caja, Deshacer y Rehacer), sobre el <c>SlotEditor</c> y el historial del Core:
/// una entrada de historial por operacion (Deshacer exacto), casillas bloqueadas y de guion respetadas, y los mismos mensajes de bloqueo que <c>ContextMenuSAV.CheckDest</c> de PKHeX.
/// Es la UNICA via por la que la interfaz escribe en el guardado abierto (junto a las acciones del motor, que usan el mismo historial).
/// </summary>
public sealed class EditorCasillas
{
    private readonly Sesion _sesion;

    public EditorCasillas(Sesion sesion)
    {
        _sesion = sesion;
        Historial = new SlotEditor<object>(sesion.Guardado);
        Idioma.Asegurar();
    }

    public SaveFile Guardado => _sesion.Guardado;
    /// <summary>El historial del Core. Lo comparten el editor, el menu de caja y las acciones del motor (Legalizar / Optimizar): UN solo Deshacer coherente.</summary>
    public SlotEditor<object> Historial { get; }

    public bool PuedeDeshacer => Historial.Changelog.CanUndo;
    public bool PuedeRehacer => Historial.Changelog.CanRedo;

    /// <summary>Se dispara tras cada cambio real (colocar, borrar, mover, deshacer...) con las casillas tocadas; la interfaz refresca, cuenta el cambio y guarda la copia de recuperacion.</summary>
    public event Action<TipoCambio, IReadOnlyList<ISlotInfo>>? Cambiado;

    public enum TipoCambio { Escritura, Deshacer, Rehacer, Masivo }

    /// <summary>Una entrada del historial de la sesión tal como la ve el usuario (pantalla «Historial y Deshacer»): qué se hizo, cuándo y sobre cuántas casillas. Refleja EXACTAMENTE la pila de Deshacer del Core.</summary>
    /// <summary>Reloj de las entradas del historial; las pruebas visuales lo fijan para que las referencias no dependan de la hora.</summary>
    public static Func<DateTime> Reloj { get; set; } = () => DateTime.Now;

    public sealed record EntradaHistorial(int Numero, DateTime Hora, string Descripcion, int Casillas)
    {
        public string Accesible => $"{Numero}. {Descripcion}";
    }

    private readonly List<EntradaHistorial> _hechas = [];
    private readonly List<EntradaHistorial> _deshechas = [];
    private int _siguiente = 1;

    /// <summary>Lo hecho en esta sesión, de la más antigua a la más reciente (la última es la que Deshacer revierte).</summary>
    public IReadOnlyList<EntradaHistorial> Hechas => _hechas;
    /// <summary>Lo deshecho que Rehacer puede volver a aplicar, de la más reciente a la más antigua (la primera es la que Rehacer repone).</summary>
    public IReadOnlyList<EntradaHistorial> Deshechas => _deshechas;
    /// <summary>Cuenta de anotaciones (escritura, lote, Deshacer, Rehacer): cambia con CUALQUIER modificación del historial. Las acciones del motor la usan para saber si su entrada sigue siendo la última.</summary>
    public int Version { get; private set; }
    /// <summary>Descripción para la PRÓXIMA anotación que no traiga la suya (la pone quien escribe por una vía externa: el motor, el editor).</summary>
    public string? DescripcionSiguiente { get; set; }

    public static string Describir(ISlotInfo info) => info switch
    {
        SlotInfoBox b => IdiomaInterfaz.F("EditorCasillas.CajaCasilla", b.Box + 1, b.Slot + 1),
        SlotInfoParty p => IdiomaInterfaz.F("EditorCasillas.EquipoCasilla", p.Slot + 1),
        _ => IdiomaInterfaz.F("EditorCasillas.Casilla", ZonasGuardado.Titulo(info.Type), info.Slot + 1),
    };

    private void Anotar(TipoCambio tipo, IReadOnlyList<ISlotInfo> casillas, string? descripcion)
    {
        Version++;
        switch (tipo)
        {
            case TipoCambio.Deshacer:
                if (_hechas.Count > 0) { var e = _hechas[^1]; _hechas.RemoveAt(_hechas.Count - 1); _deshechas.Insert(0, e); }
                break;
            case TipoCambio.Rehacer:
                if (_deshechas.Count > 0) { var e = _deshechas[0]; _deshechas.RemoveAt(0); _hechas.Add(e); }
                break;
            default:
                var texto = descripcion ?? DescripcionSiguiente ?? (casillas.Count switch { 0 => IdiomaInterfaz.T("EditorCasillas.Cambio"), 1 => IdiomaInterfaz.T("EditorCasillas.Cambio2") + Describir(casillas[0]), _ => IdiomaInterfaz.F("EditorCasillas.CambioCasillas", casillas.Count) });
                DescripcionSiguiente = null;
                _hechas.Add(new EntradaHistorial(_siguiente++, Reloj(), texto, casillas.Count));
                _deshechas.Clear();
                break;
        }
        Cambiado?.Invoke(tipo, casillas);
    }

    private static string Resumir(string mensaje) => mensaje.Replace(IdiomaInterfaz.T("EditorCasillas.PuedesDeshacerlo"), string.Empty, StringComparison.Ordinal).Trim();

    // ---- Lectura y comprobaciones ----
    public PKM Leer(ISlotInfo info) => info.Read(Guardado);
    public PKM Leer(CasillaRef c) => c.Info.Read(Guardado);
    public bool EstaVacia(ISlotInfo info) => info.Read(Guardado).Species == 0;
    public bool EsEscribible(ISlotInfo info) => info.CanWriteTo(Guardado);

    /// <summary>Por que NO se puede escribir <paramref name="pk"/> en <paramref name="destino"/> (null si se puede): lo que decide <c>CheckDest</c> de PKHeX mas la regla del equipo.</summary>
    public string? Bloqueo(ISlotInfo destino, PKM pk)
    {
        if (!destino.CanWriteTo(Guardado)) return MessageStrings.MsgSaveSlotLocked;
        switch (destino.CanWriteTo(Guardado, pk))
        {
            case WriteBlockedMessage.InvalidPartyConfiguration: return MessageStrings.MsgSaveSlotEmpty;
            case WriteBlockedMessage.InvalidDestination: return MessageStrings.MsgSaveSlotLocked;
            case WriteBlockedMessage.IncompatibleFormat: return MessageStrings.MsgSaveSlotBadData;
        }
        return null;
    }

    /// <summary>Avisos de compatibilidad del Core antes de escribir (<c>EvaluateCompatibility</c>): por ejemplo, un Pokemon de otra generacion o con datos que el formato recorta. Lista vacia si no hay ninguno.</summary>
    public IReadOnlyList<string> Compatibilidad(PKM pk) => Guardado.EvaluateCompatibility(pk, Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma);

    /// <summary>
    /// ¿Quitar el Pokemon de esta casilla del equipo deja el equipo vacio o solo con huevos? PKHeX solo comprueba lo que se COLOCA; aqui se comprueba tambien lo que se QUITA
    /// (un equipo sin Pokemon capaces de combatir no es valido en el juego).
    /// </summary>
    private bool QuitarDejaEquipoInvalido(ISlotInfo origen)
    {
        if (origen is not SlotInfoParty p) return false;
        var restantes = 0;
        for (var i = 0; i < Math.Min(6, Guardado.PartyCount); i++)
        {
            if (i == p.Slot) continue;
            var otro = Guardado.GetPartySlotAtIndex(i);
            if (otro.Species != 0 && !otro.IsEgg) restantes++;
        }
        // Si el que sale ya era un huevo, el equipo no pierde ningun Pokemon capaz de combatir.
        return restantes == 0 && !Guardado.GetPartySlotAtIndex(p.Slot).IsEgg;
    }

    /// <summary>Hay algun bloqueo que impida QUITAR el Pokemon de la casilla (casilla bloqueada o equipo que quedaria invalido). Null si se puede.</summary>
    public string? BloqueoAlQuitar(ISlotInfo origen)
    {
        if (!origen.CanWriteTo(Guardado)) return MessageStrings.MsgSaveSlotLocked;
        if (QuitarDejaEquipoInvalido(origen)) return MessageStrings.MsgSaveSlotEmpty;
        return null;
    }

    // ---- Escritura ----
    private InformeOperacion Terminar(TipoCambio tipo, IReadOnlyList<ISlotInfo> casillas, InformeOperacion informe)
    {
        if (informe.Correcto) Anotar(tipo, casillas, casillas.Count == 1 ? $"{Resumir(informe.Mensaje).TrimEnd('.')} · {Describir(casillas[0])}" : casillas.Count > 1 ? IdiomaInterfaz.F("EditorCasillas.NCasillas", Resumir(informe.Mensaje).TrimEnd('.'), casillas.Count) : Resumir(informe.Mensaje));
        return informe;
    }

    /// <summary>«Colocar aqui»: escribe <paramref name="pk"/> en la casilla (un Deshacer).</summary>
    public InformeOperacion Colocar(ISlotInfo destino, PKM pk)
    {
        if (Bloqueo(destino, pk) is { } motivo) return InformeOperacion.Falla(TipoResultado.Bloqueada, motivo);
        var r = Historial.Set(destino, pk.Clone(), SlotTouchType.Swap);   // PKHeX 26.10.18: con el tipo Set por defecto importaria al guardado y Deshacer no devolveria la partida byte a byte (igual que AutoLegalidad.Ui.SlotWrites.Exact; esta capa no referencia el nucleo)
        return r == SlotTouchResult.Success ? Terminar(TipoCambio.Escritura, [destino], InformeOperacion.Ok(IdiomaInterfaz.T("EditorCasillas.PokemonColocado"))) : InformeOperacion.Falla(TipoResultado.Fallo, MessageStrings.MsgSaveSlotBadData);
    }

    /// <summary>Borra la casilla. Un equipo no puede quedarse vacio ni solo con huevos; una casilla bloqueada no se toca.</summary>
    public InformeOperacion Borrar(ISlotInfo origen)
    {
        if (EstaVacia(origen)) return InformeOperacion.Falla(TipoResultado.SinCambios, IdiomaInterfaz.T("EditorCasillas.CasillaVacia"));
        if (BloqueoAlQuitar(origen) is { } motivo) return InformeOperacion.Falla(origen.CanWriteTo(Guardado) ? TipoResultado.EquipoInvalido : TipoResultado.Bloqueada, motivo);
        var r = Historial.Delete(origen);
        return r == SlotTouchResult.Success ? Terminar(TipoCambio.Escritura, [origen], InformeOperacion.Ok(IdiomaInterfaz.T("EditorCasillas.PokemonBorradoPuedesDeshacerlo"))) : InformeOperacion.Falla(TipoResultado.Fallo, MessageStrings.MsgSaveSlotBadData);
    }

    /// <summary>Borra varias casillas como UNA operacion (un solo Deshacer). Las bloqueadas y las que dejarian el equipo invalido se omiten y se cuentan.</summary>
    public InformeOperacion Borrar(IReadOnlyList<ISlotInfo> origenes)
    {
        var validas = new List<ISlotInfo>();
        var omitidas = 0;
        // El equipo se comprueba en conjunto: no se pueden borrar todos los Pokemon capaces de combatir.
        var party = origenes.OfType<SlotInfoParty>().ToList();
        var capacesQueQuedan = 0;
        for (var i = 0; i < Math.Min(6, Guardado.PartyCount); i++)
        {
            var pk = Guardado.GetPartySlotAtIndex(i);
            if (pk.Species != 0 && !pk.IsEgg && party.All(x => x.Slot != i)) capacesQueQuedan++;
        }
        var protegerEquipo = party.Count > 0 && capacesQueQuedan == 0 && Guardado.PartyCount > 0;
        foreach (var o in origenes)
        {
            if (EstaVacia(o) || !o.CanWriteTo(Guardado)) { if (!EstaVacia(o)) omitidas++; continue; }
            if (protegerEquipo && o is SlotInfoParty && !Guardado.GetPartySlotAtIndex(o.Slot).IsEgg) { omitidas++; continue; }
            validas.Add(o);
        }
        if (validas.Count == 0) return InformeOperacion.Falla(omitidas > 0 ? TipoResultado.Bloqueada : TipoResultado.SinCambios, omitidas > 0 ? MessageStrings.MsgSaveSlotLocked : IdiomaInterfaz.T("EditorCasillas.NadaBorrar"));
        // El equipo se reescribe entero en cada paso: se borra de mayor a menor indice para que los huecos no muevan los que faltan.
        var ordenadas = validas.OrderByDescending(v => v is SlotInfoParty ? v.Slot : -1).ToList();
        var ok = Historial.Batch(ordenadas, lista => { foreach (var s in lista) s.WriteTo(Guardado, Guardado.BlankPKM, EntityImportSettings.None); });
        if (!ok) return InformeOperacion.Falla(TipoResultado.Fallo, MessageStrings.MsgSaveSlotLocked);
        return Terminar(TipoCambio.Escritura, ordenadas, InformeOperacion.Ok(validas.Count == 1 ? IdiomaInterfaz.T("EditorCasillas.PokemonBorradoPuedesDeshacerlo") : IdiomaInterfaz.F("EditorCasillas.PokemonBorradosPuedesDeshacerlo", validas.Count), validas.Count, omitidas));
    }

    /// <summary>
    /// Las comprobaciones de <see cref="Mover"/> sin escribir nada: null si se puede mover, copiar o intercambiar de <paramref name="origen"/> a <paramref name="destino"/>, o el informe de fallo que daria. Las usa tambien el arrastre
    /// (X10) para pintar en rojo, antes de soltar, la casilla donde no se puede; asi lo que se promete mientras se arrastra y lo que pasa al soltar salen de la MISMA regla.
    /// </summary>
    public InformeOperacion? ComprobarMover(ISlotInfo origen, ISlotInfo destino, ModoMover modo, out PKM pk, out PKM otro, out bool intercambia)
    {
        pk = origen.Read(Guardado);
        otro = destino.Read(Guardado);
        intercambia = false;
        if (origen.Type == destino.Type && origen.Slot == destino.Slot && SonLaMisma(origen, destino)) return InformeOperacion.Falla(TipoResultado.SinCambios, IdiomaInterfaz.T("EditorCasillas.OrigenDestinoSonMisma"));
        if (pk.Species == 0) return InformeOperacion.Falla(TipoResultado.SinCambios, IdiomaInterfaz.T("EditorCasillas.CasillaOrigenVacia"));
        intercambia = modo == ModoMover.MoverOIntercambiar && otro.Species != 0;

        if (Bloqueo(destino, pk) is { } motivo) return InformeOperacion.Falla(TipoResultado.Bloqueada, motivo);
        if (modo != ModoMover.Copiar)
        {
            if (!origen.CanWriteTo(Guardado)) return InformeOperacion.Falla(TipoResultado.Bloqueada, MessageStrings.MsgSaveSlotLocked);
            if (intercambia && Bloqueo(origen, otro) is { } m2) return InformeOperacion.Falla(TipoResultado.Bloqueada, m2);
            // Sin intercambio el origen queda vacio: no puede dejar el equipo invalido (salvo que el destino sea del mismo equipo).
            if (!intercambia && !(origen is SlotInfoParty && destino is SlotInfoParty) && BloqueoAlQuitar(origen) is { } m3) return InformeOperacion.Falla(TipoResultado.EquipoInvalido, m3);
        }
        return null;
    }

    /// <summary>Mueve, intercambia o copia de <paramref name="origen"/> a <paramref name="destino"/> (ver <see cref="ModoMover"/>), como UNA operacion.</summary>
    public InformeOperacion Mover(ISlotInfo origen, ISlotInfo destino, ModoMover modo = ModoMover.MoverOIntercambiar)
    {
        if (ComprobarMover(origen, destino, modo, out var pk, out var otro, out var intercambia) is { } fallo) return fallo;

        var copia = pk.Clone();
        var ok = Historial.Batch(modo == ModoMover.Copiar ? [destino] : [origen, destino], _ =>
        {
            destino.WriteTo(Guardado, copia, EntityImportSettings.None);
            if (modo == ModoMover.Copiar) return;
            if (intercambia) origen.WriteTo(Guardado, otro.Clone(), EntityImportSettings.None);
            else origen.WriteTo(Guardado, Guardado.BlankPKM, EntityImportSettings.None);
        });
        if (!ok) return InformeOperacion.Falla(TipoResultado.Bloqueada, MessageStrings.MsgSaveSlotLocked);
        var texto = modo switch
        {
            ModoMover.Copiar => IdiomaInterfaz.T("EditorCasillas.PokemonCopiado"),
            ModoMover.Sobrescribir => IdiomaInterfaz.T("EditorCasillas.PokemonMovidoDestinoSobrescrito"),
            _ => intercambia ? IdiomaInterfaz.T("EditorCasillas.PokemonIntercambiados") : IdiomaInterfaz.T("EditorCasillas.PokemonMovido"),
        };
        return Terminar(TipoCambio.Escritura, modo == ModoMover.Copiar ? [destino] : [origen, destino], InformeOperacion.Ok(texto + IdiomaInterfaz.T("EditorCasillas.PuedesDeshacerlo"), modo == ModoMover.Copiar || !intercambia ? 1 : 2));
    }

    private static bool SonLaMisma(ISlotInfo a, ISlotInfo b) => (a, b) switch
    {
        (SlotInfoBox x, SlotInfoBox y) => x.Box == y.Box && x.Slot == y.Slot,
        (SlotInfoParty x, SlotInfoParty y) => x.Slot == y.Slot,
        _ => ReferenceEquals(a, b) || a.Equals(b),
    };

    /// <summary>Mueve VARIOS Pokemon a casillas libres consecutivas de la caja <paramref name="caja"/> (y siguientes), como UNA operacion. Solo casillas vacias: nunca pisa nada.</summary>
    public InformeOperacion MoverAHuecos(IReadOnlyList<ISlotInfo> origenes, int cajaDestino, bool copiar)
    {
        var sav = Guardado;
        if (!sav.HasBox) return InformeOperacion.Falla(TipoResultado.NoAdmitido, MessageStrings.MsgSaveBoxFailNone);
        var items = origenes.Select(o => (Info: o, Pk: o.Read(sav))).Where(x => x.Pk.Species != 0).ToList();
        if (items.Count == 0) return InformeOperacion.Falla(TipoResultado.SinCambios, IdiomaInterfaz.T("EditorCasillas.PokemonMover"));
        var huecos = new List<ISlotInfo>();
        for (var b = 0; b < sav.BoxCount && huecos.Count < items.Count; b++)
        {
            var caja = (cajaDestino + b) % sav.BoxCount;
            for (var s = 0; s < sav.BoxSlotCount && huecos.Count < items.Count; s++)
            {
                var info = new SlotInfoBox(caja, s, sav);
                if (!info.CanWriteTo(sav) || info.Read(sav).Species != 0) continue;
                if (items.Any(x => SonLaMisma(x.Info, info))) continue;
                huecos.Add(info);
            }
        }
        if (huecos.Count < items.Count) return InformeOperacion.Falla(TipoResultado.NoAdmitido, IdiomaInterfaz.F("EditorCasillas.SitioHacenFaltaCasillas", items.Count, huecos.Count));
        if (!copiar)
        {
            // Casillas bloqueadas, y el equipo en CONJUNTO (comprobar una a una dejaba pasar mover todos los Pokemon capaces de combatir).
            foreach (var x in items) if (!x.Info.CanWriteTo(sav)) return InformeOperacion.Falla(TipoResultado.Bloqueada, MessageStrings.MsgSaveSlotLocked);
            var saliendo = items.Where(x => x.Info is SlotInfoParty).Select(x => x.Info.Slot).ToHashSet();
            if (saliendo.Count > 0)
            {
                var quedan = 0;
                for (var i = 0; i < Math.Min(6, sav.PartyCount); i++) { var o = sav.GetPartySlotAtIndex(i); if (o.Species != 0 && !o.IsEgg && !saliendo.Contains(i)) quedan++; }
                var salenCapaces = saliendo.Any(i => !sav.GetPartySlotAtIndex(i).IsEgg);
                if (quedan == 0 && salenCapaces) return InformeOperacion.Falla(TipoResultado.EquipoInvalido, MessageStrings.MsgSaveSlotEmpty);
            }
        }
        var afectadas = copiar ? huecos : [.. items.Select(x => x.Info), .. huecos];
        var ok = Historial.Batch(afectadas, _ =>
        {
            for (var i = 0; i < items.Count; i++) huecos[i].WriteTo(sav, items[i].Pk.Clone(), EntityImportSettings.None);
            if (copiar) return;
            // Primero los de mayor indice de equipo, para que el equipo no se recoloque bajo los pies.
            foreach (var x in items.OrderByDescending(v => v.Info is SlotInfoParty ? v.Info.Slot : -1)) x.Info.WriteTo(sav, sav.BlankPKM, EntityImportSettings.None);
        });
        if (!ok) return InformeOperacion.Falla(TipoResultado.Bloqueada, MessageStrings.MsgSaveSlotLocked);
        return Terminar(TipoCambio.Escritura, afectadas, InformeOperacion.Ok(IdiomaInterfaz.F(copiar ? "EditorCasillas.CopiadosDeshacer" : "EditorCasillas.MovidosDeshacer", items.Count), items.Count));
    }

    /// <summary>«Clonar a la caja»: escribe <paramref name="pk"/> en TODAS las casillas de la caja (<c>SetClonesToCurrentBox</c>); las bloqueadas se saltan y se cuentan. Un Deshacer.</summary>
    public InformeOperacion ClonarACaja(PKM pk, int caja)
    {
        var sav = Guardado;
        if (!sav.HasBox) return InformeOperacion.Falla(TipoResultado.NoAdmitido, MessageStrings.MsgSaveBoxFailNone);
        if (pk.Species == 0) return InformeOperacion.Falla(TipoResultado.SinCambios, IdiomaInterfaz.T("EditorCasillas.PokemonClonar"));
        var todas = Enumerable.Range(0, sav.BoxSlotCount).Select(i => (ISlotInfo)new SlotInfoBox(caja, i, sav)).ToList();
        var libres = todas.Where(s => !sav.IsBoxSlotOverwriteProtected(caja, s.Slot) && s.CanWriteTo(sav)).ToList();
        var omitidas = todas.Count - libres.Count;
        if (libres.Count == 0) return InformeOperacion.Falla(TipoResultado.Bloqueada, MessageStrings.MsgSaveSlotLocked);
        var ok = Historial.Batch(libres, lista => { foreach (var s in lista) s.WriteTo(sav, pk.Clone(), EntityImportSettings.None); });
        if (!ok) return InformeOperacion.Falla(TipoResultado.Bloqueada, MessageStrings.MsgSaveSlotLocked);
        var msg = omitidas > 0 ? string.Format(MessageStrings.MsgSaveBoxImportSkippedLocked, omitidas) : IdiomaInterfaz.T("EditorCasillas.PokemonClonadoTodaCaja");
        return Terminar(TipoCambio.Escritura, libres, InformeOperacion.Ok(msg + IdiomaInterfaz.T("EditorCasillas.PuedesDeshacerlo"), libres.Count, omitidas));
    }

    // ---- Operaciones masivas con un solo Deshacer (menu de caja, importar) ----
    /// <summary>
    /// Ejecuta <paramref name="accion"/> (que modifica el guardado directamente, como <c>BoxManipulator</c>; devuelve cuantas casillas toco) como UNA entrada del historial sobre <paramref name="casillas"/>.
    /// A diferencia de <c>SlotEditor.Batch</c> la reversion tambien guarda los punteros de equipos de batalla de las cajas (Gen. 7-9 y Let's Go), que las ordenaciones del Core reordenan: Deshacer es EXACTO.
    /// Si la accion no toca nada no queda entrada en el historial; si lanza, todo vuelve a como estaba.
    /// </summary>
    public int Lote(IReadOnlyList<ISlotInfo> casillas, Func<int> accion, bool notificar = true, string? descripcion = null)
    {
        var reversion = new ReversionLote(Guardado, casillas);
        using var cambio = new SlotChangelog.Change(Historial.Changelog, reversion);
        var tocadas = accion();
        if (tocadas <= 0) { cambio.Cancel(); return tocadas; }
        cambio.Commit();
        if (notificar) Anotar(TipoCambio.Masivo, casillas, descripcion);
        else if (descripcion is not null) DescripcionSiguiente = descripcion;
        return tocadas;
    }

    // ---- Deshacer / Rehacer ----
    public InformeOperacion Deshacer()
    {
        if (!PuedeDeshacer) return InformeOperacion.Falla(TipoResultado.SinCambios, IdiomaInterfaz.T("EditorCasillas.NadaDeshacer"));
        IReadOnlyList<ISlotInfo> tocadas = [];
        Historial.Undo();
        tocadas = Historial.Publisher.Previous is { } p ? [p] : [];
        Anotar(TipoCambio.Deshacer, tocadas, null);
        return InformeOperacion.Ok(IdiomaInterfaz.T("EditorCasillas.UltimoCambioDeshecho"));
    }

    public InformeOperacion Rehacer()
    {
        if (!PuedeRehacer) return InformeOperacion.Falla(TipoResultado.SinCambios, IdiomaInterfaz.T("EditorCasillas.NadaRehacer"));
        Historial.Redo();
        IReadOnlyList<ISlotInfo> tocadas = Historial.Publisher.Previous is { } p ? [p] : [];
        Anotar(TipoCambio.Rehacer, tocadas, null);
        return InformeOperacion.Ok(IdiomaInterfaz.T("EditorCasillas.CambioRehecho"));
    }

    /// <summary>Avisa de un cambio hecho por otra via que comparte el historial (el editor o una accion del motor): refresca las vistas y cuenta el cambio.</summary>
    public void Notificar(TipoCambio tipo, IReadOnlyList<ISlotInfo> casillas, string? descripcion = null) => Anotar(tipo, casillas, descripcion);
}

/// <summary>Textos de PKHeX en castellano para los mensajes del Core (<c>MessageStrings</c>): se cargan una vez.</summary>
internal static class Idioma
{
    /// <summary>Delega en <see cref="Ajustes.Pkhex.TextosNucleo.Preparar"/>: UN solo mecanismo y UNA sola bandera para los mensajes del Core en castellano.</summary>
    public static void Asegurar() => Ajustes.Pkhex.TextosNucleo.Preparar();
}
