// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;
using SesionM = Pokekeep.Mobile.Ui.Modelo.Sesion;

namespace Pokekeep.Mobile.Ui.Partida;

/// <summary>
/// Regalos misteriosos (<c>SAV_Wondercard</c>) sin pantalla: el album de la partida (PGT/PCD de Gen. 4, WC5, WC6, WC7, WR7 y WB7), su borrador, las tarjetas recibidas y las operaciones del formulario
/// (colocar con las mismas comprobaciones, borrar con el relleno de huecos, mover, marcar usadas, quitar y anadir recibidas). Escribe SOLO al aplicar, con las mismas llamadas y el mismo orden de <c>Save()</c>.
/// </summary>
public sealed class ModeloRegalos
{
    private readonly SesionM _sesion;
    private readonly SaveFile _sav;
    private readonly IMysteryGiftStorage _tarjetas;
    private readonly IMysteryGiftFlags? _banderas;
    private byte[][] _originales = [];
    private SortedSet<int> _recibidasOriginales = [];

    public ModeloRegalos(SesionM sesion)
    {
        _sesion = sesion; _sav = sesion.Guardado;
        _tarjetas = ((IMysteryGiftStorageProvider)_sav).MysteryGiftStorage;
        _banderas = _tarjetas as IMysteryGiftFlags;
        Cargar();
    }

    public DataMysteryGift[] Album { get; private set; } = [];
    public SortedSet<int> Recibidas { get; private set; } = [];
    public SaveFile Guardado => _sav;
    public bool TieneBanderas => _banderas is not null;
    public int MaximoBanderas => _banderas?.MysteryGiftReceivedFlagMax ?? 0;
    public int Generacion => _sav.Generation;
    public bool TieneCapsula => _sav is SAV4HGSS;

    /// <summary>Por que este juego NO tiene album editable (null si lo tiene). Solo Gen. 4 a 7 (incl. Let's Go) implementan <c>IMysteryGiftStorageProvider</c> en el Core.</summary>
    public static string? NoAplica(SaveFile sav)
        => Aplica(sav) ? null : sav.Generation >= 8 || sav is SAV7b ? IdiomaInterfaz.T("Partida.NoAplica.RegalosNoEditables") : sav.Generation <= 3 ? IdiomaInterfaz.T("Partida.NoAplica.RegalosGeneracion") : IdiomaInterfaz.T("Partida.NoAplica.Regalos");

    public static bool Aplica(SaveFile sav) => sav is IMysteryGiftStorageProvider && sav.Generation is >= 4 and <= 7;

    private void Cargar()
    {
        var count = _tarjetas.GiftCountMax;
        var size = _sav is SAV4HGSS ? count + 1 : count;
        var album = new DataMysteryGift[size];
        for (var i = 0; i < count; i++) album[i] = _tarjetas.GetMysteryGift(i);
        if (_sav is SAV4HGSS s4) album[^1] = s4.LockCapsuleSlot;
        Album = album;
        _originales = [.. album.Select(g => g.Write().ToArray())];
        Recibidas = [];
        if (_banderas is { } f)
            for (var i = 1; i < f.MysteryGiftReceivedFlagMax; i++)
                if (f.GetMysteryGiftReceivedFlag(i)) Recibidas.Add(i);
        _recibidasOriginales = [.. Recibidas];
    }

    public void Recargar() => Cargar();

    public bool HayCambios => Recibidas.Count != _recibidasOriginales.Count || !Recibidas.SetEquals(_recibidasOriginales)
        || Album.Where((g, i) => !g.Write().SequenceEqual(_originales[i])).Any();

    public bool Modificada(int indice) => !Album[indice].Write().SequenceEqual(_originales[indice]);

    // ---- Etiquetas de ranuras ----
    public string EtiquetaRanura(int i)
    {
        if (_sav.Generation == 4)
        {
            if (i < 8) return $"PGT {i + 1}";
            if (i < 11) return $"PCD {i - 7}";
            return IdiomaInterfaz.T("ModeloRegalos.CapsulaCierre");
        }
        return IdiomaInterfaz.F("ModeloRegalos.Ranura", i + 1);
    }

