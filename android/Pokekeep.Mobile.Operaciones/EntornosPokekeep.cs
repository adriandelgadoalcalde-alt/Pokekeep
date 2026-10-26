// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Runtime.CompilerServices;
using AutoLegalidad.Ui;
using AutoLegalidad.Ui.Controls;
using AutoLegalidad.Ui.Operation;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Modelo;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Operaciones;

/// <summary>Borrador del editor para el núcleo (<c>IPKMView</c>): es el MISMO objeto que el borrador del editor de la interfaz, así que aplicar o deshacer escribe directamente en él.</summary>
internal sealed class BorradorEditor(PKM inicial) : IPKMView
{
    public PKM Actual { get; } = inicial;
    public PKM Data => Actual;
    public bool Unicode => true;
    public bool HaX => false;
    public bool ChangingFields { get; set; }
    public bool EditsComplete => true;
    public PKM PreparePKM(bool click = true) => Actual.Clone();
    public void PopulateFields(PKM pk, bool focus = true, bool skipConversionCheck = false) { if (pk.Data.Length == Actual.Data.Length) pk.Data.CopyTo(Actual.Data); else throw new InvalidOperationException(IdiomaInterfaz.T("EntornosPokekeep.ElFormatoDeLa")); }
    public void NotifyWasExported(PKM pk) { }
}

/// <summary>El entorno de una sesión es SIEMPRE el mismo objeto (el coordinador compara la referencia para detectar que cambió la partida).</summary>
internal sealed class EntornoSesion
{
    public EntornoSesion(Sesion sesion)
    {
        var sav = sesion.Guardado;
        Editor = new BorradorEditor(sav.BlankPKM);
        // El MISMO historial de casillas que usa la interfaz (cajas, menú de caja, editor): un solo Deshacer coherente.
        Env = new CoreEditEnvironment(sav, Editor, sesion.Casillas.Historial);
    }
    public BorradorEditor Editor { get; }
    public CoreEditEnvironment Env { get; }
}

/// <summary>Los entornos de edición por sesión y la sesión ACTIVA de la app. Lo comparten las acciones de una casilla y las operaciones masivas (F9-0, ADR 0069).</summary>
internal sealed class EntornosPokekeep(Action? nuevaGeneracion)
{
    private readonly ConditionalWeakTable<Sesion, EntornoSesion> _entornos = new();

    /// <summary>La partida que la app tiene delante ahora (null si ninguna).</summary>
    public Sesion? Activa { get; private set; }

    /// <summary>Si cambia, las respuestas del motor pendientes quedan descartadas (nueva generación de sesión) y las operaciones en curso acaban en «Resultado descartado» (regla 7).</summary>
    public void SesionActiva(Sesion? sesion)
    {
        if (ReferenceEquals(Activa, sesion)) return;
        Activa = sesion;
        nuevaGeneracion?.Invoke();
    }

    public EntornoSesion De(Sesion s) => _entornos.GetValue(s, x => new EntornoSesion(x));

    public IPokekeepHost Anfitrion(Sesion sesion, CoreEditEnvironment env, int caja) => new AnfitrionSesion(this, sesion, env, caja);

    /// <summary>Lo que el coordinador necesita de la app. Si la sesión de esta operación ya no es la activa, el entorno que devuelve es OTRO objeto: el coordinador lo ve como «otra partida».</summary>
    private sealed class AnfitrionSesion(EntornosPokekeep padre, Sesion sesion, CoreEditEnvironment env, int caja) : IPokekeepHost
    {
        // Sin partida delante (Activa nula) tampoco vale el entorno de la operación: un resultado tardío no se aplica a una partida que ya no está.
        private readonly IEditEnvironment _ninguna = new CoreEditEnvironment(FakeSaveFile.Default, new BorradorEditor(FakeSaveFile.Default.BlankPKM));
        public IEditEnvironment Env => padre.Activa is null ? _ninguna : ReferenceEquals(padre.Activa, sesion) ? env : padre.De(padre.Activa).Env;
        public int CurrentBox { get; } = caja;
        public bool EditorIsCompleteIgnoringStats => true;
        public void ClearSlotHighlights() { }
        public void NotifyUndoRedoChanged() { }
    }
}
