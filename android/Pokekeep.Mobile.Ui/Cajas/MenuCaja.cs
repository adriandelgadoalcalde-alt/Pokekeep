// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Modelo;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Cajas;

public enum CategoriaCaja { Limpiar, Ordenar, OrdenarAvanzado, Modificar }

/// <summary>Una entrada del menu de caja (<c>BoxMenuStrip</c>): la manipulacion del Core, su nombre en castellano (el de PKHeX) y que hace en una linea.</summary>
public sealed record EntradaManip(IBoxManip Manip, CategoriaCaja Categoria, string Titulo, string Descripcion, bool Destructiva)
{
    public BoxManipType Tipo => Manip.Type;
}

/// <summary>Resultado de aplicar una manipulacion: ademas del informe, a que cajas afecto.</summary>
public sealed record ResultadoManip(InformeOperacion Informe, int CajaInicio, int CajaFin);

/// <summary>
/// El menu de caja de PKHeX (<c>BoxMenuStrip</c>): TODAS las manipulaciones de <see cref="BoxManipUtil.ManipCategories"/> (limpiar, ordenar, ordenar avanzado y modificar), visibles solo cuando el guardado las admite
/// (<c>IBoxManip.Usable</c>), aplicadas con el MISMO <see cref="IBoxManip.Execute"/> del Core sobre la caja actual o todas, normales o invertidas, y como UNA entrada del historial (un Deshacer exacto, que PKHeX no tiene).
/// </summary>
public sealed class MenuCaja(EditorCasillas editor)
{
    private SaveFile Sav => editor.Guardado;

