using QuadroApp.Service.Toast;
using QuadroApp.Service.Interfaces;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WorkflowService.Tests.TestInfrastructure;

public sealed class TestToastService : IToastService
{
    private readonly ObservableCollection<ToastMessage> _messages = [];

    public ReadOnlyObservableCollection<ToastMessage> Messages { get; }
    public List<string> SuccessMessages { get; } = [];
    public List<string> ErrorMessages { get; } = [];
    public List<string> WarningMessages { get; } = [];
    public List<string> InfoMessages { get; } = [];

    public TestToastService()
    {
        Messages = new ReadOnlyObservableCollection<ToastMessage>(_messages);
    }

    public void Show(string message, ToastType type, int durationMs = 3000)
    {
        _messages.Add(new ToastMessage(message, type));

        switch (type)
        {
            case ToastType.Success:
                Success(message);
                break;
            case ToastType.Error:
                Error(message);
                break;
            case ToastType.Warning:
                Warning(message);
                break;
            case ToastType.Info:
                Info(message);
                break;
        }
    }

    public void Success(string message) => SuccessMessages.Add(message);

    public void Error(string message) => ErrorMessages.Add(message);

    public void Warning(string message) => WarningMessages.Add(message);

    public void Info(string message) => InfoMessages.Add(message);
}
