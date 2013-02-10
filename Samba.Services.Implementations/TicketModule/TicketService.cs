﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using Samba.Domain.Models.Accounts;
using Samba.Domain.Models.Settings;
using Samba.Domain.Models.Tickets;
using Samba.Infrastructure.Data;
using Samba.Infrastructure.Data.Serializer;
using Samba.Localization.Properties;
using Samba.Persistance.Data;
using Samba.Persistance.Data.Specification;
using Samba.Services.Common;

namespace Samba.Services.Implementations.TicketModule
{
    [Export(typeof(ITicketService))]
    public class TicketService : AbstractService, ITicketService
    {
        private IWorkspace _workspace;
        private readonly IDepartmentService _departmentService;
        private readonly IPrinterService _printerService;
        private readonly IApplicationState _applicationState;
        private readonly IApplicationStateSetter _applicationStateSetter;
        private readonly IAutomationService _automationService;
        private readonly ISettingService _settingService;
        private readonly ICacheService _cacheService;

        [ImportingConstructor]
        public TicketService(IDepartmentService departmentService, IPrinterService printerService,
            IApplicationState applicationState, IApplicationStateSetter applicationStateSetter,
            IAutomationService automationService, ISettingService settingService, ICacheService cacheService)
        {
            _departmentService = departmentService;
            _printerService = printerService;
            _applicationState = applicationState;
            _applicationStateSetter = applicationStateSetter;
            _automationService = automationService;
            _settingService = settingService;
            _cacheService = cacheService;

            ValidatorRegistry.RegisterDeleteValidator(new TicketTagGroupDeleteValidator());
        }

        public void UpdateAccount(Ticket ticket, Account account)
        {
            Debug.Assert(ticket != null);
            if (account == Account.Null)
            {
                var template = _workspace.Single<AccountTransactionTemplate>(
                        x => x.Id == _applicationState.CurrentDepartment.TicketTemplate.SaleTransactionTemplate.Id);
                account = _cacheService.GetAccountById(template.DefaultTargetAccountId);
            }
            
            ticket.UpdateAccount(account);
            _automationService.NotifyEvent(RuleEventNames.AccountSelectedForTicket,
                    new
                    {
                        Ticket = _applicationState.CurrentTicket,
                        AccountName = account.Name,
                        PhoneNumber = account.SearchString
                    });
        }

        public Ticket OpenTicket(int ticketId)
        {
            var ticket = _applicationState.CurrentTicket;
            Debug.Assert(_workspace == null);
            Debug.Assert(ticket == null);
            Debug.Assert(_applicationState.CurrentDepartment != null);

            _workspace = WorkspaceFactory.Create();
            ticket = ticketId == 0
                                 ? CreateTicket(_workspace)
                                 : _workspace.Single<Ticket>(t => t.Id == ticketId,
                                                             x => x.Orders.Select(y => y.OrderTagValues), x => x.Calculations);
            _applicationStateSetter.SetCurrentTicket(ticket);

            if (ticket.Id == 0)
                _automationService.NotifyEvent(RuleEventNames.TicketCreated, new { Ticket = ticket });

            return ticket;
        }

        private Ticket CreateTicket(IWorkspace workspace)
        {
            var account = _applicationState.SelectedAccountForTicket;
            if(account == null )
            {
                var aid = _applicationState.CurrentDepartment.TicketTemplate.SaleTransactionTemplate.DefaultTargetAccountId;
                if(aid > 0) account = _cacheService.GetAccountById(aid);
            }
                 
            return Ticket.Create(_applicationState.CurrentDepartment, account);
        }

        public Ticket OpenTicketByTicketNumber(string ticketNumber)
        {
            Debug.Assert(_applicationState.CurrentTicket == null);
            var id = Dao.Select<Ticket, int>(x => x.Id, x => x.TicketNumber == ticketNumber).FirstOrDefault();
            if (id > 0)
            {
                var ticket = OpenTicket(id);
                return ticket;
            }
            return null;
        }

