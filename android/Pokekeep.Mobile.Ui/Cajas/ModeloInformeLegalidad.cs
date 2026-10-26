// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Ajustes.Pkhex;
using Pokekeep.Mobile.Ui.Modelo;
using Pokekeep.Mobile.Ui.ViewModels;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Cajas;

public sealed record DatosInformeLegalidad(bool Valido, bool Mostrar, string Resumen, string Detalle, string Copia, bool DetalleInicial, bool PermiteCopiar);

/// <summary>DisplayLegalityReport de Main: mismo localizador y texto de copia, sin TaskDialog ni portapapeles global.</summary>
public static class ModeloInformeLegalidad
{
    public static DatosInformeLegalidad Crear(PKM pk, StorageSlotType tipo, AjustesPkhex ajustes, bool invertirDetalle = false)
    {
        TextosNucleo.Preparar();
        lock (Pokekeep.Sprites.LegalityGate.Cerrojo)
        {
            var la = AnalizadorLegalidad.AnalizarPara(pk, tipo);
            if (la is null) return new(false, true, IdiomaInterfaz.T("Intercambio.PodidoAnalizarPokemon"), IdiomaInterfaz.T("Intercambio.PodidoAnalizarPokemon"), IdiomaInterfaz.T("Intercambio.PodidoAnalizarPokemon"), false, !ajustes.Display.ExportLegalityNeverClipboard);
            var idioma = GameLanguage.IsLanguageValid(ajustes.Startup.Language) ? ajustes.Startup.Language : "es";
            var localizador = LegalityLocalizationContext.Create(la, idioma);
            var localizadorUi = LegalityLocalizationContext.Create(la, Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma);
            var simple = TextosEncuentro.Traducir(localizadorUi.Report(false));
            var detalle = TextosEncuentro.Traducir(localizadorUi.Report(true));
            var encuentro = la.EncounterOriginal.GetTextLines(ajustes.Display.ExportLegalityVerboseProperties);
            var copia = localizador.Report(true) + Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, encuentro);
            return new(la.Valid, !(ajustes.Display.IgnoreLegalPopup && la.Valid), simple, detalle, copia,
                invertirDetalle ^ ajustes.Display.ExportLegalityAlwaysVerbose, !ajustes.Display.ExportLegalityNeverClipboard);
        }
    }

    public static InformeHojaViewModel? Hoja(string titulo, DatosInformeLegalidad datos, IPortapapeles? portapapeles)
        => !datos.Mostrar ? null : new InformeHojaViewModel(titulo, datos.Resumen,
            copiar: datos.PermiteCopiar && portapapeles is not null ? async t =>
            {
                if (!await portapapeles.CopiarAsync(t)) throw new InvalidOperationException(IdiomaInterfaz.T("ModeloInformeLegalidad.ElPortapapelesNoAdmitio"));
            } : null,
            estado: datos.Valido ? IdiomaInterfaz.T("Informe.Columna.Legal") : IdiomaInterfaz.T("ModeloInformeLegalidad.RevisarLegalidad"), claseEstado: datos.Valido ? "ok" : "error",
            detalle: datos.Detalle, textoParaCopiar: datos.Copia, detalleInicial: datos.DetalleInicial);
}