    /// <summary>Textos de PKHeX en castellano (<c>lang_es.txt</c>, entradas <c>mnu_*</c> de BoxMenuStrip) y una explicacion de una linea.</summary>
    private static Dictionary<BoxManipType, (string Titulo, string Descripcion)> Textos => new()
    {
        [BoxManipType.DeleteAll] = (IdiomaInterfaz.T("Form.Limpiar"), IdiomaInterfaz.T("MenuCaja.VaciaTodasCasillasEsten")),
        [BoxManipType.DeleteEggs] = (IdiomaInterfaz.T("MenuCaja.Huevos"), IdiomaInterfaz.T("MenuCaja.BorraHuevos")),
        [BoxManipType.DeletePastGen] = (IdiomaInterfaz.T("MenuCaja.GeneracionPasada"), IdiomaInterfaz.T("MenuCaja.BorraPokemonVienenGeneraciones")),
        [BoxManipType.DeleteForeign] = ("Extranjero", IdiomaInterfaz.T("MenuCaja.BorraPokemonCuyoEntrenador")),
        [BoxManipType.DeleteUntrained] = (IdiomaInterfaz.T("MenuCaja.Entrenamiento"), IdiomaInterfaz.T("MenuCaja.BorraPokemonEVEntrenados")),
        [BoxManipType.DeleteItemless] = (IdiomaInterfaz.T("MenuCaja.ObjetoEquipado"), IdiomaInterfaz.T("MenuCaja.BorraPokemonLlevanObjeto")),
        [BoxManipType.DeleteIllegal] = (IdiomaInterfaz.T("MenuCaja.Ilegal"), IdiomaInterfaz.T("MenuCaja.BorraPokemonAnalisisLegalidad")),
        [BoxManipType.DeleteClones] = ("Clones", IdiomaInterfaz.T("MenuCaja.BorraPokemonRepetidosMismos")),

        [BoxManipType.SortSpecies] = (IdiomaInterfaz.T("MenuCaja.NPokedex"), IdiomaInterfaz.T("MenuCaja.MenorMayorNumeroPokedex")),
        [BoxManipType.SortSpeciesReverse] = (IdiomaInterfaz.T("MenuCaja.NPokedexInverso"), IdiomaInterfaz.T("MenuCaja.MayorMenorNumeroPokedex")),
        [BoxManipType.SortLevel] = (IdiomaInterfaz.T("MenuCaja.NivelBajoAlto"), IdiomaInterfaz.T("MenuCaja.NivelMenorMayor")),
        [BoxManipType.SortLevelReverse] = (IdiomaInterfaz.T("MenuCaja.NivelAltoBajo"), IdiomaInterfaz.T("MenuCaja.NivelMayorMenor")),
        [BoxManipType.SortDate] = (IdiomaInterfaz.T("MenuCaja.FechaEncuentro"), IdiomaInterfaz.T("MenuCaja.FechaObtuvo")),
        [BoxManipType.SortName] = (IdiomaInterfaz.T("MenuCaja.NombreEspecie"), IdiomaInterfaz.T("MenuCaja.OrdenAlfabeticoNombreEspecie")),
        [BoxManipType.SortFavorite] = (IdiomaInterfaz.T("MenuCaja.Favoritos"), IdiomaInterfaz.T("MenuCaja.FavoritosPrimero")),
        [BoxManipType.SortParty] = (IdiomaInterfaz.T("Form.Equipo"), IdiomaInterfaz.T("MenuCaja.SubePrincipioPokemonEstan")),
        [BoxManipType.SortShiny] = (IdiomaInterfaz.T("Catalogo.Variocolor"), IdiomaInterfaz.T("MenuCaja.VariocolorPrimero")),
        [BoxManipType.SortAlpha] = (IdiomaInterfaz.T("Catalogo.Alfa"), IdiomaInterfaz.T("MenuCaja.PokemonAlfaPrimero")),
        [BoxManipType.SortRandom] = (IdiomaInterfaz.T("MenuCaja.Aleatorio"), IdiomaInterfaz.T("MenuCaja.OrdenAzar")),

        [BoxManipType.SortUsage] = ("Uso", IdiomaInterfaz.T("MenuCaja.UsoCombateCompetitivo")),
        [BoxManipType.SortPotential] = (IdiomaInterfaz.T("MenuCaja.PotencialIVs"), IdiomaInterfaz.T("MenuCaja.SumaIVMayorMenor")),
        [BoxManipType.SortTraining] = (IdiomaInterfaz.T("MenuCaja.EntrenamientoEVs"), IdiomaInterfaz.T("MenuCaja.SumaEVMayorMenor")),
        [BoxManipType.SortOwner] = (IdiomaInterfaz.T("MenuCaja.Propiedad"), IdiomaInterfaz.T("MenuCaja.AgrupaEntrenadorOriginal")),
        [BoxManipType.SortType] = (IdiomaInterfaz.T("MenuCaja.Tipo2"), IdiomaInterfaz.T("MenuCaja.Tipo")),
        [BoxManipType.SortTypeTera] = (IdiomaInterfaz.T("Catalogo.Teratipo"), IdiomaInterfaz.T("MenuCaja.Teratipo")),
        [BoxManipType.SortVersion] = (IdiomaInterfaz.T("MenuCaja.Version"), IdiomaInterfaz.T("MenuCaja.GeneracionJuegoLugarEncuentro")),
        [BoxManipType.SortBST] = (IdiomaInterfaz.T("MenuCaja.TotalEstadisticasBase"), IdiomaInterfaz.T("MenuCaja.SumaEstadisticasBase")),
        [BoxManipType.SortCP] = ("PC", IdiomaInterfaz.T("MenuCaja.PuntosCombateLetS")),
        [BoxManipType.SortScale] = (IdiomaInterfaz.T("Catalogo.Escala"), IdiomaInterfaz.T("MenuCaja.TamanoEscala")),
        [BoxManipType.SortRibbons] = (IdiomaInterfaz.T("MenuCaja.CuentaCintas"), IdiomaInterfaz.T("MenuCaja.NumeroCintasMayorMenor")),
        [BoxManipType.SortMarks] = (IdiomaInterfaz.T("MenuCaja.CuentaEmblemas"), IdiomaInterfaz.T("MenuCaja.NumeroEmblemasMayorMenor")),
        [BoxManipType.SortLegal] = (IdiomaInterfaz.T("Informe.Columna.Legal"), IdiomaInterfaz.T("MenuCaja.IlegalesPrimeroAnalizaCada")),
        [BoxManipType.SortEncounterType] = (IdiomaInterfaz.T("MenuCaja.TipoEncuentro"), IdiomaInterfaz.T("MenuCaja.EncuentroAnalisisIdentificaPuede")),

        [BoxManipType.ModifyHatchEggs] = (IdiomaInterfaz.T("MenuCaja.EclosionarHuevos"), IdiomaInterfaz.T("MenuCaja.HaceEclosionarTodosHuevos")),
        [BoxManipType.ModifyMaxFriendship] = (IdiomaInterfaz.T("MenuCaja.AmistadMaxima"), IdiomaInterfaz.T("MenuCaja.SubeAmistadMaximo")),
        [BoxManipType.ModifyMaxLevel] = (IdiomaInterfaz.T("MenuCaja.NivelMaximo"), IdiomaInterfaz.T("MenuCaja.SubeNivelMaximo")),
        [BoxManipType.ModifyResetMoves] = (IdiomaInterfaz.T("MenuCaja.ReiniciarMovimientos"), IdiomaInterfaz.T("MenuCaja.DejaMovimientosPokemonAprende")),
        [BoxManipType.ModifyRandomMoves] = (IdiomaInterfaz.T("MenuCaja.MovimientosAleatorios"), IdiomaInterfaz.T("MenuCaja.PoneMovimientosAzar")),
        [BoxManipType.ModifyHyperTrain] = (IdiomaInterfaz.T("MenuCaja.EntrenamientoExtremo"), IdiomaInterfaz.T("MenuCaja.AplicaEntrenamientoExtremoSugerido")),
        [BoxManipType.ModifyGanbaru] = (IdiomaInterfaz.T("MenuCaja.MaximizarTodosNEs"), IdiomaInterfaz.T("MenuCaja.PoneMaximoValoresGanbaru")),
        [BoxManipType.ModifyRemoveNicknames] = (IdiomaInterfaz.T("MenuCaja.EliminarMotes"), IdiomaInterfaz.T("MenuCaja.QuitaTodosMotes")),
        [BoxManipType.ModifyRemoveItem] = (IdiomaInterfaz.T("MenuCaja.EliminarObjetoEquipado"), IdiomaInterfaz.T("MenuCaja.QuitaObjetoLlevan")),
        [BoxManipType.ModifyHeal] = (IdiomaInterfaz.T("MenuCaja.CurarEstadisticasPP"), IdiomaInterfaz.T("MenuCaja.RestauraPSPPEstado")),
    };

