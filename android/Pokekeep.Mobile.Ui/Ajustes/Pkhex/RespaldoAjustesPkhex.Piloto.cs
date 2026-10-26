// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Mobile.Ui.Ajustes.Pkhex;

public static partial class RespaldoAjustesPkhex
{
    // lang_es.txt no trae la etiqueta «PropertyGrid.CheckActiveHandler» (solo su descripción): en PKHeX castellano la fila sale con el nombre de la propiedad en inglés.
    // «Entrenador actual» es como lang_es.txt traduce Handler en la descripción de este mismo ajuste.
    static partial void AnadirEtiquetasPiloto(Action<string, string> a) => a("CheckActiveHandler", "Comprobar el entrenador actual");
}