    public string Grupo(int i) => _sav.Generation == 4 ? (i < 8 ? IdiomaInterfaz.F("ModeloRegalos.RegalosPGT", NombreReloj.Oficial) : i < 11 ? IdiomaInterfaz.T("ModeloRegalos.RegalosGrandesPCD") : IdiomaInterfaz.T("ModeloRegalos.CapsulaCierre")) : IdiomaInterfaz.F("ModeloRegalos.Ranuras", (i / 6) * 6 + 1, Math.Min(Album.Length, (i / 6) * 6 + 6));

    // ---- Operaciones del formulario ----
    /// <summary>Primera ranura vacia del mismo tipo (<c>GetLastUnfilledByType</c>): las tarjetas se guardan sin huecos intermedios.</summary>
    public static int PrimeraVaciaDelTipo(DataMysteryGift regalo, IReadOnlyList<DataMysteryGift> album)
    {
        for (var i = 0; i < album.Count; i++)
        {
            var e = album[i];
            if (!e.IsEmpty) continue;
            if (e.Type != regalo.Type) continue;
            return i;
        }
        return -1;
    }

    /// <summary>Por que NO se puede colocar la tarjeta en esa ranura (null si se puede), con los mismos controles que <c>ClickSet</c>.</summary>
    public string? Motivo(DataMysteryGift regalo, int indice)
    {
        if (!regalo.IsCardCompatible(_sav, out var msg)) return $"{MessageStrings.MsgMysteryGiftSlotFail} {msg}".Trim();
        var (_, _, motivo) = Destino(regalo, indice);
        return motivo;
    }

    private (int Indice, DataMysteryGift Regalo, string? Motivo) Destino(DataMysteryGift regalo, int indice)
    {
        var ultimoVacio = PrimeraVaciaDelTipo(regalo, Album);
        if (ultimoVacio > -1 && ultimoVacio < indice) indice = ultimoVacio;
        if (regalo is PCD { IsLockCapsule: true }) indice = 11;
        if (indice >= Album.Length) return (indice, regalo, IdiomaInterfaz.T("ModeloRegalos.TarjetaTieneRanuraJuego"));
        var otro = Album[indice];
        if (regalo is PCD { CanConvertToPGT: true } pcd && otro is PGT) return (indice, pcd.Gift, null);
        if (regalo.Type != otro.Type) return (indice, regalo, $"{MessageStrings.MsgMysteryGiftSlotFail} {regalo.Type} != {otro.Type}");
        if (regalo is PCD g && ((g is { IsLockCapsule: true }) != (indice == 11))) return (indice, regalo, IdiomaInterfaz.F("ModeloRegalos.RanuraCapsulaCierreValida", MessageStrings.MsgMysteryGiftSlotFail));
        return (indice, regalo, null);
    }

    /// <summary>Coloca la tarjeta (<c>ClickSet</c>): devuelve la ranura realmente usada o el motivo del rechazo.</summary>
    public (int Indice, string? Rechazo) Colocar(DataMysteryGift regalo, int indice)
    {
        if (!regalo.IsCardCompatible(_sav, out var msg)) return (-1, $"{MessageStrings.MsgMysteryGiftSlotFail} {msg}".Trim());
        var (idx, g, motivo) = Destino(regalo, indice);
        if (motivo is not null) return (-1, motivo);
        Album[idx] = g.Clone();
        if (_banderas is { } f && (uint)g.CardID < f.MysteryGiftReceivedFlagMax) Recibidas.Add(g.CardID);
        return (idx, null);
    }

    /// <summary>Borra la ranura y baja el hueco al final de su lista (<c>ClickDelete</c>); devuelve la ultima ranura tocada.</summary>
    public int Borrar(int indice)
    {
        Album[indice].Clear();
        var i = indice;
        while (i < Album.Length - 1)
        {
            if (Album[i + 1].IsEmpty) break;
            if (Album[i + 1].Type != Album[i].Type) break;
            i++;
            (Album[i - 1], Album[i]) = (Album[i], Album[i - 1]);
        }
        return i;
    }