        public TicketCommitResult CloseTicket(Ticket ticket)
        {
            var result = new TicketCommitResult();
            Debug.Assert(ticket != null);
            var changed = false;

            if (ticket.Id > 0)
            {
                var lup = Dao.Single<Ticket, DateTime>(ticket.Id, x => x.LastUpdateTime);
                if (ticket.LastUpdateTime.CompareTo(lup) != 0)
                {
                    var currentTicket = Dao.Single<Ticket>(x => x.Id == ticket.Id, x => x.Orders);
                    if (currentTicket.LocationName != ticket.LocationName)
                    {
                        result.ErrorMessage = string.Format(Resources.TicketMovedRetryLastOperation_f, currentTicket.LocationName);
                        changed = true;
                    }

                    if (currentTicket.IsPaid != ticket.IsPaid)
                    {
                        if (currentTicket.IsPaid)
                        {
                            result.ErrorMessage = Resources.TicketPaidChangesNotSaved;
                        }
                        if (ticket.IsPaid)
                        {
                            result.ErrorMessage = Resources.TicketChangedRetryLastOperation;
                        }
                        changed = true;
                    }
                    else if (currentTicket.LastPaymentDate != ticket.LastPaymentDate)
                    {
                        var currentPaymentIds = ticket.Payments.Select(x => x.Id).Distinct();
                        var unknownPayments = currentTicket.Payments.Where(x => !currentPaymentIds.Contains(x.Id)).FirstOrDefault();
                        if (unknownPayments != null)
                        {
                            result.ErrorMessage = Resources.TicketPaidLastChangesNotSaved;
                            changed = true;
                        }
                    }
                }
            }

            var canSumbitTicket = !changed && ticket.CanSubmit; // Fişi kaydedebilmek için gün sonu yapılmamış ve fişin ödenmemiş olması gerekir.

            if (canSumbitTicket)
            {
                ticket.Recalculate(_settingService.ProgramSettings.AutoRoundDiscount, _applicationState.CurrentLoggedInUser.Id);

                if (ticket.Orders.Count > 0)
                {
                    var department = _departmentService.GetDepartment(ticket.DepartmentId);

                    if (ticket.Orders.Where(x => !x.Locked).FirstOrDefault() != null)
                    {
                        var number = _settingService.GetNextNumber(department.TicketTemplate.OrderNumerator.Id);
                        ticket.MergeOrdersAndUpdateOrderNumbers(number);
                        ticket.Orders.Where(x => x.Id == 0).ToList().ForEach(x => x.CreatedDateTime = DateTime.Now);
                    }

                    if (ticket.Id == 0)
                    {
                        _workspace.Add(ticket);
                        UpdateTicketNumbers(department.TicketTemplate.TicketNumerator, ticket);
                        ticket.LastOrderDate = DateTime.Now;
                        _workspace.CommitChanges();
                    }

                    Debug.Assert(!string.IsNullOrEmpty(ticket.TicketNumber));
                    Debug.Assert(ticket.Id > 0);

                    //Otomatik yazdırma
                    _printerService.AutoPrintTicket(ticket);
                    ticket.LockTicket();
                }

                if (ticket.Id > 0)  // eğer adisyonda satır yoksa ID burada 0 olmalı.
                    _workspace.CommitChanges();
                Debug.Assert(ticket.Orders.Count(x => x.OrderNumber == 0) == 0);
            }
            result.TicketId = ticket.Id;
            _workspace = null;
            _applicationStateSetter.SetCurrentTicket(null);
            _applicationStateSetter.SetSelectedAccountForTicket(null);

            return result;
        }

        public void AddPayment(Ticket ticket, PaymentTemplate template, decimal tenderedAmount)
        {
            ticket.AddPayment(template, tenderedAmount, _applicationState.CurrentLoggedInUser.Id);
        }

        public void PaySelectedTicket(Ticket ticket, PaymentTemplate template)
        {
            AddPayment(ticket, template, ticket.GetRemainingAmount());
        }

