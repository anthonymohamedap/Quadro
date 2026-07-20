using System;
using QuadroApp.Model.DB;

namespace QuadroApp.Service.Security;

/// <summary>US-32 — gevoelige acties die rol-afhankelijk zijn.</summary>
public enum Permissie
{
    LeverancierVerwijderen,
    LijstVerwijderen,
    PrijzenWijzigen,
    Factureren,
    GebruikersBeheren,
    GdprBeheer
}

/// <summary>Statische rol → permissie mapping. Admin mag alles.</summary>
public static class RolPermissies
{
    public static bool Heeft(GebruikersRol rol, Permissie permissie) => rol switch
    {
        GebruikersRol.Admin => true,
        GebruikersRol.Medewerker => permissie switch
        {
            Permissie.Factureren => true, // dagelijkse workflow blijft mogelijk
            _ => false
        },
        _ => false
    };
}

/// <summary>Gegooid wanneer een actie zonder de vereiste permissie wordt uitgevoerd.</summary>
public sealed class OnvoldoendeRechtenException : InvalidOperationException
{
    public OnvoldoendeRechtenException(Permissie permissie)
        : base($"Onvoldoende rechten voor deze actie ({permissie}). Vraag een beheerder.") { }
}