    /// <summary>Intercambia o mueve (<c>SwapSlots</c>): devuelve la ranura de destino o el motivo del rechazo.</summary>
    public (int Indice, string? Rechazo) Mover(int destino, int origen)
    {
        var g = Album;
        var s1 = g[destino]; var s2 = g[origen];
        if (s1.Type != s2.Type)
        {
            if (s2 is PCD { CanConvertToPGT: true } && s1 is PGT)
            {
                var primera = Array.FindIndex(g, static z => z.IsEmpty);
                if ((uint)primera < destino) destino = primera;
                var (idx, rechazo) = Colocar(s2, destino);
                return (idx, rechazo ?? string.Format(MessageStrings.MsgMysteryGiftSlotAlternate, s2.Type, s1.Type));
            }
            return (-1, string.Format(MessageStrings.MsgMysteryGiftSlotFailSwap, s2.Type, s1.Type));
        }
        if ((s1 is PCD && destino == 11) || (s2 is PCD && origen == 11)) return (-1, IdiomaInterfaz.F("ModeloRegalos.CambioCapsulaCierreValido", MessageStrings.MsgMysteryGiftSlotFail));
        if (!s1.IsEmpty) { (g[origen], g[destino]) = (s1, s2); return (destino, null); }
        for (var i = origen; i != destino; i++)
        {
            if (g[i + 1].IsEmpty) return (i, null);
            (g[i + 1], g[i]) = (g[i], g[i + 1]);
        }
        return (destino, null);
    }

    /// <summary>Marca todas las tarjetas como usadas o sin usar (<c>B_ModifyAll_Click</c>). WR7 no tiene ese dato.</summary>
    public void MarcarTodas(bool usadas) { foreach (var g in Album) g.GiftUsed = usadas; }
    public bool AdmiteUsada => Album.Length > 0 && Album[0] is not WR7;

    public bool QuitarRecibida(int id) => Recibidas.Remove(id);
    public string? AnadirRecibida(int id)
    {
        if (_banderas is null) return IdiomaInterfaz.T("ModeloRegalos.JuegoGuardaListaTarjetas");
        if (id <= 0 || id >= _banderas.MysteryGiftReceivedFlagMax) return IdiomaInterfaz.F("ModeloRegalos.NumeroDebeEstarEntre", _banderas.MysteryGiftReceivedFlagMax - 1);
        Recibidas.Add(id);
        return null;
    }

    // ---- Aplicar ----
    /// <summary>Escribe lo mismo que <c>SaveReceivedFlags</c> y <c>SaveReceivedCards</c> de PKHeX, en su orden, como una entrada del historial.</summary>
    public InformePartida Aplicar()
    {
        if (!HayCambios) return new InformePartida(ResultadoPartida.SinCambios, string.Empty, 0);
        var nuevas = Album.Count(g => !g.IsEmpty);
        var inf = EscrituraPartida.Aplicar(_sesion, IdiomaInterfaz.F("ModeloRegalos.AplicarResumen", IdiomaInterfaz.P("ModeloRegalos.Tarjetas", nuevas), IdiomaInterfaz.P("ModeloRegalos.Recibidas", Recibidas.Count)), () =>
        {
            if (_banderas is { } f)
            {
                f.ClearReceivedFlags();
                foreach (var i in Recibidas) f.SetMysteryGiftReceivedFlag(i, true);
            }
            if (_tarjetas is MysteryBlock4 s4)
            {
                s4.IsDeliveryManActive = Album.Any(g => !g.IsEmpty);
                MysteryBlock4.UpdateSlotPGT(Album, _sav is SAV4HGSS);
                if (_sav is SAV4HGSS hgss) hgss.LockCapsuleSlot = (PCD)Album[^1];
            }
            var count = _tarjetas.GiftCountMax;
            for (var i = 0; i < count; i++) _tarjetas.SetMysteryGift(i, Album[i]);
            if (_tarjetas is MysteryBlock5 s5) s5.EndAccess();
        });
        Cargar();
        return inf;
    }
}
