// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Desafios;
using PKHeX.Core;
using TextosD = AutoLegalidad.Desafios.Textos;

namespace AutoLegalidad.Ui.Desafios;

/// <summary>
/// Apartado «Insignias del Pokéathlon» de la ficha del Pokémon en HeartGold y SoulSilver: las cinco casillas (Velocidad, Fortaleza, Precisión, Resistencia y Salto) de la especie del Pokémon abierto y «Dar las cinco».
/// Las insignias son de la ESPECIE y viven en la partida (no en el Pokémon): marcar una casilla la escribe en la partida abierta con <see cref="InsigniasPokeathlon"/>. Solo es visible en HGSS y para las especies 1 a 493.
/// </summary>
public sealed class PanelInsignias : GroupBox
{
    private readonly bool _es;
    private readonly CheckBox[] _casillas = new CheckBox[(int)PokeathlonStat4.Count];
    private readonly Button _darTodas = new() { Name = "B_InsigniasDarTodas", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
    private SaveFile? _sav;
    private ushort _especie;
    private bool _cargando;

    /// <summary>Se dispara cuando una escritura ha cambiado las insignias en la partida (el editor principal puede marcar la partida como modificada).</summary>
    public event EventHandler? Cambiado;

    public PanelInsignias(bool espanol)
    {
        _es = espanol;
        Name = "GB_PokeathlonInsignias";
        Text = TextosDesafiosUi.InsigniasTitulo(_es);
        // Alto fijo (tres filas de casillas como mucho): un GroupBox acoplado con AutoSize y un contenido que se reparte en filas se queda sin alto.
        Height = (int)(112 * DeviceDpi / 96.0);
        Padding = new Padding(6, 4, 6, 6);
        Visible = false;

        var flujo = new FlowLayoutPanel { Name = "FLP_Insignias", FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Dock = DockStyle.Fill };
        string[] claves = ["curso.velocidad", "curso.fortaleza", "curso.precision", "curso.resistencia", "curso.salto"];
        for (var i = 0; i < _casillas.Length; i++)
        {
            var bit = i;
            var chk = new CheckBox { Name = "CHK_Insignia" + i, Text = TextosD.Doble(claves[i]).Para(_es), AutoSize = true, Margin = new Padding(3, 3, 8, 3) };
            chk.CheckedChanged += (_, _) => { if (!_cargando) Escribir(); };
            _casillas[bit] = chk;
            flujo.Controls.Add(chk);
        }
        _darTodas.Text = TextosDesafiosUi.DarLasCinco(_es);
        _darTodas.Click += (_, _) =>
        {
            if (_sav is null || _especie == 0)
                return;
            InsigniasPokeathlon.DarTodas(_sav, _especie);
            Mostrar();
            Cambiado?.Invoke(this, EventArgs.Empty);
        };
        flujo.Controls.Add(_darTodas);
        Controls.Add(flujo);
    }

    /// <summary>Casilla de la insignia de un curso (para pruebas).</summary>
    public CheckBox Casilla(PokeathlonStat4 curso) => _casillas[(int)curso];

    /// <summary>Apunta el apartado a una partida y una especie. Se esconde si la partida no es HGSS o la especie no está entre la 1 y la 493.</summary>
    public void Actualizar(SaveFile? sav, ushort especie)
    {
        _sav = sav;
        _especie = especie;
        var visible = sav is SAV4HGSS && especie is >= 1 and <= (int)Species.Arceus;
        Visible = visible;
        if (visible)
            Mostrar();
    }

    private void Mostrar()
    {
        if (_sav is null)
            return;
        var bits = InsigniasPokeathlon.Leer(_sav, _especie);
        _cargando = true;
        try
        {
            for (var i = 0; i < _casillas.Length; i++)
                _casillas[i].Checked = ((bits >> i) & 1) != 0;
        }
        finally { _cargando = false; }
    }

    private void Escribir()
    {
        if (_sav is null || _especie == 0)
            return;
        byte bits = 0;
        for (var i = 0; i < _casillas.Length; i++)
            if (_casillas[i].Checked)
                bits |= (byte)(1 << i);
        InsigniasPokeathlon.Escribir(_sav, _especie, bits);
        Cambiado?.Invoke(this, EventArgs.Empty);
    }
}
