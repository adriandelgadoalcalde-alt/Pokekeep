// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Pokekeep.Mobile.Ui.Cajas;
using Pokekeep.Mobile.Ui.Herramientas.Banco;

namespace Pokekeep.Mobile.Ui.ViewModels;

// X38: «Guardar en el banco» y «Mover al banco» en el menú de la casilla y en el de la selección. Aparte para no tocar el resto de CajasViewModel.
public sealed partial class CajasViewModel
{
    /// <summary>Las dos entradas del banco para unas casillas: guardar una copia (no toca la partida) o mover (quita de la partida lo que quede a salvo; pide confirmación y se puede deshacer).</summary>
    private IEnumerable<EntradaHoja> EntradasBanco(IReadOnlyList<CasillaRef> casillas)
    {
        var motivo = _anfitrion?.MotivoGuardarEnBanco;
        var n = casillas.Count;
        var puedeQuitar = casillas.Any(c => Sesion.Casillas.EsEscribible(c.Info));
        yield return new EntradaHoja(TextosBanco.GuardarEnElBanco, TextosBanco.GuardarEnElBancoAyuda(n), "Download", () => _anfitrion?.GuardarEnBanco(casillas, false), Motivo: _anfitrion is null ? TextosBanco.NoDisponibleAqui : motivo);
        yield return new EntradaHoja(TextosBanco.MoverAlBanco, TextosBanco.MoverAlBancoAyuda(n), "Move", () => _anfitrion?.GuardarEnBanco(casillas, true), Destructiva: true,
            Motivo: _anfitrion is null ? TextosBanco.NoDisponibleAqui : motivo ?? (puedeQuitar ? null : TextosBanco.SoloLecturaNoSeQuita));
    }

    private SeccionHoja SeccionBanco(IReadOnlyList<CasillaRef> casillas) => new(TextosBanco.GrupoMenu, [.. EntradasBanco(casillas)]);

    private IEnumerable<EntradaHoja> EntradasBancoDeLaSeleccion()
    {
        var lista = MarcadasOcupadas();
        if (lista.Count == 0) return [];
        return EntradasBanco(lista);
    }
}
