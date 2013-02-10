using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Samba.Domain.Models.Tickets;
using Samba.Services;

namespace Samba.Modules.PaymentModule
{
    [Export(typeof(ITicketGroupViewModel))]
    public class TicketGroupViewModel : ITicketGroupViewModel
    {
        private readonly ITicketService _ticketService;

        [ImportingConstructor]
        public TicketGroupViewModel(ITicketService ticketService)
        {
            Tickets = new List<Ticket>();
            _ticketService = ticketService;
        }

        public List<Ticket> Tickets { get; set; }

        public bool ContainsItems { get { return Tickets.Count() > 0; } }

        public bool Locked { get { return Tickets.All(x => x.Locked); } }

        public decimal TotalAmount
        {
            get { return Tickets.Sum(x => x.TotalAmount); }
        }

        public void AddCalculation(CalculationTemplate calculationTemplate, decimal amount)
        {
            Tickets.ForEach(x => x.AddCalculation(calculationTemplate, amount));
        }

        public decimal GetRemainingAmount()
        {
            return Tickets.Sum(x => x.GetRemainingAmount());
        }

        public int GetPrintCount(int id)
        {
            if (Tickets.Count == 0) return 0;
            return Tickets.Min(x => x.GetPrintCount(id));
        }

        public void ClearPaidItems()
        {
            Tickets.ForEach(x => x.PaidItems.Clear());
        }

        public void Clear()
        {
            Tickets.Clear();
        }

        public void UpdateTickets(params Ticket[] tickets)
        {
            Tickets.Clear();
            Tickets.AddRange(tickets);
        }

        public void Recalculate()
        {
            Tickets.ForEach(_ticketService.RecalculateTicket);
        }

        public decimal GetPostTaxServicesTotal()
        {
            return Tickets.Sum(x => x.GetPostTaxServicesTotal());
        }

        public decimal GetSum()
        {
            return Tickets.Sum(x => x.GetSum());
        }

        public IEnumerable<Order> GetCalulatingOrders()
        {
            return Tickets.SelectMany(x => x.Orders).Where(x => x.CalculatePrice);
        }

        public IEnumerable<PaidItem> GetPaidItems()
        {
            return Tickets.SelectMany(x => x.PaidItems);
        }

        public Ticket[] GetTickets()
        {
            return Tickets.ToArray();
        }

        public void AddPaidItems(PaidItem paidItem)
        {
            if (ContainsItems) Tickets[0].PaidItems.Add(paidItem);
        }

        public void AddPayment(PaymentTemplate paymentTemplate, decimal tenderedAmount)
        {
            foreach (var ticket in Tickets.OrderBy(x => x.Date))
            {
                var payingAmount = ticket.GetRemainingAmount();
                if (payingAmount == 0) continue;

                if (payingAmount > tenderedAmount)
                    payingAmount = tenderedAmount;

                _ticketService.AddPayment(ticket, paymentTemplate, payingAmount);

                tenderedAmount -= payingAmount;

                if (tenderedAmount == 0) break;
                Debug.Assert(tenderedAmount > 0);
            }
        }

        public void SaveTickets()
        {
            _ticketService.SaveTickets(Tickets);
        }
    }
}
