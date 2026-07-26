using System;

namespace QuadroApp.Service.Concurrency;

/// <summary>
/// REL-03: gegooid wanneer een opslag mislukt doordat iemand anders hetzelfde record
/// intussen wijzigde (optimistic-concurrency-conflict). Draagt een gebruiksvriendelijke
/// melding die de UI rechtstreeks kan tonen — de technische EF-uitzondering zit als
/// InnerException voor de logs.
/// </summary>
public sealed class GelijktijdigeWijzigingException : Exception
{
    public GelijktijdigeWijzigingException(string melding, Exception? inner = null)
        : base(melding, inner) { }
}
