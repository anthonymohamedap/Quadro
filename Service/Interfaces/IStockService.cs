using System;
using System.Threading.Tasks;

namespace QuadroApp.Service.Interfaces
{
    public interface IStockService
    {
        Task ReserveStockForWerkBonAsync(int werkBonId);
        Task ConsumeReservationsForWerkBonAsync(int werkBonId);
        Task ReleaseReservationsForWerkBonAsync(int werkBonId, bool cancelOpenOrders = false);
        Task PlaceSupplierOrderForWerkTaakAsync(int werkTaakId, DateTime bestelDatum);
        Task CreateSupplierOrderAsync(int typeLijstId, decimal aantalMeter, DateTime bestelDatum, string? opmerking = null);
        Task ReceiveSupplierOrderLineAsync(int bestelLijnId, decimal? aantalMeter = null);
        Task CancelSupplierOrderAsync(int bestellingId);
        Task RefreshAlertsAsync();
    }
}