    public static string NombreCategoria(CategoriaCaja c) => c switch
    {
        CategoriaCaja.Limpiar => IdiomaInterfaz.T("Form.Limpiar"),
        CategoriaCaja.Ordenar => IdiomaInterfaz.T("Mochila.Ordenar"),
        CategoriaCaja.OrdenarAvanzado => IdiomaInterfaz.T("MenuCaja.OrdenarAvanzado"),
        _ => IdiomaInterfaz.T("MenuCaja.Modificar"),
    };

    /// <summary>Todas las entradas que el guardado admite, por categoria y en el orden de PKHeX. Una entrada que PKHeX define dos veces (la fecha de Gen. 2 usa el momento del dia) aparece UNA vez, la que sirve a este guardado.</summary>
    public IReadOnlyList<EntradaManip> Entradas(CategoriaCaja categoria)
    {
        var lista = BoxManipUtil.ManipCategories[(int)categoria];
        var resultado = new List<EntradaManip>();
        foreach (var manip in lista)
        {
            if (!manip.Usable(Sav)) continue;
            if (resultado.Any(r => r.Tipo == manip.Type)) continue;   // DeleteUntrained y SortDate tienen variantes: solo vale la primera que el guardado admite
            var (titulo, desc) = Textos.TryGetValue(manip.Type, out var t) ? t : (manip.Type.ToString(), string.Empty);
            if (manip.Type == BoxManipType.SortDate && Sav.Generation < 4) desc = IdiomaInterfaz.T("MenuCaja.MomentoDiaObtuvo");
            if (manip.Type == BoxManipType.DeleteUntrained && Sav is SAV8LA) desc = IdiomaInterfaz.T("MenuCaja.BorraPokemonValoresGanbaru");
            resultado.Add(new EntradaManip(manip, categoria, titulo, desc, categoria == CategoriaCaja.Limpiar));
        }
        return resultado;
    }