        public void UpdateTicketNumbers(Numerator numerator, params Ticket[] tickets)
        {
            foreach (var ticket in tickets.Where(ticket => string.IsNullOrEmpty(ticket.TicketNumber)))
            {
                ticket.TicketNumber = _settingService.GetNextString(numerator.Id);
            }
        }

        public TicketCommitResult MoveOrders(Ticket ticket, IEnumerable<Order> selectedOrders, int targetTicketId)
        {
            var clonedOrders = selectedOrders.Select(ObjectCloner.Clone).ToList();
            ticket.RemoveOrders(selectedOrders);

            if (ticket.Orders.Count == 0)
            {
                var info = targetTicketId.ToString();
                if (targetTicketId > 0)
                {
                    var tData = Dao.Single<Ticket, dynamic>(targetTicketId, x => new { x.LocationName, x.TicketNumber });
                    info = tData.LocationName + " - " + tData.TicketNumber;
                }
                if (!string.IsNullOrEmpty(ticket.Note)) ticket.Note += "\r";
                ticket.Note += ticket.LocationName + " => " + info;
            }

            CloseTicket(ticket);
            ticket = OpenTicket(targetTicketId);

            foreach (var clonedOrder in clonedOrders)
            {
                ticket.Orders.Add(clonedOrder);
            }

            foreach (var template in from order in clonedOrders.GroupBy(x => x.AccountTransactionTemplateId)
                                     where !ticket.AccountDocument.AccountTransactions.Any(x => x.AccountTransactionTemplateId == order.Key)
                                     select _cacheService.GetAccountTransactionTemplateById(order.Key))
            {
                ticket.AccountDocument.AccountTransactions.Add(AccountTransaction.Create(template));
            }

            ticket.LastOrderDate = DateTime.Now;
            return CloseTicket(ticket);
        }

        public void RecalculateTicket(Ticket ticket)
        {
            var total = ticket.TotalAmount;
            ticket.Recalculate(_settingService.ProgramSettings.AutoRoundDiscount, _applicationState.CurrentLoggedInUser.Id);
            if (total != ticket.TotalAmount)
            {
                _automationService.NotifyEvent(RuleEventNames.TicketTotalChanged,
                    new
                    {
                        Ticket = ticket,
                        PreviousTotal = total,
                        TicketTotal = ticket.GetSum(),
                        DiscountTotal = ticket.GetPreTaxServicesTotal(),
                        PaymentTotal = ticket.GetPaymentAmount()
                    });
            }
        }

        public void RegenerateTaxRates(Ticket ticket)
        {
            foreach (var order in ticket.Orders)
            {
                var order1 = order;
                var mi = _cacheService.GetMenuItem(x => x.Id == order1.MenuItemId);
                if (mi == null) continue;
                var item = order1;
                var portion = mi.Portions.FirstOrDefault(x => x.Name == item.PortionName);
                if (portion != null) order1.UpdatePortion(portion, order1.PriceTag, mi.TaxTemplate);
            }
        }

        public void UpdateTag(Ticket ticket, TicketTagGroup tagGroup, TicketTag ticketTag)
        {
            ticket.SetTagValue(tagGroup.Name, ticketTag.Name);
            if (tagGroup.Numerator != null)
            {
                ticket.TicketNumber = "";
                UpdateTicketNumbers(tagGroup.Numerator, ticket);
            }

            if (ticketTag.AccountId > 0)
                UpdateAccount(ticket, Dao.SingleWithCache<Account>(x => x.Id == ticketTag.AccountId));

            if (tagGroup.SaveFreeTags)
            {
                SaveFreeTicketTag(tagGroup.Id, ticketTag.Name);
            }

            var tagData = new TicketTagData
            {
                Ticket = ticket,
                TicketTagGroup = tagGroup,
                TagName = tagGroup.Name,
                TagValue = ticketTag.Name
            };

            _automationService.NotifyEvent(RuleEventNames.TicketTagSelected,
                        new
                        {
                            Ticket = ticket,
                            tagData.TagName,
                            tagData.TagValue,
                            NumericValue = tagGroup.IsNumeric ? Convert.ToDecimal(ticketTag.Name) : 0,
                            TicketTag = ticket.GetTagData()
                        });

            tagData.PublishEvent(EventTopicNames.TagSelectedForSelectedTicket);
        }

