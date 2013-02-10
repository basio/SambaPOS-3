using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using Microsoft.Practices.Prism.Commands;
using Samba.Domain.Models.Accounts;
using Samba.Domain.Models.Tickets;
using Samba.Localization.Properties;
using Samba.Presentation.Common;
using Samba.Presentation.ViewModels;
using Samba.Services;
using Samba.Services.Common;

namespace Samba.Modules.PosModule
{
    [Export]
    public class AccountTicketsViewModel : ObservableObject
    {
        private readonly ITicketService _ticketService;
        private readonly IApplicationStateSetter _applicationStateSetter;
        private readonly IApplicationState _applicationState;
        public PaymentButtonGroupViewModel PaymentButtonGroup { get; set; }
        public ICaptionCommand MakePaymentCommand { get; set; }
        public ICaptionCommand MakeFastPaymentCommand { get; set; }
        public ICaptionCommand CloseCommand { get; set; }

        [ImportingConstructor]
        public AccountTicketsViewModel(ITicketService ticketService, IApplicationStateSetter applicationStateSetter, IApplicationState applicationState)
        {
            _ticketService = ticketService;
            _applicationState = applicationState;
            _applicationStateSetter = applicationStateSetter;
            OpenTicketCommand = new DelegateCommand<int?>(OnOpenTicket);
            MakePaymentCommand = new CaptionCommand<string>(Resources.Settle, OnMakePayment);
            MakeFastPaymentCommand = new CaptionCommand<string>("Fast", OnMakeFastPayment);
            CloseCommand = new CaptionCommand<string>(Resources.Close, OnClose);
            PaymentButtonGroup = new PaymentButtonGroupViewModel(MakeFastPaymentCommand, MakePaymentCommand, CloseCommand);
        }

        private void OnClose(string obj)
        {
            EventServiceFactory.EventService.PublishEvent(EventTopicNames.ActivatePosView);
        }

        private void OnMakeFastPayment(string obj)
        {
            throw new NotImplementedException();
        }

        private void OnMakePayment(string obj)
        {
            SelectedAccount.PublishEvent(EventTopicNames.MakePayment);
        }

        private void OnOpenTicket(int? obj)
        {
            _ticketService.OpenTicket(obj.GetValueOrDefault(0));
            EventServiceFactory.EventService.PublishEvent(EventTopicNames.ActivatePosView);
        }

        public DelegateCommand<int?> OpenTicketCommand { get; set; }

        private Account _selectedAccount;
        public Account SelectedAccount
        {
            get { return _selectedAccount; }
        }

        public void UpdateSelectedAccount(Account account)
        {
            _selectedAccount = account;
            PaymentButtonGroup.UpdatePaymentButtons(_applicationState.CurrentDepartment.TicketTemplate.PaymentTemplates.Where(x => x.DisplayUnderTicket));
            if (_selectedAccount != null)
            {
                OpenTickets = _ticketService.GetTicketData(x => x.AccountId == SelectedAccount.Id && !x.IsPaid).Select(x => new OpenTicketButtonViewModel(x, true));
                if (OpenTickets.Count() == 0)
                {
                    SelectedAccount.PublishEvent(EventTopicNames.AccountSelectedForTicket);
                    return;
                }
            }
            _applicationStateSetter.SetSelectedAccountForTicket(_selectedAccount);
            RaisePropertyChanged(() => SelectedAccount);
        }

        private IEnumerable<OpenTicketButtonViewModel> _openTickets;
        public IEnumerable<OpenTicketButtonViewModel> OpenTickets
        {
            get { return _openTickets; }
            set
            {
                _openTickets = value;
                RaisePropertyChanged(() => OpenTickets);
            }
        }
    }
}
