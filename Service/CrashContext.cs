using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace QuadroApp.Service;

/// <summary>
/// Houdt bij welk scherm open stond en op welke knoppen laatst geklikt werd, zodat crash.log
/// vertelt waar de gebruiker mee bezig was. Een onobserveerde Task komt pas bij een latere
/// garbage collection boven; de laatste klikken tonen dan wat er vlak daarvoor gebeurde.
/// Thread-safe: de crash-handlers draaien soms op de finalizer-thread.
/// </summary>
public static class CrashContext
{
    private const int MaxActies = 8;
    private static readonly object Slot = new();
    private static readonly Queue<string> Acties = new();
    private static string? _scherm;

    /// <summary>Het scherm (ViewModel) dat nu in het hoofdvenster staat.</summary>
    public static void NoteerScherm(object? viewModel)
    {
        lock (Slot) _scherm = viewModel?.GetType().Name;
    }

    /// <summary>Een actie van de gebruiker (bv. een klik op een knop).</summary>
    public static void NoteerActie(string actie)
    {
        if (string.IsNullOrWhiteSpace(actie)) return;
        lock (Slot)
        {
            Acties.Enqueue($"[{DateTime.Now:HH:mm:ss}] {actie}");
            while (Acties.Count > MaxActies) Acties.Dequeue();
        }
    }

    /// <summary>Tekst voor crash.log: bron van de fout, scherm en laatste acties (nieuwste laatst).</summary>
    public static string Beschrijf(string bron)
    {
        lock (Slot)
        {
            var sb = new StringBuilder();
            sb.Append("Bron: ").Append(bron).Append('\n');
            sb.Append("Scherm: ").Append(_scherm ?? "(onbekend)").Append('\n');
            sb.Append("Laatste acties:");
            if (Acties.Count == 0) sb.Append(" (geen)");
            foreach (var a in Acties) sb.Append("\n  ").Append(a);
            return sb.ToString();
        }
    }

    /// <summary>Enkel voor testen.</summary>
    public static void Leeg()
    {
        lock (Slot)
        {
            Acties.Clear();
            _scherm = null;
        }
    }
}
