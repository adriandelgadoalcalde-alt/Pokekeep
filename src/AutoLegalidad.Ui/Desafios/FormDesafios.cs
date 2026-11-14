// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Desafios;
using PKHeX.Core;

namespace AutoLegalidad.Ui.Desafios;

/// <summary>
/// Formulario nuevo para los desafíos que PKHeX no tiene editor: Torre Batalla de Diamante Brillante y Perla Reluciente (cuatro clases), PWT de Negro 2 y Blanco 2, PB del Metro Batalla, Colina Entrenador de Esmeralda,
/// Maison de Gen 6, Árbol Batalla de Gen 7, Torre de Espada y Escudo y Royale de Z-A. Lectura, escritura (una casilla por campo) y «Máximo» / «Ir al jefe». Trabaja sobre una copia de la partida y la vuelca con
/// «Guardar», como los demás editores de PKHeX (<see cref="SaveFile.CopyChangesFrom"/>).
/// </summary>
public sealed class FormDesafios : Form
{
    private readonly SaveFile _origen;
    private readonly SaveFile _sav;
    private readonly bool _es;
    private readonly PanelDesafios _panel;

    /// <summary>Instalaciones de la partida que ningún formulario de PKHeX edita (las del Frente de Esmeralda, Gen 4, Metro y Pokéathlon ya tienen su pestaña en su formulario).</summary>
    public static IReadOnlyList<InstalacionDesafio> InstalacionesSinEditor(SaveFile sav) =>
    [
        .. CatalogoDesafios.Para(sav).Where(i => i is not (Frente3Esmeralda or Frente4 or Metro5 or Pokeathlon4Maximo)),
    ];

    /// <summary>Si la partida tiene algo que este formulario pueda editar (para enseñar o esconder el botón del menú de herramientas).</summary>
    public static bool Aplica(SaveFile sav) => InstalacionesSinEditor(sav).Count > 0;

    public FormDesafios(SaveFile origen, bool espanol)
    {
        _origen = origen;
        _sav = origen.Clone();
        _es = espanol;
        Name = "FormPokekeepDesafios";
        Text = TextosDesafiosUi.TituloEditor(_es);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        ShowInTaskbar = false;
        MinimumSize = new Size(640, 420);
        ClientSize = new Size(820, 560);

        _panel = new PanelDesafios(() => _sav, InstalacionesSinEditor(_sav), _es, editable: true);

        var guardar = new Button { Name = "B_Save", Text = TextosDesafiosUi.Guardar(_es), AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, MinimumSize = new Size(90, 0) };
        var cancelar = new Button { Name = "B_Cancel", Text = TextosDesafiosUi.Cancelar(_es), AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, MinimumSize = new Size(90, 0), DialogResult = DialogResult.Cancel };
        guardar.Click += (_, _) =>
        {
            _origen.CopyChangesFrom(_sav);
            DialogResult = DialogResult.OK;
            Close();
        };
        cancelar.Click += (_, _) => Close();
        AcceptButton = guardar;
        CancelButton = cancelar;

        var pie = new FlowLayoutPanel { Name = "FLP_DesafiosPie", Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(6) };
        pie.Controls.AddRange([cancelar, guardar]);

        Controls.Add(_panel);
        Controls.Add(pie);
    }

    public PanelDesafios Panel => _panel;
}
