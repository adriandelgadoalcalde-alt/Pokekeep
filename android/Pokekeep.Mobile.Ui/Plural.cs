// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Mobile.Ui;

/// <summary>Cantidad con su sustantivo en singular o plural («1 cambio», «2 cambios»): nunca «cambio(s)».</summary>
public static class Plural
{
    public static string De(int n, string uno, string varios) => n == 1 ? $"1 {uno}" : $"{n} {varios}";
}
