using QuadroApp.ViewModels;
using System.Threading.Tasks;
using WorkflowService.Tests.TestInfrastructure;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>US-98 — de weeklijst kan een week vooruit of terug, ook over de jaargrens.</summary>
public class WeekLijstNavigatieTests
{
    private static WeekWerkLijstViewModel Vm()
    {
        var factory = DbFactoryBuilder.CreateInMemoryFactory();
        return new WeekWerkLijstViewModel(factory, new QuadroApp.Service.WorkflowService(
            factory, Microsoft.Extensions.Logging.Abstractions.NullLogger<QuadroApp.Service.WorkflowService>.Instance, new TestToastService()));
    }

    [Fact]
    public async Task Volgende_en_vorige_week()
    {
        var vm = Vm();
        await vm.InitializeAsync(2026, 44);

        await vm.VolgendeWeekCommand.ExecuteAsync(null);
        Assert.Equal((2026, 45), (vm.Year, vm.WeekNr));

        await vm.VorigeWeekCommand.ExecuteAsync(null);
        await vm.VorigeWeekCommand.ExecuteAsync(null);
        Assert.Equal((2026, 43), (vm.Year, vm.WeekNr));
    }

    [Fact]
    public async Task Over_de_jaargrens()
    {
        var vm = Vm();
        await vm.InitializeAsync(2026, 53);   // 2026 heeft 53 ISO-weken

        await vm.VolgendeWeekCommand.ExecuteAsync(null);
        Assert.Equal((2027, 1), (vm.Year, vm.WeekNr));

        await vm.VorigeWeekCommand.ExecuteAsync(null);
        Assert.Equal((2026, 53), (vm.Year, vm.WeekNr));
    }
}