    public IReadOnlyList<EntradaManip> Todas() => [.. Enum.GetValues<CategoriaCaja>().SelectMany(Entradas)];

    /// <summary>Texto de confirmacion de PKHeX (<c>GetPrompt</c>) para esta manipulacion.</summary>
    public string Pregunta(EntradaManip e, bool todas) => e.Manip.GetPrompt(todas);

    /// <summary>
    /// Ejecuta la manipulacion. Igual que <c>BoxManipulator.Execute</c> (<c>usable</c>, region sin casillas bloqueadas, <c>Execute</c> del Core con el rango de cajas y el sentido), pero dentro de UNA entrada
    /// del historial. Con <paramref name="todas"/> se aplica a todas las cajas; con <paramref name="invertir"/> los criterios se invierten (limpiar) o el orden se da la vuelta (ordenar).
    /// El analisis de legalidad que algunas hacen pasa por <see cref="Pokekeep.Sprites.LegalityGate"/>.
    /// </summary>
    public ResultadoManip Ejecutar(EntradaManip entrada, int caja, bool todas, bool invertir = false, bool notificar = true)
    {
        var manip = entrada.Manip;
        var inicio = todas ? 0 : caja;
        var fin = todas ? Sav.BoxCount - 1 : caja;
        if (!manip.Usable(Sav)) return new(InformeOperacion.Falla(TipoResultado.NoAdmitido, IdiomaInterfaz.T("MenuCaja.OpcionDisponiblePartida")), inicio, fin);
        if (Sav.IsAnySlotLockedInBox(inicio, fin)) return new(InformeOperacion.Falla(TipoResultado.Bloqueada, manip.GetFail(todas)), inicio, fin);

        var casillas = new List<ISlotInfo>();
        for (var b = inicio; b <= fin; b++)
            for (var s = 0; s < Sav.BoxSlotCount; s++) casillas.Add(new SlotInfoBox(b, s, Sav));

        var param = new BoxManipParam(inicio, fin, invertir);
        int tocadas;
        lock (Pokekeep.Sprites.LegalityGate.Cerrojo)
            tocadas = editor.Lote(casillas, () => manip.Execute(Sav, param), notificar);
        if (tocadas <= 0) return new(InformeOperacion.Falla(TipoResultado.SinCambios, IdiomaInterfaz.T("MenuCaja.CambiadoNadaNingunaCasilla")), inicio, fin);
        var mensaje = manip.GetSuccess(todas);
        if (string.IsNullOrWhiteSpace(mensaje)) mensaje = IdiomaInterfaz.T("MenuCaja.Hecho");
        return new(InformeOperacion.Ok(IdiomaInterfaz.F("MenuCaja.PuedesDeshacerlo", mensaje, tocadas), tocadas), inicio, fin);
    }

    /// <summary>
    /// En segundo plano (el Core no es cancelable y algunas manipulaciones analizan cada Pokemon). El aviso de cambio NO se da desde el hilo del grupo (refresca la interfaz): se da aqui, de vuelta
    /// en el hilo que llamo, cuando termina.
    /// </summary>
    public async Task<ResultadoManip> EjecutarAsync(EntradaManip entrada, int caja, bool todas, bool invertir = false)
    {
        var r = await Task.Run(() => Ejecutar(entrada, caja, todas, invertir, notificar: false));
        if (r.Informe.Correcto) editor.Notificar(EditorCasillas.TipoCambio.Masivo, []);
        return r;
    }
}