        public int GetOpenTicketCount()
        {
            return Dao.Count<Ticket>(x => !x.IsPaid);
        }

        public IEnumerable<OpenTicketData> GetTicketData(Expression<Func<Ticket, bool>> prediction)
        {
            return Dao.Select(x => new OpenTicketData
            {
                Id = x.Id,
                LastOrderDate = x.LastOrderDate,
                TicketNumber = x.TicketNumber,
                LocationName = x.LocationName,
                AccountName = x.AccountName,
                RemainingAmount = x.RemainingAmount,
                Date = x.Date
            }, prediction);
        }

        public void SaveFreeTicketTag(int id, string freeTag)
        {
            if (string.IsNullOrEmpty(freeTag)) return;

            using (var workspace = WorkspaceFactory.Create())
            {
                var tt = workspace.Single<TicketTagGroup>(x => x.Id == id);
                Debug.Assert(tt != null);
                var tag = tt.TicketTags.SingleOrDefault(x => x.Name.ToLower() == freeTag.ToLower());

                if (tag != null) return;
                tag = new TicketTag { Name = freeTag };
                tt.TicketTags.Add(tag);
                workspace.Add(tag);
                workspace.CommitChanges();
                Dao.ResetCache();
            }
        }

        public IList<TicketExplorerRowData> GetFilteredTickets(DateTime startDate, DateTime endDate, IList<ITicketExplorerFilter> filters)
        {
            endDate = endDate.Date.AddDays(1).AddMinutes(-1);
            Expression<Func<Ticket, bool>> qFilter = x => x.Date >= startDate && x.Date < endDate;
            qFilter = filters.Aggregate(qFilter, (current, filter) => current.And(filter.GetExpression()));
            return Dao.Query(qFilter).Select(x => new TicketExplorerRowData(x)).ToList();
        }

        public IList<ITicketExplorerFilter> CreateTicketExplorerFilters()
        {
            var item = new TicketExplorerFilter { FilterType = FilterType.OpenTickets };
            return new List<ITicketExplorerFilter> { item };
        }

        public IEnumerable<Ticket> LoadTickets(Expression<Func<Ticket, bool>> prediction)
        {
            var tickets = Dao.Query(prediction, x => x.Orders.Select(y => y.OrderTagValues),
                x => x.Calculations, x => x.Payments, x => x.PaidItems, x => x.Tags,
                x => x.AccountDocument.AccountTransactions,
                x => x.AccountDocument.AccountTransactions.Select(y => y.SourceTransactionValue),
                x => x.AccountDocument.AccountTransactions.Select(y => y.TargetTransactionValue));
            return tickets;
        }

        public void SaveTickets(IEnumerable<Ticket> tickets)
        {
            tickets.ToList().ForEach(RecalculateTicket);
            Dao.SafeSave(tickets);
        }

        public IEnumerable<Order> ExtractSelectedOrders(Ticket model, IEnumerable<Order> selectedOrders)
        {
            var selectedItems = selectedOrders.Where(x => x.SelectedQuantity > 0 && x.SelectedQuantity < x.Quantity).ToList();
            var newItems = model.ExtractSelectedOrders(selectedItems);
            foreach (var newItem in newItems)
                _workspace.Add(newItem);
            return newItems;
        }

        public override void Reset()
        {

        }
    }

    public class TicketTagGroupDeleteValidator : SpecificationValidator<TicketTagGroup>
    {
        public override string GetErrorMessage(TicketTagGroup model)
        {
            if (Dao.Exists<TicketTemplate>(x => x.TicketTagGroups.Any(y => y.Id == model.Id)))
                return string.Format(Resources.DeleteErrorUsedBy_f, Resources.TicketTagGroup, Resources.TicketTemplate);
            return "";
        }
    }
}
