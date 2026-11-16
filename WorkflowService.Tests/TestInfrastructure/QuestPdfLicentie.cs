using System.Runtime.CompilerServices;

namespace WorkflowService.Tests.TestInfrastructure;

/// <summary>
/// T-01 uit docs/analyse/Codebase_Analyse_2026-11.md: de QuestPDF-licentie wordt één keer gezet voor het
/// hele testproject, vóór er een test loopt (zoals App.axaml.cs in de app). Zo hangt een PDF-test niet
/// meer af van welke andere test eerst liep.
/// </summary>
internal static class QuestPdfLicentie
{
    [ModuleInitializer]
    internal static void Zet() => QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
}
